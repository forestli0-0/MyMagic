using CombatSystem.Core;
using CombatSystem.Input;
using CombatSystem.Persistence;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 玩家移动输入驱动，负责读取键盘输入并转换为世界空间方向。
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 读取 Horizontal/Vertical 轴输入
    /// - 根据相机朝向计算世界空间移动方向
    /// - 将方向和速度传递给 MovementComponent
    /// 
    /// 使用方式：
    /// - 挂载在玩家角色上，与 MovementComponent 配合使用
    /// - 通过 useCameraYaw 控制是否使用相机相对移动
    /// </remarks>
    public class PlayerMovementDriver : MonoBehaviour
    {
        #region 序列化字段

        [Tooltip("移动组件引用")]
        [SerializeField] private MovementComponent movement;

        [Tooltip("技能组件引用")]
        [SerializeField] private SkillUserComponent skillUser;

        [Tooltip("输入读取器（Input System）")]
        [SerializeField] private InputReader inputReader;

        [Tooltip("视角相机（用于计算相机相对移动方向）")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("是否使用相机朝向作为移动参考（按 W 向相机前方移动）")]
        [SerializeField] private bool useCameraYaw = true;

        [Header("Right Click Move")]
        [Tooltip("右键移动检测层，默认全部层")]
        [SerializeField] private LayerMask clickMoveMask = ~0;

        [Tooltip("右键移动到目标点的停止距离")]
        [SerializeField] private float clickStopDistance = 0.2f;

        [Tooltip("按住右键时持续刷新目标点（模拟 LoL 连续点地）")]
        [SerializeField] private bool refreshTargetWhileHoldingRightButton = true;

        #endregion

        #region 运行时状态

        private MovementControlMode movementControlMode = MovementControlMode.KeyboardWASD;
        private bool hasClickDestination;
        private Vector3 clickDestination;

        #endregion

        #region Unity 生命周期

        /// <summary>
        /// 编辑器重置时自动获取组件引用。
        /// </summary>
        private void Reset()
        {
            movement = GetComponent<MovementComponent>();
            skillUser = GetComponent<SkillUserComponent>();
        }

        private void Awake()
        {
            if (inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            ApplyMovementMode(SettingsService.LoadOrCreate());
        }

        private void OnEnable()
        {
            SettingsService.SettingsApplied += ApplyMovementMode;

            var current = SettingsService.Current ?? SettingsService.LoadOrCreate();
            ApplyMovementMode(current);
        }

        private void OnDisable()
        {
            SettingsService.SettingsApplied -= ApplyMovementMode;
        }

        /// <summary>
        /// 每帧读取输入并驱动移动。
        /// </summary>
        private void Update()
        {
            if (!UIRoot.IsGameplayInputAllowed())
            {
                return;
            }

            if (movement == null)
            {
                return;
            }

            if (inputReader == null)
            {
                return;
            }

            // 懒加载主相机
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (movementControlMode == MovementControlMode.RightClickMove)
            {
                ProcessRightClickMove();
                return;
            }

            ProcessKeyboardMove();
        }

        #endregion

        #region 内部方法

        private void ProcessKeyboardMove()
        {
            // 读取输入（-1 到 1 的原始值）
            var input = inputReader.Move;
            if (input.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (skillUser != null && skillUser.IsChanneling && !skillUser.CanMoveWhileCasting)
            {
                skillUser.InterruptCast();
                return;
            }

            // 计算移动方向
            var direction = CalculateMoveDirection(input);

            // 归一化防止对角移动过快
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            // 传递给移动组件执行（速度由 MovementComponent 从属性系统读取）
            movement.SetMoveInput(direction);
        }

        private void ProcessRightClickMove()
        {
            if (Mouse.current == null)
            {
                return;
            }

            var pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!pointerOverUi && Mouse.current.rightButton.wasPressedThisFrame)
            {
                UpdateClickDestination();
            }

            if (!pointerOverUi && refreshTargetWhileHoldingRightButton && Mouse.current.rightButton.isPressed)
            {
                UpdateClickDestination();
            }

            if (!hasClickDestination)
            {
                return;
            }

            if (skillUser != null && skillUser.IsChanneling && !skillUser.CanMoveWhileCasting)
            {
                skillUser.InterruptCast();
                return;
            }

            var delta = clickDestination - transform.position;
            delta.y = 0f;

            var stopDistance = Mathf.Max(0.01f, clickStopDistance);
            if (delta.sqrMagnitude <= stopDistance * stopDistance)
            {
                hasClickDestination = false;
                return;
            }

            movement.SetMoveInput(delta.normalized);
        }

        private void UpdateClickDestination()
        {
            if (viewCamera == null || Mouse.current == null)
            {
                return;
            }

            var pointerPosition = Mouse.current.position.ReadValue();
            if (!TryResolvePointerWorldPoint(pointerPosition, out var worldPoint))
            {
                return;
            }

            clickDestination = worldPoint;
            hasClickDestination = true;
        }

        private bool TryResolvePointerWorldPoint(Vector2 screenPoint, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;

            if (viewCamera == null)
            {
                return false;
            }

            var ray = viewCamera.ScreenPointToRay(screenPoint);
            if (Physics.Raycast(ray, out var hit, 500f, clickMoveMask, QueryTriggerInteraction.Ignore))
            {
                worldPoint = hit.point;
                return true;
            }

            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (!plane.Raycast(ray, out var enter))
            {
                return false;
            }

            worldPoint = ray.GetPoint(enter);
            return true;
        }

        private void ApplyMovementMode(SettingsData data)
        {
            movementControlMode = data != null ? data.movementControlMode : MovementControlMode.KeyboardWASD;
            if (movementControlMode == MovementControlMode.KeyboardWASD)
            {
                hasClickDestination = false;
            }
        }

        /// <summary>
        /// 根据输入计算世界空间移动方向。
        /// </summary>
        /// <param name="input">原始输入向量（x=水平, y=垂直）</param>
        /// <returns>世界空间移动方向</returns>
        private Vector3 CalculateMoveDirection(Vector2 input)
        {
            // 默认：输入直接映射到世界坐标（X=左右, Z=前后）
            var direction = new Vector3(input.x, 0f, input.y);

            // 使用相机朝向：按 W 向相机前方移动
            if (useCameraYaw && viewCamera != null)
            {
                // 获取相机的水平前向（忽略俯仰角）
                var forward = viewCamera.transform.forward;
                forward.y = 0f;
                forward.Normalize();

                // 获取相机的水平右向
                var right = viewCamera.transform.right;
                right.y = 0f;
                right.Normalize();

                // 组合输入：前后输入沿相机前向，左右输入沿相机右向
                direction = forward * input.y + right * input.x;
            }

            return direction;
        }

        #endregion
    }
}
