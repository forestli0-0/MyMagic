using System;
using System.Collections.Generic;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        public readonly struct SkillEquipState
        {
            public readonly bool IsEquipped;
            public readonly int SlotNumber;

            public SkillEquipState(bool isEquipped, int slotNumber)
            {
                IsEquipped = isEquipped && slotNumber > 0;
                SlotNumber = Mathf.Max(0, slotNumber);
            }

            public static SkillEquipState None => new SkillEquipState(false, 0);
            public static SkillEquipState EquippedInSlot(int slotNumber) => new SkillEquipState(true, slotNumber);
        }

        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private InventorySlotUI slotTemplate;
        [Header("Layout")]
        [SerializeField] private bool autoFitCellSize = true;
        [SerializeField] private float minCellSize = 88f;
        [SerializeField] private float maxCellSize = 130f;

        private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();
        private readonly List<int> customDisplayIndices = new List<int>(32);
        private InventoryComponent inventory;
        private bool useCustomDisplayIndices;
        private Func<ItemInstance, SkillEquipState> skillEquipStateResolver;

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
            RefreshGridCellSize();

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

        public void SetSkillEquipStateResolver(Func<ItemInstance, SkillEquipState> resolver)
        {
            skillEquipStateResolver = resolver;
            Refresh();
        }

        public void SetDragTargetState(int inventoryIndex, bool valid)
        {
            var slot = ResolveSlotByInventoryIndex(inventoryIndex);
            if (slot == null)
            {
                return;
            }

            slot.SetDragTargetState(valid
                ? InventorySlotUI.DragTargetVisualState.Valid
                : InventorySlotUI.DragTargetVisualState.Invalid);
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

        private void Unbind()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged()
        {
            RefreshGridCellSize();
            EnsureSlots();
            Refresh();
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshGridCellSize();
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
                if (slot == null)
                {
                    continue;
                }

                // Defensive reset: clear any residual scale feedback so all cells stay visually aligned.
                var slotRect = slot.transform as RectTransform;
                if (slotRect != null && slotRect.localScale != Vector3.one)
                {
                    slotRect.localScale = Vector3.one;
                }

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
                var skillEquipState = ResolveSkillEquipState(item);
                slot.SetSkillEquipState(skillEquipState.IsEquipped, skillEquipState.SlotNumber);
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

        private InventorySlotUI ResolveSlotByInventoryIndex(int inventoryIndex)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (slot.SlotIndex == inventoryIndex)
                {
                    return slot;
                }
            }

            return null;
        }

        private void RefreshGridCellSize()
        {
            if (!autoFitCellSize || slotsRoot == null)
            {
                return;
            }

            var grid = slotsRoot.GetComponent<GridLayoutGroup>();
            if (grid == null || grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount || grid.constraintCount <= 0)
            {
                return;
            }

            var width = slotsRoot.rect.width;
            if (width <= 1f)
            {
                return;
            }

            var columns = grid.constraintCount;
            var totalSpacing = grid.spacing.x * Mathf.Max(0, columns - 1);
            var totalPadding = grid.padding.left + grid.padding.right;
            var availableWidth = width - totalSpacing - totalPadding;
            if (availableWidth <= 1f)
            {
                return;
            }

            var target = availableWidth / columns;
            var min = Mathf.Max(40f, minCellSize);
            // Fixed-column layout should fill row width to avoid trailing right-side gaps.
            var size = Mathf.Max(min, target);
            // Keep maxCellSize as a soft cap: only apply when it does not reintroduce trailing gap.
            if (maxCellSize > min && target <= maxCellSize)
            {
                size = Mathf.Clamp(target, min, maxCellSize);
            }

            // Snap to integer pixel size to avoid subtle visual mismatch caused by sub-pixel layout.
            size = Mathf.Max(min, Mathf.Round(size));

            var cell = grid.cellSize;
            if (Mathf.Abs(cell.x - size) < 0.1f && Mathf.Abs(cell.y - size) < 0.1f)
            {
                return;
            }

            grid.cellSize = new Vector2(size, size);
        }

        private SkillEquipState ResolveSkillEquipState(ItemInstance item)
        {
            if (item == null || skillEquipStateResolver == null)
            {
                return SkillEquipState.None;
            }

            var state = skillEquipStateResolver.Invoke(item);
            if (!state.IsEquipped || state.SlotNumber <= 0)
            {
                return SkillEquipState.None;
            }

            return state;
        }
    }
}
