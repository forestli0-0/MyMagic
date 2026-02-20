using CombatSystem.Core;
using CombatSystem.Data;
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
        [Tooltip("单位根组件（用于目标与阵营判定）")]
        [SerializeField] private UnitRoot unitRoot;
        [Tooltip("队伍组件（用于敌我判定）")]
        [SerializeField] private TeamComponent team;
        [Tooltip("目标系统（用于按技能形状判断普攻是否在范围内）")]
        [SerializeField] private TargetingSystem targetingSystem;

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
        [Tooltip("按住右键时持续刷新锁定目标（可选）")]
        [SerializeField] private bool refreshAttackTargetWhileHoldingRightButton = true;
        [Tooltip("施法开始时清除右键目标点，避免朝向被旧移动目标瞬间拉回。")]
        [SerializeField] private bool clearClickDestinationOnCast = true;

        [Header("Auto Basic Attack")]
        [Tooltip("右键点击敌人时自动追击并普攻")]
        [SerializeField] private bool autoBasicAttackOnRightClick = true;
        [Tooltip("进入普攻范围后的额外停止缓冲")]
        [SerializeField] private float attackRangeBuffer = 0.1f;
        [Tooltip("自动追击时超过该距离将丢失目标（0 表示不限制）")]
        [SerializeField] private float loseAttackTargetDistance = 40f;

        [Header("Camera")]
        [Tooltip("是否自动确保主相机挂载 GameplayCameraController")]
        [SerializeField] private bool autoSetupGameplayCamera = true;

        [Tooltip("玩法相机控制器（可留空自动获取）")]
        [SerializeField] private GameplayCameraController gameplayCamera;

        #endregion

        #region 运行时状态

        private MovementControlMode movementControlMode = MovementControlMode.KeyboardWASD;
        private bool hasClickDestination;
        private Vector3 clickDestination;
        private bool hasAttackTarget;
        private CombatTarget attackTarget;

        #endregion

        #region Unity 生命周期

        /// <summary>
        /// 编辑器重置时自动获取组件引用。
        /// </summary>
        private void Reset()
        {
            movement = GetComponent<MovementComponent>();
            skillUser = GetComponent<SkillUserComponent>();
            unitRoot = GetComponent<UnitRoot>();
            team = GetComponent<TeamComponent>();
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

            EnsureCombatReferences();
            TryResolveViewCamera();
            EnsureGameplayCameraController(true);
            ApplyMovementMode(SettingsService.LoadOrCreate());
        }

        private void OnEnable()
        {
            SettingsService.SettingsApplied += ApplyMovementMode;
            EnsureGameplayCameraController(true);
            EnsureCombatReferences();
            if (skillUser != null)
            {
                skillUser.SkillCastStarted += HandleSkillCastStarted;
            }

            var current = SettingsService.Current ?? SettingsService.LoadOrCreate();
            ApplyMovementMode(current);
        }

        private void OnDisable()
        {
            SettingsService.SettingsApplied -= ApplyMovementMode;
            if (skillUser != null)
            {
                skillUser.SkillCastStarted -= HandleSkillCastStarted;
            }
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

            TryResolveViewCamera();
            EnsureGameplayCameraController(false);

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
                ProcessRightClickCommand(true, true);
            }

            var refreshAttackTarget = autoBasicAttackOnRightClick && refreshAttackTargetWhileHoldingRightButton;
            var canRefreshWhileHolding = !hasAttackTarget || refreshAttackTarget;
            var refreshMoveTarget = canRefreshWhileHolding && refreshTargetWhileHoldingRightButton;
            if (!pointerOverUi && Mouse.current.rightButton.isPressed && (refreshMoveTarget || refreshAttackTarget))
            {
                ProcessRightClickCommand(refreshMoveTarget, refreshAttackTarget);
            }

            if (TryProcessAutoBasicAttack())
            {
                return;
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

        private void ProcessRightClickCommand(bool allowMoveDestination, bool allowAttackTarget)
        {
            if (viewCamera == null || Mouse.current == null)
            {
                return;
            }

            var pointerPosition = Mouse.current.position.ReadValue();
            if (!TryResolvePointerWorldHit(pointerPosition, out var hit, out var worldPoint))
            {
                return;
            }

            if (allowAttackTarget && autoBasicAttackOnRightClick && TryResolveAttackTarget(hit, out var target))
            {
                SetAttackTarget(target);
                hasClickDestination = false;
                return;
            }

            if (!allowMoveDestination)
            {
                return;
            }

            ClearAttackTarget();
            clickDestination = worldPoint;
            hasClickDestination = true;
        }

        private bool TryResolvePointerWorldHit(Vector2 screenPoint, out RaycastHit hit, out Vector3 worldPoint)
        {
            hit = default;
            worldPoint = Vector3.zero;

            if (viewCamera == null)
            {
                return false;
            }

            var ray = viewCamera.ScreenPointToRay(screenPoint);
            if (Physics.Raycast(ray, out hit, 500f, clickMoveMask, QueryTriggerInteraction.Ignore))
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

        private bool TryResolveAttackTarget(RaycastHit hit, out CombatTarget target)
        {
            target = default;
            if (!autoBasicAttackOnRightClick)
            {
                return false;
            }

            if (hit.collider == null || !CombatTarget.TryCreate(hit.collider.gameObject, out target))
            {
                return false;
            }

            if (!IsAttackTargetValid(target))
            {
                return false;
            }

            return true;
        }

        private bool TryProcessAutoBasicAttack()
        {
            if (!autoBasicAttackOnRightClick || !hasAttackTarget || movement == null || skillUser == null)
            {
                return false;
            }

            EnsureCombatReferences();
            if (!IsAttackTargetValid(attackTarget))
            {
                ClearAttackTarget();
                return false;
            }

            if (skillUser.IsChanneling && !skillUser.CanMoveWhileCasting)
            {
                skillUser.InterruptCast();
                return true;
            }

            var basicAttack = skillUser.BasicAttack;
            if (basicAttack == null)
            {
                ClearAttackTarget();
                return false;
            }

            var targetPosition = attackTarget.Transform.position;
            var delta = targetPosition - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            if (IsBasicAttackInRange(basicAttack, attackTarget, delta))
            {
                movement.Stop();
                TryFaceTowards(delta.normalized);

                if (skillUser.CanCast(basicAttack))
                {
                    skillUser.TryCast(basicAttack, attackTarget.GameObject);
                }

                return true;
            }

            movement.SetMoveInput(delta.normalized);
            return true;
        }

        private bool IsBasicAttackInRange(SkillDefinition basicAttack, CombatTarget target, Vector3 flatDelta)
        {
            if (basicAttack == null || !target.IsValid || target.Transform == null)
            {
                return false;
            }

            var targeting = basicAttack.Targeting;
            if (targetingSystem != null && unitRoot != null && targeting != null)
            {
                var aimDirection = flatDelta.sqrMagnitude > 0.0001f ? flatDelta.normalized : transform.forward;
                var aimPoint = target.Transform.position;
                var inShape = targetingSystem.IsWithinTargetingShape(
                    targeting,
                    unitRoot,
                    target,
                    target.GameObject,
                    true,
                    aimPoint,
                    aimDirection);

                if (inShape)
                {
                    return true;
                }
            }

            var fallbackRange = GetBasicAttackFallbackRange(basicAttack) + Mathf.Max(0f, attackRangeBuffer);
            return fallbackRange <= 0f || flatDelta.sqrMagnitude <= fallbackRange * fallbackRange;
        }

        private static float GetBasicAttackFallbackRange(SkillDefinition basicAttack)
        {
            if (basicAttack == null || basicAttack.Targeting == null)
            {
                return 0f;
            }

            var targeting = basicAttack.Targeting;
            if (targeting.Mode == TargetingMode.Sphere && targeting.Radius > 0f)
            {
                return targeting.Radius;
            }

            if (targeting.Range > 0f)
            {
                return targeting.Range;
            }

            if (targeting.Radius > 0f)
            {
                return targeting.Radius;
            }

            return 0f;
        }

        private void ApplyMovementMode(SettingsData data)
        {
            movementControlMode = data != null ? data.movementControlMode : MovementControlMode.KeyboardWASD;
            if (movementControlMode == MovementControlMode.KeyboardWASD)
            {
                hasClickDestination = false;
                ClearAttackTarget();
            }
        }

        private void HandleSkillCastStarted(SkillCastEvent evt)
        {
            if (!clearClickDestinationOnCast || movementControlMode != MovementControlMode.RightClickMove)
            {
                return;
            }

            if (skillUser != null && evt.Skill != null && skillUser.IsBasicAttackSkill(evt.Skill))
            {
                return;
            }

            hasClickDestination = false;
        }

        private void SetAttackTarget(CombatTarget target)
        {
            attackTarget = target;
            hasAttackTarget = target.IsValid;
        }

        private void ClearAttackTarget()
        {
            attackTarget = default;
            hasAttackTarget = false;
        }

        private bool IsAttackTargetValid(CombatTarget target)
        {
            if (!target.IsValid || target.Transform == null || target.GameObject == gameObject)
            {
                return false;
            }

            if (target.Health == null)
            {
                return false;
            }

            if (!target.Health.IsAlive)
            {
                return false;
            }

            if (target.Team != null && team != null && team.IsSameTeam(target.Team))
            {
                return false;
            }

            if (loseAttackTargetDistance > 0f)
            {
                var selfPos = transform.position;
                var targetPos = target.Transform.position;
                selfPos.y = 0f;
                targetPos.y = 0f;
                if ((targetPos - selfPos).sqrMagnitude > loseAttackTargetDistance * loseAttackTargetDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private void TryFaceTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (skillUser != null && skillUser.IsCasting && !skillUser.CanRotateWhileCasting)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction);
        }

        private void EnsureCombatReferences()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (team == null)
            {
                team = GetComponent<TeamComponent>();
            }

            if (targetingSystem == null)
            {
                targetingSystem = FindFirstObjectByType<TargetingSystem>();
            }
        }

        private void TryResolveViewCamera()
        {
            if (viewCamera != null)
            {
                return;
            }

            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                viewCamera = FindFirstObjectByType<Camera>();
            }
        }

        private void EnsureGameplayCameraController(bool snapToTarget)
        {
            if (!autoSetupGameplayCamera)
            {
                return;
            }

            TryResolveViewCamera();
            if (viewCamera == null)
            {
                return;
            }

            if (gameplayCamera == null || gameplayCamera.gameObject != viewCamera.gameObject)
            {
                gameplayCamera = viewCamera.GetComponent<GameplayCameraController>();
            }

            if (gameplayCamera == null)
            {
                gameplayCamera = viewCamera.gameObject.AddComponent<GameplayCameraController>();
            }

            gameplayCamera.SetFollowTarget(transform, snapToTarget);
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
