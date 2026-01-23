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

        private readonly List<Text> statEntries = new List<Text>();
        private ItemInstance currentItem;
        private ItemInstance compareItem;

        public void ShowItem(ItemInstance item, ItemInstance compare)
        {
            currentItem = item;
            compareItem = compare;
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
                    nameText.text = "Select an item";
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
                slotText.text = definition.IsEquippable ? definition.Slot.ToString() : "Not Equippable";
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
                        ? $"Stack: {currentItem.Stack}"
                        : $"{description}\nStack: {currentItem.Stack}";
                }

                descriptionText.text = description;
            }

            var selectedStats = CollectStatModifiers(currentItem);
            var compareStats = CollectStatModifiers(compareItem);
            var keys = BuildStatKeys(selectedStats, compareStats);

            if (keys.Count == 0)
            {
                AddStatLine("No modifiers.", new Color(0.75f, 0.75f, 0.75f, 1f));
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
                return "Stat";
            }

            return string.IsNullOrWhiteSpace(stat.DisplayName) ? stat.name : stat.DisplayName;
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
