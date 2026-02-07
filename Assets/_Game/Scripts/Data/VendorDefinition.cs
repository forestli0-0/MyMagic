using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 商人定义：配置商人的商品列表和价格倍率。
    /// 作为 ScriptableObject 存储静态配置数据。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Vendors/Vendor Definition", fileName = "Vendor_")]
    public class VendorDefinition : DefinitionBase
    {
        [SerializeField] private int vendorRuntimeVersion = 1;

        [Header("Pricing")]
        [Tooltip("购买价格倍率，玩家从商人处购买时的价格 = 物品基础价格 × 此倍率")]
        [SerializeField] private float buyPriceMultiplier = 1f;
        [Tooltip("出售价格倍率，玩家卖给商人时的价格 = 物品基础价格 × 此倍率")]
        [SerializeField] private float sellPriceMultiplier = 0.5f;

        [Header("Inventory")]
        [Tooltip("商人出售的商品列表")]
        [SerializeField] private List<VendorItemEntry> items = new List<VendorItemEntry>();

        [Header("Identity")]
        [Tooltip("Optional vendor tag for scene queries or save mapping")]
        [SerializeField] private string vendorTag = "default";

        [Header("Runtime")]
        [Tooltip("Whether sold items should be available in buy-back list")]
        [SerializeField] private bool allowBuyBack = true;
        [Tooltip("Max number of buy-back entries")]
        [SerializeField] private int maxBuyBackEntries = 12;
        [Tooltip("Inventory refresh mode")]
        [SerializeField] private VendorRefreshMode refreshMode = VendorRefreshMode.Never;
        [Tooltip("Refresh interval in minutes when using timed refresh")]
        [SerializeField] private int refreshIntervalMinutes = 30;
        [Tooltip("Restock inventory whenever vendor UI opens")]
        [SerializeField] private bool restockOnOpen = false;

        /// <summary>购买价格倍率（至少为0）</summary>
        public float BuyPriceMultiplier => Mathf.Max(0f, buyPriceMultiplier);
        /// <summary>出售价格倍率（至少为0）</summary>
        public float SellPriceMultiplier => Mathf.Max(0f, sellPriceMultiplier);
        /// <summary>商品列表</summary>
        public IReadOnlyList<VendorItemEntry> Items => items;
        /// <summary>商人标签</summary>
        public string VendorTag => string.IsNullOrWhiteSpace(vendorTag) ? "default" : vendorTag.Trim();
        /// <summary>是否支持回购</summary>
        public bool AllowBuyBack => vendorRuntimeVersion <= 0 || allowBuyBack;
        /// <summary>回购列表容量</summary>
        public int MaxBuyBackEntries => vendorRuntimeVersion <= 0 ? 12 : Mathf.Max(0, maxBuyBackEntries);
        /// <summary>刷新模式</summary>
        public VendorRefreshMode RefreshMode => vendorRuntimeVersion <= 0 ? VendorRefreshMode.Never : refreshMode;
        /// <summary>定时刷新间隔（分钟）</summary>
        public int RefreshIntervalMinutes => vendorRuntimeVersion <= 0 ? 30 : Mathf.Max(1, refreshIntervalMinutes);
        /// <summary>进入商店时是否补货</summary>
        public bool RestockOnOpen => vendorRuntimeVersion > 0 && restockOnOpen;
    }

    public enum VendorRefreshMode
    {
        Never = 0,
        Timed = 1
    }

    /// <summary>
    /// 商人商品条目：定义商人出售的单个商品配置
    /// </summary>
    [Serializable]
    public class VendorItemEntry
    {
        [Tooltip("出售的物品定义")]
        [SerializeField] private ItemDefinition item;
        [Tooltip("价格覆盖，-1 表示使用物品基础价格")]
        [SerializeField] private int priceOverride = -1;
        [Tooltip("库存数量（仅在非无限库存时有效）")]
        [SerializeField] private int stock = 1;
        [Tooltip("是否为无限库存")]
        [SerializeField] private bool infiniteStock = true;

        /// <summary>物品定义</summary>
        public ItemDefinition Item => item;
        /// <summary>价格覆盖值，-1 表示使用默认价格</summary>
        public int PriceOverride => priceOverride;
        /// <summary>库存数量</summary>
        public int Stock => Mathf.Max(0, stock);
        /// <summary>是否为无限库存</summary>
        public bool InfiniteStock => infiniteStock;
    }
}
