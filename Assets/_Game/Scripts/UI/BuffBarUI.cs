using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// Buff 状态栏 UI 组件 - 负责展示单位当前所有激活的 Buff/Debuff 效果。
    /// 通过绑定 BuffController 来自动同步显示状态。
    /// </summary>
    public class BuffBarUI : MonoBehaviour
    {
        [Header("布局设置")]
        [Tooltip("Buff 图标的父容器，用于收集子节点图标")]
        [SerializeField] private Transform iconRoot;
        
        [Tooltip("预分配的 Buff 图标槽位列表")]
        [SerializeField] private List<BuffIconUI> icons = new List<BuffIconUI>(16);
        
        [Tooltip("最大显示图标数量覆盖(-1 表示使用外部传入的值)")]
        [SerializeField] private int maxIconsOverride = -1;
        
        [Tooltip("是否在 Update 中持续刷新剩余时间")]
        [SerializeField] private bool showDuration = true;

        /// <summary>
        /// 当前绑定的 Buff 控制器引用
        /// </summary>
        private BuffController buffController;
        private readonly List<BuffDisplayData> displayBuffs = new List<BuffDisplayData>(16);
        private readonly Dictionary<BuffDefinition, int> displayIndexByBuff = new Dictionary<BuffDefinition, int>(16);

        private void Awake()
        {
            // 初始化时收集子节点中的图标组件
            CollectIcons();
        }

        /// <summary>
        /// 绑定 Buff 控制器并初始化显示。
        /// </summary>
        /// <param name="controller">要监听的 BuffController</param>
        /// <param name="maxIcons">最大可显示的图标数量</param>
        public void Bind(BuffController controller, int maxIcons)
        {
            buffController = controller;
            CollectIcons();
            Refresh(maxIcons);
        }

        /// <summary>
        /// 刷新所有 Buff 图标显示状态。
        /// 根据当前激活的 Buff 列表更新每个槽位的内容。
        /// </summary>
        /// <param name="maxIcons">最大显示数量</param>
        public void Refresh(int maxIcons)
        {
            if (buffController == null)
            {
                return;
            }

            BuildDisplayList();
            // 确定实际显示数量：取配置覆盖值、外部传入值、实际 Buff 数和可用槽位数的最小值
            var limit = maxIconsOverride >= 0 ? maxIconsOverride : maxIcons;
            var count = Mathf.Min(limit, displayBuffs.Count, icons.Count);

            for (int i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                if (icon == null)
                {
                    continue;
                }

                if (i < count)
                {
                    // 在有效范围内：绑定对应的 Buff 数据
                    var entry = displayBuffs[i];
                    icon.gameObject.SetActive(true);
                    icon.Bind(entry.Definition, entry.Stacks, entry.Remaining);
                }
                else
                {
                    // 超出范围：隐藏多余的槽位
                    icon.gameObject.SetActive(false);
                }
            }
        }

        private void Update()
        {
            // 如果不需要实时刷新时间，则跳过
            if (!showDuration)
            {
                return;
            }

            // 遍历所有激活的图标，更新其计时器显示
            for (int i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                if (icon != null && icon.gameObject.activeSelf)
                {
                    icon.UpdateTimer();
                }
            }
        }

        /// <summary>
        /// 从 iconRoot 子节点中收集所有 BuffIconUI 组件。
        /// 仅在 icons 列表为空时执行收集。
        /// </summary>
        private void CollectIcons()
        {
            if (iconRoot == null)
            {
                iconRoot = transform;
            }

            if (icons.Count == 0)
            {
                iconRoot.GetComponentsInChildren(true, icons);
            }
        }

        private void BuildDisplayList()
        {
            displayBuffs.Clear();
            displayIndexByBuff.Clear();

            var buffs = buffController.ActiveBuffs;
            var now = Time.time;

            for (int i = 0; i < buffs.Count; i++)
            {
                var instance = buffs[i];
                var definition = instance.Definition;
                if (definition == null)
                {
                    continue;
                }

                var stacks = Mathf.Max(1, instance.Stacks);
                var remaining = instance.EndTime > 0f ? Mathf.Max(0f, instance.EndTime - now) : -1f;
                var appliedTime = instance.AppliedTime;

                if (displayIndexByBuff.TryGetValue(definition, out var index))
                {
                    var data = displayBuffs[index];
                    data.Stacks += stacks;

                    if (data.Remaining < 0f || remaining < 0f)
                    {
                        data.Remaining = -1f;
                    }
                    else if (remaining > data.Remaining)
                    {
                        data.Remaining = remaining;
                    }

                    if (appliedTime < data.FirstAppliedTime)
                    {
                        data.FirstAppliedTime = appliedTime;
                    }

                    displayBuffs[index] = data;
                    continue;
                }

                displayIndexByBuff.Add(definition, displayBuffs.Count);
                displayBuffs.Add(new BuffDisplayData(definition, stacks, remaining, appliedTime));
            }

            displayBuffs.Sort((a, b) => a.FirstAppliedTime.CompareTo(b.FirstAppliedTime));
        }

        private struct BuffDisplayData
        {
            public BuffDefinition Definition;
            public int Stacks;
            public float Remaining;
            public float FirstAppliedTime;

            public BuffDisplayData(BuffDefinition definition, int stacks, float remaining, float appliedTime)
            {
                Definition = definition;
                Stacks = stacks;
                Remaining = remaining;
                FirstAppliedTime = appliedTime;
            }
        }
    }
}
