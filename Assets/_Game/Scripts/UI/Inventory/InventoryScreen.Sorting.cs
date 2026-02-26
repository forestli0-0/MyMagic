using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public partial class InventoryScreen
    {
        private void SetFilter(InventoryFilter filter)
        {
            if (activeFilter == filter)
            {
                return;
            }

            activeFilter = filter;
            ClearInventorySelection();
            RefreshAll();
        }

        private void SetRarityFilter(RarityQuickFilter filter)
        {
            if (activeRarityFilter == filter)
            {
                return;
            }

            activeRarityFilter = filter;
            ClearInventorySelection();
            RefreshAll();
        }

        private void HandleSearchKeywordChanged(string keyword)
        {
            var normalized = keyword != null ? keyword.Trim() : string.Empty;
            if (string.Equals(searchKeyword, normalized, StringComparison.Ordinal))
            {
                return;
            }

            searchKeyword = normalized;
            ClearInventorySelection();
            RefreshAll();
        }

        private void HandleSortModeChanged(int dropdownIndex)
        {
            var clamped = Mathf.Clamp(dropdownIndex, 0, (int)InventorySortMode.Category);
            SetSortMode((InventorySortMode)clamped);
        }

        private void EnsureSortDropdownOptions()
        {
            if (sortDropdown == null)
            {
                return;
            }

            if (sortDropdown.options == null || sortDropdown.options.Count == 0)
            {
                sortDropdown.ClearOptions();
                sortDropdown.AddOptions(new List<string>
                {
                    "默认顺序",
                    "名称 A-Z",
                    "稀有度 ↓",
                    "价格 ↓",
                    "分类"
                });
            }

            var value = Mathf.Clamp((int)activeSortMode, 0, Mathf.Max(0, sortDropdown.options.Count - 1));
            sortDropdown.SetValueWithoutNotify(value);
            sortDropdown.RefreshShownValue();
            RefreshSortPickerCaption();
        }

        private void SetSortMode(InventorySortMode mode)
        {
            if (activeSortMode == mode)
            {
                return;
            }

            activeSortMode = mode;
            ClearInventorySelection();
            RefreshAll();
            RefreshSortPickerOptionStates();
        }

        private void SetupSortPicker()
        {
            if (sortDropdown == null)
            {
                return;
            }

            if (sortDropdown.GetComponent<DropdownOverlayFix>() != null)
            {
                var overlayFix = sortDropdown.GetComponent<DropdownOverlayFix>();
                overlayFix.enabled = false;
                Destroy(overlayFix);
            }

            sortDropdown.enabled = false;
            if (sortDropdown.template != null)
            {
                sortDropdown.template.gameObject.SetActive(false);
            }

            sortPickerButton = EnsureSortPickerButton();
            if (sortPickerButton == null)
            {
                // 回退：至少保留原生下拉可用，避免阻断背包界面。
                sortDropdown.enabled = true;
                sortDropdown.onValueChanged.RemoveListener(HandleSortModeChanged);
                sortDropdown.onValueChanged.AddListener(HandleSortModeChanged);
#if UNITY_EDITOR
                if (!sortDropdownBindingLogged)
                {
                    sortDropdownBindingLogged = true;
                    Debug.LogWarning("[InventoryScreen] Failed to create sort picker button proxy. Falling back to native dropdown.", this);
                }
#endif
                return;
            }

            sortPickerButton.onClick.RemoveListener(ToggleSortPicker);
            sortPickerButton.onClick.AddListener(ToggleSortPicker);

            ResolveSortPickerFont();
            EnsureSortPickerPanel();
            RefreshSortPickerCaption();
            RefreshSortPickerOptionStates();
        }

        private Button EnsureSortPickerButton()
        {
            if (sortDropdown == null)
            {
                return null;
            }

            if (sortPickerButton != null)
            {
                return sortPickerButton;
            }

            var nativeButton = sortDropdown.GetComponent<Button>();
            if (nativeButton != null)
            {
                return nativeButton;
            }

            var proxyTransform = sortDropdown.transform.Find("SortPickerButtonProxy");
            GameObject proxyGo;
            if (proxyTransform != null)
            {
                proxyGo = proxyTransform.gameObject;
            }
            else
            {
                proxyGo = new GameObject("SortPickerButtonProxy", typeof(RectTransform), typeof(Image), typeof(Button));
                proxyGo.transform.SetParent(sortDropdown.transform, false);

                var proxyRect = proxyGo.GetComponent<RectTransform>();
                proxyRect.anchorMin = Vector2.zero;
                proxyRect.anchorMax = Vector2.one;
                proxyRect.offsetMin = Vector2.zero;
                proxyRect.offsetMax = Vector2.zero;
            }

            var proxyImage = proxyGo.GetComponent<Image>();
            if (proxyImage != null)
            {
                proxyImage.color = new Color(0f, 0f, 0f, 0f);
                proxyImage.raycastTarget = true;
            }

            var proxyButton = proxyGo.GetComponent<Button>();
            if (proxyButton != null)
            {
                proxyButton.transition = Selectable.Transition.None;
            }

            return proxyButton;
        }

        private void ResolveSortPickerFont()
        {
            if (sortPickerFont != null)
            {
                return;
            }

            if (sortDropdown != null && sortDropdown.captionText != null && sortDropdown.captionText.font != null)
            {
                sortPickerFont = sortDropdown.captionText.font;
                return;
            }

            sortPickerFont = ResolveBuiltinFallbackFont();
        }

        private void EnsureSortPickerPanel()
        {
            if (sortPickerPanel != null)
            {
                return;
            }

            sortPickerCanvas = ResolveRootCanvas();
            if (sortPickerCanvas == null)
            {
                return;
            }

            var panelRoot = new GameObject("InventorySortPickerPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(VerticalLayoutGroup));
            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.SetParent(sortPickerCanvas.transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(240f, 206f);

            var panelCanvas = panelRoot.GetComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingLayerID = sortPickerCanvas.sortingLayerID;
            panelCanvas.sortingOrder = sortPickerCanvas.sortingOrder + 220;

            var panelImage = panelRoot.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.14f, 0.22f, 0.98f);
            panelImage.raycastTarget = true;

            var panelLayout = panelRoot.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(6, 6, 6, 6);
            panelLayout.spacing = 4f;
            panelLayout.childAlignment = TextAnchor.UpperLeft;
            panelLayout.childControlHeight = true;
            panelLayout.childControlWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.childForceExpandWidth = true;

            sortPickerPanel = panelRect;
            sortPickerPanel.gameObject.SetActive(false);

            sortPickerOptionButtons.Clear();
            sortPickerOptionModes.Clear();

            AddSortPickerOption(InventorySortMode.Default, "默认顺序");
            AddSortPickerOption(InventorySortMode.NameAscending, "名称 A-Z");
            AddSortPickerOption(InventorySortMode.RarityDescending, "稀有度 ↓");
            AddSortPickerOption(InventorySortMode.PriceDescending, "价格 ↓");
            AddSortPickerOption(InventorySortMode.Category, "分类");
        }

        private void AddSortPickerOption(InventorySortMode mode, string label)
        {
            if (sortPickerPanel == null)
            {
                return;
            }

            var buttonRoot = new GameObject($"SortOption_{mode}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var buttonRect = buttonRoot.GetComponent<RectTransform>();
            buttonRect.SetParent(sortPickerPanel, false);

            var layout = buttonRoot.GetComponent<LayoutElement>();
            layout.preferredHeight = 36f;
            layout.flexibleWidth = 1f;

            var image = buttonRoot.GetComponent<Image>();
            image.color = filterInactiveColor;
            image.raycastTarget = true;

            var button = buttonRoot.GetComponent<Button>();
            button.targetGraphic = image;

            var textRoot = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var textRect = textRoot.GetComponent<RectTransform>();
            textRect.SetParent(buttonRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            var text = textRoot.GetComponent<Text>();
            text.text = label;
            text.font = sortPickerFont;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = filterInactiveTextColor;
            text.raycastTarget = false;

            button.onClick.AddListener(() =>
            {
                SetSortMode(mode);
                HideSortPicker();
            });

            sortPickerOptionModes.Add(mode);
            sortPickerOptionButtons.Add(button);
        }

        private void ToggleSortPicker()
        {
            if (sortPickerPanel == null)
            {
                EnsureSortPickerPanel();
            }

            if (sortPickerPanel == null)
            {
                return;
            }

            var targetActive = !sortPickerPanel.gameObject.activeSelf;
            if (!targetActive)
            {
                HideSortPicker();
                return;
            }

            RepositionSortPicker();
            sortPickerPanel.gameObject.SetActive(true);
            sortPickerPanel.SetAsLastSibling();
            RefreshSortPickerOptionStates();
        }

        private void HideSortPicker()
        {
            if (sortPickerPanel != null)
            {
                sortPickerPanel.gameObject.SetActive(false);
            }
        }

        private void RepositionSortPicker()
        {
            if (sortPickerPanel == null || sortDropdown == null)
            {
                return;
            }

            var canvas = ResolveRootCanvas();
            if (canvas == null)
            {
                return;
            }

            sortPickerCanvas = canvas;
            var canvasRect = canvas.transform as RectTransform;
            var dropdownRect = sortDropdown.transform as RectTransform;
            if (canvasRect == null || dropdownRect == null)
            {
                return;
            }

            var corners = new Vector3[4];
            dropdownRect.GetWorldCorners(corners);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[0]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvas.worldCamera, out var localPoint);
            sortPickerPanel.anchoredPosition = localPoint + new Vector2(0f, -4f);
        }

        private Canvas ResolveRootCanvas()
        {
            if (UIRoot.Instance != null)
            {
                if (UIRoot.Instance.ModalCanvas != null)
                {
                    return UIRoot.Instance.ModalCanvas.rootCanvas;
                }

                if (UIRoot.Instance.ScreensCanvas != null)
                {
                    return UIRoot.Instance.ScreensCanvas.rootCanvas;
                }
            }

            var localCanvas = sortDropdown != null ? sortDropdown.GetComponentInParent<Canvas>() : null;
            return localCanvas != null ? localCanvas.rootCanvas : null;
        }

        private void RefreshSortPickerCaption()
        {
            if (sortDropdown == null)
            {
                return;
            }

            var label = ResolveSortModeLabel(activeSortMode);
            if (sortDropdown.captionText != null)
            {
                sortDropdown.captionText.text = label;
                sortDropdown.captionText.color = filterActiveTextColor;
            }

            var fallbackLabel = sortDropdown.transform.Find("Label") != null
                ? sortDropdown.transform.Find("Label").GetComponent<Text>()
                : null;
            if (fallbackLabel != null)
            {
                fallbackLabel.text = label;
                fallbackLabel.color = filterActiveTextColor;
                if (fallbackLabel.font == null)
                {
                    fallbackLabel.font = sortPickerFont;
                }
            }
        }

        private void RefreshSortPickerOptionStates()
        {
            for (int i = 0; i < sortPickerOptionButtons.Count; i++)
            {
                var button = sortPickerOptionButtons[i];
                if (button == null)
                {
                    continue;
                }

                var isActive = i >= 0 &&
                               i < sortPickerOptionModes.Count &&
                               sortPickerOptionModes[i] == activeSortMode;
                // Keep active sort option interactable so visual state stays consistent.
                button.interactable = true;

                var image = button.targetGraphic as Image;
                if (image != null)
                {
                    image.color = isActive ? filterActiveColor : filterInactiveColor;
                }
                UIStyleKit.ApplyButtonStateColors(
                    button,
                    isActive ? filterActiveColor : filterInactiveColor,
                    isActive ? 0.06f : 0.1f,
                    0.18f,
                    0.52f,
                    0.08f);

                var text = button.GetComponentInChildren<Text>(true);
                if (text != null)
                {
                    text.color = isActive ? filterActiveTextColor : filterInactiveTextColor;
                }
            }
        }

        private static string ResolveSortModeLabel(InventorySortMode mode)
        {
            switch (mode)
            {
                case InventorySortMode.NameAscending:
                    return "名称 A-Z";
                case InventorySortMode.RarityDescending:
                    return "稀有度 ↓";
                case InventorySortMode.PriceDescending:
                    return "价格 ↓";
                case InventorySortMode.Category:
                    return "分类";
                default:
                    return "默认顺序";
            }
        }

        private void BuildFilteredDisplayIndices(List<int> output)
        {
            output.Clear();
            if (inventory == null)
            {
                return;
            }

            var items = inventory.Items;
            var hasSearchKeyword = !string.IsNullOrWhiteSpace(searchKeyword);
            var hasRarityFilter = activeRarityFilter != RarityQuickFilter.All;
            var hasSort = activeSortMode != InventorySortMode.Default;

            if (!hasSearchKeyword && !hasRarityFilter && !hasSort && activeFilter == InventoryFilter.All)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    output.Add(i);
                }

                return;
            }

            filteredItemIndices.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || !MatchesCurrentQuery(item))
                {
                    continue;
                }

                filteredItemIndices.Add(i);
            }

            if (hasSort)
            {
                filteredItemIndices.Sort(CompareFilteredItemIndex);
            }

            for (int i = 0; i < filteredItemIndices.Count; i++)
            {
                output.Add(filteredItemIndices[i]);
            }

            // 追加空格，保留拖拽落位能力。
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    output.Add(i);
                }
            }
        }

        private int CompareFilteredItemIndex(int leftIndex, int rightIndex)
        {
            if (inventory == null || leftIndex == rightIndex)
            {
                return leftIndex.CompareTo(rightIndex);
            }

            var items = inventory.Items;
            if (leftIndex < 0 || rightIndex < 0 || leftIndex >= items.Count || rightIndex >= items.Count)
            {
                return leftIndex.CompareTo(rightIndex);
            }

            var left = items[leftIndex];
            var right = items[rightIndex];
            if (left == null || right == null)
            {
                return leftIndex.CompareTo(rightIndex);
            }

            var leftDefinition = left.Definition;
            var rightDefinition = right.Definition;
            if (leftDefinition == null || rightDefinition == null)
            {
                return leftIndex.CompareTo(rightIndex);
            }

            var result = 0;
            switch (activeSortMode)
            {
                case InventorySortMode.NameAscending:
                    result = string.Compare(
                        ResolveDisplayName(left),
                        ResolveDisplayName(right),
                        StringComparison.OrdinalIgnoreCase);
                    break;
                case InventorySortMode.RarityDescending:
                    result = ((int)rightDefinition.Rarity).CompareTo((int)leftDefinition.Rarity);
                    break;
                case InventorySortMode.PriceDescending:
                    result = rightDefinition.BasePrice.CompareTo(leftDefinition.BasePrice);
                    break;
                case InventorySortMode.Category:
                    result = ((int)leftDefinition.Category).CompareTo((int)rightDefinition.Category);
                    break;
            }

            if (result != 0)
            {
                return result;
            }

            return leftIndex.CompareTo(rightIndex);
        }

        private int CompareOrganizeItems(ItemInstance left, ItemInstance right)
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

            var leftDefinition = left.Definition;
            var rightDefinition = right.Definition;
            if (leftDefinition == null && rightDefinition == null)
            {
                return 0;
            }

            if (leftDefinition == null)
            {
                return 1;
            }

            if (rightDefinition == null)
            {
                return -1;
            }

            var result = 0;
            switch (activeSortMode)
            {
                case InventorySortMode.NameAscending:
                    result = string.Compare(
                        ResolveDisplayName(left),
                        ResolveDisplayName(right),
                        StringComparison.OrdinalIgnoreCase);
                    break;
                case InventorySortMode.RarityDescending:
                    result = ((int)rightDefinition.Rarity).CompareTo((int)leftDefinition.Rarity);
                    break;
                case InventorySortMode.PriceDescending:
                    result = rightDefinition.BasePrice.CompareTo(leftDefinition.BasePrice);
                    break;
                case InventorySortMode.Category:
                    result = ((int)leftDefinition.Category).CompareTo((int)rightDefinition.Category);
                    break;
                default:
                    result = ((int)leftDefinition.Category).CompareTo((int)rightDefinition.Category);
                    if (result != 0)
                    {
                        break;
                    }

                    result = ((int)rightDefinition.Rarity).CompareTo((int)leftDefinition.Rarity);
                    break;
            }

            if (result != 0)
            {
                return result;
            }

            result = string.Compare(
                ResolveDisplayName(left),
                ResolveDisplayName(right),
                StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }

            result = rightDefinition.BasePrice.CompareTo(leftDefinition.BasePrice);
            if (result != 0)
            {
                return result;
            }

            return right.Stack.CompareTo(left.Stack);
        }

        private bool MatchesCurrentQuery(ItemInstance item)
        {
            return MatchesCurrentFilter(item) &&
                   MatchesSearchKeyword(item) &&
                   MatchesCurrentRarityFilter(item);
        }

        private bool MatchesCurrentFilter(ItemInstance item)
        {
            if (activeFilter == InventoryFilter.All)
            {
                return true;
            }

            if (item == null || item.Definition == null)
            {
                return false;
            }

            var category = item.Definition.Category;
            switch (activeFilter)
            {
                case InventoryFilter.Equipment:
                    return item.Definition.IsEquippable ||
                           category == ItemCategory.Weapon ||
                           category == ItemCategory.Armor ||
                           category == ItemCategory.Accessory;
                case InventoryFilter.Consumable:
                    return category == ItemCategory.Consumable;
                case InventoryFilter.Skill:
                    return category == ItemCategory.Skill;
                case InventoryFilter.Quest:
                    return category == ItemCategory.Quest;
                default:
                    return true;
            }
        }

        private bool MatchesSearchKeyword(ItemInstance item)
        {
            if (string.IsNullOrWhiteSpace(searchKeyword))
            {
                return true;
            }

            if (item == null || item.Definition == null)
            {
                return false;
            }

            var keyword = searchKeyword.Trim();
            if (keyword.Length == 0)
            {
                return true;
            }

            var displayName = ResolveDisplayName(item);
            if (!string.IsNullOrEmpty(displayName) &&
                displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var description = item.Definition.Description;
            if (!string.IsNullOrEmpty(description) &&
                description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var categoryName = item.Definition.Category.ToString();
            if (categoryName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var rarityName = item.Definition.Rarity.ToString();
            return rarityName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesCurrentRarityFilter(ItemInstance item)
        {
            if (activeRarityFilter == RarityQuickFilter.All)
            {
                return true;
            }

            if (item == null || item.Definition == null)
            {
                return false;
            }

            switch (activeRarityFilter)
            {
                case RarityQuickFilter.Common:
                    return item.Definition.Rarity == ItemRarity.Common;
                case RarityQuickFilter.Magic:
                    return item.Definition.Rarity == ItemRarity.Magic;
                case RarityQuickFilter.Rare:
                    return item.Definition.Rarity == ItemRarity.Rare;
                case RarityQuickFilter.Epic:
                    return item.Definition.Rarity == ItemRarity.Epic;
                case RarityQuickFilter.Legendary:
                    return item.Definition.Rarity == ItemRarity.Legendary;
                default:
                    return true;
            }
        }

        private static string ResolveDisplayName(ItemInstance item)
        {
            var definition = item != null ? item.Definition : null;
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

        private void RefreshFilterButtonStates()
        {
            ApplyFilterButtonState(allFilterButton, activeFilter == InventoryFilter.All);
            ApplyFilterButtonState(equipmentFilterButton, activeFilter == InventoryFilter.Equipment);
            ApplyFilterButtonState(consumableFilterButton, activeFilter == InventoryFilter.Consumable);
            ApplyFilterButtonState(skillFilterButton, activeFilter == InventoryFilter.Skill);
            ApplyFilterButtonState(questFilterButton, activeFilter == InventoryFilter.Quest);
            ApplyFilterButtonState(rarityAllButton, activeRarityFilter == RarityQuickFilter.All);
            ApplyFilterButtonState(rarityCommonButton, activeRarityFilter == RarityQuickFilter.Common);
            ApplyFilterButtonState(rarityMagicButton, activeRarityFilter == RarityQuickFilter.Magic);
            ApplyFilterButtonState(rarityRareButton, activeRarityFilter == RarityQuickFilter.Rare);
            ApplyFilterButtonState(rarityEpicButton, activeRarityFilter == RarityQuickFilter.Epic);
            ApplyFilterButtonState(rarityLegendaryButton, activeRarityFilter == RarityQuickFilter.Legendary);
        }

        private void ApplyFilterButtonState(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            // Keep active filter buttons interactable to preserve active-state visuals.
            button.interactable = true;

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = active ? filterActiveColor : filterInactiveColor;
            }
            UIStyleKit.ApplyButtonStateColors(
                button,
                active ? filterActiveColor : filterInactiveColor,
                active ? 0.06f : 0.1f,
                0.18f,
                0.52f,
                0.08f);

            var labels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].color = active ? filterActiveTextColor : filterInactiveTextColor;
            }
        }

        private void HandleThemeChanged(UIThemeConfig theme)
        {
            ApplyThemeColors();
            RefreshFilterButtonStates();
        }

        private void ApplyThemeColors()
        {
            filterActiveColor = UIStyleKit.TabActiveColor;
            filterInactiveColor = UIStyleKit.TabInactiveColor;
            filterActiveTextColor = UIStyleKit.TabActiveTextColor;
            filterInactiveTextColor = UIStyleKit.TabInactiveTextColor;
        }


    }
}
