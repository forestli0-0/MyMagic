using CombatSystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 单个 Buff 图标 UI 组件 - 显示 Buff 的图标、层数和剩余时间。
    /// 由 BuffBarUI 管理和更新。
    /// </summary>
    public class BuffIconUI : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("Buff 图标 Image 组件")]
        [SerializeField] private Image icon;
        
        [Tooltip("显示层数的文本组件")]
        [SerializeField] private Text stackText;
        
        [Tooltip("显示剩余秒数的文本组件")]
        [SerializeField] private Text timerText;

        /// <summary>
        /// Buff 结束的时间点（Time.time 基准），-1 表示永久效果
        /// </summary>
        private float endTime;

        /// <summary>
        /// 绑定 Buff 数据并更新显示。
        /// </summary>
        /// <param name="buff">Buff 定义数据</param>
        /// <param name="stacks">当前层数</param>
        /// <param name="remaining">剩余时间（秒），-1 表示永久</param>
        public void Bind(BuffDefinition buff, int stacks, float remaining)
        {
            // 设置图标
            if (icon != null)
            {
                icon.sprite = buff != null ? buff.Icon : null;
                icon.enabled = buff != null && icon.sprite != null;
            }

            // 设置层数显示（仅当层数 > 1 时显示）
            if (stackText != null)
            {
                if (stacks > 1)
                {
                    stackText.text = stacks.ToString();
                    stackText.enabled = true;
                }
                else
                {
                    stackText.text = string.Empty;
                    stackText.enabled = false;
                }
            }

            // 记录结束时间点，用于后续计时更新
            endTime = remaining > 0f ? Time.time + remaining : -1f;
            UpdateTimer();
        }

        /// <summary>
        /// 更新剩余时间显示。
        /// 由 BuffBarUI 在 Update 中调用以实时刷新倒计时。
        /// </summary>
        public void UpdateTimer()
        {
            if (timerText == null)
            {
                return;
            }

            // -1 表示永久效果，不显示计时
            if (endTime <= 0f)
            {
                timerText.text = string.Empty;
                timerText.enabled = false;
                return;
            }

            // 计算并显示剩余秒数（向上取整）
            var remaining = Mathf.Max(0f, endTime - Time.time);
            var seconds = Mathf.CeilToInt(remaining);
            timerText.text = seconds.ToString();
            timerText.enabled = true;
        }
    }
}
