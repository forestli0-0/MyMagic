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
        [Header("Layout")]
        [SerializeField] private bool useCharacterLayout = true;
        [SerializeField] private Vector2 slotSize = new Vector2(82f, 82f);
        [SerializeField] private Vector2 layoutOffset = Vector2.zero;
        [SerializeField] private RectTransform silhouetteRoot;

        private readonly List<EquipmentSlotUI> slots = new List<EquipmentSlotUI>();
        private EquipmentComponent equipment;

        public event Action<int> SlotSelected;
        public event Action<int, bool> SlotHoverChanged;
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

        public void SetDragTargetState(int slotIndex, bool valid)
        {
            var slot = ResolveSlotByIndex(slotIndex);
            if (slot == null)
            {
                return;
            }

            slot.SetDragTargetState(valid
                ? EquipmentSlotUI.DragTargetVisualState.Valid
                : EquipmentSlotUI.DragTargetVisualState.Invalid);
        }

        public void ClearDragTargetStates()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null || !slots[i].gameObject.activeInHierarchy)
                {
                    continue;
                }

                slots[i].ClearDragTargetState();
            }
        }

        public bool ContainsScreenPoint(Vector2 screenPosition, Camera eventCamera = null)
        {
            if (slotsRoot == null)
            {
                return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(slotsRoot, screenPosition, eventCamera);
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

            ApplyRootLayoutMode();

            var targetCount = equipment.Slots != null ? equipment.Slots.Count : 0;
            while (slots.Count < targetCount)
            {
                var instance = Instantiate(slotTemplate, slotsRoot);
                instance.gameObject.SetActive(true);
                instance.Clicked += HandleSlotClicked;
                instance.HoverChanged += HandleSlotHoverChanged;
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
                slots[i].HoverChanged -= HandleSlotHoverChanged;
                slots[i].HoverChanged += HandleSlotHoverChanged;
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
                slots[i].SetCompactMode(useCharacterLayout);
                ApplySlotLayout(slots[i], slotType, accessoryIndex);
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

        private void HandleSlotHoverChanged(EquipmentSlotUI slot, bool hovered)
        {
            if (slot == null)
            {
                return;
            }

            SlotHoverChanged?.Invoke(slot.SlotIndex, hovered);
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
                slots[i].HoverChanged -= HandleSlotHoverChanged;
                slots[i].DragStarted -= HandleSlotDragStarted;
                slots[i].Dragging -= HandleSlotDragging;
                slots[i].DragEnded -= HandleSlotDragEnded;
                slots[i].Dropped -= HandleSlotDropped;
            }
        }

        private EquipmentSlotUI ResolveSlotByIndex(int slotIndex)
        {
            if (slotIndex < 0)
            {
                return null;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (slot.SlotIndex == slotIndex)
                {
                    return slot;
                }
            }

            return null;
        }

        private void ApplyRootLayoutMode()
        {
            if (slotsRoot == null)
            {
                return;
            }

            if (silhouetteRoot != null)
            {
                silhouetteRoot.gameObject.SetActive(useCharacterLayout);
            }

            var vertical = slotsRoot.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                vertical.enabled = !useCharacterLayout;
            }

            var horizontal = slotsRoot.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
            {
                horizontal.enabled = !useCharacterLayout;
            }

            var grid = slotsRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.enabled = !useCharacterLayout;
            }
        }

        private void ApplySlotLayout(EquipmentSlotUI slot, ItemSlot slotType, int accessoryIndex)
        {
            if (slot == null)
            {
                return;
            }

            var rect = slot.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            var layoutElement = slot.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = slot.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = useCharacterLayout;
            if (!useCharacterLayout)
            {
                return;
            }

            var anchored = ResolveSlotPosition(slotType, accessoryIndex) + layoutOffset;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = slotSize;
            rect.anchoredPosition = anchored;

            layoutElement.minWidth = slotSize.x;
            layoutElement.preferredWidth = slotSize.x;
            layoutElement.minHeight = slotSize.y;
            layoutElement.preferredHeight = slotSize.y;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }

        private static Vector2 ResolveSlotPosition(ItemSlot slotType, int accessoryIndex)
        {
            switch (slotType)
            {
                case ItemSlot.Weapon:
                    return new Vector2(-118f, 30f);
                case ItemSlot.Headband:
                    return new Vector2(0f, 126f);
                case ItemSlot.Clothes:
                    return new Vector2(0f, 30f);
                case ItemSlot.Shoes:
                    return new Vector2(0f, -106f);
                case ItemSlot.Accessory:
                    return ResolveAccessoryPosition(accessoryIndex);
                default:
                    return new Vector2(0f, -186f);
            }
        }

        private static Vector2 ResolveAccessoryPosition(int accessoryIndex)
        {
            switch (accessoryIndex)
            {
                case 1:
                    return new Vector2(118f, 30f);
                case 2:
                    return new Vector2(-118f, -106f);
                case 3:
                    return new Vector2(118f, -106f);
                default:
                    return new Vector2(118f, -186f - (accessoryIndex - 4) * 46f);
            }
        }
    }
}
