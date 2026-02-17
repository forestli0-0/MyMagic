using System;
using System.Collections.Generic;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CombatSystem.UI
{
    /// <summary>
    /// 背包网格 UI，以格子形式展示所有背包物品。
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 根据 InventoryComponent 容量动态生成/复用槽位
    /// - 监听 InventoryChanged 事件自动刷新
    /// - 通过 SlotSelected 事件通知上层选中变化
    /// </remarks>
    public class InventoryGridUI : MonoBehaviour
    {
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private InventorySlotUI slotTemplate;

        private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();
        private readonly List<int> customDisplayIndices = new List<int>(32);
        private InventoryComponent inventory;
        private bool useCustomDisplayIndices;

        public event Action<int> SlotSelected;
        public event Action<InventorySlotUI, PointerEventData> SlotDragStarted;
        public event Action<InventorySlotUI, PointerEventData> SlotDragging;
        public event Action<InventorySlotUI, PointerEventData> SlotDragEnded;
        public event Action<InventorySlotUI, PointerEventData> SlotDropped;

        private void OnDestroy()
        {
            Unbind();
            DetachSlotEvents();
        }

        public void Bind(InventoryComponent targetInventory, IReadOnlyList<int> displayIndices = null)
        {
            if (inventory != targetInventory)
            {
                Unbind();
                inventory = targetInventory;
                if (inventory != null)
                {
                    inventory.InventoryChanged += HandleInventoryChanged;
                }
            }

            ApplyDisplayIndices(displayIndices);

            EnsureSlots();
            Refresh();
        }

        public void SetSelectedIndex(int index)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].SetSelected(slots[i].SlotIndex == index);
            }
        }

        private void Unbind()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged()
        {
            EnsureSlots();
            Refresh();
        }

        private void EnsureSlots()
        {
            if (slotsRoot == null || slotTemplate == null)
            {
                return;
            }

            var targetCount = ResolveTargetSlotCount();
            while (slots.Count < targetCount)
            {
                var instance = Instantiate(slotTemplate, slotsRoot);
                instance.gameObject.SetActive(true);
                instance.Clicked += HandleSlotClicked;
                instance.DragStarted += HandleSlotDragStarted;
                instance.Dragging += HandleSlotDragging;
                instance.DragEnded += HandleSlotDragEnded;
                instance.Dropped += HandleSlotDropped;
                slots.Add(instance);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Clicked -= HandleSlotClicked;
                slots[i].Clicked += HandleSlotClicked;
                slots[i].DragStarted -= HandleSlotDragStarted;
                slots[i].DragStarted += HandleSlotDragStarted;
                slots[i].Dragging -= HandleSlotDragging;
                slots[i].Dragging += HandleSlotDragging;
                slots[i].DragEnded -= HandleSlotDragEnded;
                slots[i].DragEnded += HandleSlotDragEnded;
                slots[i].Dropped -= HandleSlotDropped;
                slots[i].Dropped += HandleSlotDropped;
                slots[i].gameObject.SetActive(i < targetCount);
            }
        }

        private void Refresh()
        {
            if (slots.Count == 0)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var inventoryIndex = ResolveInventoryIndexForDisplaySlot(i);
                slot.SetSlotIndex(inventoryIndex);

                ItemInstance item = null;
                if (inventory != null &&
                    inventoryIndex >= 0 &&
                    inventoryIndex < inventory.Items.Count)
                {
                    item = inventory.Items[inventoryIndex];
                }

                slot.SetItem(item);
            }
        }

        private void HandleSlotClicked(InventorySlotUI slot)
        {
            if (slot == null || slot.SlotIndex < 0)
            {
                return;
            }

            SlotSelected?.Invoke(slot.SlotIndex);
        }

        private void HandleSlotDragStarted(InventorySlotUI slot, PointerEventData eventData)
        {
            SlotDragStarted?.Invoke(slot, eventData);
        }

        private void HandleSlotDragging(InventorySlotUI slot, PointerEventData eventData)
        {
            SlotDragging?.Invoke(slot, eventData);
        }

        private void HandleSlotDragEnded(InventorySlotUI slot, PointerEventData eventData)
        {
            SlotDragEnded?.Invoke(slot, eventData);
        }

        private void HandleSlotDropped(InventorySlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.SlotIndex < 0)
            {
                return;
            }

            SlotDropped?.Invoke(slot, eventData);
        }

        private int ResolveTargetSlotCount()
        {
            if (inventory == null)
            {
                return 0;
            }

            if (useCustomDisplayIndices)
            {
                return customDisplayIndices.Count;
            }

            return inventory.Capacity;
        }

        private int ResolveInventoryIndexForDisplaySlot(int displaySlotIndex)
        {
            if (displaySlotIndex < 0)
            {
                return -1;
            }

            if (useCustomDisplayIndices)
            {
                if (displaySlotIndex >= customDisplayIndices.Count)
                {
                    return -1;
                }

                return customDisplayIndices[displaySlotIndex];
            }

            return displaySlotIndex;
        }

        private void ApplyDisplayIndices(IReadOnlyList<int> displayIndices)
        {
            customDisplayIndices.Clear();
            useCustomDisplayIndices = displayIndices != null;

            if (!useCustomDisplayIndices || inventory == null)
            {
                return;
            }

            var capacity = inventory.Capacity;
            for (int i = 0; i < displayIndices.Count; i++)
            {
                var index = displayIndices[i];
                if (index < 0 || index >= capacity)
                {
                    continue;
                }

                if (customDisplayIndices.Contains(index))
                {
                    continue;
                }

                customDisplayIndices.Add(index);
            }
        }

        private void DetachSlotEvents()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Clicked -= HandleSlotClicked;
                slots[i].DragStarted -= HandleSlotDragStarted;
                slots[i].Dragging -= HandleSlotDragging;
                slots[i].DragEnded -= HandleSlotDragEnded;
                slots[i].Dropped -= HandleSlotDropped;
            }
        }
    }
}
