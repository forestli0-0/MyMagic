using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 技能指示器组件。
    /// 用于在地面上绘制技能范围和方向，支持圆形、扇形和矩形显示模式。
    /// 通过 LineRenderer 组件来渲染指示器图形。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SkillIndicator : MonoBehaviour
    {
        #region 序列化字段

        [Tooltip("指示器锚点Transform，指示器位置将跟随此锚点")]
        [SerializeField] private Transform anchor;

        [Tooltip("用于绘制指示器的LineRenderer组件")]
        [SerializeField] private LineRenderer line;

        [Tooltip("指示器相对于锚点的高度偏移，防止与地面重叠")]
        [SerializeField] private float heightOffset = 0.05f;

        [Tooltip("默认的自身范围半径（当技能没有指定范围时使用）")]
        [SerializeField] private float selfRadius = 1f;

        [Tooltip("绘制圆形/扇形时的分段数，值越大越平滑")]
        [SerializeField] private int segments = 48;

        [Tooltip("指示器线条宽度")]
        [SerializeField] private float lineWidth = 0.05f;

        [Tooltip("指示器线条颜色")]
        [SerializeField] private Color lineColor = new Color(0.1f, 0.9f, 1f, 0.8f);

        [Header("Target Highlight")]
        [Tooltip("是否启用目标高亮显示")]
        [SerializeField] private bool showTargetHighlight = true;

        [Tooltip("目标高亮圆圈半径")]
        [SerializeField] private float targetHighlightRadius = 0.8f;

        [Tooltip("目标高亮圆圈颜色")]
        [SerializeField] private Color targetHighlightColor = new Color(1f, 0.2f, 0.2f, 0.9f);

        #endregion

        #region 私有字段

        /// <summary>当前显示的技能定义</summary>
        private SkillDefinition currentSkill;

        /// <summary>当前技能的目标选择定义</summary>
        private TargetingDefinition currentTargeting;

        /// <summary>当前瞄准方向（水平面上的单位向量）</summary>
        private Vector3 aimDirection = Vector3.forward;
        /// <summary>当前瞄准点</summary>
        private Vector3 aimPoint;
        /// <summary>是否有有效瞄准点</summary>
        private bool hasAimPoint;

        /// <summary>指示器是否可见</summary>
        private bool isVisible;

        /// <summary>目标高亮子物体</summary>
        private GameObject targetHighlightObject;

        /// <summary>目标高亮LineRenderer</summary>
        private LineRenderer targetHighlightLine;

        /// <summary>当前高亮的目标Transform</summary>
        private Transform currentHighlightTarget;

        #endregion

        #region Unity 生命周期

        /// <summary>
        /// 编辑器重置时自动获取LineRenderer组件。
        /// </summary>
        private void Reset()
        {
            line = GetComponent<LineRenderer>();
        }

        /// <summary>
        /// 初始化：配置LineRenderer并隐藏指示器。
        /// </summary>
        private void Awake()
        {
            // 获取LineRenderer组件
            if (line == null)
            {
                line = GetComponent<LineRenderer>();
            }

            // 配置LineRenderer基本属性
            if (line != null)
            {
                line.useWorldSpace = false;  // 使用本地坐标系
                line.loop = false;           // 不闭合（手动处理闭合）
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.startColor = lineColor;
                line.endColor = lineColor;
            }

            // 默认锚点为自身
            if (anchor == null)
            {
                anchor = transform;
            }

            // 创建目标高亮子物体
            CreateTargetHighlight();

            // 初始状态为隐藏
            Hide();
        }

        /// <summary>
        /// LateUpdate确保在角色移动后更新指示器位置。
        /// </summary>
        private void LateUpdate()
        {
            if (!isVisible)
            {
                return;
            }

            UpdateIndicator();

            // 更新目标高亮位置
            if (currentHighlightTarget != null)
            {
                UpdateTargetHighlight();
            }
        }

        /// <summary>
        /// 销毁时清理动态创建的子物体。
        /// </summary>
        private void OnDestroy()
        {
            if (targetHighlightObject != null)
            {
                Destroy(targetHighlightObject);
                targetHighlightObject = null;
                targetHighlightLine = null;
            }
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 显示指定技能的指示器。
        /// </summary>
        /// <param name="skill">要显示的技能定义</param>
        public void Show(SkillDefinition skill)
        {
            currentSkill = skill;
            currentTargeting = skill != null ? skill.Targeting : null;
            isVisible = true;

            if (line != null)
            {
                line.enabled = true;
            }

            UpdateIndicator();
        }

        /// <summary>
        /// 隐藏指示器。
        /// </summary>
        public void Hide()
        {
            isVisible = false;
            currentSkill = null;
            currentTargeting = null;
            hasAimPoint = false;
            aimPoint = default;

            if (line != null)
            {
                line.enabled = false;
                line.positionCount = 0;
            }

            // 隐藏目标高亮
            HideTargetHighlight();
        }

        /// <summary>
        /// 设置指示器的锚点。
        /// 指示器位置将跟随此锚点更新。
        /// </summary>
        /// <param name="newAnchor">新的锚点Transform</param>
        public void SetAnchor(Transform newAnchor)
        {
            anchor = newAnchor;
        }

        /// <summary>
        /// 设置瞄准方向。
        /// 指示器将朝向此方向旋转。
        /// </summary>
        /// <param name="direction">瞄准方向向量</param>
        public void SetAimDirection(Vector3 direction)
        {
            // 忽略Y轴，只在水平面上旋转
            direction.y = 0f;

            // 避免零向量
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            aimDirection = direction.normalized;
        }

        /// <summary>
        /// 设置瞄准点。
        /// 当目标选择需要指定点时，用于更新指示器位置。
        /// </summary>
        /// <param name="point">瞄准点坐标</param>
        public void SetAimPoint(Vector3 point)
        {
            aimPoint = point;
            hasAimPoint = true;
        }

        /// <summary>
        /// 清除瞄准点。
        /// </summary>
        public void ClearAimPoint()
        {
            hasAimPoint = false;
            aimPoint = default;
        }

        /// <summary>
        /// 设置当前高亮的目标。
        /// 在目标脚下显示红色圆圈标记。
        /// </summary>
        /// <param name="target">要高亮的目标Transform，null则隐藏高亮</param>
        public void SetHighlightTarget(Transform target)
        {
            currentHighlightTarget = target;

            if (!showTargetHighlight || targetHighlightLine == null)
            {
                return;
            }

            if (target == null || !isVisible)
            {
                HideTargetHighlight();
                return;
            }

            ShowTargetHighlight();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 更新指示器的位置、旋转和形状。
        /// </summary>
        private void UpdateIndicator()
        {
            if (line == null || anchor == null)
            {
                return;
            }

            // 更新位置：跟随锚点，加上高度偏移
            var position = anchor.position;
            if (currentTargeting != null && currentTargeting.Origin == TargetingOrigin.TargetPoint && hasAimPoint)
            {
                position = aimPoint;
            }
            transform.position = new Vector3(position.x, position.y + heightOffset, position.z);

            // 更新旋转：朝向瞄准方向
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(aimDirection);
            }

            // 根据目标选择模式绘制不同形状
            var mode = currentTargeting != null ? currentTargeting.Mode : TargetingMode.Self;
            switch (mode)
            {
                case TargetingMode.Cone:
                    // 扇形：用于锥形范围技能
                    DrawCone(GetIndicatorRange(), currentTargeting != null ? currentTargeting.Angle : 45f);
                    break;
                case TargetingMode.Line:
                    // 线性矩形：用于技能射击体
                    DrawRectangle(GetIndicatorRange(), GetIndicatorWidth(), false);
                    break;
                case TargetingMode.Box:
                    // 盒形矩形：用于地面矩形范围
                    DrawRectangle(GetIndicatorRange(), GetIndicatorWidth(), true);
                    break;
                case TargetingMode.Self:
                    // 圆形（自身范围）：用于以自身为中心的技能
                    DrawCircle(GetSelfRange());
                    break;
                case TargetingMode.Single:
                case TargetingMode.Sphere:
                case TargetingMode.Random:
                case TargetingMode.Chain:
                default:
                    // 默认圆形：显示技能射程范围
                    DrawCircle(GetIndicatorRange());
                    break;
            }
        }

        /// <summary>
        /// 获取指示器显示范围。
        /// 优先使用技能的射程或半径设置。
        /// </summary>
        /// <returns>指示器半径</returns>
        private float GetIndicatorRange()
        {
            if (currentTargeting == null)
            {
                return Mathf.Max(0.1f, selfRadius);
            }

            // 球形范围技能使用Radius
            if (currentTargeting.Mode == TargetingMode.Sphere && currentTargeting.Radius > 0f)
            {
                return Mathf.Max(0.1f, currentTargeting.Radius);
            }

            // 其他技能使用Range
            return Mathf.Max(0.1f, currentTargeting.Range);
        }

        /// <summary>
        /// 获取矩形技能的半宽度。
        /// </summary>
        private float GetIndicatorWidth()
        {
            if (currentTargeting != null && currentTargeting.Radius > 0f)
            {
                return Mathf.Max(0.1f, currentTargeting.Radius);
            }

            return Mathf.Max(0.1f, selfRadius);
        }

        /// <summary>
        /// 获取自身范围技能的显示半径。
        /// 优先使用Radius，其次Range，最后使用默认值。
        /// </summary>
        /// <returns>自身范围半径</returns>
        private float GetSelfRange()
        {
            if (currentTargeting != null)
            {
                if (currentTargeting.Radius > 0f)
                {
                    return Mathf.Max(0.1f, currentTargeting.Radius);
                }

                if (currentTargeting.Range > 0f)
                {
                    return Mathf.Max(0.1f, currentTargeting.Range);
                }
            }

            return Mathf.Max(0.1f, selfRadius);
        }

        /// <summary>
        /// 绘制圆形指示器。
        /// 用于显示技能射程或自身范围。
        /// </summary>
        /// <param name="radius">圆形半径</param>
        private void DrawCircle(float radius)
        {
            var count = Mathf.Max(8, segments);
            line.positionCount = count + 1;  // +1是为了闭合圆形
            var step = Mathf.PI * 2f / count;

            for (int i = 0; i <= count; i++)
            {
                var angle = step * i;
                var x = Mathf.Sin(angle) * radius;
                var z = Mathf.Cos(angle) * radius;
                line.SetPosition(i, new Vector3(x, 0f, z));
            }
        }

        /// <summary>
        /// 绘制扇形指示器。
        /// 用于显示锥形范围技能（如扇形攻击）。
        /// </summary>
        /// <param name="radius">扇形半径</param>
        /// <param name="angle">扇形角度（度数）</param>
        private void DrawCone(float radius, float angle)
        {
            var arcSegments = Mathf.Max(6, segments / 2);
            var halfAngle = Mathf.Clamp(angle, 0f, 180f) * 0.5f * Mathf.Deg2Rad;

            // 扇形由：原点 -> 弧线 -> 回到原点 组成
            line.positionCount = arcSegments + 3;
            line.SetPosition(0, Vector3.zero);  // 扇形起点（原点）

            // 绘制弧线
            for (int i = 0; i <= arcSegments; i++)
            {
                var t = (float)i / arcSegments;
                var rad = Mathf.Lerp(-halfAngle, halfAngle, t);
                var x = Mathf.Sin(rad) * radius;
                var z = Mathf.Cos(rad) * radius;
                line.SetPosition(i + 1, new Vector3(x, 0f, z));
            }

            line.SetPosition(arcSegments + 2, Vector3.zero);  // 扇形终点（回到原点）
        }

        /// <summary>
        /// 绘制矩形指示器（线性/盒形）。
        /// </summary>
        /// <param name="length">矩形长度</param>
        /// <param name="halfWidth">矩形半宽</param>
        /// <param name="centered">是否以原点为中心</param>
        private void DrawRectangle(float length, float halfWidth, bool centered)
        {
            var safeLength = Mathf.Max(0.1f, length);
            var safeHalfWidth = Mathf.Max(0.05f, halfWidth);
            var halfLength = safeLength * 0.5f;

            var zMin = centered ? -halfLength : 0f;
            var zMax = centered ? halfLength : safeLength;

            line.positionCount = 5;
            line.SetPosition(0, new Vector3(-safeHalfWidth, 0f, zMin));
            line.SetPosition(1, new Vector3(safeHalfWidth, 0f, zMin));
            line.SetPosition(2, new Vector3(safeHalfWidth, 0f, zMax));
            line.SetPosition(3, new Vector3(-safeHalfWidth, 0f, zMax));
            line.SetPosition(4, new Vector3(-safeHalfWidth, 0f, zMin));
        }

        /// <summary>
        /// 创建目标高亮子物体。
        /// </summary>
        private void CreateTargetHighlight()
        {
            if (targetHighlightObject != null)
            {
                return;
            }

            targetHighlightObject = new GameObject("TargetHighlight");
            targetHighlightObject.transform.SetParent(transform.parent, false);

            targetHighlightLine = targetHighlightObject.AddComponent<LineRenderer>();
            targetHighlightLine.useWorldSpace = true;
            targetHighlightLine.loop = true;
            targetHighlightLine.startWidth = lineWidth;
            targetHighlightLine.endWidth = lineWidth;
            targetHighlightLine.startColor = targetHighlightColor;
            targetHighlightLine.endColor = targetHighlightColor;

            // 复制主指示器的材质
            if (line != null && line.sharedMaterial != null)
            {
                targetHighlightLine.sharedMaterial = line.sharedMaterial;
            }

            targetHighlightLine.enabled = false;
        }

        /// <summary>
        /// 显示目标高亮。
        /// </summary>
        private void ShowTargetHighlight()
        {
            if (targetHighlightLine == null || currentHighlightTarget == null)
            {
                return;
            }

            targetHighlightLine.enabled = true;
            UpdateTargetHighlight();
        }

        /// <summary>
        /// 隐藏目标高亮。
        /// </summary>
        private void HideTargetHighlight()
        {
            currentHighlightTarget = null;

            if (targetHighlightLine != null)
            {
                targetHighlightLine.enabled = false;
                targetHighlightLine.positionCount = 0;
            }
        }

        /// <summary>
        /// 更新目标高亮位置和形状。
        /// </summary>
        private void UpdateTargetHighlight()
        {
            if (targetHighlightLine == null || currentHighlightTarget == null)
            {
                HideTargetHighlight();
                return;
            }

            // 在目标脚下绘制圆圈
            var position = currentHighlightTarget.position;
            var y = position.y + heightOffset;

            var count = Mathf.Max(8, segments / 2);
            targetHighlightLine.positionCount = count;
            var step = Mathf.PI * 2f / count;

            for (int i = 0; i < count; i++)
            {
                var angle = step * i;
                var x = position.x + Mathf.Sin(angle) * targetHighlightRadius;
                var z = position.z + Mathf.Cos(angle) * targetHighlightRadius;
                targetHighlightLine.SetPosition(i, new Vector3(x, y, z));
            }
        }

        #endregion
    }
}
