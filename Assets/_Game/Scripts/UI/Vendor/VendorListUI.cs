using System;
using System.Collections.Generic;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CombatSystem.UI
{
    public class VendorListUI : MonoBehaviour
    {
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private VendorItemSlotUI slotTemplate;

        private readonly List<VendorItemSlotUI> slots = new List<VendorItemSlotUI>(16);
        private VendorService vendorService;

        public event Action<int> SlotSelected;
        public event Action<VendorItemSlotUI, PointerEventData> SlotDragStarted;
        public event Action<VendorItemSlotUI, PointerEventData> SlotDragging;
        public event Action<VendorItemSlotUI, PointerEventData> SlotDragEnded;
        public event Action<VendorItemSlotUI, PointerEventData> SlotDropped;

        private void OnDestroy()
        {
            Unbind();
            DetachSlotEvents();
        }

        public void Bind(VendorService vendor)
        {
            if (vendorService == vendor)
            {
                Refresh();
                return;
            }

            Unbind();
            vendorService = vendor;
            if (vendorService != null)
            {
                vendorService.VendorUpdated += HandleVendorUpdated;
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
            if (vendorService != null)
            {
                vendorService.VendorUpdated -= HandleVendorUpdated;
            }
        }

        private void HandleVendorUpdated()
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

            var targetCount = vendorService != null ? vendorService.Items.Count : 0;
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

                VendorItemState item = null;
                if (vendorService != null && i < vendorService.Items.Count)
                {
                    item = vendorService.Items[i];
                }

                slot.SetItem(item);
            }
        }

        private void HandleSlotClicked(VendorItemSlotUI slot)
        {
            if (slot == null)
            {
                return;
            }

            if (VendorScreen.DebugEnabled)
            {
                Debug.Log($"[UI][Vendor] Slot clicked index={slot.SlotIndex}", this);
            }

            SlotSelected?.Invoke(slot.SlotIndex);
        }

        private void HandleSlotDragStarted(VendorItemSlotUI slot, PointerEventData eventData)
        {
            SlotDragStarted?.Invoke(slot, eventData);
        }

        private void HandleSlotDragging(VendorItemSlotUI slot, PointerEventData eventData)
        {
            SlotDragging?.Invoke(slot, eventData);
        }

        private void HandleSlotDragEnded(VendorItemSlotUI slot, PointerEventData eventData)
        {
            SlotDragEnded?.Invoke(slot, eventData);
        }

        private void HandleSlotDropped(VendorItemSlotUI slot, PointerEventData eventData)
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
