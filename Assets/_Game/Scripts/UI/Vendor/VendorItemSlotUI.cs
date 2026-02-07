using System;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class VendorItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image selection;
        [SerializeField] private Image background;
        [SerializeField] private Text priceText;
        [SerializeField] private Text stockText;

        private int slotIndex = -1;
        private VendorItemState itemState;
        private bool hasItem;
        private bool canBuy;
        private bool isSelected;

        public int SlotIndex => slotIndex;
        public VendorItemState ItemState => itemState;

        public event Action<VendorItemSlotUI> Clicked;
        public event Action<VendorItemSlotUI, PointerEventData> DragStarted;
        public event Action<VendorItemSlotUI, PointerEventData> Dragging;
        public event Action<VendorItemSlotUI, PointerEventData> DragEnded;
        public event Action<VendorItemSlotUI, PointerEventData> Dropped;

        private void Awake()
        {
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (selection != null)
            {
                // Older generated templates may leave this object inactive.
                if (!selection.gameObject.activeSelf)
                {
                    selection.gameObject.SetActive(true);
                }

                selection.enabled = false;
            }

            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }
            else if (VendorScreen.DebugEnabled)
            {
                Debug.LogWarning("[UI][Vendor] Button reference missing on VendorItemSlotUI.", this);
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

        public void SetItem(VendorItemState state)
        {
            itemState = state;

            hasItem = state != null && state.Definition != null;
            canBuy = hasItem && state.Definition.CanBuy && state.CanBuy(1);

            if (icon != null)
            {
                var sprite = hasItem ? state.Definition.Icon : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (button != null)
            {
                button.interactable = hasItem;
            }

            if (priceText != null)
            {
                priceText.text = hasItem ? $"G {state.Price}" : string.Empty;
            }

            if (stockText != null)
            {
                if (state == null)
                {
                    stockText.text = string.Empty;
                }
                else if (!state.Definition.CanBuy)
                {
                    stockText.text = "locked";
                }
                else if (state.IsSoldOut)
                {
                    stockText.text = "sold out";
                }
                else
                {
                    stockText.text = state.InfiniteStock ? "inf" : state.RemainingStock.ToString();
                }
            }

            RefreshVisualState();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            RefreshVisualState();
        }

        private void RefreshVisualState()
        {
            if (selection != null)
            {
                selection.enabled = isSelected;
                selection.color = isSelected ? new Color(1f, 0.95f, 0.3f, 0.35f) : new Color(1f, 1f, 1f, 0.15f);
            }

            if (background != null)
            {
                if (!hasItem)
                {
                    background.color = new Color(0f, 0f, 0f, 0.2f);
                }
                else if (isSelected)
                {
                    background.color = canBuy
                        ? new Color(0.08f, 0.32f, 0.62f, 0.8f)
                        : new Color(0.45f, 0.22f, 0.22f, 0.8f);
                }
                else
                {
                    background.color = canBuy
                        ? new Color(0f, 0f, 0f, 0.2f)
                        : new Color(0f, 0f, 0f, 0.45f);
                }
            }
        }

        private void HandleClicked()
        {
            if (VendorScreen.DebugEnabled)
            {
                Debug.Log($"[UI][Vendor] Vendor item clicked index={slotIndex}", this);
            }
            Clicked?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!hasItem)
            {
                return;
            }

            DragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!hasItem)
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
