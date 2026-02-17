using System;
using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 库存界面主屏幕，管理背包与装备的展示与交互。
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 整合背包网格、装备面板、物品对比面板
    /// - 处理物品选择、穿戴、卸下操作
    /// - 响应库存/装备变化事件刷新 UI
    /// 
    /// 使用方式：
    /// - 通过 I 键（InventoryHotkey）开关界面
    /// - 继承 UIScreenBase，进入时隐藏 HUD
    /// </remarks>
    public class InventoryScreen : UIScreenBase
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private EquipmentComponent equipment;

        [Header("Widgets")]
        [SerializeField] private InventoryGridUI inventoryGrid;
        [SerializeField] private EquipmentPanelUI equipmentPanel;
        [SerializeField] private ItemComparePanelUI comparePanel;
        [SerializeField] private Button allFilterButton;
        [SerializeField] private Button equipmentFilterButton;
        [SerializeField] private Button consumableFilterButton;
        [SerializeField] private Button questFilterButton;
        [SerializeField] private InputField searchInputField;
        [SerializeField] private Dropdown sortDropdown;
        [SerializeField] private Button rarityAllButton;
        [SerializeField] private Button rarityCommonButton;
        [SerializeField] private Button rarityMagicButton;
        [SerializeField] private Button rarityRareButton;
        [SerializeField] private Button rarityEpicButton;
        [SerializeField] private Button rarityLegendaryButton;
        [SerializeField] private Text capacityText;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unequipButton;
        [SerializeField] private Text actionHintText;
        [SerializeField] private Color filterActiveColor = new Color(0.26f, 0.38f, 0.56f, 1f);
        [SerializeField] private Color filterInactiveColor = new Color(0.2f, 0.22f, 0.26f, 1f);
        [SerializeField] private Color filterActiveTextColor = new Color(0.97f, 0.98f, 1f, 1f);
        [SerializeField] private Color filterInactiveTextColor = new Color(0.85f, 0.87f, 0.9f, 1f);

        private int selectedInventoryIndex = -1;
        private int selectedEquipmentIndex = -1;
        private InventoryFilter activeFilter = InventoryFilter.All;
        private InventorySortMode activeSortMode = InventorySortMode.Default;
        private RarityQuickFilter activeRarityFilter = RarityQuickFilter.All;
        private string searchKeyword = string.Empty;
        private readonly List<int> filteredDisplayIndices = new List<int>(32);
        private readonly List<int> filteredItemIndices = new List<int>(32);
        private bool subscribed;
        private bool sortDropdownBindingLogged;
        private Button sortPickerButton;
        private RectTransform sortPickerPanel;
        private Canvas sortPickerCanvas;
        private Font sortPickerFont;
        private readonly List<Button> sortPickerOptionButtons = new List<Button>(8);
        private readonly List<InventorySortMode> sortPickerOptionModes = new List<InventorySortMode>(8);
        private DragPayload dragPayload;
        private bool dragActive;
        private Image dragIcon;
        private Canvas dragCanvas;

        private enum InventoryFilter
        {
            All = 0,
            Equipment = 1,
            Consumable = 2,
            Quest = 3
        }

        private enum InventorySortMode
        {
            Default = 0,
            NameAscending = 1,
            RarityDescending = 2,
            PriceDescending = 3,
            Category = 4
        }

        private enum RarityQuickFilter
        {
            All = 0,
            Common = 1,
            Magic = 2,
            Rare = 3,
            Epic = 4,
            Legendary = 5
        }

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        public override void OnEnter()
        {
            EnsureReferences();
            Subscribe();
            RefreshAll();

            if (uiManager != null)
            {
                uiManager.SetHudVisible(false);
            }
        }

        public override void OnExit()
        {
            Unsubscribe();
            HideSortPicker();
            EndDrag();
            if (uiManager != null)
            {
                uiManager.SetHudVisible(true);
            }
        }

        private void OnDestroy()
        {
            if (dragIcon != null)
            {
                Destroy(dragIcon.gameObject);
                dragIcon = null;
                dragCanvas = null;
            }
        }

        public override void OnFocus()
        {
            RefreshAll();
        }

        private void Update()
        {
            if (sortPickerPanel == null || !sortPickerPanel.gameObject.activeSelf)
            {
                return;
            }

            if (!UnityEngine.Input.GetMouseButtonDown(0))
            {
                return;
            }

            var panelContains = RectTransformUtility.RectangleContainsScreenPoint(
                sortPickerPanel,
                UnityEngine.Input.mousePosition,
                sortPickerCanvas != null ? sortPickerCanvas.worldCamera : null);

            var dropdownRect = sortDropdown != null ? sortDropdown.transform as RectTransform : null;
            var dropdownContains = dropdownRect != null && RectTransformUtility.RectangleContainsScreenPoint(
                dropdownRect,
                UnityEngine.Input.mousePosition,
                sortPickerCanvas != null ? sortPickerCanvas.worldCamera : null);

            if (!panelContains && !dropdownContains)
            {
                HideSortPicker();
            }
        }

        public void EquipSelected()
        {
            if (inventory == null || equipment == null)
            {
                return;
            }

            if (selectedInventoryIndex < 0 || selectedInventoryIndex >= inventory.Items.Count)
            {
                return;
            }

            var item = inventory.Items[selectedInventoryIndex];
            if (equipment.TryEquip(item, inventory))
            {
                selectedInventoryIndex = -1;
                RefreshSelectionAfterChange();
            }
        }

        public void UnequipSelected()
        {
            if (inventory == null || equipment == null)
            {
                return;
            }

            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= equipment.Slots.Count)
            {
                return;
            }

            if (equipment.TryUnequip(selectedEquipmentIndex, inventory))
            {
                selectedEquipmentIndex = -1;
                RefreshSelectionAfterChange();
            }
        }

        public void ShowAllItems()
        {
            SetFilter(InventoryFilter.All);
        }

        public void ShowEquipmentItems()
        {
            SetFilter(InventoryFilter.Equipment);
        }

        public void ShowConsumableItems()
        {
            SetFilter(InventoryFilter.Consumable);
        }

        public void ShowQuestItems()
        {
            SetFilter(InventoryFilter.Quest);
        }

        public void ShowAllRarities()
        {
            SetRarityFilter(RarityQuickFilter.All);
        }

        public void ShowCommonRarities()
        {
            SetRarityFilter(RarityQuickFilter.Common);
        }

        public void ShowMagicRarities()
        {
            SetRarityFilter(RarityQuickFilter.Magic);
        }

        public void ShowRareRarities()
        {
            SetRarityFilter(RarityQuickFilter.Rare);
        }

        public void ShowEpicRarities()
        {
            SetRarityFilter(RarityQuickFilter.Epic);
        }

        public void ShowLegendaryRarities()
        {
            SetRarityFilter(RarityQuickFilter.Legendary);
        }

        private void EnsureReferences()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (inventory == null)
            {
                inventory = FindFirstObjectByType<InventoryComponent>();
            }

            if (equipment == null)
            {
                equipment = FindFirstObjectByType<EquipmentComponent>();
            }
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (inventory != null)
            {
                inventory.InventoryChanged += HandleInventoryChanged;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged += HandleEquipmentChanged;
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.SlotSelected += HandleInventorySlotSelected;
                inventoryGrid.SlotDragStarted += HandleInventoryDragStarted;
                inventoryGrid.SlotDragging += HandleInventoryDragging;
                inventoryGrid.SlotDragEnded += HandleInventoryDragEnded;
                inventoryGrid.SlotDropped += HandleInventorySlotDropped;
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.SlotSelected += HandleEquipmentSlotSelected;
                equipmentPanel.SlotDragStarted += HandleEquipmentDragStarted;
                equipmentPanel.SlotDragging += HandleEquipmentDragging;
                equipmentPanel.SlotDragEnded += HandleEquipmentDragEnded;
                equipmentPanel.SlotDropped += HandleEquipmentSlotDropped;
            }

            if (equipButton != null)
            {
                equipButton.onClick.AddListener(HandlePrimaryAction);
            }

            if (unequipButton != null)
            {
                unequipButton.onClick.AddListener(HandleSecondaryAction);
            }

            if (allFilterButton != null)
            {
                allFilterButton.onClick.AddListener(ShowAllItems);
            }

            if (equipmentFilterButton != null)
            {
                equipmentFilterButton.onClick.AddListener(ShowEquipmentItems);
            }

            if (consumableFilterButton != null)
            {
                consumableFilterButton.onClick.AddListener(ShowConsumableItems);
            }

            if (questFilterButton != null)
            {
                questFilterButton.onClick.AddListener(ShowQuestItems);
            }

            if (searchInputField != null)
            {
                searchInputField.SetTextWithoutNotify(searchKeyword);
                searchInputField.onValueChanged.AddListener(HandleSearchKeywordChanged);
            }

            if (sortDropdown != null)
            {
                EnsureSortDropdownOptions();
                SetupSortPicker();
            }

            if (rarityAllButton != null)
            {
                rarityAllButton.onClick.AddListener(ShowAllRarities);
            }

            if (rarityCommonButton != null)
            {
                rarityCommonButton.onClick.AddListener(ShowCommonRarities);
            }

            if (rarityMagicButton != null)
            {
                rarityMagicButton.onClick.AddListener(ShowMagicRarities);
            }

            if (rarityRareButton != null)
            {
                rarityRareButton.onClick.AddListener(ShowRareRarities);
            }

            if (rarityEpicButton != null)
            {
                rarityEpicButton.onClick.AddListener(ShowEpicRarities);
            }

            if (rarityLegendaryButton != null)
            {
                rarityLegendaryButton.onClick.AddListener(ShowLegendaryRarities);
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged -= HandleEquipmentChanged;
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.SlotSelected -= HandleInventorySlotSelected;
                inventoryGrid.SlotDragStarted -= HandleInventoryDragStarted;
                inventoryGrid.SlotDragging -= HandleInventoryDragging;
                inventoryGrid.SlotDragEnded -= HandleInventoryDragEnded;
                inventoryGrid.SlotDropped -= HandleInventorySlotDropped;
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.SlotSelected -= HandleEquipmentSlotSelected;
                equipmentPanel.SlotDragStarted -= HandleEquipmentDragStarted;
                equipmentPanel.SlotDragging -= HandleEquipmentDragging;
                equipmentPanel.SlotDragEnded -= HandleEquipmentDragEnded;
                equipmentPanel.SlotDropped -= HandleEquipmentSlotDropped;
            }

            if (equipButton != null)
            {
                equipButton.onClick.RemoveListener(HandlePrimaryAction);
            }

            if (unequipButton != null)
            {
                unequipButton.onClick.RemoveListener(HandleSecondaryAction);
            }

            if (allFilterButton != null)
            {
                allFilterButton.onClick.RemoveListener(ShowAllItems);
            }

            if (equipmentFilterButton != null)
            {
                equipmentFilterButton.onClick.RemoveListener(ShowEquipmentItems);
            }

            if (consumableFilterButton != null)
            {
                consumableFilterButton.onClick.RemoveListener(ShowConsumableItems);
            }

            if (questFilterButton != null)
            {
                questFilterButton.onClick.RemoveListener(ShowQuestItems);
            }

            if (searchInputField != null)
            {
                searchInputField.onValueChanged.RemoveListener(HandleSearchKeywordChanged);
            }

            if (sortDropdown != null)
            {
                if (sortPickerButton != null)
                {
                    sortPickerButton.onClick.RemoveListener(ToggleSortPicker);
                }
            }

            if (rarityAllButton != null)
            {
                rarityAllButton.onClick.RemoveListener(ShowAllRarities);
            }

            if (rarityCommonButton != null)
            {
                rarityCommonButton.onClick.RemoveListener(ShowCommonRarities);
            }

            if (rarityMagicButton != null)
            {
                rarityMagicButton.onClick.RemoveListener(ShowMagicRarities);
            }

            if (rarityRareButton != null)
            {
                rarityRareButton.onClick.RemoveListener(ShowRareRarities);
            }

            if (rarityEpicButton != null)
            {
                rarityEpicButton.onClick.RemoveListener(ShowEpicRarities);
            }

            if (rarityLegendaryButton != null)
            {
                rarityLegendaryButton.onClick.RemoveListener(ShowLegendaryRarities);
            }

            subscribed = false;
        }

        private void RefreshAll()
        {
            RefreshInventoryGrid();

            if (equipmentPanel != null)
            {
                equipmentPanel.Bind(equipment);
            }

            RefreshSelectionAfterChange();
            RefreshFilterButtonStates();
        }

        private void HandleInventoryChanged()
        {
            RefreshInventoryGrid();
            RefreshSelectionAfterChange();
        }

        private void HandleEquipmentChanged()
        {
            RefreshSelectionAfterChange();
        }

        private void RefreshInventoryGrid()
        {
            RefreshCapacityText();

            if (inventoryGrid == null)
            {
                return;
            }

            BuildFilteredDisplayIndices(filteredDisplayIndices);
            inventoryGrid.Bind(inventory, filteredDisplayIndices);
        }

        private void RefreshCapacityText()
        {
            if (capacityText == null)
            {
                return;
            }

            if (inventory == null)
            {
                capacityText.text = "容量 --/--";
                capacityText.color = new Color(0.8f, 0.82f, 0.86f, 1f);
                return;
            }

            var used = 0;
            var items = inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    used++;
                }
            }

            capacityText.text = $"容量 {used}/{inventory.Capacity}";
            capacityText.color = used >= inventory.Capacity
                ? new Color(0.95f, 0.58f, 0.52f, 1f)
                : new Color(0.8f, 0.88f, 0.98f, 1f);
        }

        private void HandleInventorySlotSelected(int index)
        {
            selectedInventoryIndex = index;
            selectedEquipmentIndex = -1;

            if (equipmentPanel != null)
            {
                equipmentPanel.SetSelectedIndex(-1);
            }

            RefreshSelectionAfterChange();
        }

        private void HandleEquipmentSlotSelected(int index)
        {
            selectedEquipmentIndex = index;
            selectedInventoryIndex = -1;

            if (inventoryGrid != null)
            {
                inventoryGrid.SetSelectedIndex(-1);
            }

            RefreshSelectionAfterChange();
        }

        private void HandleInventoryDragStarted(InventorySlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.Item == null)
            {
                return;
            }

            BeginDrag(DragSource.Inventory, slot.SlotIndex, slot.Item, eventData);
        }

        private void HandleInventoryDragging(InventorySlotUI slot, PointerEventData eventData)
        {
            UpdateDragIcon(eventData);
        }

        private void HandleInventoryDragEnded(InventorySlotUI slot, PointerEventData eventData)
        {
            EndDrag();
        }

        private void HandleInventorySlotDropped(InventorySlotUI slot, PointerEventData eventData)
        {
            if (!dragActive || slot == null)
            {
                return;
            }

            if (dragPayload.Source == DragSource.Inventory)
            {
                // 背包内拖拽：交换/合并/移动
                HandleInventoryToInventory(slot.SlotIndex);
            }
            else if (dragPayload.Source == DragSource.Equipment)
            {
                // 装备拖到背包：按目标槽位放置
                HandleEquipmentToInventory(slot.SlotIndex);
            }
        }

        private void HandleEquipmentDragStarted(EquipmentSlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.Item == null)
            {
                return;
            }

            BeginDrag(DragSource.Equipment, slot.SlotIndex, slot.Item, eventData);
        }

        private void HandleEquipmentDragging(EquipmentSlotUI slot, PointerEventData eventData)
        {
            UpdateDragIcon(eventData);
        }

        private void HandleEquipmentDragEnded(EquipmentSlotUI slot, PointerEventData eventData)
        {
            EndDrag();
        }

        private void HandleEquipmentSlotDropped(EquipmentSlotUI slot, PointerEventData eventData)
        {
            if (!dragActive || slot == null)
            {
                return;
            }

            if (dragPayload.Source == DragSource.Inventory)
            {
                // 背包拖到装备：按槽位穿戴
                HandleInventoryToEquipment(slot.SlotIndex);
            }
            else if (dragPayload.Source == DragSource.Equipment)
            {
                // 装备内部拖拽：同类型槽位交换
                HandleEquipmentToEquipment(slot.SlotIndex);
            }
        }

        private void RefreshSelectionAfterChange()
        {
            var inventoryItem = ResolveInventorySelection();
            var equipmentItem = ResolveEquipmentSelection();

            if (inventoryGrid != null)
            {
                inventoryGrid.SetSelectedIndex(selectedInventoryIndex);
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.SetSelectedIndex(selectedEquipmentIndex);
            }

            if (comparePanel != null)
            {
                if (inventoryItem != null)
                {
                    comparePanel.ShowItem(inventoryItem, FindEquippedForSlot(inventoryItem));
                }
                else if (equipmentItem != null)
                {
                    comparePanel.ShowItem(equipmentItem, null);
                }
                else
                {
                    comparePanel.ShowItem(null, null);
                }
            }

            UpdateButtons(inventoryItem, equipmentItem);
        }

        private ItemInstance ResolveInventorySelection()
        {
            if (inventory == null)
            {
                selectedInventoryIndex = -1;
                return null;
            }

            if (selectedInventoryIndex < 0 || selectedInventoryIndex >= inventory.Items.Count)
            {
                selectedInventoryIndex = -1;
                return null;
            }

            var selected = inventory.Items[selectedInventoryIndex];
            if (selected != null && !MatchesCurrentQuery(selected))
            {
                selectedInventoryIndex = -1;
                return null;
            }

            return selected;
        }

        private ItemInstance ResolveEquipmentSelection()
        {
            if (equipment == null)
            {
                selectedEquipmentIndex = -1;
                return null;
            }

            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= equipment.Slots.Count)
            {
                selectedEquipmentIndex = -1;
                return null;
            }

            return equipment.Slots[selectedEquipmentIndex].Item;
        }

        private void UpdateButtons(ItemInstance inventoryItem, ItemInstance equipmentItem)
        {
            if (inventoryItem != null && inventoryItem.Definition != null)
            {
                if (inventoryItem.Definition.IsEquippable)
                {
                    ApplyActionState(equipButton, "装备", true);
                    ApplyActionState(unequipButton, "丢弃", true);
                    SetActionHint("主操作: 装备该物品    次操作: 丢弃该物品");
                    return;
                }

                if (CanSplit(inventoryItem) && HasEmptyInventorySlot())
                {
                    ApplyActionState(equipButton, "拆分", true);
                    ApplyActionState(unequipButton, "丢弃", true);
                    SetActionHint("主操作: 平分堆叠    次操作: 丢弃 1 个");
                    return;
                }

                if (inventoryItem.IsStackable)
                {
                    ApplyActionState(equipButton, "丢弃", true);
                    ApplyActionState(unequipButton, "拆分", false);
                    SetActionHint("主操作: 丢弃 1 个");
                    return;
                }

                ApplyActionState(equipButton, "丢弃", true);
                ApplyActionState(unequipButton, "拆分", false);
                SetActionHint("主操作: 丢弃该物品");
                return;
            }

            if (equipmentItem != null)
            {
                ApplyActionState(equipButton, "卸下", true);
                ApplyActionState(unequipButton, "丢弃", true);
                SetActionHint("主操作: 卸下到背包    次操作: 直接丢弃装备");
                return;
            }

            ApplyActionState(equipButton, "装备", false);
            ApplyActionState(unequipButton, "卸下", false);
            SetActionHint("选择背包或装备中的物品");
        }

        private void HandlePrimaryAction()
        {
            var inventoryItem = ResolveInventorySelection();
            if (inventoryItem != null && inventoryItem.Definition != null)
            {
                if (inventoryItem.Definition.IsEquippable)
                {
                    EquipSelected();
                    return;
                }

                if (CanSplit(inventoryItem) && HasEmptyInventorySlot())
                {
                    SplitSelected();
                    return;
                }

                DropSelectedInventory(1);
                return;
            }

            var equipmentItem = ResolveEquipmentSelection();
            if (equipmentItem != null)
            {
                UnequipSelected();
            }
        }

        private void HandleSecondaryAction()
        {
            var inventoryItem = ResolveInventorySelection();
            if (inventoryItem != null && inventoryItem.Definition != null)
            {
                DropSelectedInventory(1);
                return;
            }

            var equipmentItem = ResolveEquipmentSelection();
            if (equipmentItem != null)
            {
                DropSelectedEquipment();
            }
        }

        private bool SplitSelected()
        {
            if (inventory == null || selectedInventoryIndex < 0 || selectedInventoryIndex >= inventory.Items.Count)
            {
                return false;
            }

            var selected = inventory.Items[selectedInventoryIndex];
            if (!CanSplit(selected))
            {
                return false;
            }

            var targetIndex = FindFirstEmptyInventorySlot();
            if (targetIndex < 0)
            {
                return false;
            }

            var splitAmount = selected.Stack / 2;
            if (splitAmount <= 0)
            {
                return false;
            }

            selected.SetStack(selected.Stack - splitAmount);
            var splitItem = selected.CloneWithStack(splitAmount);
            if (!inventory.TrySetItemAt(targetIndex, splitItem, out var replaced) || replaced != null)
            {
                selected.SetStack(selected.Stack + splitAmount);
                return false;
            }

            return true;
        }

        private bool DropSelectedInventory(int amount)
        {
            if (inventory == null || selectedInventoryIndex < 0 || selectedInventoryIndex >= inventory.Items.Count)
            {
                return false;
            }

            var selected = inventory.Items[selectedInventoryIndex];
            if (selected == null)
            {
                return false;
            }

            var removeAmount = selected.IsStackable
                ? Mathf.Clamp(amount, 1, selected.Stack)
                : selected.Stack;
            return inventory.TryRemoveAt(selectedInventoryIndex, removeAmount);
        }

        private bool DropSelectedEquipment()
        {
            if (equipment == null || selectedEquipmentIndex < 0 || selectedEquipmentIndex >= equipment.Slots.Count)
            {
                return false;
            }

            return equipment.TryReplaceSlotItem(selectedEquipmentIndex, null);
        }

        private bool HasEmptyInventorySlot()
        {
            return FindFirstEmptyInventorySlot() >= 0;
        }

        private int FindFirstEmptyInventorySlot()
        {
            if (inventory == null)
            {
                return -1;
            }

            var items = inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool CanSplit(ItemInstance item)
        {
            return item != null && item.IsStackable && item.Stack > 1;
        }

        private void ApplyActionState(Button button, string label, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            SetButtonLabel(button, label);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var labels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null)
                {
                    continue;
                }

                labels[i].text = label;
            }
        }

        private void SetActionHint(string hint)
        {
            if (actionHintText == null)
            {
                return;
            }

            actionHintText.text = string.IsNullOrEmpty(hint) ? string.Empty : hint;
        }

        private void SetFilter(InventoryFilter filter)
        {
            if (activeFilter == filter)
            {
                return;
            }

            activeFilter = filter;
            selectedInventoryIndex = -1;
            RefreshAll();
        }

        private void SetRarityFilter(RarityQuickFilter filter)
        {
            if (activeRarityFilter == filter)
            {
                return;
            }

            activeRarityFilter = filter;
            selectedInventoryIndex = -1;
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
            selectedInventoryIndex = -1;
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
            selectedInventoryIndex = -1;
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

            sortPickerFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
                button.interactable = !isActive;

                var image = button.targetGraphic as Image;
                if (image != null)
                {
                    image.color = isActive ? filterActiveColor : filterInactiveColor;
                }

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

            button.interactable = !active;

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = active ? filterActiveColor : filterInactiveColor;
            }

            var labels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].color = active ? filterActiveTextColor : filterInactiveTextColor;
            }
        }

        private ItemInstance FindEquippedForSlot(ItemInstance item)
        {
            if (equipment == null || item == null || item.Definition == null || !item.Definition.IsEquippable)
            {
                return null;
            }

            var slots = equipment.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Slot == item.Definition.Slot && slot.Item != null)
                {
                    return slot.Item;
                }
            }

            return null;
        }

        private void BeginDrag(DragSource source, int sourceIndex, ItemInstance item, PointerEventData eventData)
        {
            if (item == null || item.Definition == null)
            {
                return;
            }

            // 创建/显示拖拽图标
            EnsureDragIcon();
            dragPayload = new DragPayload(source, sourceIndex, item);
            dragActive = true;

            if (dragIcon != null)
            {
                dragIcon.sprite = item.Definition.Icon;
                dragIcon.enabled = dragIcon.sprite != null;
            }

            UpdateDragIcon(eventData);
        }

        private void EndDrag()
        {
            if (!dragActive)
            {
                return;
            }

            // 收起拖拽图标
            dragActive = false;
            dragPayload = default;

            if (dragIcon != null)
            {
                dragIcon.enabled = false;
            }
        }

        private void UpdateDragIcon(PointerEventData eventData)
        {
            if (!dragActive || dragIcon == null || eventData == null)
            {
                return;
            }

            if (dragCanvas == null)
            {
                dragIcon.rectTransform.position = eventData.position;
                return;
            }

            if (dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                dragIcon.rectTransform.position = eventData.position;
                return;
            }

            var canvasTransform = dragCanvas.transform as RectTransform;
            if (canvasTransform == null)
            {
                dragIcon.rectTransform.position = eventData.position;
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasTransform,
                eventData.position,
                dragCanvas.worldCamera,
                out var localPoint);
            dragIcon.rectTransform.localPosition = localPoint;
        }

        private void EnsureDragIcon()
        {
            if (dragIcon != null)
            {
                return;
            }

            // 挂到 Overlay 画布，避免被 UI 遮挡
            Canvas targetCanvas = null;
            if (UIRoot.Instance != null)
            {
                targetCanvas = UIRoot.Instance.OverlayCanvas != null
                    ? UIRoot.Instance.OverlayCanvas
                    : UIRoot.Instance.ScreensCanvas;
            }

            if (targetCanvas == null)
            {
                targetCanvas = GetComponentInParent<Canvas>();
            }

            if (targetCanvas == null)
            {
                return;
            }

            dragCanvas = targetCanvas;
            var root = new GameObject("InventoryDragIcon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.SetParent(targetCanvas.transform, false);
            rectTransform.sizeDelta = new Vector2(64f, 64f);

            var canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            dragIcon = root.GetComponent<Image>();
            dragIcon.raycastTarget = false;
            dragIcon.enabled = false;
        }

        private void HandleInventoryToInventory(int targetIndex)
        {
            if (inventory == null)
            {
                return;
            }

            if (targetIndex < 0 || targetIndex >= inventory.Capacity)
            {
                return;
            }

            if (dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= inventory.Items.Count)
            {
                return;
            }

            if (dragPayload.SourceIndex == targetIndex)
            {
                return;
            }

            var targetItem = inventory.Items[targetIndex];
            if (targetItem != null)
            {
                // 目标有物品：优先合并堆叠，否则交换
                if (!inventory.TryMergeStack(dragPayload.SourceIndex, targetIndex))
                {
                    inventory.TrySwapItems(dragPayload.SourceIndex, targetIndex);
                }

                return;
            }

            // 目标空：直接移动
            inventory.TryMoveItem(dragPayload.SourceIndex, targetIndex);
        }

        private void HandleInventoryToEquipment(int targetIndex)
        {
            if (inventory == null || equipment == null)
            {
                return;
            }

            if (dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= inventory.Items.Count)
            {
                return;
            }

            if (targetIndex < 0 || targetIndex >= equipment.Slots.Count)
            {
                return;
            }

            var item = inventory.Items[dragPayload.SourceIndex];
            if (item == null)
            {
                return;
            }

            if (equipment.TryEquipToSlot(item, targetIndex, inventory))
            {
                return;
            }
        }

        private void HandleEquipmentToInventory(int targetIndex)
        {
            if (inventory == null || equipment == null)
            {
                return;
            }

            if (dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= equipment.Slots.Count)
            {
                return;
            }

            if (targetIndex < 0 || targetIndex >= inventory.Capacity)
            {
                return;
            }

            var sourceSlot = equipment.Slots[dragPayload.SourceIndex];
            if (sourceSlot == null || sourceSlot.Item == null)
            {
                return;
            }

            var inventoryItem = inventory.Items[targetIndex];
            if (inventoryItem != null)
            {
                // 目标格有装备：仅允许同槽位互换
                if (inventoryItem.Definition == null || !inventoryItem.Definition.IsEquippable)
                {
                    return;
                }

                if (inventoryItem.Definition.Slot != sourceSlot.Slot)
                {
                    return;
                }

                // 背包槽位放入装备，然后把原装备放回装备位
                if (!inventory.TrySetItemAt(targetIndex, sourceSlot.Item, out _))
                {
                    return;
                }

                equipment.TryReplaceSlotItem(dragPayload.SourceIndex, inventoryItem);
                return;
            }

            // 目标格为空：直接卸下放入该格
            if (!inventory.TrySetItemAt(targetIndex, sourceSlot.Item, out _))
            {
                return;
            }

            equipment.TryReplaceSlotItem(dragPayload.SourceIndex, null);
        }

        private void HandleEquipmentToEquipment(int targetIndex)
        {
            if (equipment == null)
            {
                return;
            }

            if (dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= equipment.Slots.Count)
            {
                return;
            }

            if (targetIndex < 0 || targetIndex >= equipment.Slots.Count)
            {
                return;
            }

            if (equipment.TrySwapSlots(dragPayload.SourceIndex, targetIndex))
            {
                return;
            }
        }

        private enum DragSource
        {
            None,
            Inventory,
            Equipment
        }

        private readonly struct DragPayload
        {
            public readonly DragSource Source;
            public readonly int SourceIndex;
            public readonly ItemInstance Item;

            public DragPayload(DragSource source, int sourceIndex, ItemInstance item)
            {
                Source = source;
                SourceIndex = sourceIndex;
                Item = item;
            }
        }
    }

    /// <summary>
    /// Keeps runtime dropdown list above gameplay UI layers.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Dropdown))]
    public class DropdownOverlayFix : MonoBehaviour
    {
        [SerializeField] private int overlayOrderOffset = 120;
        [SerializeField] private bool moveListToRootCanvas = true;
        [SerializeField] private Color itemTextColor = new Color(0.95f, 0.97f, 1f, 1f);

        private Dropdown dropdown;
        private Canvas rootCanvas;
        private Font fallbackFont;

        private void Awake()
        {
            dropdown = GetComponent<Dropdown>();
            ResolveRootCanvas();
            ResolveFallbackFont();
        }

        private void OnEnable()
        {
            ResolveRootCanvas();
            ResolveFallbackFont();
        }

        private void LateUpdate()
        {
            EnsureOpenListOnTop();
        }

        private void ResolveRootCanvas()
        {
            if (rootCanvas != null)
            {
                return;
            }

            var currentCanvas = GetComponentInParent<Canvas>();
            if (currentCanvas != null)
            {
                rootCanvas = currentCanvas.rootCanvas;
            }
        }

        private void EnsureOpenListOnTop()
        {
            if (dropdown == null)
            {
                return;
            }

            var listRect = dropdown.transform.Find("Dropdown List") as RectTransform;
            if (listRect == null)
            {
                return;
            }

            ResolveRootCanvas();
            if (moveListToRootCanvas && rootCanvas != null && listRect.parent != rootCanvas.transform)
            {
                listRect.SetParent(rootCanvas.transform, true);
            }

            listRect.SetAsLastSibling();

            var listCanvas = listRect.GetComponent<Canvas>();
            if (listCanvas == null)
            {
                listCanvas = listRect.gameObject.AddComponent<Canvas>();
            }

            listCanvas.overrideSorting = true;
            if (rootCanvas != null)
            {
                listCanvas.sortingLayerID = rootCanvas.sortingLayerID;
                listCanvas.sortingOrder = rootCanvas.sortingOrder + Mathf.Max(10, overlayOrderOffset);
            }
            else
            {
                listCanvas.sortingOrder = Mathf.Max(1000, listCanvas.sortingOrder);
            }

            if (listRect.GetComponent<GraphicRaycaster>() == null)
            {
                listRect.gameObject.AddComponent<GraphicRaycaster>();
            }

            PatchRuntimeOptionLabels(listRect);
        }

        private void ResolveFallbackFont()
        {
            if (fallbackFont != null)
            {
                return;
            }

            if (dropdown != null && dropdown.captionText != null && dropdown.captionText.font != null)
            {
                fallbackFont = dropdown.captionText.font;
                return;
            }

            fallbackFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void PatchRuntimeOptionLabels(RectTransform listRect)
        {
            if (dropdown == null || listRect == null || dropdown.options == null)
            {
                return;
            }

            var content = listRect.Find("Viewport/Content") as RectTransform;
            if (content == null)
            {
                return;
            }

            var optionCount = dropdown.options.Count;
            if (optionCount <= 0)
            {
                return;
            }

            var itemCount = content.childCount;
            for (int i = 0; i < itemCount; i++)
            {
                var item = content.GetChild(i) as RectTransform;
                if (item == null)
                {
                    continue;
                }

                var label = ResolveOrCreateItemLabel(item);
                if (label == null)
                {
                    continue;
                }

                var optionIndex = Mathf.Clamp(i, 0, optionCount - 1);
                var optionData = dropdown.options[optionIndex];
                label.text = optionData != null ? optionData.text : string.Empty;
                label.color = itemTextColor;
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.raycastTarget = false;
                label.fontSize = Mathf.Max(14, label.fontSize);

                if (label.font == null)
                {
                    ResolveFallbackFont();
                    label.font = fallbackFont;
                }

                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(18f, 0f);
                labelRect.offsetMax = new Vector2(-18f, 0f);

                var itemImage = item.GetComponent<Image>();
                if (itemImage != null && itemImage.color.a < 0.45f)
                {
                    itemImage.color = new Color(0.2f, 0.23f, 0.3f, 0.9f);
                }
            }
        }

        private static Text ResolveOrCreateItemLabel(RectTransform item)
        {
            if (item == null)
            {
                return null;
            }

            var labelTransform = item.Find("Item Label");
            if (labelTransform != null)
            {
                return labelTransform.GetComponent<Text>();
            }

            var existingText = item.GetComponentInChildren<Text>(true);
            if (existingText != null)
            {
                return existingText;
            }

            var labelGo = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(item, false);
            return labelGo.GetComponent<Text>();
        }
    }
}
