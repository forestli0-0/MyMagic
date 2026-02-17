using System;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 装备槽位 UI，展示单个装备位的物品信息。
    /// </summary>
    /// <remarks>
    /// 功能：
    /// - 显示槽位类型标签、物品图标、物品名称
    /// - 通过 Clicked 事件通知上层选中变化
    /// </remarks>
    public class EquipmentSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Image selection;
        [SerializeField] private Text slotLabel;
        [SerializeField] private Text itemLabel;

        private int slotIndex = -1;
        private ItemSlot slotType = ItemSlot.None;
        private ItemInstance item;

        public int SlotIndex => slotIndex;
        public ItemSlot SlotType => slotType;
        public ItemInstance Item => item;

        public event Action<EquipmentSlotUI> Clicked;
        public event Action<EquipmentSlotUI, PointerEventData> DragStarted;
        public event Action<EquipmentSlotUI, PointerEventData> Dragging;
        public event Action<EquipmentSlotUI, PointerEventData> DragEnded;
        public event Action<EquipmentSlotUI, PointerEventData> Dropped;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Configure(int index, ItemSlot slot, string label)
        {
            slotIndex = index;
            slotType = slot;

            if (slotLabel != null)
            {
                slotLabel.text = string.IsNullOrEmpty(label) ? slot.ToString() : label;
            }
        }

        public void SetItem(ItemInstance newItem)
        {
            item = newItem;

            if (icon != null)
            {
                var sprite = item != null && item.Definition != null ? item.Definition.Icon : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (itemLabel != null)
            {
                if (item != null && item.Definition != null)
                {
                    var displayName = item.Definition.DisplayName;
                    itemLabel.text = string.IsNullOrWhiteSpace(displayName) ? item.Definition.name : displayName;
                }
                else
                {
                    itemLabel.text = "Empty";
                }
            }
        }

        public void SetSelected(bool selected)
        {
            if (selection != null)
            {
                selection.enabled = selected;
            }

            if (background != null)
            {
                background.color = selected
                    ? new Color(0.23f, 0.31f, 0.46f, 1f)
                    : new Color(0.14f, 0.16f, 0.2f, 1f);
            }
        }

        private void HandleClicked()
        {
            Clicked?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (item == null)
            {
                return;
            }

            DragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (item == null)
            {
                return;
            }

            Dragging?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DragEnded?.Invoke(this, eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            Dropped?.Invoke(this, eventData);
        }
    }
}
