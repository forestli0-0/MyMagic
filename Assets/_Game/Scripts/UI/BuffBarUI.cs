using System.Collections.Generic;
using CombatSystem.Core;
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

            var buffs = buffController.ActiveBuffs;
            // 确定实际显示数量：取配置覆盖值、外部传入值、实际 Buff 数和可用槽位数的最小值
            var limit = maxIconsOverride >= 0 ? maxIconsOverride : maxIcons;
            var count = Mathf.Min(limit, buffs.Count, icons.Count);

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
                    var instance = buffs[i];
                    // 计算剩余时间，-1 表示永久 Buff
                    var remaining = instance.EndTime > 0f ? Mathf.Max(0f, instance.EndTime - Time.time) : -1f;
                    icon.gameObject.SetActive(true);
                    icon.Bind(instance.Definition, instance.Stacks, remaining);
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
    }
}
