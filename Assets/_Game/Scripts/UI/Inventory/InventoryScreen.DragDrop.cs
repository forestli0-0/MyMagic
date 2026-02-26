using System;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public partial class InventoryScreen
    {
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
            ClearDragTargetStates();
            UpdateDragTargetHover(eventData);
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

            ClearDragTargetStates();
            currentDragInventoryTarget = null;
            currentDragEquipmentTarget = null;
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

        private void UpdateDragTargetHover(PointerEventData eventData)
        {
            if (!dragActive || eventData == null)
            {
                return;
            }

            var inventoryTarget = eventData.pointerEnter != null
                ? eventData.pointerEnter.GetComponentInParent<InventorySlotUI>()
                : null;
            var equipmentTarget = eventData.pointerEnter != null
                ? eventData.pointerEnter.GetComponentInParent<EquipmentSlotUI>()
                : null;

            if (inventoryTarget == currentDragInventoryTarget &&
                equipmentTarget == currentDragEquipmentTarget)
            {
                return;
            }

            ClearDragTargetStates();
            currentDragInventoryTarget = inventoryTarget;
            currentDragEquipmentTarget = equipmentTarget;

            if (currentDragInventoryTarget != null && inventoryGrid != null)
            {
                var isValid = CanDropOnInventorySlot(currentDragInventoryTarget.SlotIndex);
                inventoryGrid.SetDragTargetState(currentDragInventoryTarget.SlotIndex, isValid);
                return;
            }

            if (currentDragEquipmentTarget != null && equipmentPanel != null)
            {
                var isValid = CanDropOnEquipmentSlot(currentDragEquipmentTarget.SlotIndex);
                equipmentPanel.SetDragTargetState(currentDragEquipmentTarget.SlotIndex, isValid);
            }
        }

        private void ClearDragTargetStates()
        {
            if (inventoryGrid != null)
            {
                inventoryGrid.ClearDragTargetStates();
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.ClearDragTargetStates();
            }
        }

        private bool CanDropOnInventorySlot(int targetIndex)
        {
            if (!dragActive || inventory == null || targetIndex < 0 || targetIndex >= inventory.Capacity)
            {
                return false;
            }

            if (dragPayload.Source == DragSource.Inventory)
            {
                if (dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= inventory.Items.Count)
                {
                    return false;
                }

                if (dragPayload.SourceIndex == targetIndex)
                {
                    return false;
                }

                return dragPayload.Item != null;
            }

            if (dragPayload.Source == DragSource.Equipment)
            {
                if (equipment == null || dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= equipment.Slots.Count)
                {
                    return false;
                }

                if (targetIndex >= inventory.Items.Count)
                {
                    return false;
                }

                var sourceSlot = equipment.Slots[dragPayload.SourceIndex];
                if (sourceSlot == null || sourceSlot.Item == null)
                {
                    return false;
                }

                var targetItem = inventory.Items[targetIndex];
                if (targetItem == null)
                {
                    return true;
                }

                return targetItem.Definition != null &&
                       targetItem.Definition.IsEquippable &&
                       targetItem.Definition.Slot == sourceSlot.Slot;
            }

            return false;
        }

        private bool CanDropOnEquipmentSlot(int targetIndex)
        {
            if (!dragActive || equipment == null || targetIndex < 0 || targetIndex >= equipment.Slots.Count)
            {
                return false;
            }

            if (dragPayload.Source == DragSource.Inventory)
            {
                if (inventory == null || dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= inventory.Items.Count)
                {
                    return false;
                }

                var sourceItem = inventory.Items[dragPayload.SourceIndex];
                if (sourceItem == null || sourceItem.Definition == null || !sourceItem.Definition.IsEquippable)
                {
                    return false;
                }

                var targetSlot = equipment.Slots[targetIndex];
                if (targetSlot == null || sourceItem.Definition.Slot != targetSlot.Slot)
                {
                    return false;
                }

                if (targetSlot.Item != null && !inventory.CanAddItem(targetSlot.Item))
                {
                    return false;
                }

                return true;
            }

            if (dragPayload.Source == DragSource.Equipment)
            {
                if (dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= equipment.Slots.Count)
                {
                    return false;
                }

                if (dragPayload.SourceIndex == targetIndex)
                {
                    return false;
                }

                var sourceSlot = equipment.Slots[dragPayload.SourceIndex];
                var targetSlot = equipment.Slots[targetIndex];
                if (sourceSlot == null || targetSlot == null)
                {
                    return false;
                }

                return sourceSlot.Slot == targetSlot.Slot;
            }

            return false;
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

        private void HandleInventoryDragStarted(InventorySlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.Item == null)
            {
                return;
            }

            ResetDoubleClickState();
            BeginDrag(DragSource.Inventory, slot.SlotIndex, slot.Item, eventData);
        }

        private void HandleInventoryDragging(InventorySlotUI slot, PointerEventData eventData)
        {
            UpdateDragIcon(eventData);
            UpdateDragTargetHover(eventData);
        }

        private void HandleInventoryDragEnded(InventorySlotUI slot, PointerEventData eventData)
        {
            TryHandleSkillSlotDrop(eventData);
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

            ResetDoubleClickState();
            BeginDrag(DragSource.Equipment, slot.SlotIndex, slot.Item, eventData);
        }

        private void TryExecuteInventoryDoubleClickAction()
        {
            if (currentPrimaryAction != InventoryActionCommand.EquipSelected)
            {
                return;
            }

            _ = ExecuteActionCommand(currentPrimaryAction);
        }

        private void ResetDoubleClickState()
        {
            lastInventoryClickIndex = InvalidSelectionIndex;
            lastInventoryClickAt = InvalidClickTimestamp;
        }

        private void ClearInventorySelection()
        {
            selectedInventoryIndex = InvalidSelectionIndex;
        }

        private void ClearEquipmentSelection()
        {
            selectedEquipmentIndex = InvalidSelectionIndex;
        }

        private void ClearHoveredEquipmentState()
        {
            hoveredEquipmentIndex = InvalidSelectionIndex;
            hoveredEquipmentClearAt = NoHoverClearPending;
        }

        private bool HasExplicitSelection()
        {
            return selectedInventoryIndex >= 0 || selectedEquipmentIndex >= 0;
        }

        private void HandleEquipmentDragging(EquipmentSlotUI slot, PointerEventData eventData)
        {
            UpdateDragIcon(eventData);
            UpdateDragTargetHover(eventData);
        }

        private void HandleEquipmentDragEnded(EquipmentSlotUI slot, PointerEventData eventData)
        {
            EndDrag();
        }

        private bool TryHandleSkillSlotDrop(PointerEventData eventData)
        {
            if (!dragActive || eventData == null || dragPayload.Source != DragSource.Inventory || inventory == null)
            {
                return false;
            }

            if (dragPayload.SourceIndex < 0 || dragPayload.SourceIndex >= inventory.Items.Count)
            {
                return false;
            }

            var sourceItem = inventory.Items[dragPayload.SourceIndex];
            var targetSlot = ResolveSkillSlotDropTarget(eventData);
            if (targetSlot == null)
            {
                if (TryResolveLinkedSkill(sourceItem, out _))
                {
                    UIToast.Info("请拖到下方技能栏槽位。");
                }

                return false;
            }

            if (skillBar == null)
            {
                skillBar = FindFirstObjectByType<SkillBarUI>(FindObjectsInactive.Include);
            }

            if (!TryResolveLinkedSkill(sourceItem, out var linkedSkill))
            {
                UIToast.Warning("该物品未配置可装配技能。");
                return false;
            }

            if (skillBar == null)
            {
                UIToast.Warning("技能栏未就绪。");
                return false;
            }

            if (!skillBar.CanAssignSkillToSlot(linkedSkill, targetSlot))
            {
                UIToast.Warning("该槽位不可装配此技能。");
                return false;
            }

            if (!skillBar.TryAssignSkillToSlot(linkedSkill, targetSlot))
            {
                UIToast.Warning("技能装配失败。");
                return false;
            }

            RefreshInventoryGrid();
            RefreshSelectionAfterChange();
            UIToast.Success($"已装配技能：{linkedSkill.DisplayName}");
            return true;
        }

        private static bool TryResolveLinkedSkill(ItemInstance item, out SkillDefinition skill)
        {
            skill = null;
            if (item == null || item.Definition == null)
            {
                return false;
            }

            if (item.Definition.Category != ItemCategory.Skill)
            {
                return false;
            }

            skill = item.Definition.LinkedSkill;
            return skill != null;
        }

        private SkillSlotUI ResolveSkillSlotDropTarget(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return null;
            }

            var target = ResolveSkillSlotFromObject(eventData.pointerCurrentRaycast.gameObject);
            if (target != null)
            {
                return target;
            }

            target = ResolveSkillSlotFromObject(eventData.pointerEnter);
            if (target != null)
            {
                return target;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                var raycastProbe = new PointerEventData(eventSystem)
                {
                    position = eventData.position
                };
                dropRaycastBuffer.Clear();
                eventSystem.RaycastAll(raycastProbe, dropRaycastBuffer);
                for (int i = 0; i < dropRaycastBuffer.Count; i++)
                {
                    target = ResolveSkillSlotFromObject(dropRaycastBuffer[i].gameObject);
                    if (target != null)
                    {
                        return target;
                    }
                }
            }

            EnsureSkillBarReference();
            if (skillBar != null)
            {
                var fallback = skillBar.ResolveSlotAtScreenPoint(eventData.position, eventData.enterEventCamera);
                if (fallback != null)
                {
                    return fallback;
                }
            }

            return null;
        }

        private static SkillSlotUI ResolveSkillSlotFromObject(GameObject source)
        {
            if (source == null)
            {
                return null;
            }

            return source.GetComponentInParent<SkillSlotUI>();
        }

        private InventoryGridUI.SkillEquipState ResolveSkillEquipStateForItem(ItemInstance item)
        {
            if (!TryResolveLinkedSkill(item, out var linkedSkill))
            {
                return InventoryGridUI.SkillEquipState.None;
            }

            EnsureSkillBarReference();
            if (skillBar == null)
            {
                return InventoryGridUI.SkillEquipState.None;
            }

            return skillBar.TryGetSkillSlotNumber(linkedSkill, out var slotNumber)
                ? InventoryGridUI.SkillEquipState.EquippedInSlot(slotNumber)
                : InventoryGridUI.SkillEquipState.None;
        }

        private string BuildSkillEquipStatusLine(ItemInstance item)
        {
            if (!TryResolveLinkedSkill(item, out _))
            {
                return null;
            }

            var equipState = ResolveSkillEquipStateForItem(item);
            return equipState.IsEquipped
                ? $"技能栏：已装配（槽 {equipState.SlotNumber}）"
                : "技能栏：未装配";
        }

        private void EnsureSkillBarReference()
        {
            if (skillBar != null)
            {
                return;
            }

            skillBar = FindFirstObjectByType<SkillBarUI>(FindObjectsInactive.Include);
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


        private static Font ResolveBuiltinFallbackFont()
        {
            try
            {
                var legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacyFont != null)
                {
                    return legacyFont;
                }
            }
            catch (Exception)
            {
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception)
            {
                return null;
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
