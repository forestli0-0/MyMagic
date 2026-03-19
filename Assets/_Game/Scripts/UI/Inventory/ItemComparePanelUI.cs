using System;
using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 物品对比面板 UI，展示选中物品详情与当前装备对比。
    /// </summary>
    /// <remarks>
    /// 功能：
    /// - 显示物品名称（按稀有度着色）、槽位、描述
    /// - 汇总物品属性修正器（基础 + 词缀 + 装备 Buff）
    /// - 与已装备物品对比时，显示增益（绿）/ 减益（红）差值
    /// </remarks>
    public class ItemComparePanelUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text slotText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private RectTransform statsRoot;
        [SerializeField] private Text statTemplate;
        [SerializeField] private Color projectedHeaderColor = new Color(0.78f, 0.84f, 0.95f, 1f);
        [SerializeField, Range(0, 12)] private int reservedStatRows = 0;

        private readonly List<Text> statEntries = new List<Text>();
        private ItemInstance currentItem;
        private ItemInstance compareItem;
        private string extraStatusLine;
        private Func<string, float> currentStatValueResolver;

        private struct StatDeltaAggregate
        {
            public float Additive;
            public float Multiplier;
            public bool HasSelectedOverride;
            public float SelectedOverrideValue;
            public bool HasCompareOverride;
        }

        public void ShowItem(ItemInstance item, ItemInstance compare, string statusLine = null)
        {
            currentItem = item;
            compareItem = compare;
            extraStatusLine = statusLine;
            Refresh();
        }

        public void SetCurrentStatValueResolver(Func<string, float> resolver)
        {
            currentStatValueResolver = resolver;
            Refresh();
        }

        private void Refresh()
        {
            ClearStats();

            if (currentItem == null || currentItem.Definition == null)
            {
                if (icon != null)
                {
                    icon.enabled = false;
                }

                if (nameText != null)
                {
                    nameText.text = "请选择物品";
                    nameText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                }

                if (slotText != null)
                {
                    slotText.text = string.Empty;
                }

                if (descriptionText != null)
                {
                    descriptionText.text = string.Empty;
                }

                PadStatRows();

                return;
            }

            var definition = currentItem.Definition;
            var displayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;
            if (nameText != null)
            {
                nameText.text = displayName;
                nameText.color = GetRarityColor(currentItem.Rarity);
            }

            if (slotText != null)
            {
                var rarity = ResolveRarityName(currentItem.Rarity);
                var category = ResolveCategoryName(definition.Category);
                if (definition.IsEquippable)
                {
                    slotText.text = $"{category} | {definition.Slot} | {rarity}";
                }
                else
                {
                    slotText.text = $"{category} | {rarity}";
                }
            }

            if (icon != null)
            {
                icon.sprite = definition.Icon;
                icon.enabled = icon.sprite != null;
            }

            if (descriptionText != null)
            {
                var description = definition.Description ?? string.Empty;
                if (currentItem.IsStackable && currentItem.Stack > 1)
                {
                    description = string.IsNullOrEmpty(description)
                        ? $"堆叠: {currentItem.Stack}"
                        : $"{description}\n堆叠: {currentItem.Stack}";
                }

                if (definition.CanSell)
                {
                    var baseSellPrice = definition.SellPriceOverride >= 0 ? definition.SellPriceOverride : definition.BasePrice;
                    if (baseSellPrice > 0)
                    {
                        description = string.IsNullOrEmpty(description)
                            ? $"基础售价: {baseSellPrice}"
                            : $"{description}\n基础售价: {baseSellPrice}";
                    }
                }

                if (compareItem != null && compareItem.Definition != null)
                {
                    var compareName = string.IsNullOrWhiteSpace(compareItem.Definition.DisplayName)
                        ? compareItem.Definition.name
                        : compareItem.Definition.DisplayName;
                    description = string.IsNullOrEmpty(description)
                        ? $"对比对象: {compareName}"
                            : $"{description}\n对比对象: {compareName}";
                }

                if (!string.IsNullOrWhiteSpace(extraStatusLine))
                {
                    description = string.IsNullOrEmpty(description)
                        ? extraStatusLine
                        : $"{description}\n{extraStatusLine}";
                }

                descriptionText.text = description;
            }

            var selectedStats = CollectStatModifiers(currentItem);
            var compareStats = CollectStatModifiers(compareItem);
            var keys = BuildStatKeys(selectedStats, compareStats);

            if (keys.Count == 0)
            {
                AddStatLine("无属性修正。", new Color(0.75f, 0.75f, 0.75f, 1f));
                PadStatRows();
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                selectedStats.TryGetValue(key, out var selectedValue);
                compareStats.TryGetValue(key, out var compareValue);

                var diff = selectedValue - compareValue;
                var line = FormatStatLine(key, selectedValue, diff, compareItem != null);
                var color = ResolveCompareColor(diff, compareItem != null);
                AddStatLine(line, color);
            }

            AddProjectedCharacterPreview(selectedStats, compareStats);
            PadStatRows();
        }

        private void ClearStats()
        {
            for (int i = 0; i < statEntries.Count; i++)
            {
                if (statEntries[i] != null)
                {
                    statEntries[i].gameObject.SetActive(false);
                }
            }

            if (statTemplate != null)
            {
                statTemplate.gameObject.SetActive(false);
            }
        }

        private void AddStatLine(string text, Color color)
        {
            if (statTemplate == null || statsRoot == null)
            {
                return;
            }

            Text entry;
            if (statEntries.Count == 0)
            {
                entry = Instantiate(statTemplate, statsRoot);
                statEntries.Add(entry);
            }
            else
            {
                var index = 0;
                while (index < statEntries.Count && statEntries[index].gameObject.activeSelf)
                {
                    index++;
                }

                if (index >= statEntries.Count)
                {
                    entry = Instantiate(statTemplate, statsRoot);
                    statEntries.Add(entry);
                }
                else
                {
                    entry = statEntries[index];
                }
            }

            entry.gameObject.SetActive(true);
            entry.text = text;
            entry.color = color;
        }

        private void PadStatRows()
        {
            if (statTemplate == null || statsRoot == null || reservedStatRows <= 0)
            {
                return;
            }

            var activeCount = 0;
            for (int i = 0; i < statEntries.Count; i++)
            {
                if (statEntries[i] != null && statEntries[i].gameObject.activeSelf)
                {
                    activeCount++;
                }
            }

            while (activeCount < reservedStatRows)
            {
                Text entry;
                if (activeCount < statEntries.Count)
                {
                    entry = statEntries[activeCount];
                    if (entry == null)
                    {
                        entry = Instantiate(statTemplate, statsRoot);
                        statEntries[activeCount] = entry;
                    }
                }
                else
                {
                    entry = Instantiate(statTemplate, statsRoot);
                    statEntries.Add(entry);
                }

                entry.gameObject.SetActive(true);
                entry.text = " ";
                entry.color = new Color(1f, 1f, 1f, 0f);
                activeCount++;
            }
        }

        private static List<StatKey> BuildStatKeys(
            Dictionary<StatKey, float> selected,
            Dictionary<StatKey, float> compare)
        {
            var keys = new List<StatKey>();
            if (selected != null)
            {
                keys.AddRange(selected.Keys);
            }

            if (compare != null)
            {
                foreach (var key in compare.Keys)
                {
                    if (!keys.Contains(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            keys.Sort(CompareStatKeys);
            return keys;
        }

        private static int CompareStatKeys(StatKey left, StatKey right)
        {
            var leftName = ResolveStatName(left.Stat);
            var rightName = ResolveStatName(right.Stat);
            var nameCompare = string.CompareOrdinal(leftName, rightName);
            if (nameCompare != 0)
            {
                return nameCompare;
            }

            return GetOperationOrder(left.Operation).CompareTo(GetOperationOrder(right.Operation));
        }

        private static int GetOperationOrder(ModifierOperation operation)
        {
            switch (operation)
            {
                case ModifierOperation.Add:
                    return 0;
                case ModifierOperation.Multiply:
                    return 1;
                case ModifierOperation.Override:
                    return 2;
                default:
                    return 3;
            }
        }

        private static Dictionary<StatKey, float> CollectStatModifiers(ItemInstance item)
        {
            var result = new Dictionary<StatKey, float>();
            if (item == null || item.Definition == null)
            {
                return result;
            }

            AddModifiers(result, item.Definition.BaseModifiers);
            AddEquipBuffModifiers(result, item.Definition.EquipBuffs);

            if (item.Definition.AllowAffixes && item.Affixes != null)
            {
                for (int i = 0; i < item.Affixes.Count; i++)
                {
                    AddModifiers(result, item.Affixes[i]?.Modifiers);
                }
            }

            return result;
        }

        private void AddProjectedCharacterPreview(
            Dictionary<StatKey, float> selectedStats,
            Dictionary<StatKey, float> compareStats)
        {
            if (currentStatValueResolver == null)
            {
                return;
            }

            var aggregated = BuildAggregatedDelta(selectedStats, compareStats);
            if (aggregated.Count == 0)
            {
                return;
            }

            var projectedLines = BuildProjectedStatLines(aggregated);
            if (projectedLines.Count == 0)
            {
                return;
            }

            AddStatLine("---- 装备后角色属性 ----", projectedHeaderColor);
            for (int i = 0; i < projectedLines.Count; i++)
            {
                var line = projectedLines[i];
                AddStatLine(line.Text, line.Color);
            }
        }

        private Dictionary<StatDefinition, StatDeltaAggregate> BuildAggregatedDelta(
            Dictionary<StatKey, float> selectedStats,
            Dictionary<StatKey, float> compareStats)
        {
            var result = new Dictionary<StatDefinition, StatDeltaAggregate>();

            AccumulateModifiers(result, selectedStats, true);
            AccumulateModifiers(result, compareStats, false);

            return result;
        }

        private static void AccumulateModifiers(
            Dictionary<StatDefinition, StatDeltaAggregate> aggregate,
            Dictionary<StatKey, float> modifiers,
            bool isSelectedItem)
        {
            if (aggregate == null || modifiers == null)
            {
                return;
            }

            foreach (var pair in modifiers)
            {
                var stat = pair.Key.Stat;
                if (stat == null)
                {
                    continue;
                }

                if (!aggregate.TryGetValue(stat, out var bucket))
                {
                    bucket = new StatDeltaAggregate();
                }

                var value = pair.Value;
                if (!isSelectedItem)
                {
                    value = -value;
                }

                switch (pair.Key.Operation)
                {
                    case ModifierOperation.Add:
                        bucket.Additive += value;
                        break;
                    case ModifierOperation.Multiply:
                        bucket.Multiplier += value;
                        break;
                    case ModifierOperation.Override:
                        if (isSelectedItem)
                        {
                            bucket.HasSelectedOverride = true;
                            bucket.SelectedOverrideValue = pair.Value;
                        }
                        else
                        {
                            bucket.HasCompareOverride = true;
                        }

                        break;
                }

                aggregate[stat] = bucket;
            }
        }

        private List<ProjectedStatLine> BuildProjectedStatLines(Dictionary<StatDefinition, StatDeltaAggregate> aggregated)
        {
            var list = new List<ProjectedStatLine>();
            if (aggregated == null || aggregated.Count == 0)
            {
                return list;
            }

            var orderedStats = new List<StatDefinition>(aggregated.Keys);
            orderedStats.Sort((left, right) =>
            {
                var leftName = ResolveStatName(left);
                var rightName = ResolveStatName(right);
                return string.CompareOrdinal(leftName, rightName);
            });

            for (int i = 0; i < orderedStats.Count; i++)
            {
                var stat = orderedStats[i];
                if (stat == null || string.IsNullOrWhiteSpace(stat.Id))
                {
                    continue;
                }

                var current = currentStatValueResolver.Invoke(stat.Id);
                if (float.IsNaN(current) || float.IsInfinity(current))
                {
                    continue;
                }

                var bucket = aggregated[stat];
                if (bucket.HasCompareOverride && !bucket.HasSelectedOverride)
                {
                    // 旧装备使用覆盖值，而新装备没有覆盖时，仅靠净变化无法可靠推导总值。
                    continue;
                }

                var predicted = current;
                if (bucket.HasSelectedOverride)
                {
                    predicted = bucket.SelectedOverrideValue;
                }
                else
                {
                    predicted += bucket.Additive;
                    if (!Mathf.Approximately(bucket.Multiplier, 0f))
                    {
                        predicted *= 1f + bucket.Multiplier;
                    }
                }

                var delta = predicted - current;
                if (Mathf.Abs(delta) <= 0.0001f && !bucket.HasSelectedOverride)
                {
                    continue;
                }

                var text = $"{ResolveStatName(stat)}: {FormatTotalStatValue(current, stat)} -> {FormatTotalStatValue(predicted, stat)} ({FormatSignedTotalStatValue(delta, stat)})";
                var color = ResolveCompareColor(delta, true);
                list.Add(new ProjectedStatLine(text, color));
            }

            return list;
        }

        private static string FormatTotalStatValue(float value, StatDefinition stat)
        {
            if (stat != null && stat.IsPercentage)
            {
                return $"{(value * 100f):0.##}%";
            }

            if (stat != null && stat.IsInteger)
            {
                return Mathf.RoundToInt(value).ToString();
            }

            return value.ToString("0.##");
        }

        private static string FormatSignedTotalStatValue(float value, StatDefinition stat)
        {
            var sign = value >= 0f ? "+" : "-";
            var absolute = Mathf.Abs(value);
            if (stat != null && stat.IsPercentage)
            {
                return $"{sign}{(absolute * 100f):0.##}%";
            }

            if (stat != null && stat.IsInteger)
            {
                return $"{sign}{Mathf.RoundToInt(absolute)}";
            }

            return $"{sign}{absolute:0.##}";
        }

        private readonly struct ProjectedStatLine
        {
            public readonly string Text;
            public readonly Color Color;

            public ProjectedStatLine(string text, Color color)
            {
                Text = text;
                Color = color;
            }
        }

        private static void AddEquipBuffModifiers(Dictionary<StatKey, float> result, IReadOnlyList<BuffDefinition> buffs)
        {
            if (buffs == null)
            {
                return;
            }

            for (int i = 0; i < buffs.Count; i++)
            {
                AddModifiers(result, buffs[i]?.Modifiers);
            }
        }

        private static void AddModifiers(Dictionary<StatKey, float> result, IReadOnlyList<ModifierDefinition> modifiers)
        {
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier == null || modifier.Target != ModifierTargetType.Stat || modifier.Stat == null)
                {
                    continue;
                }

                var key = new StatKey(modifier.Stat, modifier.Operation);
                if (modifier.Operation == ModifierOperation.Override)
                {
                    result[key] = modifier.Value;
                }
                else if (result.TryGetValue(key, out var existing))
                {
                    result[key] = existing + modifier.Value;
                }
                else
                {
                    result[key] = modifier.Value;
                }
            }
        }

        private static string FormatStatLine(StatKey key, float selectedValue, float diff, bool showCompare)
        {
            var statName = ResolveStatName(key.Stat);
            var valueText = FormatModifierValue(key.Operation, selectedValue, key.Stat, true);
            if (!showCompare)
            {
                return $"{statName} {valueText}";
            }

            var diffText = FormatModifierValue(key.Operation, diff, key.Stat, true);
            return $"{statName} {valueText} ({diffText})";
        }

        private static string FormatModifierValue(ModifierOperation operation, float value, StatDefinition stat, bool includeSign)
        {
            var isPercent = operation == ModifierOperation.Multiply || (stat != null && stat.IsPercentage);
            var scaled = isPercent ? value * 100f : value;
            var absValue = Mathf.Abs(scaled);
            var number = FormatNumber(absValue, stat);
            var suffix = isPercent ? "%" : string.Empty;

            if (operation == ModifierOperation.Override)
            {
                return $"={number}{suffix}";
            }

            if (!includeSign)
            {
                return $"{number}{suffix}";
            }

            var sign = value >= 0f ? "+" : "-";
            return $"{sign}{number}{suffix}";
        }

        private static string FormatNumber(float value, StatDefinition stat)
        {
            var isInteger = stat != null && stat.IsInteger;
            return isInteger ? Mathf.RoundToInt(value).ToString() : value.ToString("0.##");
        }

        private static string ResolveStatName(StatDefinition stat)
        {
            if (stat == null)
            {
                return "属性";
            }

            if (TryResolveLocalizedStatName(stat.Id, out var localized))
            {
                return localized;
            }

            return string.IsNullOrWhiteSpace(stat.DisplayName) ? stat.name : stat.DisplayName;
        }

        private static bool TryResolveLocalizedStatName(string statId, out string localizedName)
        {
            localizedName = string.Empty;
            if (string.IsNullOrWhiteSpace(statId))
            {
                return false;
            }

            switch (statId)
            {
                case CombatStatIds.MaxHealth:
                    localizedName = "最大生命";
                    return true;
                case CombatStatIds.MaxMana:
                    localizedName = "最大法力";
                    return true;
                case CombatStatIds.AttackPower:
                    localizedName = "攻击力";
                    return true;
                case CombatStatIds.AbilityPower:
                    localizedName = "法术强度";
                    return true;
                case CombatStatIds.AbilityHaste:
                    localizedName = "技能急速";
                    return true;
                case CombatStatIds.AttackSpeed:
                    localizedName = "攻速加成";
                    return true;
                case CombatStatIds.CritChance:
                    localizedName = "暴击率";
                    return true;
                case CombatStatIds.CritMultiplier:
                    localizedName = "暴击伤害";
                    return true;
                case CombatStatIds.Armor:
                    localizedName = "护甲";
                    return true;
                case CombatStatIds.MagicResist:
                    localizedName = "魔抗";
                    return true;
                case CombatStatIds.MoveSpeed:
                    localizedName = "移动速度";
                    return true;
                case CombatStatIds.HealthRegen:
                    localizedName = "生命回复";
                    return true;
                case CombatStatIds.ManaRegen:
                    localizedName = "法力回复";
                    return true;
                case CombatStatIds.Tenacity:
                    localizedName = "韧性";
                    return true;
                case CombatStatIds.Lifesteal:
                    localizedName = "生命偷取";
                    return true;
                case CombatStatIds.Omnivamp:
                    localizedName = "全能吸血";
                    return true;
                case CombatStatIds.ArmorPenFlat:
                    localizedName = "护甲穿透(固定)";
                    return true;
                case CombatStatIds.ArmorPenPercent:
                    localizedName = "护甲穿透(%)";
                    return true;
                case CombatStatIds.MagicPenFlat:
                    localizedName = "法术穿透(固定)";
                    return true;
                case CombatStatIds.MagicPenPercent:
                    localizedName = "法术穿透(%)";
                    return true;
                default:
                    return false;
            }
        }

        private static Color ResolveCompareColor(float diff, bool hasCompare)
        {
            if (!hasCompare)
            {
                return Color.white;
            }

            if (diff > 0.0001f)
            {
                return new Color(0.45f, 0.85f, 0.45f, 1f);
            }

            if (diff < -0.0001f)
            {
                return new Color(0.9f, 0.45f, 0.45f, 1f);
            }

            return new Color(0.85f, 0.85f, 0.85f, 1f);
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic:
                    return new Color(0.45f, 0.65f, 1f, 1f);
                case ItemRarity.Rare:
                    return new Color(0.95f, 0.8f, 0.4f, 1f);
                case ItemRarity.Epic:
                    return new Color(0.85f, 0.5f, 1f, 1f);
                case ItemRarity.Legendary:
                    return new Color(1f, 0.6f, 0.2f, 1f);
                case ItemRarity.Common:
                default:
                    return new Color(0.85f, 0.85f, 0.85f, 1f);
            }
        }

        private static string ResolveCategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon:
                    return "武器";
                case ItemCategory.Armor:
                    return "防具";
                case ItemCategory.Accessory:
                    return "饰品";
                case ItemCategory.Consumable:
                    return "消耗品";
                case ItemCategory.Material:
                    return "材料";
                case ItemCategory.Quest:
                    return "任务";
                case ItemCategory.Skill:
                    return "技能";
                case ItemCategory.General:
                default:
                    return "通用";
            }
        }

        private static string ResolveRarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic:
                    return "魔法";
                case ItemRarity.Rare:
                    return "稀有";
                case ItemRarity.Epic:
                    return "史诗";
                case ItemRarity.Legendary:
                    return "传说";
                case ItemRarity.Common:
                default:
                    return "普通";
            }
        }

        private readonly struct StatKey : IEquatable<StatKey>
        {
            public readonly StatDefinition Stat;
            public readonly ModifierOperation Operation;

            public StatKey(StatDefinition stat, ModifierOperation operation)
            {
                Stat = stat;
                Operation = operation;
            }

            public bool Equals(StatKey other)
            {
                return Stat == other.Stat && Operation == other.Operation;
            }

            public override bool Equals(object obj)
            {
                return obj is StatKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + (Stat != null ? Stat.GetHashCode() : 0);
                    hash = (hash * 31) + (int)Operation;
                    return hash;
                }
            }
        }
    }
}
