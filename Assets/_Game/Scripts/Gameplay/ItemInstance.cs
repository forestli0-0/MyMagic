using System;
using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 物品实例，代表一件具体存在于背包或装备栏中的物品，包含其属性差异（如数量、稀有度、词缀）。
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        [Tooltip("物品基础定义")]
        [SerializeField] private ItemDefinition definition;
        [Tooltip("当前堆叠数量")]
        [SerializeField] private int stack = 1;
        [Tooltip("稀有度级别")]
        [SerializeField] private ItemRarity rarity = ItemRarity.Common;
        [Tooltip("该实例随机携带的词缀列表")]
        [SerializeField] private List<AffixDefinition> affixes = new List<AffixDefinition>();

        public ItemDefinition Definition => definition;
        public int Stack => stack;
        public ItemRarity Rarity => rarity;
        public IReadOnlyList<AffixDefinition> Affixes => affixes;

        public bool IsStackable => definition != null && definition.IsStackable && (affixes == null || affixes.Count == 0);
        public int MaxStack => definition != null ? definition.MaxStack : 1;

        public ItemInstance(ItemDefinition definition, int stack = 1, ItemRarity rarity = ItemRarity.Common, IReadOnlyList<AffixDefinition> affixes = null)
        {
            this.definition = definition;
            this.rarity = rarity;
            // 堆叠上限取决于定义，先初始化 definition 再计算
            this.stack = Mathf.Clamp(stack, 1, MaxStack);
            if (affixes != null)
            {
                this.affixes = new List<AffixDefinition>(affixes);
            }
        }

        public void SetStack(int value)
        {
            stack = Mathf.Clamp(value, 1, MaxStack);
        }

        public ItemInstance CloneWithStack(int newStack)
        {
            // 复制时保留词缀与稀有度，仅调整堆叠
            return new ItemInstance(definition, newStack, rarity, affixes);
        }
    }
}
