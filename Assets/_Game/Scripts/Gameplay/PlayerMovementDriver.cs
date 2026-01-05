using CombatSystem.Core;
using CombatSystem.UI;
using UnityEngine;

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

        [Tooltip("视角相机（用于计算相机相对移动方向）")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("移动速度")]
        [SerializeField] private float moveSpeed = 6f;

        [Tooltip("是否使用相机朝向作为移动参考（按 W 向相机前方移动）")]
        [SerializeField] private bool useCameraYaw = true;

        #endregion

        #region Unity 生命周期

        /// <summary>
        /// 编辑器重置时自动获取组件引用。
        /// </summary>
        private void Reset()
        {
            movement = GetComponent<MovementComponent>();
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

            // 懒加载主相机
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            // 读取输入（-1 到 1 的原始值）
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            // 计算移动方向
            var direction = CalculateMoveDirection(input);

            // 归一化防止对角移动过快
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            // 传递给移动组件执行
            movement.SetMoveInput(direction, moveSpeed);
        }

        #endregion

        #region 内部方法

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
