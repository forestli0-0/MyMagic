using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 飘字项目组件 - 表示单个浮动伤害/治疗数字。
    /// 由 FloatingTextManager 管理其生命周期和对象池回收。
    /// </summary>
    public class FloatingTextItem : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("显示数值的文本组件")]
        [SerializeField] private Text label;
        
        [Tooltip("用于控制透明度的 CanvasGroup")]
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("动画配置")]
        [Tooltip("飘字显示持续时间（秒）")]
        [SerializeField] private float lifetime = 0.8f;
        
        [Tooltip("飘字上升速度（像素/秒）")]
        [SerializeField] private float riseSpeed = 35f;
        
        [Tooltip("相对于世界坐标的屏幕偏移量")]
        [SerializeField] private Vector3 screenOffset = new Vector3(0f, 30f, 0f);
        
        [Tooltip("随机水平偏移范围")]
        [SerializeField] private float randomOffsetRange = 40f;
        
        [Header("颜色配置")]
        [Tooltip("伤害数字颜色（红色系）")]
        [SerializeField] private Color damageColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        
        [Tooltip("治疗数字颜色（绿色系）")]
        [SerializeField] private Color healColor = new Color(0.2f, 0.9f, 0.3f, 1f);

        /// <summary>
        /// 缓存的 RectTransform 引用
        /// </summary>
        private RectTransform rectTransform;
        
        /// <summary>
        /// 已经过的时间
        /// </summary>
        private float elapsed;
        
        /// <summary>
        /// 飘字对应的世界空间位置
        /// </summary>
        private Vector3 worldPosition;
        
        /// <summary>
        /// 用于坐标转换的相机引用
        /// </summary>
        private Camera cachedCamera;
        
        /// <summary>
        /// UI 根节点的 RectTransform
        /// </summary>
        private RectTransform root;
        
        /// <summary>
        /// 当前飘字的随机偏移量
        /// </summary>
        private Vector3 randomOffset;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
        }

        /// <summary>
        /// 激活飘字项目并初始化显示。
        /// </summary>
        /// <param name="worldPos">飘字对应的世界空间位置</param>
        /// <param name="value">数值（负数为伤害，正数为治疗）</param>
        /// <param name="camera">用于坐标转换的相机</param>
        /// <param name="rootRect">UI 根节点 RectTransform</param>
        public void Activate(Vector3 worldPos, float value, Camera camera, RectTransform rootRect)
        {
            worldPosition = worldPos;
            cachedCamera = camera != null ? camera : Camera.main;
            root = rootRect;
            elapsed = 0f;
            randomOffset = new Vector3(Random.Range(-randomOffsetRange, randomOffsetRange), 0f, 0f);

            if (label != null)
            {
                // 显示数值的绝对值
                var display = Mathf.Abs(value);
                label.text = Mathf.RoundToInt(display).ToString();
                // 根据正负值设置颜色（负数为伤害，正数为治疗）
                label.color = value < 0f ? damageColor : healColor;
            }

            SetAlpha(1f);
            UpdatePosition(0f);
        }

        /// <summary>
        /// 每帧更新飘字状态。
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        /// <returns>如果飘字仍在生命周期内返回 true，否则返回 false</returns>
        public bool Tick(float deltaTime)
        {
            elapsed += deltaTime;
            UpdatePosition(elapsed);

            // 计算生命周期进度并更新透明度（线性淡出）
            var t = lifetime > 0f ? Mathf.Clamp01(elapsed / lifetime) : 1f;
            SetAlpha(1f - t);

            return elapsed < lifetime;
        }

        /// <summary>
        /// 根据已过时间更新飘字的屏幕位置。
        /// 实现从世界坐标到 UI 锚点坐标的转换。
        /// </summary>
        /// <param name="time">已过时间</param>
        private void UpdatePosition(float time)
        {
            if (rectTransform == null || root == null)
            {
                return;
            }

            // 计算屏幕偏移：基础偏移 + 时间相关的上升距离
            var offset = screenOffset + randomOffset + Vector3.up * riseSpeed * time;
            
            // 将世界坐标转换为屏幕坐标
            var screenPoint = cachedCamera != null
                ? cachedCamera.WorldToScreenPoint(worldPosition)
                : (Vector3)worldPosition;

            // 将屏幕坐标转换为 UI 本地坐标
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint + offset, null, out var localPoint))
            {
                rectTransform.anchoredPosition = localPoint;
            }
        }

        /// <summary>
        /// 设置飘字的透明度。
        /// </summary>
        /// <param name="alpha">目标透明度（0~1）</param>
        private void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
        }
    }
}
