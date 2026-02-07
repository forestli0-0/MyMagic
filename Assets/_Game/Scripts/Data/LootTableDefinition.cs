using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 掉落表定义：配置战斗掉落的物品和货币规则。
    /// 支持权重随机、词缀生成、稀有度覆盖等高级功能。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Loot/Loot Table", fileName = "LootTable_")]
    public class LootTableDefinition : DefinitionBase
    {
        [Header("Rolls")]
        [Tooltip("最少掉落次数")]
        [SerializeField] private int minRolls = 1;
        [Tooltip("最多掉落次数")]
        [SerializeField] private int maxRolls = 1;

        [Header("Affixes")]
        [Tooltip("默认词缀池，当条目未指定词缀池时使用")]
        [SerializeField] private List<AffixDefinition> defaultAffixPool = new List<AffixDefinition>();

        [Header("Entries")]
        [Tooltip("掉落条目列表，每个条目可以是物品或货币")]
        [SerializeField] private List<LootEntry> entries = new List<LootEntry>();

        /// <summary>最少掉落次数（至少为0）</summary>
        public int MinRolls => Mathf.Max(0, minRolls);
        /// <summary>最多掉落次数（至少等于最小值）</summary>
        public int MaxRolls => Mathf.Max(MinRolls, maxRolls);
        /// <summary>默认词缀池</summary>
        public IReadOnlyList<AffixDefinition> DefaultAffixPool => defaultAffixPool;
        /// <summary>掉落条目列表</summary>
        public IReadOnlyList<LootEntry> Entries => entries;

        /// <summary>
        /// 执行掉落随机，生成掉落结果列表
        /// </summary>
        /// <param name="results">用于存储结果的列表（会被清空后填充）</param>
        /// <returns>实际生成的掉落数量</returns>
        public int RollDrops(List<LootRollResult> results)
        {
            if (results == null)
            {
                return 0;
            }

            results.Clear();
            if (entries == null || entries.Count == 0)
            {
                return 0;
            }

            // 随机决定本次掉落的次数
            var rollCount = UnityEngine.Random.Range(MinRolls, MaxRolls + 1);
            if (rollCount <= 0)
            {
                return 0;
            }

            // 对每次掉落机会进行随机
            for (int i = 0; i < rollCount; i++)
            {
                var entry = PickEntry();
                if (entry == null)
                {
                    continue;
                }

                // 处理货币类型掉落
                if (entry.Type == LootEntryType.Currency)
                {
                    var amount = UnityEngine.Random.Range(entry.MinCurrency, entry.MaxCurrency + 1);
                    if (amount > 0)
                    {
                        results.Add(LootRollResult.CreateCurrency(amount));
                    }
                    continue;
                }

                // 处理物品类型掉落
                if (entry.Item == null)
                {
                    continue;
                }

                var stack = UnityEngine.Random.Range(entry.MinStack, entry.MaxStack + 1);
                stack = Mathf.Max(1, stack);

                // 决定稀有度：使用条目覆盖或物品默认值
                var rarity = entry.OverrideRarity ? entry.Rarity : entry.Item.Rarity;
                // 生成词缀列表
                var affixes = BuildAffixes(entry, entry.Item);

                results.Add(LootRollResult.CreateItem(entry.Item, stack, rarity, affixes));
            }

            return results.Count;
        }

        /// <summary>
        /// 根据权重随机选择一个掉落条目
        /// </summary>
        /// <returns>被选中的条目，如果无有效条目则返回null</returns>
        private LootEntry PickEntry()
        {
            // 计算总权重
            var totalWeight = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                totalWeight += Mathf.Max(0, entries[i].Weight);
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            // 权重随机选择
            var roll = UnityEngine.Random.Range(0, totalWeight);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var weight = Mathf.Max(0, entry.Weight);
                if (weight <= 0)
                {
                    continue;
                }

                if (roll < weight)
                {
                    return entry;
                }

                roll -= weight;
            }

            return null;
        }

        /// <summary>
        /// 为掉落物品生成词缀列表
        /// </summary>
        /// <param name="entry">掉落条目配置</param>
        /// <param name="item">物品定义</param>
        /// <returns>生成的词缀列表，如果不生成词缀则返回null</returns>
        private List<AffixDefinition> BuildAffixes(LootEntry entry, ItemDefinition item)
        {
            // 检查是否允许生成词缀
            if (entry == null || item == null || !item.AllowAffixes)
            {
                return null;
            }

            if (!entry.RollAffixes)
            {
                return null;
            }

            // 确定词缀池：优先使用条目指定的，否则使用默认池
            var pool = entry.AffixPool;
            if (pool == null || pool.Count == 0)
            {
                pool = defaultAffixPool;
            }

            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            // 随机决定词缀数量
            var count = UnityEngine.Random.Range(entry.MinAffixes, entry.MaxAffixes + 1);
            if (count <= 0)
            {
                return null;
            }

            // 筛选出适用于该装备槽位的词缀
            var candidates = new List<AffixDefinition>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                var affix = pool[i];
                if (affix == null)
                {
                    continue;
                }

                if (!IsSlotAllowed(affix, item.Slot))
                {
                    continue;
                }

                candidates.Add(affix);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            // 从候选池中随机选择指定数量的词缀（不重复）
            count = Mathf.Min(count, candidates.Count);
            var result = new List<AffixDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                var picked = PickWeightedAffix(candidates);
                if (picked == null)
                {
                    break;
                }

                result.Add(picked);
                candidates.Remove(picked); // 移除已选中的，避免重复
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// 根据权重从候选列表中随机选择一个词缀
        /// </summary>
        private static AffixDefinition PickWeightedAffix(List<AffixDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var totalWeight = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i] != null ? candidates[i].Weight : 0;
            }

            // 如果所有权重为0，则均匀随机
            if (totalWeight <= 0)
            {
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            var roll = UnityEngine.Random.Range(0, totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                var affix = candidates[i];
                var weight = affix != null ? affix.Weight : 0;
                if (weight <= 0)
                {
                    continue;
                }

                if (roll < weight)
                {
                    return affix;
                }

                roll -= weight;
            }

            return candidates[0];
        }

        /// <summary>
        /// 检查词缀是否适用于指定的装备槽位
        /// </summary>
        private static bool IsSlotAllowed(AffixDefinition affix, ItemSlot slot)
        {
            if (affix == null)
            {
                return false;
            }

            var allowed = affix.AllowedSlots;
            // 如果词缀未限制槽位，则适用于所有槽位
            if (allowed == null || allowed.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < allowed.Count; i++)
            {
                if (allowed[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 掉落条目类型枚举
    /// </summary>
    public enum LootEntryType
    {
        /// <summary>物品掉落</summary>
        Item = 0,
        /// <summary>货币掉落</summary>
        Currency = 1
    }

    /// <summary>
    /// 掉落条目：定义单个可能的掉落物
    /// </summary>
    [Serializable]
    public class LootEntry
    {
        [Tooltip("条目类型：物品或货币")]
        [SerializeField] private LootEntryType type = LootEntryType.Item;
        [Tooltip("权重值，权重越高被选中的概率越大")]
        [SerializeField] private int weight = 1;

        [Header("Item")]
        [Tooltip("掉落的物品定义")]
        [SerializeField] private ItemDefinition item;
        [Tooltip("最少掉落数量")]
        [SerializeField] private int minStack = 1;
        [Tooltip("最多掉落数量")]
        [SerializeField] private int maxStack = 1;
        [Tooltip("是否覆盖物品的默认稀有度")]
        [SerializeField] private bool overrideRarity;
        [Tooltip("覆盖后的稀有度")]
        [SerializeField] private ItemRarity rarity = ItemRarity.Common;

        [Header("Affixes")]
        [Tooltip("是否为该物品随机生成词缀")]
        [SerializeField] private bool rollAffixes;
        [Tooltip("最少词缀数量")]
        [SerializeField] private int minAffixes;
        [Tooltip("最多词缀数量")]
        [SerializeField] private int maxAffixes;
        [Tooltip("该条目专用的词缀池，为空则使用掉落表的默认池")]
        [SerializeField] private List<AffixDefinition> affixPool = new List<AffixDefinition>();

        [Header("Currency")]
        [Tooltip("最少货币掉落量")]
        [SerializeField] private int minCurrency = 1;
        [Tooltip("最多货币掉落量")]
        [SerializeField] private int maxCurrency = 1;

        public LootEntryType Type => type;
        public int Weight => Mathf.Max(0, weight);
        public ItemDefinition Item => item;
        public int MinStack => Mathf.Max(1, minStack);
        public int MaxStack => Mathf.Max(MinStack, maxStack);
        public bool OverrideRarity => overrideRarity;
        public ItemRarity Rarity => rarity;
        public bool RollAffixes => rollAffixes;
        public int MinAffixes => Mathf.Max(0, minAffixes);
        public int MaxAffixes => Mathf.Max(MinAffixes, maxAffixes);
        public IReadOnlyList<AffixDefinition> AffixPool => affixPool;
        public int MinCurrency => Mathf.Max(0, minCurrency);
        public int MaxCurrency => Mathf.Max(MinCurrency, maxCurrency);
    }

    /// <summary>
    /// 掉落随机结果：表示一次掉落随机产生的具体结果
    /// 使用 readonly struct 避免不必要的内存分配
    /// </summary>
    public readonly struct LootRollResult
    {
        /// <summary>结果类型</summary>
        public readonly LootEntryType Type;
        /// <summary>掉落的物品定义（仅物品类型有效）</summary>
        public readonly ItemDefinition Item;
        /// <summary>物品堆叠数量</summary>
        public readonly int Stack;
        /// <summary>物品稀有度</summary>
        public readonly ItemRarity Rarity;
        /// <summary>物品词缀列表</summary>
        public readonly IReadOnlyList<AffixDefinition> Affixes;
        /// <summary>货币数量（仅货币类型有效）</summary>
        public readonly int Currency;

        private LootRollResult(LootEntryType type, ItemDefinition item, int stack, ItemRarity rarity, IReadOnlyList<AffixDefinition> affixes, int currency)
        {
            Type = type;
            Item = item;
            Stack = stack;
            Rarity = rarity;
            Affixes = affixes;
            Currency = currency;
        }

        /// <summary>是否为有效的物品掉落</summary>
        public bool IsItem => Type == LootEntryType.Item && Item != null;
        /// <summary>是否为有效的货币掉落</summary>
        public bool IsCurrency => Type == LootEntryType.Currency && Currency > 0;

        /// <summary>
        /// 创建物品类型的掉落结果
        /// </summary>
        public static LootRollResult CreateItem(ItemDefinition item, int stack, ItemRarity rarity, IReadOnlyList<AffixDefinition> affixes)
        {
            return new LootRollResult(LootEntryType.Item, item, stack, rarity, affixes, 0);
        }

        /// <summary>
        /// 创建货币类型的掉落结果
        /// </summary>
        public static LootRollResult CreateCurrency(int amount)
        {
            return new LootRollResult(LootEntryType.Currency, null, 0, ItemRarity.Common, null, Mathf.Max(0, amount));
        }
    }
}
