using System;
using System.Collections;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    public enum BossSkillExecutionPhase
    {
        Idle = 0,
        InitialDelay = 1,
        AcquireSnapshot = 2,
        Telegraph = 3,
        PreCastDelay = 4,
        CastAttempt = 5,
        PostCastDelay = 6
    }

    /// <summary>
    /// Boss 技能节奏控制器：按序列进行 telegraph + 施法。
    /// </summary>
    public class BossSkillScheduler : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private Transform explicitTarget;
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private Transform telegraphRoot;
        [SerializeField] private Animator animationLockAnimator;

        [Header("节奏")]
        [SerializeField] private List<BossSkillCycleEntry> skillCycle = new List<BossSkillCycleEntry>();
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float initialDelay = 1f;
        [SerializeField] private float retryInterval = 0.15f;
        [Tooltip("预警结束后到真正尝试施法之间的额外前摇（秒），用于确保体感是“先预警再攻击”。")]
        [SerializeField] private float castDelayAfterTelegraph = 0.12f;
        [Tooltip("预警结束后首次施法失败时的重试窗口（秒），用于减少“有预警但未命中/未施法”的体感。")]
        [SerializeField] private float postTelegraphCastRetryWindow = 0.25f;
        [Tooltip("预警后施法重试间隔（秒）。")]
        [SerializeField] private float postTelegraphCastRetryInterval = 0.05f;
        [Tooltip("Boss 施法最小停移时长（秒），用于避免动画仍在播放时出现滑步。")]
        [SerializeField] private float minimumMovementLockDuration = 0.35f;
        [Tooltip("施法动作收尾的额外停移缓冲（秒），用于消除末尾一小段滑步。")]
        [SerializeField] private float castEndMovementLockBuffer = 0.5f;
        [Tooltip("是否按 Animator Cast 状态退出来解锁移动（推荐开启，减少硬编码时长依赖）。")]
        [SerializeField] private bool lockMovementByAnimatorCastState = true;
        [Tooltip("是否监听动画事件/状态机行为的 Cast 开始/结束信号来驱动停移。")]
        [SerializeField] private bool lockMovementByAnimationSignals = true;
        [Tooltip("被视为施法状态的 Animator 状态名（shortName）。")]
        [SerializeField] private string[] castStateNames = { "Cast" };
        [Tooltip("被视为施法状态的 Animator Tag。可选。")]
        [SerializeField] private string[] castStateTags = { "Cast", "Casting" };
        [Tooltip("等待进入/退出施法动画或事件信号的最大时长（秒），超时后回退到时长锁，防止卡死。")]
        [SerializeField] private float animatorCastStateLockTimeout = 2.5f;
        [Tooltip("范围/近战预警开始时是否立即锁定 Boss 移动（推荐开启，避免边追边读条）。")]
        [SerializeField] private bool lockMovementDuringAreaTelegraph = true;
        [Tooltip("锁定型（远程单体）预警开始时是否也锁定 Boss 移动。")]
        [SerializeField] private bool lockMovementDuringLockedTelegraph;
        [Tooltip("预警停移的额外缓冲（秒），避免预警尾帧恢复追击。")]
        [SerializeField] private float telegraphMovementLockBuffer = 0.05f;

        [Header("Telegraph")]
        [SerializeField] private bool showGroundTelegraph = true;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.25f, 0.2f, 0.4f);
        [SerializeField] private Color lockedTelegraphColor = new Color(1f, 0.35f, 0.2f, 0.8f);
        [SerializeField] private Color telegraphFillColor = new Color(1f, 0.2f, 0.15f, 0.2f);
        [SerializeField] private float fallbackTelegraphRadius = 2f;
        [SerializeField] private float lockedTelegraphRadius = 1.2f;
        [SerializeField, Range(0.05f, 0.95f)] private float expandingTelegraphStartRatio = 0.2f;
        [SerializeField, Range(12, 96)] private int telegraphSegments = 48;
        [SerializeField] private float telegraphRingWidth = 0.12f;
        [SerializeField] private float meleeRangeThreshold = 2.5f;
        [SerializeField] private bool drawTelegraphFill = true;
        [SerializeField] private float telegraphHeight = 0.02f;
        [SerializeField] private bool logCastCancellation;

        private Coroutine loopRoutine;
        private float movementLockUntilTime;
        private int[] castStateNameHashes = Array.Empty<int>();
        private bool animatorCastLockActive;
        private bool animatorCastStateObserved;
        private float animatorCastLockDeadline;
        private UnitAnimationEventProxy animationEventProxy;
        private Material telegraphLineMaterial;
        private Material telegraphFillMaterial;
        private BossSkillExecutionPhase executionPhase = BossSkillExecutionPhase.Idle;
        private bool phaseMovementStopRequested;

        private enum BossTelegraphStyle
        {
            ExpandingArea = 0,
            LockedTarget = 1
        }

        private sealed class BossTelegraphRuntime
        {
            public GameObject Root;
            public LineRenderer Ring;
            public Transform Fill;
            public Transform FollowTarget;
            public Vector3[] Points;
            public BossTelegraphStyle Style;
            public float BaseRadius;
            public float StartRadius;
        }

        /// <summary>
        /// 是否处于 Boss 技能流程导致的停移窗口。
        /// </summary>
        public bool IsMovementLocked => Time.time < movementLockUntilTime || animatorCastLockActive;
        public BossSkillExecutionPhase ExecutionPhase => executionPhase;
        public bool IsPhaseMovementStopRequested => phaseMovementStopRequested;

        public event Action<BossTelegraphEvent> TelegraphStarted;

        private void Reset()
        {
            skillUser = GetComponent<SkillUserComponent>();
            animationLockAnimator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }
            
            if (animationLockAnimator == null)
            {
                animationLockAnimator = GetComponentInChildren<Animator>();
            }

            CacheCastStateNameHashes();
            TryBindAnimationEventProxy();

            if (autoStart)
            {
                StartLoop();
            }
        }

        private void OnDisable()
        {
            StopLoop();
            movementLockUntilTime = 0f;
            animatorCastLockActive = false;
            animatorCastStateObserved = false;
            animatorCastLockDeadline = 0f;
            executionPhase = BossSkillExecutionPhase.Idle;
            phaseMovementStopRequested = false;
            UnbindAnimationEventProxy();
            ReleaseTelegraphMaterials();
        }

        public void StartLoop()
        {
            if (loopRoutine != null)
            {
                return;
            }

            loopRoutine = StartCoroutine(RunLoop());
        }

        public void StopLoop()
        {
            if (loopRoutine == null)
            {
                return;
            }

            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        private IEnumerator RunLoop()
        {
            SetExecutionPhase(BossSkillExecutionPhase.InitialDelay);
            var delay = Mathf.Max(0f, initialDelay);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            var index = 0;
            while (isActiveAndEnabled)
            {
                phaseMovementStopRequested = false;
                if (skillCycle.Count == 0 || skillUser == null)
                {
                    SetExecutionPhase(BossSkillExecutionPhase.Idle);
                    yield return null;
                    continue;
                }

                if (index >= skillCycle.Count)
                {
                    index = 0;
                }

                var entry = skillCycle[index];
                index++;

                if (entry == null || entry.Skill == null)
                {
                    SetExecutionPhase(BossSkillExecutionPhase.Idle);
                    yield return null;
                    continue;
                }

                SetExecutionPhase(BossSkillExecutionPhase.AcquireSnapshot);
                var castSnapshot = default(BossCastSnapshot);
                var hasSnapshot = false;
                var castRetryDeadline = Time.time + Mathf.Max(0.05f, entry.CastRetryWindow);
                while (Time.time <= castRetryDeadline)
                {
                    if (TryAcquireCastSnapshot(entry, out castSnapshot))
                    {
                        hasSnapshot = true;
                        break;
                    }

                    yield return new WaitForSeconds(Mathf.Max(0.05f, retryInterval));
                }

                if (!hasSnapshot)
                {
                    SetExecutionPhase(BossSkillExecutionPhase.PostCastDelay);
                    yield return new WaitForSeconds(Mathf.Max(0.05f, entry.DelayOnFail));
                    SetExecutionPhase(BossSkillExecutionPhase.Idle);
                    continue;
                }

                var telegraphDuration = Mathf.Max(0f, entry.TelegraphDuration);
                if (telegraphDuration > 0f)
                {
                    SetExecutionPhase(BossSkillExecutionPhase.Telegraph);
                    var telegraphStyle = ResolveTelegraphStyle(entry.Skill);
                    phaseMovementStopRequested = ShouldLockMovementDuringTelegraph(telegraphStyle);
                    if (ShouldLockMovementDuringTelegraph(telegraphStyle))
                    {
                        var preCastLockDuration =
                            telegraphDuration
                            + Mathf.Max(0f, castDelayAfterTelegraph)
                            + Mathf.Max(0f, telegraphMovementLockBuffer);
                        ExtendMovementLock(preCastLockDuration);
                    }

                    RaiseTelegraph(entry, castSnapshot.TelegraphCenter, telegraphDuration);
                    if (showGroundTelegraph)
                    {
                        SpawnTelegraphMarker(entry, castSnapshot, telegraphDuration);
                    }

                    yield return new WaitForSeconds(telegraphDuration);
                }

                var casted = false;
                var postTelegraphDelay = Mathf.Max(0f, castDelayAfterTelegraph);
                if (postTelegraphDelay > 0f)
                {
                    SetExecutionPhase(BossSkillExecutionPhase.PreCastDelay);
                    phaseMovementStopRequested = true;
                    yield return new WaitForSeconds(postTelegraphDelay);
                }

                SetExecutionPhase(BossSkillExecutionPhase.CastAttempt);
                phaseMovementStopRequested = true;
                casted = TryCastSnapshot(entry, castSnapshot);
                if (!casted)
                {
                    var retryWindow = Mathf.Max(0f, postTelegraphCastRetryWindow);
                    if (retryWindow > 0f)
                    {
                        var retryIntervalSeconds = Mathf.Max(0.02f, postTelegraphCastRetryInterval);
                        var retryDeadline = Time.time + retryWindow;
                        while (!casted && Time.time <= retryDeadline)
                        {
                            if (TryAcquireCastSnapshot(entry, out var retrySnapshot))
                            {
                                casted = TryCastSnapshot(entry, retrySnapshot);
                            }

                            if (!casted)
                            {
                                yield return new WaitForSeconds(retryIntervalSeconds);
                            }
                        }
                    }
                }

                if (casted)
                {
                    ExtendMovementLock(ResolveMovementLockDuration(entry));
                    BeginAnimatorCastLock();
                }
                else if (logCastCancellation)
                {
                    Debug.LogFormat(
                        this,
                        "[BossSkillScheduler] Telegraph finished but cast failed. Skill={0}, requireTarget={1}",
                        entry.Skill != null ? entry.Skill.DisplayName : "<null>",
                        entry.RequireTarget);
                }

                var postDelay = casted ? entry.DelayAfterCast : entry.DelayOnFail;
                if (postDelay > 0f)
                {
                    SetExecutionPhase(BossSkillExecutionPhase.PostCastDelay);
                    phaseMovementStopRequested = false;
                    yield return new WaitForSeconds(postDelay);
                }
                else
                {
                    SetExecutionPhase(BossSkillExecutionPhase.PostCastDelay);
                    phaseMovementStopRequested = false;
                    yield return null;
                }

                SetExecutionPhase(BossSkillExecutionPhase.Idle);
                phaseMovementStopRequested = false;
            }

            SetExecutionPhase(BossSkillExecutionPhase.Idle);
            phaseMovementStopRequested = false;
        }

        private void SetExecutionPhase(BossSkillExecutionPhase nextPhase)
        {
            executionPhase = nextPhase;
        }

        private bool TryAcquireCastSnapshot(BossSkillCycleEntry entry, out BossCastSnapshot snapshot)
        {
            var target = ResolveTarget();
            return TryBuildCastSnapshot(entry, target, out snapshot);
        }

        private bool TryBuildCastSnapshot(BossSkillCycleEntry entry, Transform target, out BossCastSnapshot snapshot)
        {
            snapshot = default;
            if (entry == null || entry.Skill == null || skillUser == null)
            {
                return false;
            }

            var skill = entry.Skill;
            var targeting = skill.Targeting;
            var resolvedTarget = target;
            if (!entry.RequireTarget && explicitTarget != null)
            {
                resolvedTarget = explicitTarget;
            }

            if (entry.RequireTarget && resolvedTarget == null)
            {
                return false;
            }

            if (!skillUser.CanCast(skill))
            {
                return false;
            }

            var useExplicitTarget = entry.RequireTarget;
            if (targeting != null && targeting.Origin == TargetingOrigin.Caster && targeting.Mode != TargetingMode.Single)
            {
                useExplicitTarget = false;
            }

            var explicitTargetObject = useExplicitTarget && resolvedTarget != null ? resolvedTarget.gameObject : null;
            var hasAimPoint = false;
            var aimPoint = resolvedTarget != null ? resolvedTarget.position : transform.position;
            var aimDirection = Vector3.zero;
            if (resolvedTarget != null)
            {
                aimDirection = resolvedTarget.position - transform.position;
                aimDirection.y = 0f;
                if (aimDirection.sqrMagnitude > 0.0001f)
                {
                    aimDirection.Normalize();
                }
            }

            if (targeting != null && targeting.Origin == TargetingOrigin.TargetPoint)
            {
                hasAimPoint = resolvedTarget != null || explicitTarget != null;
                if (!hasAimPoint)
                {
                    return false;
                }
            }

            if (useExplicitTarget && explicitTargetObject == null)
            {
                return false;
            }

            if (!useExplicitTarget && entry.RequireTarget && resolvedTarget != null && targeting != null && targeting.Origin == TargetingOrigin.Caster)
            {
                if (!IsWithinCasterSkillReach(targeting, resolvedTarget.position))
                {
                    return false;
                }
            }

            if (useExplicitTarget && !skillUser.IsTargetInRangePreview(skill, explicitTargetObject, hasAimPoint, aimPoint, aimDirection))
            {
                return false;
            }

            snapshot = new BossCastSnapshot(
                explicitTargetObject,
                hasAimPoint,
                aimPoint,
                aimDirection,
                ResolveTelegraphCenter(skill, resolvedTarget),
                resolvedTarget);
            return true;
        }

        private bool TryCastSnapshot(BossSkillCycleEntry entry, BossCastSnapshot snapshot)
        {
            if (entry == null || entry.Skill == null || skillUser == null)
            {
                return false;
            }

            return skillUser.TryCast(
                entry.Skill,
                snapshot.ExplicitTarget,
                snapshot.HasAimPoint,
                snapshot.AimPoint,
                snapshot.AimDirection);
        }

        private Transform ResolveTarget()
        {
            if (explicitTarget != null)
            {
                return explicitTarget;
            }

            if (!string.IsNullOrWhiteSpace(targetTag))
            {
                var tagged = PlayerUnitLocator.FindGameObjectWithTagSafe(targetTag);
                if (tagged != null)
                {
                    return tagged.transform;
                }
            }

            return null;
        }

        private Vector3 ResolveTelegraphCenter(SkillDefinition skill, Transform target)
        {
            if (skill != null && skill.Targeting != null && skill.Targeting.Origin == TargetingOrigin.Caster)
            {
                return transform.position;
            }

            return target != null ? target.position : transform.position;
        }

        private bool IsWithinCasterSkillReach(TargetingDefinition targeting, Vector3 targetPosition)
        {
            if (targeting == null)
            {
                return true;
            }

            var reach = 0f;
            switch (targeting.Mode)
            {
                case TargetingMode.Sphere:
                case TargetingMode.Random:
                case TargetingMode.Chain:
                    reach = Mathf.Max(targeting.Radius, targeting.Range);
                    break;
                default:
                    reach = targeting.Range;
                    break;
            }

            if (reach <= 0f)
            {
                return true;
            }

            var origin = transform.position;
            origin.y = 0f;
            targetPosition.y = 0f;
            var maxDistance = reach + 0.1f;
            return (targetPosition - origin).sqrMagnitude <= maxDistance * maxDistance;
        }

        private void RaiseTelegraph(BossSkillCycleEntry entry, Vector3 center, float duration)
        {
            TelegraphStarted?.Invoke(new BossTelegraphEvent(entry.Skill, center, duration));
        }

        private void SpawnTelegraphMarker(BossSkillCycleEntry entry, BossCastSnapshot snapshot, float duration)
        {
            if (entry == null || entry.Skill == null)
            {
                return;
            }

            var style = ResolveTelegraphStyle(entry.Skill);
            var runtime = CreateTelegraphRuntime(entry.Skill, snapshot, style);
            if (runtime == null || runtime.Root == null)
            {
                return;
            }

            StartCoroutine(AnimateTelegraph(runtime, Mathf.Max(0.05f, duration)));
        }

        private BossTelegraphRuntime CreateTelegraphRuntime(SkillDefinition skill, BossCastSnapshot snapshot, BossTelegraphStyle style)
        {
            var baseRadius = style == BossTelegraphStyle.LockedTarget
                ? ResolveLockedTelegraphRadius(skill)
                : ResolveTelegraphRadius(skill);
            var safeRadius = Mathf.Max(0.4f, baseRadius);
            var startRadius = style == BossTelegraphStyle.ExpandingArea
                ? Mathf.Max(0.2f, safeRadius * Mathf.Clamp01(expandingTelegraphStartRatio))
                : safeRadius;
            var center = ResolveTelegraphRuntimeCenter(snapshot, style);

            var root = new GameObject(style == BossTelegraphStyle.LockedTarget ? "BossTelegraph_Locked" : "BossTelegraph_Area");
            root.transform.SetParent(telegraphRoot != null ? telegraphRoot : null, true);
            root.transform.position = center;

            var ring = root.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = Mathf.Clamp(telegraphSegments, 12, 96);
            ring.alignment = LineAlignment.TransformZ;
            ring.textureMode = LineTextureMode.Stretch;
            ring.numCapVertices = 4;
            ring.startWidth = telegraphRingWidth;
            ring.endWidth = telegraphRingWidth;
            ring.startColor = style == BossTelegraphStyle.LockedTarget ? lockedTelegraphColor : telegraphColor;
            ring.endColor = style == BossTelegraphStyle.LockedTarget ? lockedTelegraphColor : telegraphColor;

            var lineMaterial = GetOrCreateTelegraphLineMaterial();
            if (lineMaterial != null)
            {
                ring.sharedMaterial = lineMaterial;
            }

            Transform fill = null;
            if (drawTelegraphFill)
            {
                fill = CreateTelegraphFill(root.transform, style);
            }

            var runtime = new BossTelegraphRuntime
            {
                Root = root,
                Ring = ring,
                Fill = fill,
                FollowTarget = style == BossTelegraphStyle.LockedTarget ? snapshot.TelegraphTarget : null,
                Points = new Vector3[ring.positionCount],
                Style = style,
                BaseRadius = safeRadius,
                StartRadius = startRadius
            };

            UpdateTelegraphGeometry(runtime, startRadius);
            return runtime;
        }

        private Transform CreateTelegraphFill(Transform parent, BossTelegraphStyle style)
        {
            var fill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fill.name = "TelegraphFill";
            fill.transform.SetParent(parent, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localRotation = Quaternion.identity;
            fill.transform.localScale = new Vector3(1f, Mathf.Max(0.005f, telegraphHeight), 1f);

            var collider = fill.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = fill.GetComponent<Renderer>();
            if (renderer != null)
            {
                var fillColor = style == BossTelegraphStyle.LockedTarget
                    ? new Color(lockedTelegraphColor.r, lockedTelegraphColor.g, lockedTelegraphColor.b, telegraphFillColor.a)
                    : telegraphFillColor;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var fillMaterial = GetOrCreateTelegraphFillMaterial();
                if (fillMaterial != null)
                {
                    renderer.sharedMaterial = fillMaterial;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", fillColor);
                block.SetColor("_BaseColor", fillColor);
                renderer.SetPropertyBlock(block);
            }

            return fill.transform;
        }

        private IEnumerator AnimateTelegraph(BossTelegraphRuntime runtime, float duration)
        {
            var safeDuration = Mathf.Max(0.05f, duration);
            var elapsed = 0f;

            while (runtime != null && runtime.Root != null && elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / safeDuration);
                var radius = ResolveAnimatedRadius(runtime, progress);
                UpdateTelegraphPosition(runtime);
                UpdateTelegraphGeometry(runtime, radius);
                yield return null;
            }

            if (runtime != null && runtime.Root != null)
            {
                Destroy(runtime.Root);
            }
        }

        private void UpdateTelegraphPosition(BossTelegraphRuntime runtime)
        {
            if (runtime == null || runtime.Root == null)
            {
                return;
            }

            if (runtime.Style != BossTelegraphStyle.LockedTarget || runtime.FollowTarget == null)
            {
                return;
            }

            var targetPosition = runtime.FollowTarget.position;
            runtime.Root.transform.position = new Vector3(targetPosition.x, targetPosition.y + telegraphHeight, targetPosition.z);
        }

        private float ResolveAnimatedRadius(BossTelegraphRuntime runtime, float progress)
        {
            if (runtime == null)
            {
                return 0.5f;
            }

            if (runtime.Style == BossTelegraphStyle.ExpandingArea)
            {
                return Mathf.Lerp(runtime.StartRadius, runtime.BaseRadius, progress);
            }

            // 目标锁定型预警做轻微脉冲，传达“锁定中”状态。
            var pulse = 1f + Mathf.Sin(progress * Mathf.PI * 8f) * 0.03f;
            return runtime.BaseRadius * pulse;
        }

        private void UpdateTelegraphGeometry(BossTelegraphRuntime runtime, float radius)
        {
            if (runtime == null || runtime.Ring == null)
            {
                return;
            }

            var safeRadius = Mathf.Max(0.05f, radius);
            var count = runtime.Points != null ? runtime.Points.Length : 0;
            if (count < 3)
            {
                return;
            }

            var angleStep = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                var angle = angleStep * i;
                runtime.Points[i] = new Vector3(Mathf.Cos(angle) * safeRadius, 0f, Mathf.Sin(angle) * safeRadius);
            }

            runtime.Ring.SetPositions(runtime.Points);

            if (runtime.Fill != null)
            {
                runtime.Fill.localScale = new Vector3(safeRadius * 2f, Mathf.Max(0.005f, telegraphHeight), safeRadius * 2f);
            }
        }

        private Vector3 ResolveTelegraphRuntimeCenter(BossCastSnapshot snapshot, BossTelegraphStyle style)
        {
            if (style == BossTelegraphStyle.LockedTarget && snapshot.TelegraphTarget != null)
            {
                var targetPos = snapshot.TelegraphTarget.position;
                return new Vector3(targetPos.x, targetPos.y + telegraphHeight, targetPos.z);
            }

            return new Vector3(snapshot.TelegraphCenter.x, snapshot.TelegraphCenter.y + telegraphHeight, snapshot.TelegraphCenter.z);
        }

        private Material GetOrCreateTelegraphLineMaterial()
        {
            if (telegraphLineMaterial != null)
            {
                return telegraphLineMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            telegraphLineMaterial = new Material(shader)
            {
                name = "BossTelegraphLine_Mat"
            };
            return telegraphLineMaterial;
        }

        private Material GetOrCreateTelegraphFillMaterial()
        {
            if (telegraphFillMaterial != null)
            {
                return telegraphFillMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            telegraphFillMaterial = new Material(shader)
            {
                name = "BossTelegraphFill_Mat"
            };
            return telegraphFillMaterial;
        }

        private void ReleaseTelegraphMaterials()
        {
            if (telegraphLineMaterial != null)
            {
                SafeDestroyObject(telegraphLineMaterial);
                telegraphLineMaterial = null;
            }

            if (telegraphFillMaterial != null)
            {
                SafeDestroyObject(telegraphFillMaterial);
                telegraphFillMaterial = null;
            }
        }

        private static void SafeDestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private BossTelegraphStyle ResolveTelegraphStyle(SkillDefinition skill)
        {
            if (skill == null || skill.Targeting == null)
            {
                return BossTelegraphStyle.ExpandingArea;
            }

            return IsLockedTargetTelegraph(skill.Targeting)
                ? BossTelegraphStyle.LockedTarget
                : BossTelegraphStyle.ExpandingArea;
        }

        private bool IsLockedTargetTelegraph(TargetingDefinition targeting)
        {
            if (targeting == null)
            {
                return false;
            }

            if (targeting.Origin == TargetingOrigin.TargetPoint)
            {
                return true;
            }

            if (targeting.Mode != TargetingMode.Single)
            {
                return false;
            }

            if (targeting.Origin != TargetingOrigin.Caster)
            {
                return true;
            }

            return targeting.Range > Mathf.Max(0.5f, meleeRangeThreshold);
        }

        private bool ShouldLockMovementDuringTelegraph(BossTelegraphStyle style)
        {
            switch (style)
            {
                case BossTelegraphStyle.LockedTarget:
                    return lockMovementDuringLockedTelegraph;
                case BossTelegraphStyle.ExpandingArea:
                default:
                    return lockMovementDuringAreaTelegraph;
            }
        }

        private readonly struct BossCastSnapshot
        {
            public readonly GameObject ExplicitTarget;
            public readonly bool HasAimPoint;
            public readonly Vector3 AimPoint;
            public readonly Vector3 AimDirection;
            public readonly Vector3 TelegraphCenter;
            public readonly Transform TelegraphTarget;

            public BossCastSnapshot(
                GameObject explicitTarget,
                bool hasAimPoint,
                Vector3 aimPoint,
                Vector3 aimDirection,
                Vector3 telegraphCenter,
                Transform telegraphTarget)
            {
                ExplicitTarget = explicitTarget;
                HasAimPoint = hasAimPoint;
                AimPoint = aimPoint;
                AimDirection = aimDirection;
                TelegraphCenter = telegraphCenter;
                TelegraphTarget = telegraphTarget;
            }
        }

        private float ResolveMovementLockDuration(BossSkillCycleEntry entry)
        {
            var lockDuration = Mathf.Max(0f, minimumMovementLockDuration);
            if (entry != null)
            {
                lockDuration = Mathf.Max(lockDuration, entry.DelayAfterCast);
            }

            if (entry != null && entry.Skill != null)
            {
                lockDuration = Mathf.Max(
                    lockDuration,
                    Mathf.Max(0f, entry.Skill.CastTime)
                    + Mathf.Max(0f, entry.Skill.ChannelTime)
                    + Mathf.Max(0f, entry.Skill.PostCastTime));
            }

            return lockDuration + Mathf.Max(0f, castEndMovementLockBuffer);
        }

        private void ExtendMovementLock(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            var lockUntil = Time.time + duration;
            if (lockUntil > movementLockUntilTime)
            {
                movementLockUntilTime = lockUntil;
            }
        }

        private void Update()
        {
            UpdateAnimatorCastLock();
        }

        private void BeginAnimatorCastLock()
        {
            TryBindAnimationEventProxy();

            var canUseStatePolling = lockMovementByAnimatorCastState && animationLockAnimator != null;
            var canUseAnimationSignals = lockMovementByAnimationSignals && animationEventProxy != null;
            if (!canUseStatePolling && !canUseAnimationSignals)
            {
                return;
            }

            animatorCastLockActive = true;
            animatorCastStateObserved = false;
            animatorCastLockDeadline = Time.time + Mathf.Max(0.3f, animatorCastStateLockTimeout);

            if (canUseStatePolling && IsCastAnimatorState(animationLockAnimator.GetCurrentAnimatorStateInfo(0)))
            {
                animatorCastStateObserved = true;
            }
        }

        private void UpdateAnimatorCastLock()
        {
            if (!animatorCastLockActive)
            {
                return;
            }

            if (Time.time >= animatorCastLockDeadline)
            {
                ReleaseAnimatorCastLock(false);
                return;
            }

            if (!lockMovementByAnimatorCastState)
            {
                return;
            }

            if (animationLockAnimator == null)
            {
                animationLockAnimator = GetComponentInChildren<Animator>();
                TryBindAnimationEventProxy();
                if (animationLockAnimator == null)
                {
                    return;
                }
            }

            var state = animationLockAnimator.GetCurrentAnimatorStateInfo(0);
            var inCastState = IsCastAnimatorState(state);
            if (!animatorCastStateObserved)
            {
                if (inCastState)
                {
                    animatorCastStateObserved = true;
                }

                return;
            }

            if (inCastState)
            {
                return;
            }

            ReleaseAnimatorCastLock(true);
        }

        private void HandleAnimationCastStateEntered()
        {
            if (!animatorCastLockActive)
            {
                return;
            }

            animatorCastStateObserved = true;
        }

        private void HandleAnimationCastStateExited()
        {
            if (!animatorCastLockActive)
            {
                return;
            }

            animatorCastStateObserved = true;
            ReleaseAnimatorCastLock(true);
        }

        private void ReleaseAnimatorCastLock(bool applyEndBuffer)
        {
            if (!animatorCastLockActive)
            {
                return;
            }

            animatorCastLockActive = false;
            animatorCastStateObserved = false;
            animatorCastLockDeadline = 0f;

            if (applyEndBuffer)
            {
                ExtendMovementLock(castEndMovementLockBuffer);
            }
        }

        private void TryBindAnimationEventProxy()
        {
            if (!lockMovementByAnimationSignals)
            {
                UnbindAnimationEventProxy();
                return;
            }

            if (animationLockAnimator == null)
            {
                animationLockAnimator = GetComponentInChildren<Animator>();
            }

            if (animationLockAnimator == null)
            {
                UnbindAnimationEventProxy();
                return;
            }

            var resolvedProxy = animationLockAnimator.GetComponent<UnitAnimationEventProxy>();
            if (resolvedProxy == null)
            {
                resolvedProxy = animationLockAnimator.GetComponentInChildren<UnitAnimationEventProxy>(true);
            }

            if (resolvedProxy == null)
            {
                resolvedProxy = animationLockAnimator.GetComponentInParent<UnitAnimationEventProxy>();
            }

            if (resolvedProxy == null)
            {
                resolvedProxy = animationLockAnimator.gameObject.AddComponent<UnitAnimationEventProxy>();
            }

            if (ReferenceEquals(resolvedProxy, animationEventProxy))
            {
                return;
            }

            UnbindAnimationEventProxy();
            animationEventProxy = resolvedProxy;
            animationEventProxy.CastStateEntered += HandleAnimationCastStateEntered;
            animationEventProxy.CastStateExited += HandleAnimationCastStateExited;
        }

        private void UnbindAnimationEventProxy()
        {
            if (animationEventProxy == null)
            {
                return;
            }

            animationEventProxy.CastStateEntered -= HandleAnimationCastStateEntered;
            animationEventProxy.CastStateExited -= HandleAnimationCastStateExited;
            animationEventProxy = null;
        }

        private bool IsCastAnimatorState(AnimatorStateInfo state)
        {
            for (int i = 0; i < castStateNameHashes.Length; i++)
            {
                if (state.shortNameHash == castStateNameHashes[i])
                {
                    return true;
                }
            }

            if (castStateTags == null)
            {
                return false;
            }

            for (int i = 0; i < castStateTags.Length; i++)
            {
                var tag = castStateTags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                if (state.IsTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheCastStateNameHashes()
        {
            if (castStateNames == null || castStateNames.Length == 0)
            {
                castStateNameHashes = Array.Empty<int>();
                return;
            }

            var hashes = new List<int>(castStateNames.Length);
            for (int i = 0; i < castStateNames.Length; i++)
            {
                var stateName = castStateNames[i];
                if (string.IsNullOrWhiteSpace(stateName))
                {
                    continue;
                }

                hashes.Add(Animator.StringToHash(stateName.Trim()));
            }

            castStateNameHashes = hashes.Count > 0 ? hashes.ToArray() : Array.Empty<int>();
        }

        private float ResolveTelegraphRadius(SkillDefinition skill)
        {
            if (skill == null || skill.Targeting == null)
            {
                return Mathf.Max(0.5f, fallbackTelegraphRadius);
            }

            if (skill.Targeting.Radius > 0f)
            {
                return Mathf.Max(0.5f, skill.Targeting.Radius);
            }

            if (skill.Targeting.Range > 0f)
            {
                return Mathf.Max(0.5f, skill.Targeting.Range * 0.5f);
            }

            return Mathf.Max(0.5f, fallbackTelegraphRadius);
        }

        private float ResolveLockedTelegraphRadius(SkillDefinition skill)
        {
            if (skill == null || skill.Targeting == null)
            {
                return Mathf.Max(0.4f, lockedTelegraphRadius);
            }

            if (skill.Targeting.Mode == TargetingMode.Single)
            {
                return Mathf.Max(0.4f, lockedTelegraphRadius);
            }

            if (skill.Targeting.Radius > 0f)
            {
                return Mathf.Max(0.4f, Mathf.Min(skill.Targeting.Radius, lockedTelegraphRadius));
            }

            return Mathf.Max(0.4f, lockedTelegraphRadius);
        }
    }

    [Serializable]
    public class BossSkillCycleEntry
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private float telegraphDuration = 0.8f;
        [SerializeField] private float castRetryWindow = 1.5f;
        [SerializeField] private float delayAfterCast = 1f;
        [SerializeField] private float delayOnFail = 0.4f;
        [SerializeField] private bool requireTarget = true;

        public SkillDefinition Skill => skill;
        public float TelegraphDuration => telegraphDuration;
        public float CastRetryWindow => castRetryWindow;
        public float DelayAfterCast => delayAfterCast;
        public float DelayOnFail => delayOnFail;
        public bool RequireTarget => requireTarget;
    }

    public readonly struct BossTelegraphEvent
    {
        public readonly SkillDefinition Skill;
        public readonly Vector3 Position;
        public readonly float Duration;

        public BossTelegraphEvent(SkillDefinition skill, Vector3 position, float duration)
        {
            Skill = skill;
            Position = position;
            Duration = duration;
        }
    }
}
