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
                return;
            }

            if (selectedVendorIndex < 0 || selectedVendorIndex >= vendorService.Items.Count)
            {
                SetStatus("请选择一个商品。");
                return;
            }

            if (vendorService.TryBuy(selectedVendorIndex, 1))
            {
                RefreshSelectionAfterChange();
                SetStatus(string.Empty);
            }
            else
            {
                SetStatus(vendorService.LastFailureReason);
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
                return;
            }

            if (selectedInventoryIndex < 0 || selectedInventoryIndex >= playerInventory.Items.Count)
            {
                SetStatus("请选择背包中的物品。");
                return;
            }

            var item = playerInventory.Items[selectedInventoryIndex];
            if (item == null)
            {
                SetStatus("该槽位没有可出售物品。");
                return;
            }

            if (vendorService.TrySell(item, 1))
            {
                RefreshSelectionAfterChange();
                SetStatus(string.Empty);
            }
            else
            {
                SetStatus(vendorService.LastFailureReason);
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

            RefreshSelectionAfterChange();
            RefreshCurrency();
            RefreshSelectionDetails();

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

            if (statusText != null)
            {
                if (selectedVendorIndex >= 0 && !buyInteractable && !string.IsNullOrEmpty(buyReason))
                {
                    statusText.text = buyReason;
                }
                else if (selectedInventoryIndex >= 0 && !sellInteractable && !string.IsNullOrEmpty(sellReason))
                {
                    statusText.text = sellReason;
                }
                else
                {
                    statusText.text = statusOverride;
                }
            }

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
            if (currencyText == null || playerCurrency == null)
            {
                return;
            }

            currencyText.text = playerCurrency.Amount.ToString();
        }

        private void SetStatus(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusOverride = message ?? string.Empty;
            statusText.text = statusOverride;
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
                        var stock = state.InfiniteStock ? "inf" : state.RemainingStock.ToString();
                        SetDetail(
                            displayName,
                            $"来源: 商店  稀有度: {definition.Rarity}  分类: {definition.Category}",
                            $"购买: {buyPrice}G  出售: {sellPrice}G  库存: {stock}",
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
                            displayName,
                            $"来源: 背包  数量: {item.Stack}  稀有度: {item.Rarity}  词缀: {affixCount}",
                            $"出售单价: {sellUnit}G  预计总价: {sellTotal}G",
                            string.IsNullOrWhiteSpace(definition.Description) ? "暂无描述。" : definition.Description);
                        return;
                    }
                }
            }

            SetDetail(
                "未选中物品",
                "请点击左侧商店格子或右侧背包格子。",
                string.Empty,
                "选中后会显示名称、价格、库存和描述。");
        }

        private void SetDetail(string title, string meta, string price, string description)
        {
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

        private static string ResolveItemName(CombatSystem.Data.ItemDefinition definition)
        {
            if (definition == null)
            {
                return "Unknown";
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
