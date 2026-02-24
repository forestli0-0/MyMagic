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
    public class EquipmentSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public enum DragTargetVisualState
        {
            None = 0,
            Valid = 1,
            Invalid = 2
        }

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Image selection;
        [SerializeField] private Text slotLabel;
        [SerializeField] private Text itemLabel;

        private int slotIndex = -1;
        private ItemSlot slotType = ItemSlot.None;
        private ItemInstance item;
        private bool selected;
        private DragTargetVisualState dragTargetVisualState;
        private bool compactMode;

        public int SlotIndex => slotIndex;
        public ItemSlot SlotType => slotType;
        public ItemInstance Item => item;

        public event Action<EquipmentSlotUI> Clicked;
        public event Action<EquipmentSlotUI, PointerEventData> DragStarted;
        public event Action<EquipmentSlotUI, PointerEventData> Dragging;
        public event Action<EquipmentSlotUI, PointerEventData> DragEnded;
        public event Action<EquipmentSlotUI, PointerEventData> Dropped;
        public event Action<EquipmentSlotUI, bool> HoverChanged;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }

            RefreshVisualState();
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
                    itemLabel.text = "空";
                }
            }

            ApplyCompactVisuals();
        }

        public void SetSelected(bool selected)
        {
            this.selected = selected;
            RefreshVisualState();
        }

        public void SetDragTargetState(DragTargetVisualState state)
        {
            if (dragTargetVisualState == state)
            {
                return;
            }

            dragTargetVisualState = state;
            RefreshVisualState();
        }

        public void ClearDragTargetState()
        {
            SetDragTargetState(DragTargetVisualState.None);
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverChanged?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverChanged?.Invoke(this, false);
        }

        public void SetCompactMode(bool compact)
        {
            if (compactMode == compact)
            {
                return;
            }

            compactMode = compact;
            ApplyCompactVisuals();
        }

        private void RefreshVisualState()
        {
            var baseColor = new Color(0.14f, 0.16f, 0.2f, 1f);
            var selectedColor = new Color(0.23f, 0.31f, 0.46f, 1f);
            var dragValidColor = new Color(0.19f, 0.38f, 0.28f, 1f);
            var dragInvalidColor = new Color(0.4f, 0.2f, 0.22f, 1f);

            var selectionSelectedColor = new Color(0.35f, 0.52f, 0.86f, 0.24f);
            var selectionDragValidColor = new Color(0.42f, 0.9f, 0.65f, 0.24f);
            var selectionDragInvalidColor = new Color(0.95f, 0.45f, 0.48f, 0.24f);

            var hasDragState = dragTargetVisualState != DragTargetVisualState.None;
            if (selection != null)
            {
                selection.enabled = selected || hasDragState;
                if (dragTargetVisualState == DragTargetVisualState.Valid)
                {
                    selection.color = selectionDragValidColor;
                }
                else if (dragTargetVisualState == DragTargetVisualState.Invalid)
                {
                    selection.color = selectionDragInvalidColor;
                }
                else
                {
                    selection.color = selectionSelectedColor;
                }
            }

            if (background == null)
            {
                return;
            }

            if (dragTargetVisualState == DragTargetVisualState.Valid)
            {
                background.color = dragValidColor;
                return;
            }

            if (dragTargetVisualState == DragTargetVisualState.Invalid)
            {
                background.color = dragInvalidColor;
                return;
            }

            background.color = selected ? selectedColor : baseColor;
        }

        private void ApplyCompactVisuals()
        {
            if (slotLabel != null)
            {
                slotLabel.gameObject.SetActive(!compactMode);
            }

            if (itemLabel != null)
            {
                itemLabel.gameObject.SetActive(!compactMode);
            }

            var rowLayout = GetComponent<HorizontalLayoutGroup>();
            if (rowLayout != null)
            {
                rowLayout.enabled = !compactMode;
            }

            if (icon != null && compactMode)
            {
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(50f, 50f);
            }
        }
    }
}
