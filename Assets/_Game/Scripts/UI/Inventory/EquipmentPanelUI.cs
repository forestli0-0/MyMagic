using System;
using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CombatSystem.UI
{
    /// <summary>
    /// 装备面板 UI，展示角色所有装备槽位。
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 根据 EquipmentComponent 槽位数量动态生成槽位 UI
    /// - 监听 EquipmentChanged 事件自动刷新
    /// - 通过 SlotSelected 事件通知上层选中变化
    /// </remarks>
    public class EquipmentPanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private EquipmentSlotUI slotTemplate;

        private readonly List<EquipmentSlotUI> slots = new List<EquipmentSlotUI>();
        private EquipmentComponent equipment;

        public event Action<int> SlotSelected;
        public event Action<EquipmentSlotUI, PointerEventData> SlotDragStarted;
        public event Action<EquipmentSlotUI, PointerEventData> SlotDragging;
        public event Action<EquipmentSlotUI, PointerEventData> SlotDragEnded;
        public event Action<EquipmentSlotUI, PointerEventData> SlotDropped;

        private void OnDestroy()
        {
            Unbind();
            DetachSlotEvents();
        }

        public void Bind(EquipmentComponent targetEquipment)
        {
            if (equipment == targetEquipment)
            {
                Refresh();
                return;
            }

            Unbind();
            equipment = targetEquipment;
            if (equipment != null)
            {
                equipment.EquipmentChanged += HandleEquipmentChanged;
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
            if (equipment != null)
            {
                equipment.EquipmentChanged -= HandleEquipmentChanged;
            }
        }

        private void HandleEquipmentChanged()
        {
            EnsureSlots();
            Refresh();
        }

        private void EnsureSlots()
        {
            if (slotsRoot == null || slotTemplate == null || equipment == null)
            {
                return;
            }

            var targetCount = equipment.Slots != null ? equipment.Slots.Count : 0;
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

            var accessoryIndex = 0;
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
                var active = i < targetCount;
                slots[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var slotType = equipment.Slots[i].Slot;
                string label;
                if (slotType == ItemSlot.Accessory)
                {
                    accessoryIndex++;
                    label = $"Accessory {accessoryIndex}";
                }
                else
                {
                    label = slotType.ToString();
                }

                slots[i].Configure(i, slotType, label);
            }
        }

        private void Refresh()
        {
            if (equipment == null || equipment.Slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count && i < equipment.Slots.Count; i++)
            {
                slots[i].SetItem(equipment.Slots[i].Item);
            }
        }

        private void HandleSlotClicked(EquipmentSlotUI slot)
        {
            if (slot == null)
            {
                return;
            }

            SlotSelected?.Invoke(slot.SlotIndex);
        }

        private void HandleSlotDragStarted(EquipmentSlotUI slot, PointerEventData eventData)
        {
            SlotDragStarted?.Invoke(slot, eventData);
        }

        private void HandleSlotDragging(EquipmentSlotUI slot, PointerEventData eventData)
        {
            SlotDragging?.Invoke(slot, eventData);
        }

        private void HandleSlotDragEnded(EquipmentSlotUI slot, PointerEventData eventData)
        {
            SlotDragEnded?.Invoke(slot, eventData);
        }

        private void HandleSlotDropped(EquipmentSlotUI slot, PointerEventData eventData)
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
