using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 数值条 UI 组件 - 通用的进度条/资源条显示组件。
    /// 可用于显示血量、魔法值、能量等数值型属性。
    /// </summary>
    public class ValueBarUI : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("填充 Image（需设置为 Filled 类型）")]
        [SerializeField] private Image fill;
        
        [Tooltip("显示数值的文本组件")]
        [SerializeField] private Text valueText;
        
        [Header("显示选项")]
        [Tooltip("是否显示最大值（如：80/100）")]
        [SerializeField] private bool showMax = true;
        
        [Tooltip("是否显示百分比（如：80%）")]
        [SerializeField] private bool showPercent;
        
        [Tooltip("是否将当前值限制在 0 ~ 最大值范围内")]
        [SerializeField] private bool clampValue = true;

        /// <summary>
        /// 当前数值
        /// </summary>
        private float currentValue;
        
        /// <summary>
        /// 最大数值（至少为 1，避免除零错误）
        /// </summary>
        private float maxValue = 1f;

        /// <summary>
        /// 同时设置当前值和最大值。
        /// </summary>
        /// <param name="current">当前值</param>
        /// <param name="max">最大值</param>
        public void SetValues(float current, float max)
        {
            maxValue = Mathf.Max(1f, max);
            currentValue = clampValue ? Mathf.Clamp(current, 0f, maxValue) : current;
            Refresh();
        }

        /// <summary>
        /// 仅设置当前值，最大值保持不变。
        /// </summary>
        /// <param name="current">当前值</param>
        public void SetCurrent(float current)
        {
            currentValue = clampValue ? Mathf.Clamp(current, 0f, maxValue) : current;
            Refresh();
        }

        /// <summary>
        /// 仅设置最大值，当前值会根据配置进行限制。
        /// </summary>
        /// <param name="max">最大值</param>
        public void SetMax(float max)
        {
            maxValue = Mathf.Max(1f, max);
            if (clampValue)
            {
                currentValue = Mathf.Clamp(currentValue, 0f, maxValue);
            }

            Refresh();
        }

        /// <summary>
        /// 刷新 UI 显示：更新填充进度和文本内容。
        /// </summary>
        private void Refresh()
        {
            // 更新填充进度
            if (fill != null)
            {
                fill.fillAmount = maxValue > 0f ? Mathf.Clamp01(currentValue / maxValue) : 0f;
            }

            // 更新文本显示
            if (valueText != null)
            {
                if (showPercent)
                {
                    // 百分比模式：显示 "80%"
                    var percent = maxValue > 0f ? Mathf.RoundToInt(currentValue / maxValue * 100f) : 0;
                    valueText.text = $"{percent}%";
                }
                else if (showMax)
                {
                    // 当前/最大模式：显示 "80/100"
                    valueText.text = $"{Mathf.RoundToInt(currentValue)}/{Mathf.RoundToInt(maxValue)}";
                }
                else
                {
                    // 仅当前值模式：显示 "80"
                    valueText.text = Mathf.RoundToInt(currentValue).ToString();
                }
            }
        }

        /// <summary>
        /// 运行时动态配置 UI 组件引用。
        /// 用于在代码中动态创建 ValueBarUI 时设置填充图片和文本组件。
        /// </summary>
        /// <param name="fillImage">填充 Image 组件</param>
        /// <param name="text">数值文本组件</param>
        public void Configure(Image fillImage, Text text)
        {
            fill = fillImage;
            valueText = text;
        }
    }
}
