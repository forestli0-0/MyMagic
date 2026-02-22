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
        [SerializeField] private Text stackText;
        [SerializeField] private RectTransform skillBadgeRoot;
        [SerializeField] private Image skillBadgeBackground;
        [SerializeField] private Text skillBadgeText;

        private int slotIndex = -1;
        private ItemInstance item;
        private bool selected;
        private DragTargetVisualState dragTargetVisualState;
        private bool skillEquipped;
        private int equippedSkillSlotNumber;
        private static Font cachedBadgeFont;

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

            EnsureSkillBadgeVisual();
            RefreshSkillBadgeVisual();
            RefreshVisualState();
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

            RefreshSkillBadgeVisual();
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

        public void SetSkillEquipState(bool equipped, int slotNumber)
        {
            var normalizedSlotNumber = Mathf.Max(0, slotNumber);
            var normalizedEquipped = equipped && normalizedSlotNumber > 0;
            if (skillEquipped == normalizedEquipped &&
                equippedSkillSlotNumber == normalizedSlotNumber)
            {
                return;
            }

            skillEquipped = normalizedEquipped;
            equippedSkillSlotNumber = normalizedSlotNumber;
            RefreshSkillBadgeVisual();
            RefreshVisualState();
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

        private void RefreshVisualState()
        {
            var baseColor = new Color(0.15f, 0.17f, 0.22f, 1f);
            var selectedColor = new Color(0.24f, 0.34f, 0.5f, 1f);
            var equippedColor = new Color(0.19f, 0.25f, 0.2f, 1f);
            var dragValidColor = new Color(0.2f, 0.42f, 0.3f, 1f);
            var dragInvalidColor = new Color(0.42f, 0.2f, 0.22f, 1f);

            var selectionSelectedColor = new Color(0.38f, 0.56f, 0.9f, 0.28f);
            var selectionEquippedColor = new Color(0.4f, 0.86f, 0.58f, 0.2f);
            var selectionDragValidColor = new Color(0.42f, 0.9f, 0.65f, 0.24f);
            var selectionDragInvalidColor = new Color(0.95f, 0.45f, 0.48f, 0.24f);

            var hasDragState = dragTargetVisualState != DragTargetVisualState.None;
            if (selection != null)
            {
                selection.enabled = selected || hasDragState || skillEquipped;
                if (dragTargetVisualState == DragTargetVisualState.Valid)
                {
                    selection.color = selectionDragValidColor;
                }
                else if (dragTargetVisualState == DragTargetVisualState.Invalid)
                {
                    selection.color = selectionDragInvalidColor;
                }
                else if (skillEquipped)
                {
                    selection.color = selectionEquippedColor;
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

            if (selected)
            {
                background.color = selectedColor;
                return;
            }

            background.color = skillEquipped ? equippedColor : baseColor;
        }

        private void EnsureSkillBadgeVisual()
        {
            if (skillBadgeRoot == null)
            {
                var badgeRootObject = new GameObject("SkillEquipBadge", typeof(RectTransform));
                var badgeRect = badgeRootObject.GetComponent<RectTransform>();
                badgeRect.SetParent(transform, false);
                badgeRect.anchorMin = new Vector2(1f, 1f);
                badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.pivot = new Vector2(1f, 1f);
                badgeRect.anchoredPosition = new Vector2(-4f, -4f);
                badgeRect.sizeDelta = new Vector2(44f, 20f);
                skillBadgeRoot = badgeRect;
            }

            if (skillBadgeBackground == null && skillBadgeRoot != null)
            {
                var backgroundImage = skillBadgeRoot.GetComponent<Image>();
                if (backgroundImage == null)
                {
                    backgroundImage = skillBadgeRoot.gameObject.AddComponent<Image>();
                }

                backgroundImage.color = new Color(0.16f, 0.42f, 0.25f, 0.94f);
                backgroundImage.raycastTarget = false;
                skillBadgeBackground = backgroundImage;
            }

            if (skillBadgeText == null && skillBadgeRoot != null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(skillBadgeRoot, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                var label = labelObject.GetComponent<Text>();
                var badgeFont = ResolveBadgeFont();
                if (badgeFont != null)
                {
                    label.font = badgeFont;
                }
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = 11;
                label.fontStyle = FontStyle.Bold;
                label.color = Color.white;
                label.raycastTarget = false;
                skillBadgeText = label;
            }
        }

        private void RefreshSkillBadgeVisual()
        {
            if (skillBadgeRoot == null)
            {
                return;
            }

            var shouldShow = skillEquipped &&
                             equippedSkillSlotNumber > 0 &&
                             item != null &&
                             item.Definition != null;
            skillBadgeRoot.gameObject.SetActive(shouldShow);
            if (!shouldShow || skillBadgeText == null)
            {
                return;
            }

            skillBadgeText.text = $"槽 {equippedSkillSlotNumber}";
        }

        private Font ResolveBadgeFont()
        {
            if (cachedBadgeFont != null)
            {
                return cachedBadgeFont;
            }

            try
            {
                cachedBadgeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (ArgumentException)
            {
                // Fallback for older Unity versions.
            }

            if (cachedBadgeFont != null)
            {
                return cachedBadgeFont;
            }

            try
            {
                cachedBadgeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (ArgumentException)
            {
                // Newer Unity versions remove Arial built-in font.
            }

            if (cachedBadgeFont == null && stackText != null)
            {
                cachedBadgeFont = stackText.font;
            }

            return cachedBadgeFont;
        }
    }
}
