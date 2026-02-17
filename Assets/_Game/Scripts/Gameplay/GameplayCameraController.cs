using CombatSystem.Persistence;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// LoL 风格玩法相机：支持锁定跟随、边缘滚屏、滚轮缩放与快捷键回中。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class GameplayCameraController : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool autoFindPlayerOnStart = true;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool lockToTarget = true;
        [SerializeField] private float followSharpness = 10f;

        [Header("Rig")]
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private Transform zoomNode;
        [SerializeField] private float minPitch = 30f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private bool enforceViewAngles = true;
        [SerializeField] private float viewYawDegrees = 0f;
        [SerializeField] private float viewPitchDegrees = 55f;

        [Header("Projection")]
        [SerializeField] private bool overrideFieldOfView = true;
        [SerializeField] private float gameplayFieldOfView = 35f;

        [Header("Zoom")]
        [SerializeField] private float minDistance = 24.7f;
        [SerializeField] private float maxDistance = 36.6f;
        [SerializeField] private float defaultDistance = 28.1f;
        [SerializeField] private float zoomSensitivity = 0.008f;
        [SerializeField] private float zoomLerpSpeed = 12f;

        [Header("Composition")]
        [SerializeField] private float followOffsetRight = 0f;
        [SerializeField] private float followOffsetForward = 0f;

        [Header("Edge Pan")]
        [SerializeField] private bool edgePanEnabled = true;
        [SerializeField] private float edgePanSpeed = 18f;
        [SerializeField] private float edgePanThresholdPixels = 28f;

        [Header("Hotkeys")]
        [SerializeField] private bool enableHotkeys = true;
        [SerializeField] private Key toggleLockKey = Key.Y;
        [SerializeField] private Key recenterHoldKey = Key.Space;

        private Vector3 focusPosition;
        private float currentDistance;
        private float targetDistance;
        private bool initialized;
        private bool subscribed;

        public void SetFollowTarget(Transform target, bool snapToTarget)
        {
            followTarget = target;
            if (snapToTarget && followTarget != null)
            {
                focusPosition = GetFollowFocusPoint();
                if (rigRoot != null)
                {
                    rigRoot.position = focusPosition;
                }
            }
        }

        private void Awake()
        {
            EnsureInitialized(true);
        }

        private void OnEnable()
        {
            if (!initialized)
            {
                EnsureInitialized(true);
            }

            if (!subscribed)
            {
                SettingsService.SettingsApplied += HandleSettingsApplied;
                subscribed = true;
            }

            HandleSettingsApplied(SettingsService.Current ?? SettingsService.LoadOrCreate());
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                SettingsService.SettingsApplied -= HandleSettingsApplied;
                subscribed = false;
            }
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            var deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            TryResolveFollowTarget();
            ApplyViewAnglesIfNeeded();

            var inputAllowed = UIRoot.IsGameplayInputAllowed();
            var pointerOverUi = IsPointerOverUi();

            if (inputAllowed && enableHotkeys)
            {
                HandleLockToggleInput();
            }

            if (inputAllowed && !pointerOverUi)
            {
                HandleZoomInput();
            }

            var recenterHeld = inputAllowed && enableHotkeys && IsKeyPressed(recenterHoldKey);
            var shouldLockNow = lockToTarget || recenterHeld;

            var desiredFocus = focusPosition;
            if (shouldLockNow && followTarget != null)
            {
                desiredFocus = GetFollowFocusPoint();
            }

            if (!shouldLockNow && inputAllowed && edgePanEnabled && !pointerOverUi)
            {
                desiredFocus += CalculateEdgePanDelta(deltaTime);
            }

            if (shouldLockNow)
            {
                var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSharpness) * deltaTime);
                focusPosition = Vector3.Lerp(focusPosition, desiredFocus, t);
            }
            else
            {
                focusPosition = desiredFocus;
            }

            if (rigRoot != null)
            {
                rigRoot.position = focusPosition;
            }

            ApplyZoom(deltaTime);

            var settings = SettingsService.Current;
            if (settings != null)
            {
                settings.cameraZoomDistance = targetDistance;
                settings.cameraControlMode = lockToTarget ? CameraControlMode.LockedFollow : CameraControlMode.FreePan;
            }
        }

        private void EnsureInitialized(bool snapToTarget)
        {
            var cameraComponent = GetComponent<Camera>();
            if (cameraComponent == null)
            {
                enabled = false;
                return;
            }

            if (overrideFieldOfView)
            {
                cameraComponent.fieldOfView = Mathf.Clamp(gameplayFieldOfView, 20f, 50f);
            }

            TryResolveFollowTarget();

            if (!EnsureRigHierarchy())
            {
                enabled = false;
                return;
            }

            if (snapToTarget && followTarget != null)
            {
                focusPosition = GetFollowFocusPoint();
                rigRoot.position = focusPosition;
            }

            currentDistance = Mathf.Clamp(currentDistance <= 0f ? defaultDistance : currentDistance, minDistance, maxDistance);
            targetDistance = Mathf.Clamp(targetDistance <= 0f ? currentDistance : targetDistance, minDistance, maxDistance);
            ApplyZoom(1f);
            initialized = true;
        }

        private bool EnsureRigHierarchy()
        {
            if (rigRoot != null && pitchPivot != null && zoomNode != null && transform.parent == zoomNode)
            {
                ApplyViewAnglesIfNeeded();
                focusPosition = rigRoot.position;
                currentDistance = Mathf.Abs(zoomNode.localPosition.z);
                targetDistance = currentDistance;
                return true;
            }

            var worldPosition = transform.position;
            var worldRotation = transform.rotation;
            var focus = ResolveInitialFocusPoint(worldPosition, worldRotation);
            var originalParent = transform.parent;

            var flatForward = Vector3.ProjectOnPlane(worldRotation * Vector3.forward, Vector3.up);
            if (flatForward.sqrMagnitude <= 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            var yawRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            var pitchDegrees = Mathf.Asin(Mathf.Clamp(-(worldRotation * Vector3.forward).y, -1f, 1f)) * Mathf.Rad2Deg;
            pitchDegrees = Mathf.Clamp(pitchDegrees, minPitch, maxPitch);
            if (enforceViewAngles)
            {
                yawRotation = Quaternion.Euler(0f, viewYawDegrees, 0f);
                pitchDegrees = Mathf.Clamp(viewPitchDegrees, minPitch, maxPitch);
            }

            rigRoot = CreateRigNode("CameraRigRoot", originalParent);
            rigRoot.position = focus;
            rigRoot.rotation = yawRotation;

            pitchPivot = CreateRigNode("CameraRigPivot", rigRoot);
            pitchPivot.localPosition = Vector3.zero;
            pitchPivot.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);

            zoomNode = CreateRigNode("CameraRigZoom", pitchPivot);
            var distance = Mathf.Clamp(Vector3.Distance(worldPosition, focus), minDistance, maxDistance);
            zoomNode.localPosition = new Vector3(0f, 0f, -distance);
            zoomNode.localRotation = Quaternion.identity;

            transform.SetParent(zoomNode, true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            focusPosition = focus;
            currentDistance = distance;
            targetDistance = distance;
            return true;
        }

        private void ApplyViewAnglesIfNeeded()
        {
            if (!enforceViewAngles || rigRoot == null || pitchPivot == null)
            {
                return;
            }

            rigRoot.rotation = Quaternion.Euler(0f, viewYawDegrees, 0f);
            pitchPivot.localRotation = Quaternion.Euler(Mathf.Clamp(viewPitchDegrees, minPitch, maxPitch), 0f, 0f);
        }

        private static Transform CreateRigNode(string name, Transform parent)
        {
            var go = new GameObject(name);
            var node = go.transform;
            if (parent != null)
            {
                node.SetParent(parent, false);
            }

            return node;
        }

        private Vector3 ResolveInitialFocusPoint(Vector3 cameraPosition, Quaternion cameraRotation)
        {
            if (followTarget != null)
            {
                return GetGroundPoint(followTarget.position);
            }

            var ray = new Ray(cameraPosition, cameraRotation * Vector3.forward);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var enter))
            {
                return ray.GetPoint(enter);
            }

            return new Vector3(cameraPosition.x, 0f, cameraPosition.z);
        }

        private void TryResolveFollowTarget()
        {
            if (followTarget != null)
            {
                return;
            }

            if (!autoFindPlayerOnStart)
            {
                return;
            }

            if (!string.IsNullOrEmpty(playerTag))
            {
                var player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    followTarget = player.transform;
                    return;
                }
            }

            var movement = FindFirstObjectByType<PlayerMovementDriver>();
            if (movement != null)
            {
                followTarget = movement.transform;
            }
        }

        private void HandleSettingsApplied(SettingsData data)
        {
            if (data == null)
            {
                return;
            }

            lockToTarget = data.cameraControlMode == CameraControlMode.LockedFollow;
            edgePanEnabled = data.edgePanEnabled;
            targetDistance = Mathf.Clamp(data.cameraZoomDistance, minDistance, maxDistance);
            if (!initialized)
            {
                currentDistance = targetDistance;
            }
        }

        private void HandleLockToggleInput()
        {
            if (!IsKeyPressedThisFrame(toggleLockKey))
            {
                return;
            }

            lockToTarget = !lockToTarget;
            if (lockToTarget && followTarget != null)
            {
                focusPosition = GetFollowFocusPoint();
            }
        }

        private void HandleZoomInput()
        {
            if (Mouse.current == null)
            {
                return;
            }

            var scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) <= 0.001f)
            {
                return;
            }

            targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSensitivity, minDistance, maxDistance);
        }

        private void ApplyZoom(float deltaTime)
        {
            var lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, zoomLerpSpeed) * Mathf.Max(0f, deltaTime));
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, lerpFactor);

            if (zoomNode != null)
            {
                zoomNode.localPosition = new Vector3(0f, 0f, -currentDistance);
            }
        }

        private Vector3 CalculateEdgePanDelta(float deltaTime)
        {
            if (Mouse.current == null || rigRoot == null)
            {
                return Vector3.zero;
            }

            var threshold = Mathf.Max(1f, edgePanThresholdPixels);
            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);
            var pointer = Mouse.current.position.ReadValue();

            var horizontal = ResolveEdgeAxis(pointer.x, screenWidth, threshold);
            var vertical = ResolveEdgeAxis(pointer.y, screenHeight, threshold);

            var edge = new Vector2(horizontal, vertical);
            if (edge.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            var forward = rigRoot.forward;
            forward.y = 0f;
            forward.Normalize();

            var right = rigRoot.right;
            right.y = 0f;
            right.Normalize();

            var direction = right * edge.x + forward * edge.y;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            var intensity = Mathf.Clamp01(edge.magnitude);
            return direction * (edgePanSpeed * intensity * deltaTime);
        }

        private static float ResolveEdgeAxis(float value, float max, float threshold)
        {
            if (value <= threshold)
            {
                return -(1f - (value / threshold));
            }

            var remaining = max - value;
            if (remaining <= threshold)
            {
                return 1f - (remaining / threshold);
            }

            return 0f;
        }

        private static bool IsPointerOverUi()
        {
            var current = EventSystem.current;
            return current != null && current.IsPointerOverGameObject();
        }

        private static Vector3 GetGroundPoint(Vector3 worldPosition)
        {
            return new Vector3(worldPosition.x, 0f, worldPosition.z);
        }

        private Vector3 GetFollowFocusPoint()
        {
            if (followTarget == null)
            {
                return focusPosition;
            }

            var basePoint = GetGroundPoint(followTarget.position);
            if (rigRoot == null)
            {
                return basePoint;
            }

            return basePoint + rigRoot.right * followOffsetRight + rigRoot.forward * followOffsetForward;
        }

        private static bool IsKeyPressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key] != null && keyboard[key].isPressed;
        }

        private static bool IsKeyPressedThisFrame(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key] != null && keyboard[key].wasPressedThisFrame;
        }
    }
}
