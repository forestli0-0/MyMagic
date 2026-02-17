using System;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 背包槽位 UI，展示单个背包格子的物品信息。
    /// </summary>
    /// <remarks>
    /// 功能：
    /// - 显示物品图标、堆叠数量
    /// - 通过 Clicked 事件通知上层选中变化
    /// </remarks>
    public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Image selection;
        [SerializeField] private Text stackText;

        private int slotIndex = -1;
        private ItemInstance item;

        public int SlotIndex => slotIndex;
        public ItemInstance Item => item;

        public event Action<InventorySlotUI> Clicked;
        public event Action<InventorySlotUI, PointerEventData> DragStarted;
        public event Action<InventorySlotUI, PointerEventData> Dragging;
        public event Action<InventorySlotUI, PointerEventData> DragEnded;
        public event Action<InventorySlotUI, PointerEventData> Dropped;

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

        public void SetSlotIndex(int index)
        {
            slotIndex = index;
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

            if (stackText != null)
            {
                if (item != null && item.IsStackable && item.Stack > 1)
                {
                    stackText.text = item.Stack.ToString();
                }
                else
                {
                    stackText.text = string.Empty;
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
                    ? new Color(0.24f, 0.34f, 0.5f, 1f)
                    : new Color(0.15f, 0.17f, 0.22f, 1f);
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
