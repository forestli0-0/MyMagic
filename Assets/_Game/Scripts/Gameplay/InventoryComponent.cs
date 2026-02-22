using System;
using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 背包组件，负责管理单位的物品存储、堆叠逻辑、物品移动及交换。
    /// </summary>
    public class InventoryComponent : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("背包容量（格子数量）")]
        [SerializeField] private int capacity = 24;
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool initializeOnAwake = true;
        [Tooltip("初始获取的物品列表")]
        [SerializeField] private List<ItemInstance> startingItems = new List<ItemInstance>();

        // 运行时物品列表，保持长度始终与 Capacity 一致，空位存 null
        private readonly List<ItemInstance> items = new List<ItemInstance>(24);

        /// <summary>当背包内容发生任何变化时触发</summary>
        public event Action InventoryChanged;

        /// <summary>当前背包容量</summary>
        public int Capacity => Mathf.Max(0, capacity);
        
        /// <summary>所有物品的快照（包含空位 null）</summary>
        public IReadOnlyList<ItemInstance> Items => items;

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 初始化背包，确保格子对齐并添加初始物品。
        /// </summary>
        public void Initialize()
        {
            items.Clear();
            // 固定槽位数量，保证背包索引稳定
            EnsureSlots();
            AddStartingItems();
            InventoryChanged?.Invoke();
        }

        /// <summary>
        /// 清空背包所有内容。
        /// </summary>
        public void Clear()
        {
            // 清空但保留槽位数量
            EnsureSlots();
            for (int i = 0; i < items.Count; i++)
            {
                items[i] = null;
            }
            InventoryChanged?.Invoke();
        }

        public void LoadItems(IReadOnlyList<ItemInstance> newItems)
        {
            items.Clear();
            // 先铺满槽位，再按索引恢复，避免顺序漂移
            EnsureSlots();
            if (newItems != null)
            {
                if (newItems.Count == Capacity)
                {
                    for (int i = 0; i < Capacity; i++)
                    {
                        items[i] = newItems[i];
                    }
                }
                else
                {
                    for (int i = 0; i < newItems.Count; i++)
                    {
                        var entry = newItems[i];
                        if (entry == null || entry.Definition == null)
                        {
                            continue;
                        }

                        // 没有槽位索引时，塞入第一个空位
                        var slotIndex = FindFirstEmptySlot();
                        if (slotIndex < 0)
                        {
                            break;
                        }

                        items[slotIndex] = entry;
                    }
                }
            }

            InventoryChanged?.Invoke();
        }

        /// <summary>
        /// 检查是否能够容纳指定的物品。
        /// </summary>
        public bool CanAddItem(ItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                return false;
            }

            // 基于固定槽位统计空位
            EnsureSlots();

            var remaining = Mathf.Max(1, item.Stack);
            var stackable = item.IsStackable;

            if (stackable)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var existing = items[i];
                    if (existing == null || existing.Definition != item.Definition || !existing.IsStackable)
                    {
                        continue;
                    }

                    var space = existing.MaxStack - existing.Stack;
                    if (space <= 0)
                    {
                        continue;
                    }

                    remaining -= space;
                    if (remaining <= 0)
                    {
                        return true;
                    }
                }
            }

            var freeSlots = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    freeSlots++;
                }
            }
            if (freeSlots <= 0)
            {
                return false;
            }

            var slotsNeeded = stackable ? Mathf.CeilToInt((float)remaining / item.MaxStack) : remaining;
            return freeSlots >= slotsNeeded;
        }

        /// <summary>
        /// 尝试添加物品到背包，自动处理堆叠。
        /// </summary>
        public bool TryAddItem(ItemInstance item)
        {
            return TryAddItem(item, out _);
        }

        public bool TryAddItem(ItemInstance item, out ItemInstance remainder)
        {
            remainder = null;
            if (item == null || item.Definition == null)
            {
                return false;
            }

            // 固定槽位写入，保证可预测的落位
            EnsureSlots();

            var remaining = Mathf.Max(1, item.Stack);
            var stackable = item.IsStackable;

            if (stackable)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var existing = items[i];
                    if (existing == null || existing.Definition != item.Definition || !existing.IsStackable)
                    {
                        continue;
                    }

                    var space = existing.MaxStack - existing.Stack;
                    if (space <= 0)
                    {
                        continue;
                    }

                    var take = Mathf.Min(space, remaining);
                    existing.SetStack(existing.Stack + take);
                    remaining -= take;
                    if (remaining <= 0)
                    {
                        InventoryChanged?.Invoke();
                        return true;
                    }
                }
            }

            while (remaining > 0)
            {
                // 找到第一个空槽并放入
                var slotIndex = FindFirstEmptySlot();
                if (slotIndex < 0)
                {
                    break;
                }

                var toAdd = stackable ? Mathf.Min(remaining, item.MaxStack) : 1;
                items[slotIndex] = item.CloneWithStack(toAdd);
                remaining -= toAdd;
            }

            InventoryChanged?.Invoke();

            if (remaining > 0)
            {
                remainder = item.CloneWithStack(remaining);
                return false;
            }

            return true;
        }

        public bool TryRemoveItem(ItemInstance item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return false;
            }

            // 确保槽位数量固定
            EnsureSlots();

            var index = items.IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            return TryRemoveAt(index, amount);
        }

        public bool TryRemoveAt(int index, int amount = 1)
        {
            if (index < 0 || index >= items.Count || amount <= 0)
            {
                return false;
            }

            // 固定槽位，删除时仅清空该格
            EnsureSlots();

            var entry = items[index];
            if (entry == null)
            {
                return false;
            }

            if (amount >= entry.Stack)
            {
                items[index] = null;
            }
            else
            {
                entry.SetStack(entry.Stack - amount);
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public bool TrySwapItems(int indexA, int indexB)
        {
            if (indexA == indexB)
            {
                return false;
            }

            // 交换两个固定槽位的内容
            EnsureSlots();

            if (indexA < 0 || indexB < 0 || indexA >= items.Count || indexB >= items.Count)
            {
                return false;
            }

            var temp = items[indexA];
            items[indexA] = items[indexB];
            items[indexB] = temp;
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryMoveItem(int fromIndex, int toIndex)
        {
            EnsureSlots();

            if (fromIndex < 0 || fromIndex >= items.Count)
            {
                return false;
            }

            if (toIndex < 0 || toIndex >= Capacity)
            {
                return false;
            }

            if (fromIndex == toIndex)
            {
                return false;
            }

            var item = items[fromIndex];
            if (item == null)
            {
                return false;
            }

            // 目标槽位必须是空的
            if (items[toIndex] != null)
            {
                return false;
            }

            items[toIndex] = item;
            items[fromIndex] = null;

            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryMergeStack(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
            {
                return false;
            }

            // 仅在两个槽位都存在且同类可堆叠时合并
            EnsureSlots();

            if (fromIndex < 0 || toIndex < 0 || fromIndex >= items.Count || toIndex >= items.Count)
            {
                return false;
            }

            var source = items[fromIndex];
            var target = items[toIndex];
            if (source == null || target == null)
            {
                return false;
            }

            if (!source.IsStackable || !target.IsStackable || source.Definition != target.Definition)
            {
                return false;
            }

            var space = target.MaxStack - target.Stack;
            if (space <= 0)
            {
                return false;
            }

            var move = Mathf.Min(space, source.Stack);
            target.SetStack(target.Stack + move);

            if (move >= source.Stack)
            {
                items[fromIndex] = null;
            }
            else
            {
                source.SetStack(source.Stack - move);
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public bool TrySetItemAt(int index, ItemInstance item, out ItemInstance replaced)
        {
            replaced = null;
            // 直接写入指定槽位，便于拖拽落点
            EnsureSlots();

            if (index < 0 || index >= items.Count)
            {
                return false;
            }

            replaced = items[index];
            items[index] = item;
            InventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 一键整理背包：可选自动合并堆叠，并将空槽位压缩到末尾。
        /// 默认排序规则：分类 -> 稀有度(降序) -> 名称 -> 价格(降序)。
        /// </summary>
        /// <param name="comparison">自定义排序比较器；为空时使用默认规则。</param>
        /// <param name="mergeStacks">是否先自动合并可堆叠物品。</param>
        /// <returns>执行了整理返回 true；背包为空返回 false。</returns>
        public bool TryAutoOrganize(Comparison<ItemInstance> comparison = null, bool mergeStacks = true)
        {
            EnsureSlots();
            if (items.Count == 0)
            {
                return false;
            }

            var occupied = new List<ItemInstance>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                if (entry != null && entry.Definition != null)
                {
                    occupied.Add(entry);
                }
            }

            if (occupied.Count == 0)
            {
                return false;
            }

            List<ItemInstance> normalized;
            if (mergeStacks)
            {
                normalized = BuildMergedStacks(occupied);
            }
            else
            {
                normalized = occupied;
            }

            normalized.Sort(comparison ?? CompareByDefaultOrganizeRule);

            for (int i = 0; i < items.Count; i++)
            {
                items[i] = i < normalized.Count ? normalized[i] : null;
            }

            InventoryChanged?.Invoke();
            return true;
        }

        private void EnsureSlots()
        {
            var target = Capacity;
            if (target <= 0)
            {
                items.Clear();
                return;
            }

            // 保证 items 长度固定为 Capacity
            while (items.Count < target)
            {
                items.Add(null);
            }

            if (items.Count > target)
            {
                items.RemoveRange(target, items.Count - target);
            }
        }

        private int FindFirstEmptySlot()
        {
            // 返回第一个空槽位索引
            EnsureSlots();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<ItemInstance> BuildMergedStacks(List<ItemInstance> source)
        {
            var merged = new List<ItemInstance>(source != null ? source.Count : 0);
            if (source == null || source.Count == 0)
            {
                return merged;
            }

            var stackBuckets = new Dictionary<ItemDefinition, StackBucket>(16);
            var bucketOrder = new List<StackBucket>(16);
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item == null || item.Definition == null)
                {
                    continue;
                }

                if (!item.IsStackable)
                {
                    merged.Add(item);
                    continue;
                }

                var key = item.Definition;
                if (!stackBuckets.TryGetValue(key, out var bucket))
                {
                    bucket = new StackBucket(item);
                    stackBuckets.Add(key, bucket);
                    bucketOrder.Add(bucket);
                }

                bucket.TotalStack += Mathf.Max(1, item.Stack);
            }

            for (int i = 0; i < bucketOrder.Count; i++)
            {
                var bucket = bucketOrder[i];
                var remaining = bucket.TotalStack;
                var maxStack = Mathf.Max(1, bucket.Prototype.MaxStack);
                while (remaining > 0)
                {
                    var take = Mathf.Min(maxStack, remaining);
                    merged.Add(bucket.Prototype.CloneWithStack(take));
                    remaining -= take;
                }
            }

            return merged;
        }

        private static int CompareByDefaultOrganizeRule(ItemInstance left, ItemInstance right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var leftDef = left.Definition;
            var rightDef = right.Definition;
            if (leftDef == null && rightDef == null)
            {
                return 0;
            }

            if (leftDef == null)
            {
                return 1;
            }

            if (rightDef == null)
            {
                return -1;
            }

            var categoryCompare = GetCategoryOrder(leftDef.Category).CompareTo(GetCategoryOrder(rightDef.Category));
            if (categoryCompare != 0)
            {
                return categoryCompare;
            }

            var rarityCompare = ((int)right.Rarity).CompareTo((int)left.Rarity);
            if (rarityCompare != 0)
            {
                return rarityCompare;
            }

            var nameCompare = string.Compare(ResolveDisplayName(leftDef), ResolveDisplayName(rightDef), StringComparison.OrdinalIgnoreCase);
            if (nameCompare != 0)
            {
                return nameCompare;
            }

            var priceCompare = rightDef.BasePrice.CompareTo(leftDef.BasePrice);
            if (priceCompare != 0)
            {
                return priceCompare;
            }

            return right.Stack.CompareTo(left.Stack);
        }

        private static int GetCategoryOrder(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon:
                    return 0;
                case ItemCategory.Armor:
                    return 1;
                case ItemCategory.Accessory:
                    return 2;
                case ItemCategory.Consumable:
                    return 3;
                case ItemCategory.Material:
                    return 4;
                case ItemCategory.Quest:
                    return 5;
                case ItemCategory.Skill:
                    return 6;
                case ItemCategory.General:
                default:
                    return 7;
            }
        }

        private static string ResolveDisplayName(ItemDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(definition.name))
            {
                return definition.name;
            }

            return definition.Id ?? string.Empty;
        }

        private sealed class StackBucket
        {
            public readonly ItemInstance Prototype;
            public int TotalStack;

            public StackBucket(ItemInstance prototype)
            {
                Prototype = prototype;
                TotalStack = 0;
            }
        }

        private void AddStartingItems()
        {
            if (startingItems == null || startingItems.Count == 0)
            {
                return;
            }

            for (int i = 0; i < startingItems.Count; i++)
            {
                var item = startingItems[i];
                if (item == null || item.Definition == null)
                {
                    continue;
                }

                TryAddItem(item.CloneWithStack(item.Stack));
            }
        }
    }
}
