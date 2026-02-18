using System.Text;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class QuestGiverModal : UIModalBase
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Debug")]
        [SerializeField] private bool debugRaycastOnClick = false;
#endif

        [Header("Widgets")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text objectivesText;
        [SerializeField] private Text rewardText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryButtonText;
        [SerializeField] private Button tradeButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform dialogPanel;

        [Header("Anchor")]
        [SerializeField] private bool followNpcAnchor = true;
        [SerializeField] private Vector3 worldAnchorOffset = Vector3.zero;
        [SerializeField] private Vector2 popupScreenOffset = new Vector2(36f, 18f);
        [SerializeField] private float popupEdgePadding = 18f;

        private QuestGiverTrigger source;
        private bool subscribed;
        private RectTransform modalRootRect;
        private Camera cachedPresentationCamera;
        private float nextCameraResolveTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly System.Collections.Generic.List<RaycastResult> raycastResults = new System.Collections.Generic.List<RaycastResult>(16);
#endif

        public void Bind(QuestGiverTrigger trigger)
        {
            source = trigger;
            RefreshFromSource();
            UpdateDialogAnchor(true);
        }

        public override void OnEnter()
        {
            EnsureEventSystemInputReady();
            CacheLayoutReferences();
            EnsureTradeButton();
            Subscribe();
            RefreshFromSource();
            UpdateDialogAnchor(true);
            FocusDefaultOption();
        }

        public override void OnExit()
        {
            Unsubscribe();
            source = null;
        }

        public override void OnFocus()
        {
            EnsureEventSystemInputReady();
            RefreshFromSource();
            UpdateDialogAnchor(true);
            FocusDefaultOption();
        }

        private void LateUpdate()
        {
            UpdateDialogAnchor(false);
        }

        private void Update()
        {
            HandleKeyboardShortcuts();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugRaycastOnClick || !gameObject.activeInHierarchy)
            {
                return;
            }

            var clicked = false;
            var pointer = Vector2.zero;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                clicked = mouse.leftButton.wasPressedThisFrame;
                pointer = mouse.position.ReadValue();
            }
            else
            {
                var pointerDevice = Pointer.current;
                if (pointerDevice != null)
                {
                    clicked = pointerDevice.press.wasPressedThisFrame;
                    pointer = pointerDevice.position.ReadValue();
                }
            }

            if (clicked)
            {
                LogUiRaycast(pointer);
            }
#endif
        }

        private void HandleKeyboardShortcuts()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (uiManager != null && uiManager.CurrentModal != this)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                RequestClose();
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame && TryInvokeButton(primaryButton))
            {
                return;
            }

            if (keyboard.digit2Key.wasPressedThisFrame && TryInvokeButton(tradeButton))
            {
                return;
            }

            if (keyboard.digit3Key.wasPressedThisFrame && TryInvokeButton(closeButton))
            {
                return;
            }

            if (!keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                var selected = eventSystem.currentSelectedGameObject;
                if (selected != null)
                {
                    var selectedButton = selected.GetComponent<Button>();
                    if (selectedButton == null)
                    {
                        selectedButton = selected.GetComponentInParent<Button>();
                    }

                    if (TryInvokeButton(selectedButton))
                    {
                        return;
                    }
                }
            }

            if (TryInvokeButton(primaryButton))
            {
                return;
            }

            if (TryInvokeButton(tradeButton))
            {
                return;
            }

            TryInvokeButton(closeButton);
        }

        public void RefreshFromSource()
        {
            if (source == null)
            {
                ApplyEmpty();
                return;
            }

            var quest = source.QuestDefinition;
            var state = source.GetQuestState();

            if (quest == null)
            {
                ApplyEmpty();
                return;
            }

            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(quest.DisplayName) ? quest.Id : quest.DisplayName;
            }

            if (summaryText != null)
            {
                summaryText.text = string.IsNullOrWhiteSpace(quest.Summary) ? "无任务描述。" : quest.Summary;
            }

            if (statusText != null)
            {
                statusText.text = BuildStatusText(state);
            }

            if (objectivesText != null)
            {
                objectivesText.text = BuildObjectiveText(quest, state);
            }

            if (rewardText != null)
            {
                rewardText.text = BuildRewardText(quest.Reward);
            }

            if (feedbackText != null && string.IsNullOrWhiteSpace(feedbackText.text))
            {
                feedbackText.text = BuildHintText(state);
            }

            var primaryLabel = BuildPrimaryButtonText(state);
            var primaryInteractable = !string.IsNullOrWhiteSpace(primaryLabel);
            if (primaryButton != null)
            {
                primaryButton.interactable = primaryInteractable;
                primaryButton.gameObject.SetActive(primaryInteractable);
            }

            if (primaryButtonText != null)
            {
                primaryButtonText.text = primaryLabel;
            }

            if (tradeButton != null)
            {
                var canTrade = CanOpenVendor();
                tradeButton.interactable = canTrade;
                tradeButton.gameObject.SetActive(canTrade);
            }

            SetButtonLabel(closeButton, state == null || state.Status == QuestStatus.NotAccepted ? "稍后再说" : "关闭");
            FocusDefaultOption();
        }

        private void HandlePrimaryClicked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugRaycastOnClick)
            {
                Debug.Log("[QuestGiverModal] HandlePrimaryClicked invoked.", this);
            }
#endif
            if (source == null)
            {
                RequestClose();
                return;
            }

            var changed = source.ExecuteInteractionForDialog(out var feedback);
            if (feedbackText != null)
            {
                feedbackText.text = string.IsNullOrWhiteSpace(feedback) ? (changed ? "已更新。" : "无变更。") : feedback;
            }

            RefreshFromSource();
        }

        private void HandleCloseClicked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugRaycastOnClick)
            {
                Debug.Log("[QuestGiverModal] HandleCloseClicked invoked.", this);
            }
#endif
            RequestClose();
        }

        private void HandleTradeClicked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugRaycastOnClick)
            {
                Debug.Log("[QuestGiverModal] HandleTradeClicked invoked.", this);
            }
#endif
            if (!TryOpenVendor())
            {
                if (feedbackText != null)
                {
                    feedbackText.text = "商店未就绪。";
                }

                return;
            }

            if (feedbackText != null)
            {
                feedbackText.text = "已打开商店。";
            }
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.AddListener(HandlePrimaryClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            if (tradeButton != null)
            {
                tradeButton.onClick.AddListener(HandleTradeClicked);
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(HandlePrimaryClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }

            if (tradeButton != null)
            {
                tradeButton.onClick.RemoveListener(HandleTradeClicked);
            }

            subscribed = false;
        }

        private void ApplyEmpty()
        {
            if (titleText != null)
            {
                titleText.text = "任务";
            }

            if (summaryText != null)
            {
                summaryText.text = "没有可交互的任务。";
            }

            if (statusText != null)
            {
                statusText.text = string.Empty;
            }

            if (objectivesText != null)
            {
                objectivesText.text = string.Empty;
            }

            if (rewardText != null)
            {
                rewardText.text = string.Empty;
            }

            if (feedbackText != null)
            {
                feedbackText.text = string.Empty;
            }

            if (primaryButton != null)
            {
                primaryButton.interactable = false;
                primaryButton.gameObject.SetActive(false);
            }

            if (tradeButton != null)
            {
                tradeButton.interactable = false;
                tradeButton.gameObject.SetActive(false);
            }

            SetButtonLabel(closeButton, "关闭");
        }

        private bool CanOpenVendor()
        {
            if (source != null && source.GetComponent<VendorTrigger>() != null)
            {
                return true;
            }

            return FindFirstObjectByType<VendorTrigger>() != null && FindFirstObjectByType<VendorScreen>(FindObjectsInactive.Include) != null;
        }

        private bool TryOpenVendor()
        {
            var vendorTrigger = source != null ? source.GetComponent<VendorTrigger>() : null;
            if (vendorTrigger == null)
            {
                vendorTrigger = FindFirstObjectByType<VendorTrigger>();
            }

            var manager = uiManager;
            if (manager == null)
            {
                manager = FindFirstObjectByType<UIManager>();
                uiManager = manager;
            }

            var opened = false;
            if (vendorTrigger != null)
            {
                opened = vendorTrigger.TryOpenVendor();
            }

            if (!opened)
            {
                var vendorScreen = FindFirstObjectByType<VendorScreen>(FindObjectsInactive.Include);
                if (manager != null && vendorScreen != null)
                {
                    if (manager.CurrentScreen != vendorScreen)
                    {
                        manager.PushScreen(vendorScreen);
                    }

                    opened = true;
                }
            }

            if (opened && manager != null && manager.CurrentModal == this)
            {
                manager.CloseModal(this);
            }

            return opened;
        }

        private void EnsureTradeButton()
        {
            if (tradeButton != null)
            {
                return;
            }

            var template = closeButton != null ? closeButton : primaryButton;
            if (template == null)
            {
                return;
            }

            var row = template.transform.parent;
            if (row == null)
            {
                return;
            }

            var clone = Instantiate(template.gameObject, row);
            clone.name = "Button_Trade";

            var cloneButton = clone.GetComponent<Button>();
            if (cloneButton == null)
            {
                Destroy(clone);
                return;
            }

            cloneButton.onClick.RemoveAllListeners();

            var text = clone.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = "交易";
            }

            var layout = clone.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = 180f;
                layout.minWidth = 160f;
            }

            if (closeButton != null)
            {
                clone.transform.SetSiblingIndex(closeButton.transform.GetSiblingIndex());
            }

            tradeButton = cloneButton;
        }

        private void EnsureEventSystemInputReady()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = FindFirstObjectByType<EventSystem>();
            }

            if (eventSystem == null)
            {
                Debug.LogWarning("[QuestGiverModal] EventSystem not found.", this);
                return;
            }

            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (!module.enabled)
            {
                module.enabled = true;
            }

            TryEnableAction(module.point);
            TryEnableAction(module.leftClick);
            TryEnableAction(module.move);
            TryEnableAction(module.submit);
            TryEnableAction(module.cancel);
            TryEnableAction(module.scrollWheel);
        }

        private void CacheLayoutReferences()
        {
            if (modalRootRect == null)
            {
                modalRootRect = transform as RectTransform;
            }

            if (dialogPanel == null)
            {
                var panel = transform.Find("Panel");
                if (panel != null)
                {
                    dialogPanel = panel as RectTransform;
                }
            }
        }

        private void UpdateDialogAnchor(bool force)
        {
            if (!followNpcAnchor || source == null)
            {
                return;
            }

            CacheLayoutReferences();
            if (dialogPanel == null || modalRootRect == null)
            {
                return;
            }

            var camera = ResolvePresentationCamera(force);
            if (camera == null)
            {
                return;
            }

            var worldAnchor = source.DialogAnchorWorldPosition + worldAnchorOffset;
            var viewportPoint = camera.WorldToViewportPoint(worldAnchor);
            if (viewportPoint.z <= 0f)
            {
                return;
            }

            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldAnchor);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(modalRootRect, screenPoint, null, out var localAnchor))
            {
                return;
            }

            var panelSize = dialogPanel.rect.size;
            if (panelSize.x <= 1f || panelSize.y <= 1f)
            {
                Canvas.ForceUpdateCanvases();
                panelSize = dialogPanel.rect.size;
            }

            var placeLeft = screenPoint.x > Screen.width * 0.56f;
            var horizontalOffset = Mathf.Abs(popupScreenOffset.x);
            var targetX = localAnchor.x + (placeLeft ? -panelSize.x - horizontalOffset : horizontalOffset);
            var targetY = localAnchor.y + popupScreenOffset.y;

            var halfWidth = modalRootRect.rect.width * 0.5f;
            var halfHeight = modalRootRect.rect.height * 0.5f;

            var minX = -halfWidth + popupEdgePadding;
            var maxX = halfWidth - popupEdgePadding - panelSize.x;
            var minY = -halfHeight + popupEdgePadding + panelSize.y;
            var maxY = halfHeight - popupEdgePadding;

            targetX = Mathf.Clamp(targetX, minX, maxX);
            targetY = Mathf.Clamp(targetY, minY, maxY);

            dialogPanel.anchorMin = new Vector2(0.5f, 0.5f);
            dialogPanel.anchorMax = new Vector2(0.5f, 0.5f);
            dialogPanel.pivot = new Vector2(0f, 1f);
            dialogPanel.anchoredPosition = new Vector2(targetX, targetY);
        }

        private Camera ResolvePresentationCamera(bool forceRefresh)
        {
            if (!forceRefresh && cachedPresentationCamera != null && cachedPresentationCamera.isActiveAndEnabled)
            {
                return cachedPresentationCamera;
            }

            if (!forceRefresh && Time.unscaledTime < nextCameraResolveTime)
            {
                return cachedPresentationCamera;
            }

            nextCameraResolveTime = Time.unscaledTime + 0.3f;
            cachedPresentationCamera = Camera.main;
            if (cachedPresentationCamera == null)
            {
                cachedPresentationCamera = FindFirstObjectByType<Camera>();
            }

            return cachedPresentationCamera;
        }

        private void FocusDefaultOption()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (primaryButton != null && primaryButton.gameObject.activeInHierarchy && primaryButton.interactable)
            {
                eventSystem.SetSelectedGameObject(primaryButton.gameObject);
                return;
            }

            if (tradeButton != null && tradeButton.gameObject.activeInHierarchy && tradeButton.interactable)
            {
                eventSystem.SetSelectedGameObject(tradeButton.gameObject);
                return;
            }

            if (closeButton != null && closeButton.gameObject.activeInHierarchy && closeButton.interactable)
            {
                eventSystem.SetSelectedGameObject(closeButton.gameObject);
            }
        }

        private static void TryEnableAction(InputActionReference reference)
        {
            var action = reference != null ? reference.action : null;
            if (action != null && !action.enabled)
            {
                action.Enable();
            }
        }

        private static bool TryInvokeButton(Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
            {
                return false;
            }

            button.onClick?.Invoke();
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogUiRaycast(Vector2 pointerPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogWarning("[QuestGiverModal] Click debug: EventSystem.current is null.", this);
                return;
            }

            var eventData = new PointerEventData(eventSystem)
            {
                position = pointerPosition
            };

            raycastResults.Clear();
            eventSystem.RaycastAll(eventData, raycastResults);

            var sb = new StringBuilder(512);
            sb.Append("[QuestGiverModal] Click debug | pointer=")
              .Append(pointerPosition)
              .Append(" | hits=")
              .Append(raycastResults.Count);

            if (uiManager != null)
            {
                sb.Append(" | inputMode=").Append(uiManager.CurrentInputMode)
                  .Append(" | screen=").Append(uiManager.CurrentScreen != null ? uiManager.CurrentScreen.name : "null")
                  .Append(" | modal=").Append(uiManager.CurrentModal != null ? uiManager.CurrentModal.name : "null");
            }

            var inputModule = eventSystem.currentInputModule;
            sb.Append(" | eventSystem=").Append(eventSystem.name)
              .Append(" | module=").Append(inputModule != null ? inputModule.GetType().Name : "null");

            var uiModule = inputModule as InputSystemUIInputModule;
            if (uiModule != null)
            {
                sb.Append(" | clickEnabled=").Append(IsActionEnabled(uiModule.leftClick))
                  .Append(" | pointEnabled=").Append(IsActionEnabled(uiModule.point))
                  .Append(" | submitEnabled=").Append(IsActionEnabled(uiModule.submit))
                  .Append(" | cancelEnabled=").Append(IsActionEnabled(uiModule.cancel));
            }

            var max = Mathf.Min(8, raycastResults.Count);
            for (int i = 0; i < max; i++)
            {
                var hit = raycastResults[i];
                var go = hit.gameObject;
                sb.AppendLine()
                  .Append(i + 1)
                  .Append(". ")
                  .Append(go != null ? GetTransformPath(go.transform) : "null")
                  .Append(" | module=")
                  .Append(hit.module != null ? hit.module.GetType().Name : "null")
                  .Append(" | sort=")
                  .Append(hit.sortingOrder)
                  .Append(" | depth=")
                  .Append(hit.depth)
                  .Append(" | dist=")
                  .Append(hit.distance.ToString("F3"));
            }

            Debug.Log(sb.ToString(), this);
        }

        private static bool IsActionEnabled(InputActionReference reference)
        {
            var action = reference != null ? reference.action : null;
            return action != null && action.enabled;
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
            {
                return "null";
            }

            var sb = new StringBuilder(128);
            sb.Append(target.name);

            var current = target.parent;
            while (current != null)
            {
                sb.Insert(0, '/');
                sb.Insert(0, current.name);
                current = current.parent;
            }

            return sb.ToString();
        }
#endif

        public static QuestGiverModal EnsureRuntimeModal(UIManager manager)
        {
            if (manager == null)
            {
                return null;
            }

            var existing = FindFirstObjectByType<QuestGiverModal>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.uiManager == null)
                {
                    existing.uiManager = manager;
                }

                return existing;
            }

            var uiRoot = UIRoot.Instance != null ? UIRoot.Instance : FindFirstObjectByType<UIRoot>(FindObjectsInactive.Include);
            var parent = uiRoot != null && uiRoot.ModalCanvas != null ? uiRoot.ModalCanvas.transform : manager.transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var modalGo = new GameObject("QuestGiverModal", typeof(RectTransform), typeof(CanvasGroup));
            var modalRect = modalGo.GetComponent<RectTransform>();
            modalRect.SetParent(parent, false);
            StretchRect(modalRect);

            var modal = modalGo.AddComponent<QuestGiverModal>();
            modal.uiManager = manager;

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.SetParent(modalGo.transform, false);
            StretchRect(backdropRect);
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.12f);
            var backdropButton = backdrop.GetComponent<Button>();
            backdropButton.targetGraphic = backdropImage;
            backdropButton.onClick.AddListener(modal.HandleBackgroundClick);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(modalGo.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(-220f, 120f);
            panelRect.sizeDelta = new Vector2(440f, 500f);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.11f, 0.14f, 0.2f, 0.96f);

            var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(14, 14, 14, 14);
            panelLayout.spacing = 8f;
            panelLayout.childAlignment = TextAnchor.UpperLeft;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.childForceExpandWidth = true;

            var header = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            header.transform.SetParent(panel.transform, false);
            var headerImage = header.GetComponent<Image>();
            headerImage.color = new Color(0.08f, 0.12f, 0.2f, 0.92f);
            var headerLayout = header.GetComponent<VerticalLayoutGroup>();
            headerLayout.padding = new RectOffset(12, 12, 10, 8);
            headerLayout.spacing = 2f;
            headerLayout.childAlignment = TextAnchor.UpperLeft;
            headerLayout.childControlHeight = true;
            headerLayout.childControlWidth = true;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childForceExpandWidth = true;
            var headerElement = header.GetComponent<LayoutElement>();
            headerElement.preferredHeight = 86f;

            var titleText = CreateRuntimeText(header.transform, "Title", "交互", font, 32, TextAnchor.MiddleLeft, Color.white, 46f);
            CreateRuntimeText(header.transform, "Subtitle", "按选项继续", font, 15, TextAnchor.MiddleLeft, new Color(0.74f, 0.79f, 0.86f, 1f), 24f);

            var summaryText = CreateRuntimeText(panel.transform, "Summary", string.Empty, font, 16, TextAnchor.UpperLeft, new Color(0.9f, 0.9f, 0.92f, 1f), 70f);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;

            var statusText = CreateRuntimeText(panel.transform, "Status", string.Empty, font, 15, TextAnchor.MiddleLeft, new Color(0.95f, 0.83f, 0.45f, 1f), 24f);

            var objectivesText = CreateRuntimeText(panel.transform, "Objectives", string.Empty, font, 14, TextAnchor.UpperLeft, Color.white, 90f);
            objectivesText.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectivesText.verticalOverflow = VerticalWrapMode.Overflow;
            var objectivesLayout = objectivesText.GetComponent<LayoutElement>();
            objectivesLayout.flexibleHeight = 1f;

            var rewardText = CreateRuntimeText(panel.transform, "Reward", string.Empty, font, 14, TextAnchor.MiddleLeft, new Color(0.72f, 0.95f, 0.78f, 1f), 22f);
            var feedbackText = CreateRuntimeText(panel.transform, "Feedback", string.Empty, font, 14, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.95f, 1f), 22f);

            CreateRuntimeText(panel.transform, "OptionsTitle", "可选操作", font, 15, TextAnchor.MiddleLeft, new Color(0.74f, 0.79f, 0.86f, 1f), 22f);

            var buttonsPanel = new GameObject("ButtonsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            buttonsPanel.transform.SetParent(panel.transform, false);
            var buttonsPanelImage = buttonsPanel.GetComponent<Image>();
            buttonsPanelImage.color = new Color(0.08f, 0.12f, 0.2f, 0.92f);
            var buttonsLayout = buttonsPanel.GetComponent<VerticalLayoutGroup>();
            buttonsLayout.padding = new RectOffset(10, 10, 10, 10);
            buttonsLayout.spacing = 8f;
            buttonsLayout.childAlignment = TextAnchor.UpperCenter;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childForceExpandHeight = false;
            buttonsLayout.childForceExpandWidth = true;
            var buttonsElement = buttonsPanel.GetComponent<LayoutElement>();
            buttonsElement.preferredHeight = 176f;

            var primaryButton = CreateRuntimeButton(buttonsPanel.transform, "Button_Primary", "接取任务", font, -1f);
            var primaryButtonText = primaryButton.GetComponentInChildren<Text>(true);
            var tradeButton = CreateRuntimeButton(buttonsPanel.transform, "Button_Trade", "交易", font, -1f);
            var closeButton = CreateRuntimeButton(buttonsPanel.transform, "Button_Close", "离开", font, -1f);

            CreateRuntimeText(panel.transform, "FooterHint", "E 选择  1/2/3 快捷选项  ESC 关闭", font, 13, TextAnchor.MiddleLeft, new Color(0.74f, 0.79f, 0.86f, 1f), 20f);

            modal.titleText = titleText;
            modal.summaryText = summaryText;
            modal.statusText = statusText;
            modal.objectivesText = objectivesText;
            modal.rewardText = rewardText;
            modal.feedbackText = feedbackText;
            modal.primaryButton = primaryButton;
            modal.primaryButtonText = primaryButtonText;
            modal.tradeButton = tradeButton;
            modal.closeButton = closeButton;
            modal.dialogPanel = panelRect;

            modalGo.SetActive(false);
            return modal;
        }

        private static Text CreateRuntimeText(
            Transform parent,
            string name,
            string content,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color,
            float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            return text;
        }

        private static Button CreateRuntimeButton(Transform parent, string name, string label, Font font, float preferredWidth)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.21f, 0.25f, 0.34f, 1f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var layout = go.GetComponent<LayoutElement>();
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
                layout.minWidth = Mathf.Max(120f, preferredWidth - 20f);
            }
            else
            {
                layout.flexibleWidth = 1f;
                layout.minWidth = 0f;
            }
            layout.preferredHeight = 46f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(go.transform, false);
            StretchRect(labelRect);

            var text = labelGo.GetComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }

        private static void StretchRect(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private static string BuildStatusText(QuestRuntimeState state)
        {
            if (state == null)
            {
                return "状态: 未接取";
            }

            switch (state.Status)
            {
                case QuestStatus.InProgress:
                    return "状态: 进行中";
                case QuestStatus.ReadyToTurnIn:
                    return "状态: 可提交";
                case QuestStatus.Completed:
                    return "状态: 已完成";
                default:
                    return "状态: 未接取";
            }
        }

        private static string BuildHintText(QuestRuntimeState state)
        {
            if (state == null)
            {
                return "可接取该任务，确认后会开始追踪。";
            }

            switch (state.Status)
            {
                case QuestStatus.ReadyToTurnIn:
                    return "目标已完成，点击“提交任务”领取奖励。";
                case QuestStatus.InProgress:
                    return "任务进行中，可按 J 打开任务日志查看详情。";
                case QuestStatus.Completed:
                    return "任务已完成，可继续交易或探索。";
                default:
                    return "可接取该任务，确认后会开始追踪。";
            }
        }

        private static string BuildPrimaryButtonText(QuestRuntimeState state)
        {
            if (state == null || state.Status == QuestStatus.NotAccepted)
            {
                return "接取任务";
            }

            switch (state.Status)
            {
                case QuestStatus.InProgress:
                    return "继续交谈";
                case QuestStatus.ReadyToTurnIn:
                    return "提交任务";
                default:
                    return string.Empty;
            }
        }

        private static string BuildObjectiveText(QuestDefinition quest, QuestRuntimeState state)
        {
            if (quest == null || quest.Objectives == null || quest.Objectives.Count <= 0)
            {
                return "目标: 无";
            }

            var builder = new StringBuilder(256);
            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                var objective = quest.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                var progress = state != null ? state.GetObjectiveProgress(i) : 0;
                var required = objective.RequiredAmount;
                var done = progress >= required;
                if (objective.HiddenUntilProgress && progress <= 0 && !done)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(done ? "[x] " : "[ ] ");
                builder.Append(string.IsNullOrWhiteSpace(objective.Description) ? objective.ObjectiveId : objective.Description);
                builder.Append(" (");
                builder.Append(Mathf.Min(progress, required));
                builder.Append('/');
                builder.Append(required);
                builder.Append(')');
            }

            return builder.Length > 0 ? builder.ToString() : "目标: 无";
        }

        private static string BuildRewardText(QuestRewardDefinition reward)
        {
            if (reward == null)
            {
                return "奖励: 无";
            }

            var builder = new StringBuilder(96);
            builder.Append("奖励: ");
            var hasAny = false;

            if (reward.Currency > 0)
            {
                builder.Append(reward.Currency);
                builder.Append("G");
                hasAny = true;
            }

            if (reward.Experience > 0)
            {
                if (hasAny)
                {
                    builder.Append("  ");
                }

                builder.Append("XP ");
                builder.Append(reward.Experience);
                hasAny = true;
            }

            if (reward.Items != null && reward.Items.Count > 0)
            {
                if (hasAny)
                {
                    builder.Append("  ");
                }

                builder.Append("物品 ");
                var appended = false;
                for (int i = 0; i < reward.Items.Count; i++)
                {
                    var entry = reward.Items[i];
                    if (entry == null || entry.Item == null)
                    {
                        continue;
                    }

                    if (appended)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(string.IsNullOrWhiteSpace(entry.Item.DisplayName) ? entry.Item.Id : entry.Item.DisplayName);
                    builder.Append(" x");
                    builder.Append(entry.Stack);
                    appended = true;
                }

                hasAny = hasAny || appended;
            }

            return hasAny ? builder.ToString() : "奖励: 无";
        }
    }
}
