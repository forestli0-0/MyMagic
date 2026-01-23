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
        private InventoryComponent inventory;

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

        public void Bind(InventoryComponent targetInventory)
        {
            if (inventory == targetInventory)
            {
                Refresh();
                return;
            }

            Unbind();
            inventory = targetInventory;
            if (inventory != null)
            {
                inventory.InventoryChanged += HandleInventoryChanged;
            }

            EnsureSlots();
            Refresh();
        }

        public void SetSelectedIndex(int index)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].SetSelected(i == index);
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

            var targetCount = inventory != null ? inventory.Capacity : 0;
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
                slot.SetSlotIndex(i);

                ItemInstance item = null;
                if (inventory != null && i < inventory.Items.Count)
                {
                    item = inventory.Items[i];
                }

                slot.SetItem(item);
            }
        }

        private void HandleSlotClicked(InventorySlotUI slot)
        {
            if (slot == null)
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
            SlotDropped?.Invoke(slot, eventData);
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
