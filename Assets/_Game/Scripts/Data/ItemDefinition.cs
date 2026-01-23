using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 物品基础定义，作为 ScriptableObject 存储静态数据。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Items/Item Definition", fileName = "Item_")]
    public class ItemDefinition : DefinitionBase
    {
        [Header("表现与显示")]
        [Tooltip("物品在 UI 中显示的图标")]
        [SerializeField] private Sprite icon;
        [Tooltip("默认稀有度")]
        [SerializeField] private ItemRarity rarity = ItemRarity.Common;
        [Tooltip("物品详情描述")]
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("Equipment")]
        [SerializeField] private ItemSlot slot = ItemSlot.None;
        [SerializeField] private bool allowAffixes = true;
        [SerializeField] private List<ModifierDefinition> baseModifiers = new List<ModifierDefinition>();
        [SerializeField] private List<BuffDefinition> equipBuffs = new List<BuffDefinition>();

        [Header("堆叠属性")]
        [Tooltip("是否支持在同一个格子内堆叠")]
        [SerializeField] private bool stackable;
        [Tooltip("最大堆叠上限（建议至少为 1）")]
        [SerializeField] private int maxStack = 1;

        public Sprite Icon => icon;
        public ItemRarity Rarity => rarity;
        public string Description => description;
        public ItemSlot Slot => slot;
        public bool AllowAffixes => allowAffixes;
        public bool IsEquippable => slot != ItemSlot.None;
        public bool IsStackable => stackable;
        public int MaxStack => Mathf.Max(1, maxStack);
        public IReadOnlyList<ModifierDefinition> BaseModifiers => baseModifiers;
        public IReadOnlyList<BuffDefinition> EquipBuffs => equipBuffs;
    }
}
