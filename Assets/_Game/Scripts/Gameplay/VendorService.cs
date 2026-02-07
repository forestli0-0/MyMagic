using System;
using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 商人服务：处理玩家与商人之间的交易逻辑。
    /// 管理运行时商品库存状态，提供购买和出售接口。
    /// </summary>
    public class VendorService : MonoBehaviour
    {
        [Header("Vendor")]
        [Tooltip("商人定义配置")]
        [SerializeField] private VendorDefinition vendorDefinition;
        [Tooltip("是否在启用时自动初始化")]
        [SerializeField] private bool initializeOnEnable = true;

        [Header("Player")]
        [Tooltip("玩家背包组件引用")]
        [SerializeField] private InventoryComponent playerInventory;
        [Tooltip("玩家货币组件引用")]
        [SerializeField] private CurrencyComponent playerCurrency;
        [Tooltip("玩家标签，用于自动查找")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("是否自动查找玩家引用")]
        [SerializeField] private bool autoFindPlayer = true;

        /// <summary>
        /// 运行时商品状态列表，包含库存信息
        /// </summary>
        private readonly List<VendorItemState> runtimeItems = new List<VendorItemState>(16);
        private readonly List<VendorBuyBackEntry> buyBackItems = new List<VendorBuyBackEntry>(16);
        private DateTime nextRefreshUtc = DateTime.MinValue;

        /// <summary>
        /// 商人数据更新事件（如库存变化）
        /// </summary>
        public event Action VendorUpdated;

        /// <summary>商人定义配置</summary>
        public VendorDefinition Definition => vendorDefinition;
        /// <summary>运行时商品列表</summary>
        public IReadOnlyList<VendorItemState> Items => runtimeItems;
        /// <summary>回购列表（最近售出的物品）</summary>
        public IReadOnlyList<VendorBuyBackEntry> BuyBackItems => buyBackItems;
        /// <summary>最近一次交易失败原因（用于 UI 提示）</summary>
        public string LastFailureReason { get; private set; } = string.Empty;

        private void OnEnable()
        {
            if (initializeOnEnable)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 初始化商人服务
        /// </summary>
        public void Initialize()
        {
            EnsurePlayerReferences();
            if (vendorDefinition != null && vendorDefinition.RestockOnOpen)
            {
                BuildRuntimeItems(true);
                return;
            }

            TryRefreshInventory();
            if (runtimeItems.Count == 0)
            {
                BuildRuntimeItems();
            }
        }

        /// <summary>
        /// 设置新的商人定义并刷新商品
        /// </summary>
        public void SetVendor(VendorDefinition definition)
        {
            vendorDefinition = definition;
            BuildRuntimeItems(true);
        }

        /// <summary>
        /// 尝试购买指定索引的商品
        /// </summary>
        /// <param name="index">商品索引</param>
        /// <param name="quantity">购买数量</param>
        /// <returns>购买成功返回 true</returns>
        public bool TryBuy(int index, int quantity = 1)
        {
            TryRefreshInventory();
            if (index < 0 || index >= runtimeItems.Count)
            {
                SetFailure("未选择有效商品。");
                return false;
            }

            return TryBuy(runtimeItems[index], quantity);
        }

        /// <summary>
        /// 尝试购买指定商品
        /// </summary>
        /// <param name="item">商品状态对象</param>
        /// <param name="quantity">购买数量</param>
        /// <returns>购买成功返回 true</returns>
        public bool TryBuy(VendorItemState item, int quantity = 1)
        {
            if (!CanBuy(item, quantity, out var reason))
            {
                SetFailure(reason);
                return false;
            }

            EnsurePlayerReferences();
            if (playerInventory == null || playerCurrency == null)
            {
                SetFailure("玩家背包或货币组件缺失。");
                return false;
            }

            // 计算总价并创建物品实例
            var price = item.Price * quantity;
            var purchase = new ItemInstance(item.Definition, quantity, item.Definition.Rarity, null);
            
            // 检查背包是否有空间
            if (!playerInventory.CanAddItem(purchase))
            {
                SetFailure("背包空间不足。");
                return false;
            }

            // 尝试扣除货币
            if (!playerCurrency.TrySpend(price))
            {
                SetFailure("金币不足。");
                return false;
            }

            // 添加物品到背包
            if (!playerInventory.TryAddItem(purchase))
            {
                // 如果添加失败，退还货币
                playerCurrency.Add(price);
                SetFailure("物品放入背包失败。");
                return false;
            }

            // 扣减商人库存并通知更新
            item.ConsumeStock(quantity);
            ClearFailure();
            VendorUpdated?.Invoke();
            return true;
        }

        public bool CanBuy(int index, int quantity, out string reason)
        {
            TryRefreshInventory();
            reason = string.Empty;
            if (index < 0 || index >= runtimeItems.Count)
            {
                reason = "未选择有效商品。";
                return false;
            }

            return CanBuy(runtimeItems[index], quantity, out reason);
        }

        public bool CanBuy(VendorItemState item, int quantity, out string reason)
        {
            reason = string.Empty;
            if (item == null || item.Definition == null)
            {
                reason = "商品数据无效。";
                return false;
            }

            if (quantity <= 0)
            {
                reason = "购买数量必须大于 0。";
                return false;
            }

            if (!item.Definition.CanBuy)
            {
                reason = "该物品不可购买。";
                return false;
            }

            if (!item.CanBuy(quantity))
            {
                reason = "库存不足。";
                return false;
            }

            EnsurePlayerReferences();
            if (playerInventory == null || playerCurrency == null)
            {
                reason = "玩家背包或货币组件缺失。";
                return false;
            }

            var purchase = new ItemInstance(item.Definition, quantity, item.Definition.Rarity, null);
            if (!playerInventory.CanAddItem(purchase))
            {
                reason = "背包空间不足。";
                return false;
            }

            var totalPrice = item.Price * quantity;
            if (!playerCurrency.CanAfford(totalPrice))
            {
                reason = "金币不足。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 尝试出售物品给商人
        /// </summary>
        /// <param name="item">要出售的物品实例</param>
        /// <param name="quantity">出售数量</param>
        /// <returns>出售成功返回 true</returns>
        public bool TrySell(ItemInstance item, int quantity = 1)
        {
            if (!CanSell(item, quantity, out var reason))
            {
                SetFailure(reason);
                return false;
            }

            EnsurePlayerReferences();
            if (playerInventory == null || playerCurrency == null)
            {
                SetFailure("玩家背包或货币组件缺失。");
                return false;
            }

            // 确保不超过实际拥有数量
            if (item.Stack < quantity)
            {
                quantity = item.Stack;
            }

            var soldItem = item.CloneWithStack(quantity);

            // 从背包移除物品
            if (!playerInventory.TryRemoveItem(item, quantity))
            {
                SetFailure("无法从背包移除该物品。");
                return false;
            }

            // 计算并支付出售金额
            var payout = ResolveSellPrice(item.Definition) * quantity;
            if (payout > 0)
            {
                playerCurrency.Add(payout);
            }

            RegisterBuyBackEntry(soldItem);
            ClearFailure();
            VendorUpdated?.Invoke();
            return true;
        }

        public bool CanSell(ItemInstance item, int quantity, out string reason)
        {
            reason = string.Empty;
            if (item == null || item.Definition == null)
            {
                reason = "未选择可出售物品。";
                return false;
            }

            if (quantity <= 0)
            {
                reason = "出售数量必须大于 0。";
                return false;
            }

            if (!item.Definition.CanSell)
            {
                reason = "该物品不可出售。";
                return false;
            }

            if (ResolveSellPrice(item.Definition) <= 0)
            {
                reason = "该物品无法折现。";
                return false;
            }

            EnsurePlayerReferences();
            if (playerInventory == null || playerCurrency == null)
            {
                reason = "玩家背包或货币组件缺失。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取物品的出售价格
        /// </summary>
        public int GetSellPrice(ItemDefinition definition)
        {
            return ResolveSellPrice(definition);
        }

        public int ResolveBuyPrice(ItemDefinition definition)
        {
            if (definition == null || vendorDefinition == null)
            {
                return 0;
            }

            var basePrice = definition.BuyPriceOverride >= 0 ? definition.BuyPriceOverride : definition.BasePrice;
            return ResolvePrice(basePrice, vendorDefinition.BuyPriceMultiplier);
        }

        public int ResolveSellPrice(ItemDefinition definition)
        {
            if (definition == null || vendorDefinition == null)
            {
                return 0;
            }

            var basePrice = definition.SellPriceOverride >= 0 ? definition.SellPriceOverride : definition.BasePrice;
            return ResolvePrice(basePrice, vendorDefinition.SellPriceMultiplier);
        }

        public bool TryBuyBack(int index, int quantity = 1)
        {
            if (index < 0 || index >= buyBackItems.Count)
            {
                SetFailure("回购物品索引无效。");
                return false;
            }

            var entry = buyBackItems[index];
            if (entry == null || entry.Item == null || entry.Item.Definition == null)
            {
                SetFailure("回购数据已失效。");
                return false;
            }

            if (quantity <= 0 || quantity > entry.Item.Stack)
            {
                SetFailure("回购数量无效。");
                return false;
            }

            EnsurePlayerReferences();
            if (playerInventory == null || playerCurrency == null)
            {
                SetFailure("玩家背包或货币组件缺失。");
                return false;
            }

            var totalPrice = entry.UnitPrice * quantity;
            if (!playerCurrency.TrySpend(totalPrice))
            {
                SetFailure("金币不足。");
                return false;
            }

            var itemToAdd = entry.Item.CloneWithStack(quantity);
            if (!playerInventory.TryAddItem(itemToAdd))
            {
                playerCurrency.Add(totalPrice);
                SetFailure("背包空间不足。");
                return false;
            }

            entry.Consume(quantity);
            if (entry.IsEmpty)
            {
                buyBackItems.RemoveAt(index);
            }

            ClearFailure();
            VendorUpdated?.Invoke();
            return true;
        }

        /// <summary>
        /// 根据配置构建运行时商品列表
        /// </summary>
        private void BuildRuntimeItems(bool resetRefreshTimer = false)
        {
            runtimeItems.Clear();
            if (vendorDefinition == null || vendorDefinition.Items == null)
            {
                if (vendorDefinition == null || !vendorDefinition.AllowBuyBack)
                {
                    buyBackItems.Clear();
                }

                if (resetRefreshTimer)
                {
                    ResetRefreshTimer();
                }

                VendorUpdated?.Invoke();
                return;
            }

            var entries = vendorDefinition.Items;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Item == null)
                {
                    continue;
                }

                runtimeItems.Add(new VendorItemState(entry, ResolveBuyPrice(entry)));
            }

            if (!vendorDefinition.AllowBuyBack)
            {
                buyBackItems.Clear();
            }

            if (resetRefreshTimer)
            {
                ResetRefreshTimer();
            }

            VendorUpdated?.Invoke();
        }

        /// <summary>
        /// 计算商品的购买价格
        /// </summary>
        private int ResolveBuyPrice(VendorItemEntry entry)
        {
            if (entry == null || entry.Item == null || vendorDefinition == null)
            {
                return 0;
            }

            // 优先级：条目覆盖 > 物品购买覆盖 > 基础价格
            var explicitPrice = entry.PriceOverride >= 0 ? entry.PriceOverride : entry.Item.BuyPriceOverride;
            var basePrice = explicitPrice >= 0 ? explicitPrice : entry.Item.BasePrice;
            return ResolvePrice(basePrice, vendorDefinition.BuyPriceMultiplier);
        }

        private static int ResolvePrice(int basePrice, float multiplier)
        {
            if (multiplier <= 0f)
            {
                return 0;
            }

            // 兼容历史资产：未序列化 basePrice 时默认是 0，给出可交易的最小基准价。
            var safeBasePrice = Mathf.Max(1, basePrice);
            var price = Mathf.RoundToInt(safeBasePrice * multiplier);
            return Mathf.Max(1, price);
        }

        private void TryRefreshInventory()
        {
            if (vendorDefinition == null || vendorDefinition.RefreshMode == VendorRefreshMode.Never)
            {
                return;
            }

            if (nextRefreshUtc == DateTime.MinValue || DateTime.UtcNow >= nextRefreshUtc)
            {
                BuildRuntimeItems(true);
            }
        }

        private void ResetRefreshTimer()
        {
            if (vendorDefinition == null || vendorDefinition.RefreshMode == VendorRefreshMode.Never)
            {
                nextRefreshUtc = DateTime.MinValue;
                return;
            }

            nextRefreshUtc = DateTime.UtcNow.AddMinutes(vendorDefinition.RefreshIntervalMinutes);
        }

        private void RegisterBuyBackEntry(ItemInstance soldItem)
        {
            if (soldItem == null || soldItem.Definition == null || vendorDefinition == null || !vendorDefinition.AllowBuyBack)
            {
                return;
            }

            if (vendorDefinition.MaxBuyBackEntries <= 0)
            {
                return;
            }

            var price = ResolveBuyPrice(soldItem.Definition);
            if (price <= 0)
            {
                return;
            }

            buyBackItems.Insert(0, new VendorBuyBackEntry(soldItem, price));
            while (buyBackItems.Count > vendorDefinition.MaxBuyBackEntries)
            {
                buyBackItems.RemoveAt(buyBackItems.Count - 1);
            }
        }

        private void SetFailure(string reason)
        {
            LastFailureReason = reason ?? string.Empty;
        }

        private void ClearFailure()
        {
            LastFailureReason = string.Empty;
        }

        /// <summary>
        /// 确保玩家引用有效，自动查找缺失的引用
        /// </summary>
        private void EnsurePlayerReferences()
        {
            if (!autoFindPlayer)
            {
                return;
            }

            if (playerInventory != null && playerCurrency != null)
            {
                return;
            }

            // 通过标签查找玩家
            if (!string.IsNullOrEmpty(playerTag))
            {
                var player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    if (playerInventory == null)
                    {
                        playerInventory = player.GetComponent<InventoryComponent>();
                    }

                    if (playerCurrency == null)
                    {
                        playerCurrency = player.GetComponent<CurrencyComponent>();
                    }
                }
            }

            // 如果标签查找失败，使用全局查找
            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<InventoryComponent>();
            }

            if (playerCurrency == null)
            {
                playerCurrency = FindFirstObjectByType<CurrencyComponent>();
            }
        }
    }

    /// <summary>
    /// 商品运行时状态：包含商品的当前库存和价格信息
    /// </summary>
    [Serializable]
    public class VendorItemState
    {
        [SerializeField] private VendorItemEntry entry;
        [SerializeField] private int remainingStock;
        [SerializeField] private int price;

        /// <summary>商品条目配置</summary>
        public VendorItemEntry Entry => entry;
        /// <summary>物品定义</summary>
        public ItemDefinition Definition => entry != null ? entry.Item : null;
        /// <summary>购买价格</summary>
        public int Price => price;
        /// <summary>剩余库存（-1 表示无限）</summary>
        public int RemainingStock => remainingStock;
        /// <summary>是否为无限库存</summary>
        public bool InfiniteStock => entry != null && entry.InfiniteStock;
        /// <summary>是否售罄（无限库存始终为否）</summary>
        public bool IsSoldOut => !InfiniteStock && remainingStock <= 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="entry">商品条目配置</param>
        /// <param name="price">计算后的价格</param>
        public VendorItemState(VendorItemEntry entry, int price)
        {
            this.entry = entry;
            this.price = price;
            // 无限库存时库存值为 -1
            remainingStock = entry != null && !entry.InfiniteStock ? entry.Stock : -1;
        }

        /// <summary>
        /// 检查是否可以购买指定数量
        /// </summary>
        public bool CanBuy(int quantity)
        {
            if (quantity <= 0)
            {
                return false;
            }

            if (entry == null)
            {
                return false;
            }

            // 无限库存总是可以购买
            if (entry.InfiniteStock)
            {
                return true;
            }

            return remainingStock >= quantity;
        }

        /// <summary>
        /// 消耗库存
        /// </summary>
        public void ConsumeStock(int quantity)
        {
            if (entry == null || entry.InfiniteStock)
            {
                return;
            }

            remainingStock = Mathf.Max(0, remainingStock - Mathf.Max(0, quantity));
        }
    }

    [Serializable]
    public class VendorBuyBackEntry
    {
        [SerializeField] private ItemInstance item;
        [SerializeField] private int unitPrice;
        [SerializeField] private long soldUtcTicks;

        public ItemInstance Item => item;
        public int UnitPrice => Mathf.Max(0, unitPrice);
        public long SoldUtcTicks => soldUtcTicks;
        public bool IsEmpty => item == null || item.Definition == null || item.Stack <= 0;

        public VendorBuyBackEntry(ItemInstance soldItem, int unitPrice)
        {
            item = soldItem != null ? soldItem.CloneWithStack(soldItem.Stack) : null;
            this.unitPrice = Mathf.Max(1, unitPrice);
            soldUtcTicks = DateTime.UtcNow.Ticks;
        }

        public void Consume(int quantity)
        {
            if (item == null)
            {
                return;
            }

            var remaining = item.Stack - Mathf.Max(0, quantity);
            if (remaining <= 0)
            {
                item = null;
                return;
            }

            item.SetStack(remaining);
        }
    }
}
