using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 库存界面主屏幕，管理背包与装备的展示与交互。
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 整合背包网格、装备面板、物品对比面板
    /// - 处理物品选择、穿戴、卸下操作
    /// - 响应库存/装备变化事件刷新 UI
    /// 
    /// 使用方式：
    /// - 通过 I 键（InventoryHotkey）开关界面
    /// - 继承 UIScreenBase，与底部常驻技能栏协同显示
    /// </remarks>
    public partial class InventoryScreen : UIScreenBase
    {
        private const int InvalidSelectionIndex = -1;
        private const float NoHoverClearPending = -1f;
        private const float InvalidClickTimestamp = -999f;
        private const float DefaultEquipmentSectionHeight = 390f;
        private const int LayoutLookupDepth = 8;

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private EquipmentComponent equipment;
        [SerializeField] private SkillBarUI skillBar;
        [SerializeField] private StatsComponent playerStats;

        [Header("Widgets")]
        [SerializeField] private InventoryGridUI inventoryGrid;
        [SerializeField] private EquipmentPanelUI equipmentPanel;
        [SerializeField] private ItemComparePanelUI comparePanel;
        [SerializeField] private Button allFilterButton;
        [SerializeField] private Button equipmentFilterButton;
        [SerializeField] private Button consumableFilterButton;
        [SerializeField] private Button skillFilterButton;
        [SerializeField] private Button questFilterButton;
        [SerializeField] private InputField searchInputField;
        [SerializeField] private Dropdown sortDropdown;
        [SerializeField] private Button rarityAllButton;
        [SerializeField] private Button rarityCommonButton;
        [SerializeField] private Button rarityMagicButton;
        [SerializeField] private Button rarityRareButton;
        [SerializeField] private Button rarityEpicButton;
        [SerializeField] private Button rarityLegendaryButton;
        [SerializeField] private Text capacityText;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unequipButton;
        [SerializeField] private Text actionHintText;
        [SerializeField] private Color filterActiveColor = new Color(0.26f, 0.38f, 0.56f, 1f);
        [SerializeField] private Color filterInactiveColor = new Color(0.2f, 0.22f, 0.26f, 1f);
        [SerializeField] private Color filterActiveTextColor = new Color(0.97f, 0.98f, 1f, 1f);
        [SerializeField] private Color filterInactiveTextColor = new Color(0.85f, 0.87f, 0.9f, 1f);
        [Header("Quick Action")]
        [SerializeField, Range(0.12f, 0.5f)] private float equipDoubleClickWindow = 0.28f;
        [Header("Hover")]
        [SerializeField, Range(0f, 0.25f)] private float equipmentHoverClearDelay = 0.08f;

        [Header("Action Profiles")]
        [SerializeField] private List<InventoryActionProfile> actionProfiles = new List<InventoryActionProfile>(8);
        [SerializeField] private List<InventoryCategoryActionOverride> categoryActionOverrides = new List<InventoryCategoryActionOverride>(4);

        private int selectedInventoryIndex = InvalidSelectionIndex;
        private int selectedEquipmentIndex = InvalidSelectionIndex;
        private InventoryFilter activeFilter = InventoryFilter.All;
        private InventorySortMode activeSortMode = InventorySortMode.Default;
        private RarityQuickFilter activeRarityFilter = RarityQuickFilter.All;
        private string searchKeyword = string.Empty;
        private readonly List<int> filteredDisplayIndices = new List<int>(32);
        private readonly List<int> filteredItemIndices = new List<int>(32);
        private readonly List<RaycastResult> dropRaycastBuffer = new List<RaycastResult>(24);
        private bool subscribed;
        private bool sortDropdownBindingLogged;
        private Button sortPickerButton;
        private RectTransform sortPickerPanel;
        private Canvas sortPickerCanvas;
        private Font sortPickerFont;
        private readonly List<Button> sortPickerOptionButtons = new List<Button>(8);
        private readonly List<InventorySortMode> sortPickerOptionModes = new List<InventorySortMode>(8);
        private DragPayload dragPayload;
        private bool dragActive;
        private Image dragIcon;
        private Canvas dragCanvas;
        private InventorySlotUI currentDragInventoryTarget;
        private EquipmentSlotUI currentDragEquipmentTarget;
        private int hoveredEquipmentIndex = InvalidSelectionIndex;
        private float hoveredEquipmentClearAt = NoHoverClearPending;
        private InventoryActionCommand currentPrimaryAction = InventoryActionCommand.None;
        private InventoryActionCommand currentSecondaryAction = InventoryActionCommand.None;
        private int lastInventoryClickIndex = InvalidSelectionIndex;
        private float lastInventoryClickAt = InvalidClickTimestamp;

        private enum InventoryFilter
        {
            All = 0,
            Equipment = 1,
            Consumable = 2,
            Skill = 3,
            Quest = 4
        }

        private enum InventorySortMode
        {
            Default = 0,
            NameAscending = 1,
            RarityDescending = 2,
            PriceDescending = 3,
            Category = 4
        }

        private enum RarityQuickFilter
        {
            All = 0,
            Common = 1,
            Magic = 2,
            Rare = 3,
            Epic = 4,
            Legendary = 5
        }

        private enum InventoryActionCommand
        {
            None = 0,
            EquipSelected = 1,
            UnequipSelected = 2,
            SplitSelected = 3,
            DropInventoryOne = 4,
            DropEquipment = 5,
            UseSelectedConsumable = 6,
            OpenQuestJournal = 7
        }

        private enum InventoryActionProfileId
        {
            None = 0,
            InventoryEquippable = 1,
            InventorySplitStack = 2,
            InventoryStack = 3,
            InventoryGeneric = 4,
            EquipmentSelected = 5,
            InventoryConsumable = 6,
            InventoryQuestItem = 7
        }

        [Serializable]
        private class InventoryActionProfile
        {
            [SerializeField] private InventoryActionProfileId profileId = InventoryActionProfileId.None;
            [SerializeField] private string primaryLabel = "装备";
            [SerializeField] private InventoryActionCommand primaryCommand = InventoryActionCommand.None;
            [SerializeField] private string secondaryLabel = "卸下";
            [SerializeField] private InventoryActionCommand secondaryCommand = InventoryActionCommand.None;
            [SerializeField] private string hint = "选择背包或装备中的物品";

            public InventoryActionProfileId ProfileId => profileId;
            public string PrimaryLabel => string.IsNullOrWhiteSpace(primaryLabel) ? "操作1" : primaryLabel;
            public InventoryActionCommand PrimaryCommand => primaryCommand;
            public string SecondaryLabel => string.IsNullOrWhiteSpace(secondaryLabel) ? "操作2" : secondaryLabel;
            public InventoryActionCommand SecondaryCommand => secondaryCommand;
            public string Hint => hint ?? string.Empty;

            public InventoryActionProfile()
            {
            }

            public InventoryActionProfile(
                InventoryActionProfileId profileId,
                string primaryLabel,
                InventoryActionCommand primaryCommand,
                string secondaryLabel,
                InventoryActionCommand secondaryCommand,
                string hint)
            {
                this.profileId = profileId;
                this.primaryLabel = primaryLabel;
                this.primaryCommand = primaryCommand;
                this.secondaryLabel = secondaryLabel;
                this.secondaryCommand = secondaryCommand;
                this.hint = hint;
            }
        }

        [Serializable]
        private class InventoryCategoryActionOverride
        {
            [SerializeField] private ItemCategory category = ItemCategory.General;
            [SerializeField] private InventoryActionProfileId profileId = InventoryActionProfileId.InventoryGeneric;

            public ItemCategory Category => category;
            public InventoryActionProfileId ProfileId => profileId;

            public InventoryCategoryActionOverride()
            {
            }

            public InventoryCategoryActionOverride(ItemCategory category, InventoryActionProfileId profileId)
            {
                this.category = category;
                this.profileId = profileId;
            }
        }

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        public override void OnEnter()
        {
            ApplyThemeColors();
            EnsureReferences();
            EnsureStableEquipmentPanelHeight();
            if (uiManager != null)
            {
                uiManager.SetHudSkillBarOnlyVisible(true);
            }

            EnsureActionProfilesConfigured();
            Subscribe();
            RefreshAll();
        }

        public override void OnExit()
        {
            Unsubscribe();
            HideSortPicker();
            EndDrag();
            ResetDoubleClickState();
            ClearHoveredEquipmentState();
        }

        private void OnDestroy()
        {
            UIThemeRuntime.ThemeChanged -= HandleThemeChanged;
            if (dragIcon != null)
            {
                Destroy(dragIcon.gameObject);
                dragIcon = null;
                dragCanvas = null;
            }
        }

        public override void OnFocus()
        {
            EnsureStableEquipmentPanelHeight();
            RefreshAll();
        }

        private void OnEnable()
        {
            UIThemeRuntime.ThemeChanged += HandleThemeChanged;
            ApplyThemeColors();
            EnsureActionProfilesConfigured();
        }

        private void OnDisable()
        {
            UIThemeRuntime.ThemeChanged -= HandleThemeChanged;
        }

        private void Update()
        {
            TickHoveredEquipmentClear();

            if (UnityEngine.Input.GetKeyDown(KeyCode.R) && !IsTextInputFocused())
            {
                HandleOrganizeClicked();
            }

            if (sortPickerPanel == null || !sortPickerPanel.gameObject.activeSelf)
            {
                return;
            }

            if (!UnityEngine.Input.GetMouseButtonDown(0))
            {
                return;
            }

            var panelContains = RectTransformUtility.RectangleContainsScreenPoint(
                sortPickerPanel,
                UnityEngine.Input.mousePosition,
                sortPickerCanvas != null ? sortPickerCanvas.worldCamera : null);

            var dropdownRect = sortDropdown != null ? sortDropdown.transform as RectTransform : null;
            var dropdownContains = dropdownRect != null && RectTransformUtility.RectangleContainsScreenPoint(
                dropdownRect,
                UnityEngine.Input.mousePosition,
                sortPickerCanvas != null ? sortPickerCanvas.worldCamera : null);

            if (!panelContains && !dropdownContains)
            {
                HideSortPicker();
            }
        }

        public void EquipSelected()
        {
            if (inventory == null || equipment == null)
            {
                UIToast.Warning("无法装备：背包或装备组件未就绪。");
                return;
            }

            if (selectedInventoryIndex < 0 || selectedInventoryIndex >= inventory.Items.Count)
            {
                UIToast.Warning("请先选择可装备物品。");
                return;
            }

            var item = inventory.Items[selectedInventoryIndex];
            if (item == null || item.Definition == null)
            {
                UIToast.Warning("请选择可装备物品。");
                return;
            }

            if (equipment.TryEquip(item, inventory))
            {
                ClearInventorySelection();
                RefreshSelectionAfterChange();
                UIToast.Success($"已装备：{ResolveItemName(item)}");
                return;
            }

            UIToast.Warning("装备失败。");
        }

        public void UnequipSelected()
        {
            if (inventory == null || equipment == null)
            {
                UIToast.Warning("无法卸下：背包或装备组件未就绪。");
                return;
            }

            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= equipment.Slots.Count)
            {
                UIToast.Warning("请先选择装备槽位。");
                return;
            }

            var slotItem = equipment.Slots[selectedEquipmentIndex] != null
                ? equipment.Slots[selectedEquipmentIndex].Item
                : null;
            if (equipment.TryUnequip(selectedEquipmentIndex, inventory))
            {
                ClearEquipmentSelection();
                RefreshSelectionAfterChange();
                UIToast.Success(slotItem != null
                    ? $"已卸下：{ResolveItemName(slotItem)}"
                    : "已卸下装备。");
                return;
            }

            UIToast.Warning("卸下失败。");
        }

        public void ShowAllItems()
        {
            SetFilter(InventoryFilter.All);
        }

        public void ShowEquipmentItems()
        {
            SetFilter(InventoryFilter.Equipment);
        }

        public void ShowConsumableItems()
        {
            SetFilter(InventoryFilter.Consumable);
        }

        public void ShowQuestItems()
        {
            SetFilter(InventoryFilter.Quest);
        }

        public void ShowSkillItems()
        {
            SetFilter(InventoryFilter.Skill);
        }

        public void ShowAllRarities()
        {
            SetRarityFilter(RarityQuickFilter.All);
        }

        public void ShowCommonRarities()
        {
            SetRarityFilter(RarityQuickFilter.Common);
        }

        public void ShowMagicRarities()
        {
            SetRarityFilter(RarityQuickFilter.Magic);
        }

        public void ShowRareRarities()
        {
            SetRarityFilter(RarityQuickFilter.Rare);
        }

        public void ShowEpicRarities()
        {
            SetRarityFilter(RarityQuickFilter.Epic);
        }

        public void ShowLegendaryRarities()
        {
            SetRarityFilter(RarityQuickFilter.Legendary);
        }

        private void EnsureReferences()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (inventory == null)
            {
                inventory = FindFirstObjectByType<InventoryComponent>();
            }

            if (equipment == null)
            {
                equipment = FindFirstObjectByType<EquipmentComponent>();
            }

            if (playerStats == null && inventory != null)
            {
                playerStats = inventory.GetComponent<StatsComponent>();
            }

            if (playerStats == null && equipment != null)
            {
                playerStats = equipment.GetComponent<StatsComponent>();
            }

            if (playerStats == null)
            {
                playerStats = FindFirstObjectByType<StatsComponent>();
            }

            EnsureSkillBarReference();

            if (comparePanel != null)
            {
                comparePanel.SetCurrentStatValueResolver(ResolveCurrentStatValueForPreview);
            }

            EnsureSkillFilterButton();
        }

        private void EnsureStableEquipmentPanelHeight()
        {
            if (equipmentPanel == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            var equipmentSection = ResolveEquipmentSectionTransform();
            if (equipmentSection == null)
            {
                return;
            }

            LockLayoutElementHeight(equipmentSection, DefaultEquipmentSectionHeight);

            var detailsSection = ResolveDetailsSectionTransform(equipmentSection);
            if (detailsSection != null)
            {
                ResetDetailsSectionLayout(detailsSection);
            }
        }

        private Transform ResolveEquipmentSectionTransform()
        {
            var anchor = equipmentPanel != null ? equipmentPanel.transform : null;
            for (int i = 0; i < LayoutLookupDepth && anchor != null; i++)
            {
                if (string.Equals(anchor.name, "EquipmentSection", StringComparison.Ordinal))
                {
                    return anchor;
                }

                anchor = anchor.parent;
            }

            anchor = equipmentPanel != null ? equipmentPanel.transform : null;
            for (int i = 0; i < LayoutLookupDepth && anchor != null; i++)
            {
                if (anchor.GetComponent<LayoutElement>() != null)
                {
                    return anchor;
                }

                anchor = anchor.parent;
            }

            return null;
        }

        private static Transform ResolveDetailsSectionTransform(Transform equipmentSection)
        {
            if (equipmentSection == null || equipmentSection.parent == null)
            {
                return null;
            }

            var sidePanel = equipmentSection.parent;
            for (int i = 0; i < sidePanel.childCount; i++)
            {
                var child = sidePanel.GetChild(i);
                if (child == null || child == equipmentSection)
                {
                    continue;
                }

                if (string.Equals(child.name, "DetailsSection", StringComparison.Ordinal))
                {
                    return child;
                }
            }

            for (int i = 0; i < sidePanel.childCount; i++)
            {
                var child = sidePanel.GetChild(i);
                if (child == null || child == equipmentSection)
                {
                    continue;
                }

                if (child.GetComponentInChildren<ItemComparePanelUI>(true) != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static void LockLayoutElementHeight(Transform target, float fallbackHeight)
        {
            if (target == null)
            {
                return;
            }

            var layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = target.gameObject.AddComponent<LayoutElement>();
            }

            var preferred = layout.preferredHeight;
            if (preferred <= 1f && target is RectTransform targetRect)
            {
                preferred = targetRect.rect.height;
            }

            if (preferred <= 1f)
            {
                preferred = fallbackHeight;
            }

            layout.preferredHeight = preferred;
            layout.minHeight = Mathf.Max(layout.minHeight, preferred);
            layout.flexibleHeight = 0f;
        }

        private static void ResetDetailsSectionLayout(Transform target)
        {
            if (target == null)
            {
                return;
            }

            var layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = target.gameObject.AddComponent<LayoutElement>();
            }

            // 详情区保持弹性填充，避免固定高度把整页布局顶坏。
            layout.preferredHeight = -1f;
            layout.minHeight = 0f;
            layout.flexibleHeight = 1f;
        }

        private float ResolveCurrentStatValueForPreview(string statId)
        {
            if (string.IsNullOrWhiteSpace(statId) || playerStats == null)
            {
                return float.NaN;
            }

            return playerStats.GetValueById(statId, float.NaN);
        }

        private void EnsureSkillFilterButton()
        {
            if (skillFilterButton != null)
            {
                return;
            }

            if (allFilterButton != null)
            {
                skillFilterButton = FindSiblingButtonByName(allFilterButton.transform.parent, "FilterSkillButton");
                if (skillFilterButton == null)
                {
                    skillFilterButton = FindSiblingButtonByName(allFilterButton.transform.parent, "Button_技能");
                }
            }

            if (skillFilterButton != null)
            {
                return;
            }

            if (questFilterButton == null || questFilterButton.transform.parent == null)
            {
                return;
            }

            // 兼容旧场景：运行时补齐“技能”筛选按钮，避免必须重建整个 UI。
            var clone = Instantiate(questFilterButton.gameObject, questFilterButton.transform.parent, false);
            clone.name = "FilterSkillButton";

            var questIndex = questFilterButton.transform.GetSiblingIndex();
            clone.transform.SetSiblingIndex(Mathf.Max(0, questIndex));

            var cloneText = clone.GetComponentInChildren<Text>(true);
            if (cloneText != null)
            {
                cloneText.text = "技能";
            }

            skillFilterButton = clone.GetComponent<Button>();
        }

        private static Button FindSiblingButtonByName(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null || !string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var button = child.GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }
            }

            return null;
        }

        private void EnsureActionProfilesConfigured()
        {
            if (actionProfiles == null)
            {
                actionProfiles = new List<InventoryActionProfile>(8);
            }

            EnsureActionProfile(
                InventoryActionProfileId.None,
                "装备", InventoryActionCommand.None,
                "卸下", InventoryActionCommand.None,
                "选择背包或装备中的物品");
            EnsureActionProfile(
                InventoryActionProfileId.InventoryEquippable,
                "装备", InventoryActionCommand.EquipSelected,
                "丢弃", InventoryActionCommand.DropInventoryOne,
                "主操作: 装备该物品    次操作: 丢弃该物品");
            EnsureActionProfile(
                InventoryActionProfileId.InventorySplitStack,
                "拆分", InventoryActionCommand.SplitSelected,
                "丢弃", InventoryActionCommand.DropInventoryOne,
                "主操作: 平分堆叠    次操作: 丢弃 1 个");
            EnsureActionProfile(
                InventoryActionProfileId.InventoryStack,
                "丢弃", InventoryActionCommand.DropInventoryOne,
                "拆分", InventoryActionCommand.SplitSelected,
                "主操作: 丢弃 1 个");
            EnsureActionProfile(
                InventoryActionProfileId.InventoryGeneric,
                "丢弃", InventoryActionCommand.DropInventoryOne,
                "拆分", InventoryActionCommand.None,
                "主操作: 丢弃该物品");
            EnsureActionProfile(
                InventoryActionProfileId.InventoryConsumable,
                "使用", InventoryActionCommand.UseSelectedConsumable,
                "丢弃", InventoryActionCommand.DropInventoryOne,
                "主操作: 使用 1 个    次操作: 丢弃 1 个");
            EnsureActionProfile(
                InventoryActionProfileId.InventoryQuestItem,
                "任务", InventoryActionCommand.OpenQuestJournal,
                "丢弃", InventoryActionCommand.None,
                "主操作: 打开任务日志");
            EnsureActionProfile(
                InventoryActionProfileId.EquipmentSelected,
                "卸下", InventoryActionCommand.UnequipSelected,
                "丢弃", InventoryActionCommand.DropEquipment,
                "主操作: 卸下到背包    次操作: 直接丢弃装备");

            if (categoryActionOverrides == null)
            {
                categoryActionOverrides = new List<InventoryCategoryActionOverride>(4);
            }

            EnsureCategoryOverride(ItemCategory.Consumable, InventoryActionProfileId.InventoryConsumable);
            EnsureCategoryOverride(ItemCategory.Quest, InventoryActionProfileId.InventoryQuestItem);
        }

        private void EnsureActionProfile(
            InventoryActionProfileId profileId,
            string primaryLabel,
            InventoryActionCommand primaryCommand,
            string secondaryLabel,
            InventoryActionCommand secondaryCommand,
            string hint)
        {
            if (TryGetActionProfile(profileId, out var existing) && existing != null)
            {
                return;
            }

            actionProfiles.Add(new InventoryActionProfile(
                profileId,
                primaryLabel,
                primaryCommand,
                secondaryLabel,
                secondaryCommand,
                hint));
        }

        private void EnsureCategoryOverride(ItemCategory category, InventoryActionProfileId profileId)
        {
            if (categoryActionOverrides == null)
            {
                return;
            }

            for (int i = 0; i < categoryActionOverrides.Count; i++)
            {
                var entry = categoryActionOverrides[i];
                if (entry == null || entry.Category != category)
                {
                    continue;
                }

                return;
            }

            categoryActionOverrides.Add(new InventoryCategoryActionOverride(category, profileId));
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (inventory != null)
            {
                inventory.InventoryChanged += HandleInventoryChanged;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged += HandleEquipmentChanged;
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.SlotSelected += HandleInventorySlotSelected;
                inventoryGrid.SlotDragStarted += HandleInventoryDragStarted;
                inventoryGrid.SlotDragging += HandleInventoryDragging;
                inventoryGrid.SlotDragEnded += HandleInventoryDragEnded;
                inventoryGrid.SlotDropped += HandleInventorySlotDropped;
                inventoryGrid.SetSkillEquipStateResolver(ResolveSkillEquipStateForItem);
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.SlotSelected += HandleEquipmentSlotSelected;
                equipmentPanel.SlotHoverChanged += HandleEquipmentSlotHoverChanged;
                equipmentPanel.SlotDragStarted += HandleEquipmentDragStarted;
                equipmentPanel.SlotDragging += HandleEquipmentDragging;
                equipmentPanel.SlotDragEnded += HandleEquipmentDragEnded;
                equipmentPanel.SlotDropped += HandleEquipmentSlotDropped;
            }

            if (equipButton != null)
            {
                equipButton.onClick.AddListener(HandlePrimaryAction);
            }

            if (unequipButton != null)
            {
                unequipButton.onClick.AddListener(HandleSecondaryAction);
            }

            if (allFilterButton != null)
            {
                allFilterButton.onClick.AddListener(ShowAllItems);
            }

            if (equipmentFilterButton != null)
            {
                equipmentFilterButton.onClick.AddListener(ShowEquipmentItems);
            }

            if (consumableFilterButton != null)
            {
                consumableFilterButton.onClick.AddListener(ShowConsumableItems);
            }

            if (questFilterButton != null)
            {
                questFilterButton.onClick.AddListener(ShowQuestItems);
            }

            if (skillFilterButton != null)
            {
                skillFilterButton.onClick.AddListener(ShowSkillItems);
            }

            if (searchInputField != null)
            {
                searchInputField.SetTextWithoutNotify(searchKeyword);
                searchInputField.onValueChanged.AddListener(HandleSearchKeywordChanged);
            }

            if (sortDropdown != null)
            {
                EnsureSortDropdownOptions();
                SetupSortPicker();
            }

            if (rarityAllButton != null)
            {
                rarityAllButton.onClick.AddListener(ShowAllRarities);
            }

            if (rarityCommonButton != null)
            {
                rarityCommonButton.onClick.AddListener(ShowCommonRarities);
            }

            if (rarityMagicButton != null)
            {
                rarityMagicButton.onClick.AddListener(ShowMagicRarities);
            }

            if (rarityRareButton != null)
            {
                rarityRareButton.onClick.AddListener(ShowRareRarities);
            }

            if (rarityEpicButton != null)
            {
                rarityEpicButton.onClick.AddListener(ShowEpicRarities);
            }

            if (rarityLegendaryButton != null)
            {
                rarityLegendaryButton.onClick.AddListener(ShowLegendaryRarities);
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged -= HandleEquipmentChanged;
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.SlotSelected -= HandleInventorySlotSelected;
                inventoryGrid.SlotDragStarted -= HandleInventoryDragStarted;
                inventoryGrid.SlotDragging -= HandleInventoryDragging;
                inventoryGrid.SlotDragEnded -= HandleInventoryDragEnded;
                inventoryGrid.SlotDropped -= HandleInventorySlotDropped;
                inventoryGrid.SetSkillEquipStateResolver(null);
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.SlotSelected -= HandleEquipmentSlotSelected;
                equipmentPanel.SlotHoverChanged -= HandleEquipmentSlotHoverChanged;
                equipmentPanel.SlotDragStarted -= HandleEquipmentDragStarted;
                equipmentPanel.SlotDragging -= HandleEquipmentDragging;
                equipmentPanel.SlotDragEnded -= HandleEquipmentDragEnded;
                equipmentPanel.SlotDropped -= HandleEquipmentSlotDropped;
            }

            if (equipButton != null)
            {
                equipButton.onClick.RemoveListener(HandlePrimaryAction);
            }

            if (unequipButton != null)
            {
                unequipButton.onClick.RemoveListener(HandleSecondaryAction);
            }

            if (allFilterButton != null)
            {
                allFilterButton.onClick.RemoveListener(ShowAllItems);
            }

            if (equipmentFilterButton != null)
            {
                equipmentFilterButton.onClick.RemoveListener(ShowEquipmentItems);
            }

            if (consumableFilterButton != null)
            {
                consumableFilterButton.onClick.RemoveListener(ShowConsumableItems);
            }

            if (questFilterButton != null)
            {
                questFilterButton.onClick.RemoveListener(ShowQuestItems);
            }

            if (skillFilterButton != null)
            {
                skillFilterButton.onClick.RemoveListener(ShowSkillItems);
            }

            if (searchInputField != null)
            {
                searchInputField.onValueChanged.RemoveListener(HandleSearchKeywordChanged);
            }

            if (sortDropdown != null)
            {
                if (sortPickerButton != null)
                {
                    sortPickerButton.onClick.RemoveListener(ToggleSortPicker);
                }
            }

            if (rarityAllButton != null)
            {
                rarityAllButton.onClick.RemoveListener(ShowAllRarities);
            }

            if (rarityCommonButton != null)
            {
                rarityCommonButton.onClick.RemoveListener(ShowCommonRarities);
            }

            if (rarityMagicButton != null)
            {
                rarityMagicButton.onClick.RemoveListener(ShowMagicRarities);
            }

            if (rarityRareButton != null)
            {
                rarityRareButton.onClick.RemoveListener(ShowRareRarities);
            }

            if (rarityEpicButton != null)
            {
                rarityEpicButton.onClick.RemoveListener(ShowEpicRarities);
            }

            if (rarityLegendaryButton != null)
            {
                rarityLegendaryButton.onClick.RemoveListener(ShowLegendaryRarities);
            }

            subscribed = false;
        }

        private void RefreshAll()
        {
            RefreshInventoryGrid();

            if (equipmentPanel != null)
            {
                equipmentPanel.Bind(equipment);
            }

            RefreshSelectionAfterChange();
            RefreshFilterButtonStates();
        }

        private void HandleInventoryChanged()
        {
            RefreshInventoryGrid();
            RefreshSelectionAfterChange();
        }

        private void HandleEquipmentChanged()
        {
            RefreshSelectionAfterChange();
        }

        private void RefreshInventoryGrid()
        {
            RefreshCapacityText();

            if (inventoryGrid == null)
            {
                return;
            }

            BuildFilteredDisplayIndices(filteredDisplayIndices);
            inventoryGrid.Bind(inventory, filteredDisplayIndices);
        }

        private void RefreshCapacityText()
        {
            if (capacityText == null)
            {
                return;
            }

            if (inventory == null)
            {
                capacityText.text = "容量 --/--";
                capacityText.color = new Color(0.8f, 0.82f, 0.86f, 1f);
                return;
            }

            var used = 0;
            var items = inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    used++;
                }
            }

            capacityText.text = $"容量 {used}/{inventory.Capacity}";
            capacityText.color = used >= inventory.Capacity
                ? new Color(0.95f, 0.58f, 0.52f, 1f)
                : new Color(0.8f, 0.88f, 0.98f, 1f);
        }

        private void HandleInventorySlotSelected(int index)
        {
            var now = Time.unscaledTime;
            var isDoubleClick = index == lastInventoryClickIndex && now - lastInventoryClickAt <= equipDoubleClickWindow;
            lastInventoryClickIndex = index;
            lastInventoryClickAt = now;

            selectedInventoryIndex = index;
            ClearEquipmentSelection();
            ClearHoveredEquipmentState();

            if (equipmentPanel != null)
            {
                equipmentPanel.SetSelectedIndex(-1);
            }

            RefreshSelectionAfterChange();

            if (isDoubleClick)
            {
                TryExecuteInventoryDoubleClickAction();
            }
        }

        private void HandleEquipmentSlotSelected(int index)
        {
            selectedEquipmentIndex = index;
            ClearInventorySelection();
            ClearHoveredEquipmentState();
            ResetDoubleClickState();

            if (inventoryGrid != null)
            {
                inventoryGrid.SetSelectedIndex(-1);
            }

            RefreshSelectionAfterChange();
        }

        private void HandleEquipmentSlotHoverChanged(int index, bool hovered)
        {
            if (hovered)
            {
                hoveredEquipmentIndex = index;
                hoveredEquipmentClearAt = NoHoverClearPending;
            }
            else if (hoveredEquipmentIndex == index)
            {
                if (IsPointerInEquipmentPanel())
                {
                    return;
                }

                hoveredEquipmentClearAt = Time.unscaledTime + equipmentHoverClearDelay;
            }

            if (HasExplicitSelection())
            {
                return;
            }

            RefreshSelectionAfterChange();
        }

        private void TickHoveredEquipmentClear()
        {
            if (hoveredEquipmentClearAt < 0f)
            {
                return;
            }

            if (IsPointerInEquipmentPanel())
            {
                hoveredEquipmentClearAt = Time.unscaledTime + equipmentHoverClearDelay;
                return;
            }

            if (Time.unscaledTime < hoveredEquipmentClearAt)
            {
                return;
            }

            hoveredEquipmentClearAt = NoHoverClearPending;
            if (hoveredEquipmentIndex < 0)
            {
                return;
            }

            hoveredEquipmentIndex = InvalidSelectionIndex;
            if (HasExplicitSelection())
            {
                return;
            }

            RefreshSelectionAfterChange();
        }

        private bool IsPointerInEquipmentPanel()
        {
            return equipmentPanel != null && equipmentPanel.ContainsScreenPoint(UnityEngine.Input.mousePosition);
        }

        private void RefreshSelectionAfterChange()
        {
            var inventoryItem = ResolveInventorySelection();
            var equipmentItem = ResolveEquipmentSelection();
            var hoveredEquipmentItem = ResolveHoveredEquipmentItem();

            if (inventoryGrid != null)
            {
                inventoryGrid.SetSelectedIndex(selectedInventoryIndex);
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.SetSelectedIndex(selectedEquipmentIndex);
            }

            if (comparePanel != null)
            {
                if (inventoryItem != null)
                {
                    comparePanel.ShowItem(
                        inventoryItem,
                        FindEquippedForSlot(inventoryItem),
                        BuildSkillEquipStatusLine(inventoryItem));
                }
                else if (equipmentItem != null)
                {
                    comparePanel.ShowItem(equipmentItem, null);
                }
                else if (hoveredEquipmentItem != null)
                {
                    comparePanel.ShowItem(hoveredEquipmentItem, null);
                }
                else
                {
                    comparePanel.ShowItem(null, null);
                }
            }

            UpdateButtons(inventoryItem, equipmentItem);
        }

        private ItemInstance ResolveInventorySelection()
        {
            if (inventory == null)
            {
                ClearInventorySelection();
                return null;
            }

            if (selectedInventoryIndex < 0 || selectedInventoryIndex >= inventory.Items.Count)
            {
                ClearInventorySelection();
                return null;
            }

            var selected = inventory.Items[selectedInventoryIndex];
            if (selected != null && !MatchesCurrentQuery(selected))
            {
                ClearInventorySelection();
                return null;
            }

            return selected;
        }

        private ItemInstance ResolveEquipmentSelection()
        {
            if (equipment == null)
            {
                ClearEquipmentSelection();
                return null;
            }

            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= equipment.Slots.Count)
            {
                ClearEquipmentSelection();
                return null;
            }

            return equipment.Slots[selectedEquipmentIndex].Item;
        }

        private ItemInstance ResolveHoveredEquipmentItem()
        {
            if (equipment == null)
            {
                hoveredEquipmentIndex = InvalidSelectionIndex;
                return null;
            }

            if (hoveredEquipmentIndex < 0 || hoveredEquipmentIndex >= equipment.Slots.Count)
            {
                hoveredEquipmentIndex = InvalidSelectionIndex;
                return null;
            }

            return equipment.Slots[hoveredEquipmentIndex].Item;
        }

        private void UpdateButtons(ItemInstance inventoryItem, ItemInstance equipmentItem)
        {
            EnsureActionProfilesConfigured();
            var profile = ResolveActionProfile(inventoryItem, equipmentItem);
            if (profile == null)
            {
                profile = CreateFallbackNoneProfile();
            }

            currentPrimaryAction = profile.PrimaryCommand;
            currentSecondaryAction = profile.SecondaryCommand;

            var primaryEnabled = IsActionCommandAvailable(currentPrimaryAction, inventoryItem, equipmentItem);
            var secondaryEnabled = IsActionCommandAvailable(currentSecondaryAction, inventoryItem, equipmentItem);

            ApplyActionState(equipButton, profile.PrimaryLabel, primaryEnabled);
            ApplyActionState(unequipButton, profile.SecondaryLabel, secondaryEnabled);
            SetActionHint(string.IsNullOrWhiteSpace(profile.Hint)
                ? "选择背包或装备中的物品"
                : profile.Hint);
        }

        private void HandlePrimaryAction()
        {
            ExecuteActionCommand(currentPrimaryAction);
        }

        private void HandleSecondaryAction()
        {
            ExecuteActionCommand(currentSecondaryAction);
        }

        private bool ExecuteActionCommand(InventoryActionCommand command)
        {
            var inventoryItem = ResolveInventorySelection();
            var equipmentItem = ResolveEquipmentSelection();
            if (!IsActionCommandAvailable(command, inventoryItem, equipmentItem))
            {
                return false;
            }

            switch (command)
            {
                case InventoryActionCommand.EquipSelected:
                    EquipSelected();
                    return true;
                case InventoryActionCommand.UnequipSelected:
                    UnequipSelected();
                    return true;
                case InventoryActionCommand.SplitSelected:
                    return SplitSelected();
                case InventoryActionCommand.DropInventoryOne:
                    return DropSelectedInventory(1);
                case InventoryActionCommand.DropEquipment:
                    return DropSelectedEquipment();
                case InventoryActionCommand.UseSelectedConsumable:
                    return UseSelectedConsumable();
                case InventoryActionCommand.OpenQuestJournal:
                    return OpenQuestJournalFromInventory();
                case InventoryActionCommand.None:
                default:
                    return false;
            }
        }

        private bool IsActionCommandAvailable(InventoryActionCommand command, ItemInstance inventoryItem, ItemInstance equipmentItem)
        {
            switch (command)
            {
                case InventoryActionCommand.EquipSelected:
                    return inventoryItem != null && inventoryItem.Definition != null && inventoryItem.Definition.IsEquippable;
                case InventoryActionCommand.UnequipSelected:
                    return equipmentItem != null;
                case InventoryActionCommand.SplitSelected:
                    return inventoryItem != null && CanSplit(inventoryItem) && HasEmptyInventorySlot();
                case InventoryActionCommand.DropInventoryOne:
                    return inventoryItem != null;
                case InventoryActionCommand.DropEquipment:
                    return equipmentItem != null;
                case InventoryActionCommand.UseSelectedConsumable:
                    return inventoryItem != null &&
                           inventoryItem.Definition != null &&
                           inventoryItem.Definition.Category == ItemCategory.Consumable;
                case InventoryActionCommand.OpenQuestJournal:
                    return inventoryItem != null &&
                           inventoryItem.Definition != null &&
                           inventoryItem.Definition.Category == ItemCategory.Quest;
                case InventoryActionCommand.None:
                default:
                    return false;
            }
        }

        private InventoryActionProfile ResolveActionProfile(ItemInstance inventoryItem, ItemInstance equipmentItem)
        {
            var profileId = ResolveActionProfileId(inventoryItem, equipmentItem);
            if (TryGetActionProfile(profileId, out var resolved) && resolved != null)
            {
                return resolved;
            }

            if (TryGetActionProfile(InventoryActionProfileId.None, out var fallback) && fallback != null)
            {
                return fallback;
            }

            return null;
        }

        private InventoryActionProfileId ResolveActionProfileId(ItemInstance inventoryItem, ItemInstance equipmentItem)
        {
            if (inventoryItem != null && inventoryItem.Definition != null)
            {
                if (inventoryItem.Definition.IsEquippable)
                {
                    return InventoryActionProfileId.InventoryEquippable;
                }

                if (TryGetCategoryProfileId(inventoryItem.Definition.Category, out var categoryProfile))
                {
                    return categoryProfile;
                }

                if (CanSplit(inventoryItem) && HasEmptyInventorySlot())
                {
                    return InventoryActionProfileId.InventorySplitStack;
                }

                if (inventoryItem.IsStackable)
                {
                    return InventoryActionProfileId.InventoryStack;
                }

                return InventoryActionProfileId.InventoryGeneric;
            }

            if (equipmentItem != null)
            {
                return InventoryActionProfileId.EquipmentSelected;
            }

            return InventoryActionProfileId.None;
        }

        private bool TryGetCategoryProfileId(ItemCategory category, out InventoryActionProfileId profileId)
        {
            profileId = InventoryActionProfileId.None;
            if (categoryActionOverrides == null)
            {
                return false;
            }

            for (int i = 0; i < categoryActionOverrides.Count; i++)
            {
                var entry = categoryActionOverrides[i];
                if (entry == null || entry.Category != category)
                {
                    continue;
                }

                profileId = entry.ProfileId;
                return true;
            }

            return false;
        }

        private bool TryGetActionProfile(InventoryActionProfileId profileId, out InventoryActionProfile profile)
        {
            profile = null;
            if (actionProfiles == null)
            {
                return false;
            }

            for (int i = 0; i < actionProfiles.Count; i++)
            {
                var candidate = actionProfiles[i];
                if (candidate == null || candidate.ProfileId != profileId)
                {
                    continue;
                }

                profile = candidate;
                return true;
            }

            return false;
        }

        private static InventoryActionProfile CreateFallbackNoneProfile()
        {
            return new InventoryActionProfile(
                InventoryActionProfileId.None,
                "装备",
                InventoryActionCommand.None,
                "卸下",
                InventoryActionCommand.None,
                "选择背包或装备中的物品");
        }

        private bool SplitSelected()
        {
            if (inventory == null || selectedInventoryIndex < 0 || selectedInventoryIndex >= inventory.Items.Count)
            {
                return false;
            }

            var selected = inventory.Items[selectedInventoryIndex];
            if (!CanSplit(selected))
            {
                return false;
            }

            var targetIndex = FindFirstEmptyInventorySlot();
            if (targetIndex < 0)
            {
                return false;
            }

            var splitAmount = selected.Stack / 2;
            if (splitAmount <= 0)
            {
                return false;
            }

            selected.SetStack(selected.Stack - splitAmount);
            var splitItem = selected.CloneWithStack(splitAmount);
            if (!inventory.TrySetItemAt(targetIndex, splitItem, out var replaced) || replaced != null)
            {
                selected.SetStack(selected.Stack + splitAmount);
                return false;
            }

            return true;
        }

        private void HandleOrganizeClicked()
        {
            if (inventory == null)
            {
                UIToast.Warning("整理失败：背包未就绪。");
                return;
            }

            HideSortPicker();
            EndDrag();
            ClearInventorySelection();
            ClearEquipmentSelection();
            ClearHoveredEquipmentState();
            if (inventory.TryAutoOrganize(CompareOrganizeItems, true))
            {
                UIToast.Success("背包已整理。");
                return;
            }

            UIToast.Info("背包无需整理。");
        }

        private bool UseSelectedConsumable()
        {
            var selected = ResolveInventorySelection();
            if (selected == null || selected.Definition == null || selected.Definition.Category != ItemCategory.Consumable)
            {
                return false;
            }

            if (!DropSelectedInventory(1))
            {
                UIToast.Warning("使用失败。");
                return false;
            }

            UIToast.Success($"已使用：{ResolveItemName(selected)}");
            return true;
        }

        private bool OpenQuestJournalFromInventory()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (uiManager == null)
            {
                UIToast.Warning("无法打开任务日志。");
                return false;
            }

            var questScreen = FindFirstObjectByType<QuestJournalScreen>(FindObjectsInactive.Include);
            if (questScreen == null)
            {
                UIToast.Warning("任务日志界面未配置。");
                return false;
            }

            if (uiManager.CurrentScreen == questScreen)
            {
                return true;
            }

            uiManager.PushScreen(questScreen);
            return true;
        }

        private static bool IsTextInputFocused()
        {
            var current = EventSystem.current;
            if (current == null || current.currentSelectedGameObject == null)
            {
                return false;
            }

            return current.currentSelectedGameObject.GetComponentInParent<InputField>() != null;
        }

        private bool DropSelectedInventory(int amount)
        {
            if (inventory == null || selectedInventoryIndex < 0 || selectedInventoryIndex >= inventory.Items.Count)
            {
                return false;
            }

            var selected = inventory.Items[selectedInventoryIndex];
            if (selected == null)
            {
                return false;
            }

            var removeAmount = selected.IsStackable
                ? Mathf.Clamp(amount, 1, selected.Stack)
                : selected.Stack;
            return inventory.TryRemoveAt(selectedInventoryIndex, removeAmount);
        }

        private bool DropSelectedEquipment()
        {
            if (equipment == null || selectedEquipmentIndex < 0 || selectedEquipmentIndex >= equipment.Slots.Count)
            {
                return false;
            }

            return equipment.TryReplaceSlotItem(selectedEquipmentIndex, null);
        }

        private bool HasEmptyInventorySlot()
        {
            return FindFirstEmptyInventorySlot() >= 0;
        }

        private int FindFirstEmptyInventorySlot()
        {
            if (inventory == null)
            {
                return -1;
            }

            var items = inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool CanSplit(ItemInstance item)
        {
            return item != null && item.IsStackable && item.Stack > 1;
        }

        private static string ResolveItemName(ItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                return "物品";
            }

            if (!string.IsNullOrWhiteSpace(item.Definition.DisplayName))
            {
                return item.Definition.DisplayName;
            }

            return item.Definition.name;
        }

        private void ApplyActionState(Button button, string label, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            SetButtonLabel(button, label);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var labels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null)
                {
                    continue;
                }

                labels[i].text = label;
            }
        }

        private void SetActionHint(string hint)
        {
            if (actionHintText == null)
            {
                return;
            }

            actionHintText.text = string.IsNullOrEmpty(hint) ? string.Empty : hint;
        }

        private ItemInstance FindEquippedForSlot(ItemInstance item)
        {
            if (equipment == null || item == null || item.Definition == null || !item.Definition.IsEquippable)
            {
                return null;
            }

            var slots = equipment.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Slot == item.Definition.Slot && slot.Item != null)
                {
                    return slot.Item;
                }
            }

            return null;
        }

        public override string GetFooterHintText()
        {
            return "{MENU_CLOSE} 关闭菜单    {BACK} 返回游戏    {TAB_SWITCH} 切换页签    R 整理背包    拖拽交换 / 双击装备";
        }
    }


}

