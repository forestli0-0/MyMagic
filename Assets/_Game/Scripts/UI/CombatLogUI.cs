using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 战斗日志 UI 组件 - 以滚动列表形式展示战斗相关的文本信息。
    /// 支持固定行数，超出时自动滚动（移除最旧的条目）。
    /// </summary>
    public class CombatLogUI : MonoBehaviour
    {
        [Header("显示设置")]
        [Tooltip("用于显示日志文本的 Text 组件")]
        [SerializeField] private Text logText;
        
        [Tooltip("最大显示行数")]
        [SerializeField] private int maxLines = 6;

        /// <summary>
        /// 环形缓冲区：存储日志条目
        /// </summary>
        private string[] entries;
        
        /// <summary>
        /// 当前已有的条目数量
        /// </summary>
        private int count;
        
        /// <summary>
        /// 用于拼接日志文本的 StringBuilder，避免频繁字符串分配
        /// </summary>
        private readonly StringBuilder builder = new StringBuilder(256);

        private void Awake()
        {
            // 初始化条目数组
            entries = new string[Mathf.Max(1, maxLines)];
            RefreshText();
        }

        /// <summary>
        /// 添加一条新的日志条目。
        /// 如果已达到最大行数，会移除最旧的条目。
        /// </summary>
        /// <param name="entry">要添加的日志文本</param>
        public void AddEntry(string entry)
        {
            if (string.IsNullOrEmpty(entry))
            {
                return;
            }

            // 确保数组已初始化且大小正确
            if (entries == null || entries.Length != maxLines)
            {
                entries = new string[Mathf.Max(1, maxLines)];
                count = 0;
            }

            if (count < entries.Length)
            {
                // 未满：直接追加
                entries[count] = entry;
                count++;
            }
            else
            {
                // 已满：移除最旧的条目（索引 0），整体前移
                for (int i = 1; i < entries.Length; i++)
                {
                    entries[i - 1] = entries[i];
                }

                // 新条目放入最后位置
                entries[entries.Length - 1] = entry;
            }

            RefreshText();
        }

        /// <summary>
        /// 重新拼接所有条目并更新显示文本。
        /// </summary>
        private void RefreshText()
        {
            if (logText == null)
            {
                return;
            }

            builder.Length = 0;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(entries[i]);
            }

            logText.text = builder.ToString();
        }
    }
}
