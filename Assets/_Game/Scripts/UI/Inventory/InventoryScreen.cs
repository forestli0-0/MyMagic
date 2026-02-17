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
        private readonly List<int> filteredDisplayIndices = new List<int>(32);
        private bool subscribed;
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
            if (selected != null && !MatchesCurrentFilter(selected))
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
            activeFilter = filter;
            selectedInventoryIndex = -1;
            RefreshAll();
        }

        private void BuildFilteredDisplayIndices(List<int> output)
        {
            output.Clear();
            if (inventory == null)
            {
                return;
            }

            var items = inventory.Items;
            if (activeFilter == InventoryFilter.All)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    output.Add(i);
                }

                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || !MatchesCurrentFilter(item))
                {
                    continue;
                }

                output.Add(i);
            }

            // 在分类模式下追加空格，保留拖拽落位能力。
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    output.Add(i);
                }
            }
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

        private void RefreshFilterButtonStates()
        {
            ApplyFilterButtonState(allFilterButton, activeFilter == InventoryFilter.All);
            ApplyFilterButtonState(equipmentFilterButton, activeFilter == InventoryFilter.Equipment);
            ApplyFilterButtonState(consumableFilterButton, activeFilter == InventoryFilter.Consumable);
            ApplyFilterButtonState(questFilterButton, activeFilter == InventoryFilter.Quest);
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
}
