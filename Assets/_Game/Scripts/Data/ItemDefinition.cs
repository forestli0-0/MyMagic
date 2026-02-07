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

        [Header("Economy")]
        [SerializeField] private int economyVersion = 1;
        [Tooltip("Base price for vendor buy/sell calculations")]
        [SerializeField] private int basePrice = 1;
        [Tooltip("Override buy price. -1 means using base price")]
        [SerializeField] private int buyPriceOverride = -1;
        [Tooltip("Override sell price. -1 means using base price")]
        [SerializeField] private int sellPriceOverride = -1;
        [Tooltip("Whether this item can be bought from vendors")]
        [SerializeField] private bool canBuy = true;
        [Tooltip("Whether this item can be sold to vendors")]
        [SerializeField] private bool canSell = true;
        [Tooltip("Item level used by economy or progression scaling")]
        [SerializeField] private int itemLevel = 1;
        [Tooltip("Economy category used for future vendor filters")]
        [SerializeField] private ItemCategory itemCategory = ItemCategory.General;

        public Sprite Icon => icon;
        public ItemRarity Rarity => rarity;
        public string Description => description;
        public ItemSlot Slot => slot;
        public bool AllowAffixes => allowAffixes;
        public bool IsEquippable => slot != ItemSlot.None;
        public bool IsStackable => stackable;
        public int MaxStack => Mathf.Max(1, maxStack);
        public int BasePrice => Mathf.Max(0, basePrice);
        public int BuyPriceOverride => economyVersion <= 0 ? -1 : (buyPriceOverride >= 0 ? buyPriceOverride : -1);
        public int SellPriceOverride => economyVersion <= 0 ? -1 : (sellPriceOverride >= 0 ? sellPriceOverride : -1);
        public bool CanBuy => economyVersion <= 0 || canBuy;
        public bool CanSell => economyVersion <= 0 || canSell;
        public int ItemLevel => economyVersion <= 0 ? 1 : Mathf.Max(1, itemLevel);
        public ItemCategory Category => economyVersion <= 0 ? ItemCategory.General : itemCategory;
        public IReadOnlyList<ModifierDefinition> BaseModifiers => baseModifiers;
        public IReadOnlyList<BuffDefinition> EquipBuffs => equipBuffs;
    }

    public enum ItemCategory
    {
        General = 0,
        Weapon = 1,
        Armor = 2,
        Accessory = 3,
        Consumable = 4,
        Material = 5,
        Quest = 6
    }
}
