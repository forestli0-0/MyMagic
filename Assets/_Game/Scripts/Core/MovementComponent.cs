using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 通用移动组件，负责处理角色移动、旋转以及技能位移的强制移动。
    /// </summary>
    /// <remarks>
    /// 核心功能：
    /// - 常规移动：通过 SetMoveInput/SetMoveVelocity 接收移动指令
    /// - 强制位移：击退、拉拽、冲刺等技能效果产生的不可控移动
    /// - 施法限制：自动检测施法状态，限制移动和旋转
    /// 
    /// 移动优先级：
    /// 强制位移 > 常规移动输入
    /// 
    /// 使用方式：
    /// 1. 外部驱动（如 PlayerMovementDriver、CombatAIController）每帧调用 SetMoveInput
    /// 2. 技能效果通过 ApplyForcedMove 或 ApplyInstantMove 施加位移
    /// 3. 组件在 Update 中统一处理移动和旋转
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public class MovementComponent : MonoBehaviour
    {
        #region 序列化字段

        [Header("组件引用")]
        [Tooltip("角色控制器，用于物理移动和碰撞检测")]
        [SerializeField] private CharacterController controller;

        [Tooltip("技能使用组件，用于检测施法状态")]
        [SerializeField] private SkillUserComponent skillUser;

        [Header("移动设置")]
        [Tooltip("基础移动速度（单位/秒）")]
        [SerializeField] private float baseMoveSpeed = 5f;

        [Tooltip("旋转速度（度/秒）")]
        [SerializeField] private float rotationSpeed = 720f;

        [Tooltip("是否在移动时自动朝向移动方向")]
        [SerializeField] private bool rotateToMovement = true;

        #endregion

        #region 常规移动状态

        /// <summary>当前帧的移动输入方向</summary>
        private Vector3 moveInput;

        /// <summary>当前帧的移动速度</summary>
        private float moveSpeed;

        /// <summary>当前帧是否有移动输入</summary>
        private bool hasMoveInput;

        #endregion

        #region 强制位移状态

        /// <summary>是否正在执行强制位移</summary>
        private bool forcedActive;

        /// <summary>强制位移方向（已归一化）</summary>
        private Vector3 forcedDirection;

        /// <summary>强制位移剩余距离</summary>
        private float forcedRemaining;

        /// <summary>强制位移速度</summary>
        private float forcedSpeed;

        /// <summary>强制位移时是否旋转朝向</summary>
        private bool forcedRotate;

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否正在执行强制位移（击退、冲刺等）。
        /// </summary>
        public bool IsForcedMoving => forcedActive;

        #endregion

        #region Unity 生命周期

        /// <summary>
        /// 编辑器重置时自动获取组件引用。
        /// </summary>
        private void Reset()
        {
            controller = GetComponent<CharacterController>();
            skillUser = GetComponent<SkillUserComponent>();
        }

        /// <summary>
        /// 初始化时确保组件引用有效。
        /// </summary>
        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }
        }

        /// <summary>
        /// 每帧更新：处理强制位移或常规移动。
        /// </summary>
        private void Update()
        {
            var deltaTime = Time.deltaTime;

            // 优先处理强制位移（击退、冲刺等）
            if (forcedActive)
            {
                ProcessForcedMove(deltaTime);
                return;
            }

            // 处理常规移动输入
            ProcessNormalMove(deltaTime);
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置移动输入（每帧调用）。
        /// </summary>
        /// <param name="direction">移动方向（世界空间）</param>
        /// <param name="speed">移动速度</param>
        /// <remarks>
        /// 输入在当前帧结束后自动清空，需要每帧持续调用以保持移动。
        /// </remarks>
        public void SetMoveInput(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude <= 0.0001f || speed <= 0f)
            {
                return;
            }

            moveInput = direction;
            moveSpeed = speed;
            hasMoveInput = true;
        }

        /// <summary>
        /// 设置移动速度向量（每帧调用）。
        /// </summary>
        /// <param name="velocity">速度向量（方向 × 速度）</param>
        public void SetMoveVelocity(Vector3 velocity)
        {
            var speed = velocity.magnitude;
            if (speed <= 0.0001f)
            {
                return;
            }

            SetMoveInput(velocity / speed, speed);
        }

        /// <summary>
        /// 停止当前移动输入。
        /// </summary>
        public void Stop()
        {
            hasMoveInput = false;
            moveInput = Vector3.zero;
            moveSpeed = 0f;
        }

        /// <summary>
        /// 应用强制位移（用于击退、拉拽、冲刺等技能效果）。
        /// </summary>
        /// <param name="direction">位移方向</param>
        /// <param name="distance">位移距离</param>
        /// <param name="speed">位移速度（0 则使用基础速度）</param>
        /// <param name="rotate">是否在位移过程中旋转朝向</param>
        /// <remarks>
        /// 强制位移会覆盖常规移动，直到位移完成。
        /// 位移过程中会持续进行碰撞检测。
        /// </remarks>
        public void ApplyForcedMove(Vector3 direction, float distance, float speed, bool rotate)
        {
            if (distance <= 0f || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            forcedActive = true;
            forcedDirection = direction.normalized;
            forcedRemaining = distance;
            forcedSpeed = speed;
            forcedRotate = rotate;
        }

        /// <summary>
        /// 应用瞬间位移（无动画过渡）。
        /// </summary>
        /// <param name="displacement">位移向量</param>
        /// <remarks>
        /// 用于需要立即完成的位移效果，如闪现、传送等。
        /// 仍会进行碰撞检测。
        /// </remarks>
        public void ApplyInstantMove(Vector3 displacement)
        {
            if (displacement.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Move(displacement);
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 处理强制位移逻辑。
        /// </summary>
        private void ProcessForcedMove(float deltaTime)
        {
            var speed = forcedSpeed > 0f ? forcedSpeed : baseMoveSpeed;
            var step = speed * deltaTime;
            var move = forcedDirection * Mathf.Min(step, forcedRemaining);

            Move(move);
            forcedRemaining -= move.magnitude;

            // 根据配置决定是否旋转
            if (forcedRotate)
            {
                RotateTowards(forcedDirection);
            }

            // 位移完成后结束强制状态
            if (forcedRemaining <= 0f)
            {
                forcedActive = false;
            }
        }

        /// <summary>
        /// 处理常规移动逻辑。
        /// </summary>
        private void ProcessNormalMove(float deltaTime)
        {
            // 检查是否允许移动（施法时可能被限制）
            var canMove = skillUser == null || !skillUser.IsCasting || skillUser.CanMoveWhileCasting;

            if (hasMoveInput && canMove)
            {
                var direction = moveInput;
                if (direction.sqrMagnitude > 1f)
                {
                    direction.Normalize();
                }

                var speed = moveSpeed > 0f ? moveSpeed : baseMoveSpeed;
                Move(direction * speed * deltaTime);

                // 根据配置决定是否朝向移动方向
                if (rotateToMovement)
                {
                    RotateTowards(direction);
                }
            }

            // 清空本帧输入，等待下一帧重新设置
            hasMoveInput = false;
            moveInput = Vector3.zero;
            moveSpeed = 0f;
        }

        /// <summary>
        /// 执行实际移动（通过 CharacterController）。
        /// </summary>
        /// <param name="displacement">位移向量</param>
        private void Move(Vector3 displacement)
        {
            if (controller != null)
            {
                controller.Move(displacement);
                return;
            }

            // 回退方案：直接修改 Transform（无碰撞检测）
            transform.position += displacement;
        }

        /// <summary>
        /// 平滑旋转朝向指定方向。
        /// </summary>
        /// <param name="direction">目标方向</param>
        private void RotateTowards(Vector3 direction)
        {
            // 检查是否允许旋转（施法时可能被限制）
            var canRotate = skillUser == null || !skillUser.IsCasting || skillUser.CanRotateWhileCasting;
            if (!canRotate)
            {
                return;
            }

            // 忽略垂直分量，只在水平面旋转
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var rotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
        }

        #endregion
    }
}
