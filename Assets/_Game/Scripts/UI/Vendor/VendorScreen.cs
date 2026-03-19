using CombatSystem.Data;
using CombatSystem.Gameplay;
using CombatSystem.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 商人界面：处理商人交易的UI逻辑。
    /// 展示商人商品列表和玩家背包，支持购买和出售操作。
    /// </summary>
    public class VendorScreen : UIScreenBase
    {
        [Header("References")]
        [Tooltip("UI管理器引用")]
        [SerializeField] private UIManager uiManager;
        [Tooltip("商人服务引用")]
        [SerializeField] private VendorService vendorService;
        [Tooltip("玩家背包组件")]
        [SerializeField] private InventoryComponent playerInventory;
        [Tooltip("玩家货币组件")]
        [SerializeField] private CurrencyComponent playerCurrency;
        [Tooltip("输入读取器（用于 Esc/Cancel 关闭界面）")]
        [SerializeField] private InputReader inputReader;

        [Header("Widgets")]
        [Tooltip("商人商品列表UI")]
        [SerializeField] private VendorListUI vendorList;
        [Tooltip("玩家背包网格UI")]
        [SerializeField] private InventoryGridUI inventoryGrid;
        [Tooltip("购买按钮")]
        [SerializeField] private Button buyButton;
        [Tooltip("出售按钮")]
        [SerializeField] private Button sellButton;
        [Tooltip("货币显示文本")]
        [SerializeField] private Text currencyText;
        [Tooltip("状态文案（显示按钮不可用原因）")]
        [SerializeField] private Text statusText;
        [Tooltip("选中物品标题")]
        [SerializeField] private Text detailTitleText;
        [Tooltip("选中来源/基础信息")]
        [SerializeField] private Text detailMetaText;
        [Tooltip("价格信息")]
        [SerializeField] private Text detailPriceText;
        [Tooltip("选中物品描述")]
        [SerializeField] private Text detailDescriptionText;
        [Tooltip("详情图标")]
        [SerializeField] private Image detailIconImage;
        [Tooltip("界面标题")]
        [SerializeField] private Text screenTitleText;
        [Tooltip("开启调试日志")]
        [SerializeField] private bool debugLogging;
        [Tooltip("是否允许通过 Esc/Cancel 关闭界面")]
        [SerializeField] private bool closeOnCancel = true;

        /// <summary>当前选中的商人商品索引，-1表示未选中</summary>
        private int selectedVendorIndex = -1;
        /// <summary>当前选中的背包物品索引，-1表示未选中</summary>
        private int selectedInventoryIndex = -1;
        /// <summary>是否已订阅事件</summary>
        private bool subscribed;
        private string statusOverride = string.Empty;
        private SelectionSource lastSelectionSource = SelectionSource.None;
        private const float DoubleClickThreshold = 0.33f;
        private int lastVendorClickedIndex = -1;
        private int lastInventoryClickedIndex = -1;
        private float lastVendorClickTime = -10f;
        private float lastInventoryClickTime = -10f;
        private DragPayload dragPayload;
        private bool dragActive;
        private Image dragIcon;
        private Canvas dragCanvas;

        internal static bool DebugEnabled { get; private set; }

        private enum SelectionSource
        {
            None = 0,
            Vendor = 1,
            Inventory = 2
        }

        /// <summary>
        /// 编辑器重置时设置默认值
        /// </summary>
        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        /// <summary>
        /// 界面进入时调用：初始化引用、订阅事件、刷新显示
        /// </summary>
        public override void OnEnter()
        {
            EnsureReferences();
            statusOverride = string.Empty;
            Subscribe();
            RefreshAll();
            DebugEnabled = debugLogging;

            // 隐藏HUD，让玩家专注于交易
            if (uiManager != null)
            {
                uiManager.SetHudVisible(false);
            }
        }

        /// <summary>
        /// 界面退出时调用：取消订阅事件、恢复HUD
        /// </summary>
        public override void OnExit()
        {
            Unsubscribe();
            DebugEnabled = false;
            statusOverride = string.Empty;
            SetStatus(string.Empty);
            EndDrag();
            if (uiManager != null)
            {
                uiManager.SetHudVisible(true);
            }
        }

        private void OnDestroy()
        {
            if (dragIcon != null)
            {
                Destroy(dragIcon.gameObject);
                dragIcon = null;
                dragCanvas = null;
            }
        }

        /// <summary>
        /// 界面获得焦点时刷新显示
        /// </summary>
        public override void OnFocus()
        {
            RefreshAll();
        }

        /// <summary>
        /// 购买当前选中的商人商品
        /// </summary>
        public void BuySelected()
        {
            if (vendorService == null)
            {
                SetStatus("商店服务未就绪。");
                UIToast.Warning("商店服务未就绪。");
                return;
            }

            if (selectedVendorIndex < 0 || selectedVendorIndex >= vendorService.Items.Count)
            {
                SetStatus("请选择一个商品。");
                UIToast.Warning("请选择一个商品。");
                return;
            }

            var selectedItemName = ResolveItemName(vendorService.Items[selectedVendorIndex].Definition);
            if (vendorService.TryBuy(selectedVendorIndex, 1))
            {
                RefreshSelectionAfterChange();
                SetStatus(string.Empty);
                UIToast.Success($"购买成功：{selectedItemName} x1");
            }
            else
            {
                SetStatus(vendorService.LastFailureReason);
                UIToast.Warning(string.IsNullOrWhiteSpace(vendorService.LastFailureReason)
                    ? "购买失败。"
                    : vendorService.LastFailureReason);
            }
        }

        /// <summary>
        /// 出售当前选中的背包物品
        /// </summary>
        public void SellSelected()
        {
            if (vendorService == null || playerInventory == null)
            {
                SetStatus("背包或商店服务未就绪。");
                UIToast.Warning("背包或商店服务未就绪。");
                return;
            }

            if (selectedInventoryIndex < 0 || selectedInventoryIndex >= playerInventory.Items.Count)
            {
                SetStatus("请选择背包中的物品。");
                UIToast.Warning("请选择背包中的物品。");
                return;
            }

            var item = playerInventory.Items[selectedInventoryIndex];
            if (item == null)
            {
                SetStatus("该槽位没有可出售物品。");
                UIToast.Warning("该槽位没有可出售物品。");
                return;
            }

            var itemName = ResolveItemName(item.Definition);
            if (vendorService.TrySell(item, 1))
            {
                RefreshSelectionAfterChange();
                SetStatus(string.Empty);
                UIToast.Success($"出售成功：{itemName} x1");
            }
            else
            {
                SetStatus(vendorService.LastFailureReason);
                UIToast.Warning(string.IsNullOrWhiteSpace(vendorService.LastFailureReason)
                    ? "出售失败。"
                    : vendorService.LastFailureReason);
            }
        }

        /// <summary>
        /// 确保所有必要的引用有效
        /// </summary>
        private void EnsureReferences()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (vendorService == null)
            {
                vendorService = FindFirstObjectByType<VendorService>();
            }

            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<InventoryComponent>();
            }

            if (playerCurrency == null)
            {
                playerCurrency = FindFirstObjectByType<CurrencyComponent>();
            }

            if (inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }
        }

        /// <summary>
        /// 订阅所有相关事件
        /// </summary>
        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            // 订阅数据变化事件
            if (vendorService != null)
            {
                vendorService.VendorUpdated += HandleVendorUpdated;
            }

            if (playerInventory != null)
            {
                playerInventory.InventoryChanged += HandleInventoryChanged;
            }

            if (playerCurrency != null)
            {
                playerCurrency.CurrencyChanged += HandleCurrencyChanged;
            }

            if (closeOnCancel && inputReader != null)
            {
                inputReader.CancelPerformed += HandleCloseRequested;
                inputReader.PausePerformed += HandleCloseRequested;
            }

            // 订阅UI选择事件
            if (vendorList != null)
            {
                vendorList.SlotSelected += HandleVendorSlotSelected;
                vendorList.SlotDragStarted += HandleVendorDragStarted;
                vendorList.SlotDragging += HandleVendorDragging;
                vendorList.SlotDragEnded += HandleVendorDragEnded;
                vendorList.SlotDropped += HandleVendorSlotDropped;
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.SlotSelected += HandleInventorySlotSelected;
                inventoryGrid.SlotDragStarted += HandleInventoryDragStarted;
                inventoryGrid.SlotDragging += HandleInventoryDragging;
                inventoryGrid.SlotDragEnded += HandleInventoryDragEnded;
                inventoryGrid.SlotDropped += HandleInventorySlotDropped;
            }

            // 订阅按钮点击事件
            if (buyButton != null)
            {
                buyButton.onClick.AddListener(BuySelected);
            }

            if (sellButton != null)
            {
                sellButton.onClick.AddListener(SellSelected);
            }

            subscribed = true;

            if (debugLogging)
            {
                Debug.Log($"[UI][Vendor] Subscribe vendorService={vendorService != null} vendorList={vendorList != null} inventoryGrid={inventoryGrid != null}", this);
            }
        }

        /// <summary>
        /// 取消订阅所有事件
        /// </summary>
        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (vendorService != null)
            {
                vendorService.VendorUpdated -= HandleVendorUpdated;
            }

            if (playerInventory != null)
            {
                playerInventory.InventoryChanged -= HandleInventoryChanged;
            }

            if (playerCurrency != null)
            {
                playerCurrency.CurrencyChanged -= HandleCurrencyChanged;
            }

            if (inputReader != null)
            {
                inputReader.CancelPerformed -= HandleCloseRequested;
                inputReader.PausePerformed -= HandleCloseRequested;
            }

            if (vendorList != null)
            {
                vendorList.SlotSelected -= HandleVendorSlotSelected;
                vendorList.SlotDragStarted -= HandleVendorDragStarted;
                vendorList.SlotDragging -= HandleVendorDragging;
                vendorList.SlotDragEnded -= HandleVendorDragEnded;
                vendorList.SlotDropped -= HandleVendorSlotDropped;
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.SlotSelected -= HandleInventorySlotSelected;
                inventoryGrid.SlotDragStarted -= HandleInventoryDragStarted;
                inventoryGrid.SlotDragging -= HandleInventoryDragging;
                inventoryGrid.SlotDragEnded -= HandleInventoryDragEnded;
                inventoryGrid.SlotDropped -= HandleInventorySlotDropped;
            }

            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(BuySelected);
            }

            if (sellButton != null)
            {
                sellButton.onClick.RemoveListener(SellSelected);
            }

            subscribed = false;
        }

        /// <summary>
        /// 刷新所有UI元素
        /// </summary>
        private void RefreshAll()
        {
            // 绑定数据到UI控件
            if (vendorList != null)
            {
                vendorList.Bind(vendorService);
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.Bind(playerInventory);
            }

            RefreshCurrency();
            RefreshSelectionAfterChange();

            if (debugLogging)
            {
                var vendorCount = vendorService != null ? vendorService.Items.Count : 0;
                Debug.Log($"[UI][Vendor] RefreshAll vendorItems={vendorCount} inventory={playerInventory != null}", this);
            }
        }

        /// <summary>
        /// 商人数据更新事件处理
        /// </summary>
        private void HandleVendorUpdated()
        {
            RefreshSelectionAfterChange();
        }

        /// <summary>
        /// 背包数据变化事件处理
        /// </summary>
        private void HandleInventoryChanged()
        {
            RefreshSelectionAfterChange();
        }

        /// <summary>
        /// 货币变化事件处理
        /// </summary>
        private void HandleCurrencyChanged(int oldValue, int newValue)
        {
            RefreshCurrency();
            UpdateButtons();
            RefreshSelectionDetails();
        }

        private void HandleCloseRequested()
        {
            if (!closeOnCancel)
            {
                return;
            }

            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (uiManager == null || uiManager.CurrentScreen != this)
            {
                return;
            }

            PauseMenuHotkey.SuppressOpenForFrames();
            uiManager.PopScreen();
        }

        /// <summary>
        /// 商人商品槽位选中事件处理
        /// </summary>
        private void HandleVendorSlotSelected(int index)
        {
            var now = Time.unscaledTime;
            var isDoubleClick = index == lastVendorClickedIndex && now - lastVendorClickTime <= DoubleClickThreshold;
            lastVendorClickedIndex = index;
            lastVendorClickTime = now;

            SelectVendorIndex(index);

            if (isDoubleClick)
            {
                BuySelected();
            }

            if (debugLogging)
            {
                Debug.Log($"[UI][Vendor] Vendor slot selected index={index} doubleClick={isDoubleClick}", this);
            }
        }

        /// <summary>
        /// 背包槽位选中事件处理
        /// </summary>
        private void HandleInventorySlotSelected(int index)
        {
            var now = Time.unscaledTime;
            var isDoubleClick = index == lastInventoryClickedIndex && now - lastInventoryClickTime <= DoubleClickThreshold;
            lastInventoryClickedIndex = index;
            lastInventoryClickTime = now;

            SelectInventoryIndex(index);

            if (isDoubleClick)
            {
                SellSelected();
            }

            if (debugLogging)
            {
                Debug.Log($"[UI][Vendor] Inventory slot selected index={index} doubleClick={isDoubleClick}", this);
            }
        }

        private void HandleVendorDragStarted(VendorItemSlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.ItemState == null || slot.ItemState.Definition == null)
            {
                return;
            }

            SelectVendorIndex(slot.SlotIndex);
            BeginDrag(DragSource.Vendor, slot.SlotIndex, slot.ItemState.Definition.Icon, eventData);
        }

        private void HandleVendorDragging(VendorItemSlotUI slot, PointerEventData eventData)
        {
            UpdateDragIcon(eventData);
        }

        private void HandleVendorDragEnded(VendorItemSlotUI slot, PointerEventData eventData)
        {
            EndDrag();
        }

        private void HandleVendorSlotDropped(VendorItemSlotUI slot, PointerEventData eventData)
        {
            if (!dragActive || slot == null)
            {
                return;
            }

            if (dragPayload.Source == DragSource.Inventory)
            {
                SelectInventoryIndex(dragPayload.SourceIndex);
                SellSelected();
            }
        }

        private void HandleInventoryDragStarted(InventorySlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.Item == null || slot.Item.Definition == null)
            {
                return;
            }

            SelectInventoryIndex(slot.SlotIndex);
            BeginDrag(DragSource.Inventory, slot.SlotIndex, slot.Item.Definition.Icon, eventData);
        }

        private void HandleInventoryDragging(InventorySlotUI slot, PointerEventData eventData)
        {
            UpdateDragIcon(eventData);
        }

        private void HandleInventoryDragEnded(InventorySlotUI slot, PointerEventData eventData)
        {
            EndDrag();
        }

        private void HandleInventorySlotDropped(InventorySlotUI slot, PointerEventData eventData)
        {
            if (!dragActive || slot == null)
            {
                return;
            }

            if (dragPayload.Source == DragSource.Vendor)
            {
                SelectVendorIndex(dragPayload.SourceIndex);
                BuySelected();
            }
        }

        private void SelectVendorIndex(int index)
        {
            selectedVendorIndex = index;
            selectedInventoryIndex = -1;

            if (vendorList != null)
            {
                vendorList.SetSelectedIndex(selectedVendorIndex);
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.SetSelectedIndex(selectedInventoryIndex);
            }

            lastSelectionSource = SelectionSource.Vendor;
            statusOverride = string.Empty;
            UpdateButtons();
            RefreshSelectionDetails();
        }

        private void SelectInventoryIndex(int index)
        {
            selectedInventoryIndex = index;
            selectedVendorIndex = -1;

            if (inventoryGrid != null)
            {
                inventoryGrid.SetSelectedIndex(selectedInventoryIndex);
            }

            if (vendorList != null)
            {
                vendorList.SetSelectedIndex(selectedVendorIndex);
            }

            lastSelectionSource = SelectionSource.Inventory;
            statusOverride = string.Empty;
            UpdateButtons();
            RefreshSelectionDetails();
        }

        /// <summary>
        /// 数据变化后刷新选中状态
        /// </summary>
        private void RefreshSelectionAfterChange()
        {
            // 验证选中索引是否仍然有效
            if (vendorService == null || selectedVendorIndex >= vendorService.Items.Count)
            {
                selectedVendorIndex = -1;
            }

            if (playerInventory == null || selectedInventoryIndex >= playerInventory.Items.Count)
            {
                selectedInventoryIndex = -1;
            }

            // 选中的来源若失效，自动切换到另一侧有效选择。
            if (lastSelectionSource == SelectionSource.Vendor && selectedVendorIndex < 0 && selectedInventoryIndex >= 0)
            {
                lastSelectionSource = SelectionSource.Inventory;
            }
            else if (lastSelectionSource == SelectionSource.Inventory && selectedInventoryIndex < 0 && selectedVendorIndex >= 0)
            {
                lastSelectionSource = SelectionSource.Vendor;
            }
            else if (selectedVendorIndex < 0 && selectedInventoryIndex < 0)
            {
                lastSelectionSource = SelectionSource.None;
            }

            // 同步UI选中状态
            if (vendorList != null)
            {
                vendorList.SetSelectedIndex(selectedVendorIndex);
            }

            if (inventoryGrid != null)
            {
                inventoryGrid.SetSelectedIndex(selectedInventoryIndex);
            }

            RefreshHeader();
            UpdateButtons();
            RefreshSelectionDetails();
        }

        /// <summary>
        /// 根据当前状态更新按钮可交互性
        /// </summary>
        private void UpdateButtons()
        {
            var buyReason = string.Empty;
            var sellReason = string.Empty;
            var buyInteractable = false;
            var sellInteractable = false;

            // 购买按钮：需要选中商品且可购买
            if (buyButton != null)
            {
                if (vendorService == null)
                {
                    buyReason = "商店服务未就绪。";
                }
                else if (selectedVendorIndex < 0 || selectedVendorIndex >= vendorService.Items.Count)
                {
                    buyReason = "请选择一个商品。";
                }
                else
                {
                    buyInteractable = vendorService.CanBuy(selectedVendorIndex, 1, out buyReason);
                }

                buyButton.interactable = buyInteractable;
            }

            // 出售按钮：需要选中有效物品
            if (sellButton != null)
            {
                if (vendorService == null || playerInventory == null)
                {
                    sellReason = "背包或商店服务未就绪。";
                }
                else if (selectedInventoryIndex < 0 || selectedInventoryIndex >= playerInventory.Items.Count)
                {
                    sellReason = "请选择背包中的物品。";
                }
                else
                {
                    var selected = playerInventory.Items[selectedInventoryIndex];
                    sellInteractable = vendorService.CanSell(selected, 1, out sellReason);
                }

                sellButton.interactable = sellInteractable;
            }

            var statusMessage = statusOverride;
            var showWarningStyle = !string.IsNullOrWhiteSpace(statusOverride);
            if (statusText != null)
            {
                if (string.IsNullOrWhiteSpace(statusMessage))
                {
                    statusMessage = BuildStatusHint();
                }

                statusText.text = statusMessage;
                statusText.color = showWarningStyle
                    ? new Color(1f, 0.86f, 0.56f, 1f)
                    : new Color(0.76f, 0.82f, 0.9f, 0.96f);
            }

            RefreshActionButtonsVisuals(buyInteractable, sellInteractable);

            if (debugLogging)
            {
                Debug.Log($"[UI][Vendor] UpdateButtons buy={buyButton?.interactable} sell={sellButton?.interactable} reason={statusText?.text}", this);
            }
        }

        /// <summary>
        /// 刷新货币显示
        /// </summary>
        private void RefreshCurrency()
        {
            if (currencyText == null)
            {
                return;
            }

            currencyText.text = playerCurrency != null ? $"{playerCurrency.Amount:N0}G" : "--";
        }

        private void SetStatus(string message)
        {
            statusOverride = message ?? string.Empty;
            if (statusText != null)
            {
                statusText.text = statusOverride;
            }
        }

        private void RefreshSelectionDetails()
        {
            if (lastSelectionSource == SelectionSource.Vendor)
            {
                if (vendorService != null && selectedVendorIndex >= 0 && selectedVendorIndex < vendorService.Items.Count)
                {
                    var state = vendorService.Items[selectedVendorIndex];
                    var definition = state != null ? state.Definition : null;
                    if (definition != null)
                    {
                        var displayName = ResolveItemName(definition);
                        var buyPrice = state.Price;
                        var sellPrice = vendorService.ResolveSellPrice(definition);
                        var stock = state.InfiniteStock ? "无限" : state.RemainingStock.ToString();
                        SetDetail(
                            definition.Icon,
                            displayName,
                            $"商店商品  ·  {ResolveRarityLabel(definition.Rarity)}  ·  {ResolveCategoryLabel(definition.Category)}",
                            $"购买价 {buyPrice}G  |  回收价 {sellPrice}G  |  库存 {stock}",
                            string.IsNullOrWhiteSpace(definition.Description) ? "暂无描述。" : definition.Description);
                        return;
                    }
                }
            }
            else if (lastSelectionSource == SelectionSource.Inventory)
            {
                if (playerInventory != null && selectedInventoryIndex >= 0 && selectedInventoryIndex < playerInventory.Items.Count)
                {
                    var item = playerInventory.Items[selectedInventoryIndex];
                    if (item != null && item.Definition != null)
                    {
                        var definition = item.Definition;
                        var displayName = ResolveItemName(definition);
                        var sellUnit = vendorService != null ? vendorService.ResolveSellPrice(definition) : 0;
                        var sellTotal = sellUnit * Mathf.Max(1, item.Stack);
                        var affixCount = item.Affixes != null ? item.Affixes.Count : 0;
                        SetDetail(
                            definition.Icon,
                            displayName,
                            $"背包物品  ·  {ResolveRarityLabel(item.Rarity)}  ·  {ResolveCategoryLabel(definition.Category)}  ·  持有 {item.Stack} 件  ·  词缀 {affixCount} 个",
                            $"出售价 {sellUnit}G/件  |  整组估值 {sellTotal}G",
                            string.IsNullOrWhiteSpace(definition.Description) ? "暂无描述。" : definition.Description);
                        return;
                    }
                }
            }

            SetDetail(
                null,
                "未选中物品",
                "从左侧商店货架或右侧背包中选中一件物品。",
                string.Empty,
                "这里会显示物品说明、价格和库存等信息。");
        }

        private void SetDetail(Sprite icon, string title, string meta, string price, string description)
        {
            if (detailIconImage != null)
            {
                detailIconImage.sprite = icon;
                detailIconImage.enabled = icon != null;
                detailIconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (detailTitleText != null)
            {
                detailTitleText.text = title ?? string.Empty;
            }

            if (detailMetaText != null)
            {
                detailMetaText.text = meta ?? string.Empty;
            }

            if (detailPriceText != null)
            {
                detailPriceText.text = price ?? string.Empty;
            }

            if (detailDescriptionText != null)
            {
                detailDescriptionText.text = description ?? string.Empty;
            }
        }

        private void RefreshHeader()
        {
            if (screenTitleText != null)
            {
                screenTitleText.text = ResolveVendorDisplayName();
            }
        }

        private void RefreshActionButtonsVisuals(bool buyInteractable, bool sellInteractable)
        {
            ApplyActionButtonVisual(
                buyButton,
                lastSelectionSource == SelectionSource.Vendor && selectedVendorIndex >= 0,
                buyInteractable,
                new Color(0.22f, 0.29f, 0.38f, 1f),
                new Color(0.75f, 0.56f, 0.2f, 1f));

            ApplyActionButtonVisual(
                sellButton,
                lastSelectionSource == SelectionSource.Inventory && selectedInventoryIndex >= 0,
                sellInteractable,
                new Color(0.22f, 0.29f, 0.38f, 1f),
                new Color(0.2f, 0.53f, 0.35f, 1f));
        }

        private static void ApplyActionButtonVisual(Button button, bool emphasized, bool interactable, Color baseColor, Color accentColor)
        {
            if (button == null)
            {
                return;
            }

            var normal = emphasized ? accentColor : baseColor;
            var disabled = new Color(0.24f, 0.26f, 0.3f, 0.72f);
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = interactable ? normal : disabled;
            }

            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = disabled;
            button.colors = colors;

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = interactable ? Color.white : new Color(0.78f, 0.8f, 0.84f, 0.72f);
                label.fontStyle = emphasized ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private string BuildStatusHint()
        {
            if (lastSelectionSource == SelectionSource.Vendor && selectedVendorIndex >= 0)
            {
                return "查看成交预估后决定是否购买。Esc 可关闭界面。";
            }

            if (lastSelectionSource == SelectionSource.Inventory && selectedInventoryIndex >= 0)
            {
                return "查看成交预估后决定是否出售。Esc 可关闭界面。";
            }

            return "选择一件物品查看详情。Esc 可关闭界面。";
        }

        private string ResolveVendorDisplayName()
        {
            if (vendorService != null && vendorService.Definition != null)
            {
                var definition = vendorService.Definition;
                if (!string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    return definition.DisplayName;
                }

                if (!string.IsNullOrWhiteSpace(definition.name))
                {
                    return definition.name;
                }
            }

            return "商人交易";
        }

        private static string ResolveRarityLabel(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic:
                    return "魔法";
                case ItemRarity.Rare:
                    return "稀有";
                case ItemRarity.Epic:
                    return "史诗";
                case ItemRarity.Legendary:
                    return "传说";
                case ItemRarity.Common:
                default:
                    return "普通";
            }
        }

        private static string ResolveCategoryLabel(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon:
                    return "武器";
                case ItemCategory.Armor:
                    return "防具";
                case ItemCategory.Accessory:
                    return "饰品";
                case ItemCategory.Consumable:
                    return "消耗品";
                case ItemCategory.Material:
                    return "材料";
                case ItemCategory.Quest:
                    return "任务道具";
                case ItemCategory.Skill:
                    return "技能";
                case ItemCategory.General:
                default:
                    return "通用";
            }
        }

        private static string ResolveItemName(CombatSystem.Data.ItemDefinition definition)
        {
            if (definition == null)
            {
                return "未知物品";
            }

            if (!string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(definition.name))
            {
                return definition.name;
            }

            return definition.Id;
        }

        private void BeginDrag(DragSource source, int sourceIndex, Sprite icon, PointerEventData eventData)
        {
            EnsureDragIcon();
            dragPayload = new DragPayload(source, sourceIndex, icon);
            dragActive = true;

            if (dragIcon != null)
            {
                dragIcon.sprite = icon;
                dragIcon.enabled = dragIcon.sprite != null;
            }

            UpdateDragIcon(eventData);
        }

        private void EndDrag()
        {
            if (!dragActive)
            {
                return;
            }

            dragActive = false;
            dragPayload = default;

            if (dragIcon != null)
            {
                dragIcon.enabled = false;
            }
        }

        private void UpdateDragIcon(PointerEventData eventData)
        {
            if (!dragActive || dragIcon == null || eventData == null)
            {
                return;
            }

            if (dragCanvas == null || dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                dragIcon.rectTransform.position = eventData.position;
                return;
            }

            var canvasTransform = dragCanvas.transform as RectTransform;
            if (canvasTransform == null)
            {
                dragIcon.rectTransform.position = eventData.position;
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasTransform,
                eventData.position,
                dragCanvas.worldCamera,
                out var localPoint);
            dragIcon.rectTransform.localPosition = localPoint;
        }

        private void EnsureDragIcon()
        {
            if (dragIcon != null)
            {
                return;
            }

            Canvas targetCanvas = null;
            if (UIRoot.Instance != null)
            {
                targetCanvas = UIRoot.Instance.OverlayCanvas != null
                    ? UIRoot.Instance.OverlayCanvas
                    : UIRoot.Instance.ScreensCanvas;
            }

            if (targetCanvas == null)
            {
                targetCanvas = GetComponentInParent<Canvas>();
            }

            if (targetCanvas == null)
            {
                return;
            }

            dragCanvas = targetCanvas;
            var root = new GameObject("VendorDragIcon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.SetParent(targetCanvas.transform, false);
            rectTransform.sizeDelta = new Vector2(64f, 64f);

            var canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            dragIcon = root.GetComponent<Image>();
            dragIcon.raycastTarget = false;
            dragIcon.enabled = false;
        }

        private enum DragSource
        {
            None,
            Vendor,
            Inventory
        }

        private readonly struct DragPayload
        {
            public readonly DragSource Source;
            public readonly int SourceIndex;
            public readonly Sprite Icon;

            public DragPayload(DragSource source, int sourceIndex, Sprite icon)
            {
                Source = source;
                SourceIndex = sourceIndex;
                Icon = icon;
            }
        }
    }
}
