using System;
using System.Collections.Generic;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 通用 NPC 交互菜单：用于承载对话、任务、交易和自定义交互选项。
    /// </summary>
    public class NpcInteractionMenuModal : UIModalBase
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;

        [Header("Widgets")]
        [SerializeField] private Text npcNameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private RectTransform optionsRoot;
        [SerializeField] private Button optionTemplate;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform dialogPanel;

        [Header("Anchor")]
        [SerializeField] private bool followNpcAnchor = true;
        [SerializeField] private Vector3 worldAnchorOffset = Vector3.zero;
        [SerializeField] private Vector2 popupScreenOffset = new Vector2(36f, 18f);
        [SerializeField] private float popupEdgePadding = 18f;

        private NpcInteractionTrigger source;
        private bool subscribed;
        private RectTransform modalRootRect;
        private Camera cachedPresentationCamera;
        private float nextCameraResolveTime;
        private readonly List<Button> optionButtons = new List<Button>(8);
        private readonly List<Action> optionActions = new List<Action>(8);
        private readonly List<NpcInteractionTrigger.InteractionOptionView> optionViews = new List<NpcInteractionTrigger.InteractionOptionView>(8);

        public override bool HideGlobalFooterHint => true;

        public void Bind(NpcInteractionTrigger trigger)
        {
            source = trigger;
            RefreshFromSource();
            UpdateDialogAnchor(true);
        }

        public override void OnEnter()
        {
            EnsureEventSystemInputReady();
            CacheLayoutReferences();
            Subscribe();
            RefreshFromSource();
            UpdateDialogAnchor(true);
            FocusDefaultOption();
        }

        public override void OnExit()
        {
            Unsubscribe();
            source = null;
            optionActions.Clear();
            optionViews.Clear();
        }

        public override void OnFocus()
        {
            EnsureEventSystemInputReady();
            RefreshFromSource();
            UpdateDialogAnchor(true);
            FocusDefaultOption();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
        }

        private void LateUpdate()
        {
            UpdateDialogAnchor(false);
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

            var directIndex = ResolveShortcutIndex(keyboard);
            if (directIndex >= 0 && TryInvokeOption(directIndex))
            {
                return;
            }

            if (!keyboard.eKey.wasPressedThisFrame && !keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                var selected = eventSystem.currentSelectedGameObject;
                if (selected != null)
                {
                    for (int i = 0; i < optionButtons.Count; i++)
                    {
                        var button = optionButtons[i];
                        if (button == null || !button.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        if (selected == button.gameObject || selected.transform.IsChildOf(button.transform))
                        {
                            if (TryInvokeOption(i))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            TryInvokeOption(0);
        }

        private static int ResolveShortcutIndex(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return -1;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame) return 4;
            if (keyboard.digit6Key.wasPressedThisFrame) return 5;
            if (keyboard.digit7Key.wasPressedThisFrame) return 6;
            if (keyboard.digit8Key.wasPressedThisFrame) return 7;
            if (keyboard.digit9Key.wasPressedThisFrame) return 8;
            return -1;
        }

        private void RefreshFromSource()
        {
            if (source == null)
            {
                ApplyEmpty();
                return;
            }

            if (npcNameText != null)
            {
                npcNameText.text = source.DisplayName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = source.Greeting;
            }

            BuildOptionList();
        }

        private void ApplyEmpty()
        {
            if (npcNameText != null)
            {
                npcNameText.text = "NPC";
            }

            if (descriptionText != null)
            {
                descriptionText.text = "暂无可用交互。";
            }

            BuildOptionList();
        }

        private void BuildOptionList()
        {
            optionActions.Clear();
            optionViews.Clear();
            EnsureOptionTemplate();

            if (source != null)
            {
                source.BuildInteractionOptions(optionViews);
                for (int i = 0; i < optionViews.Count; i++)
                {
                    var localIndex = i;
                    var option = optionViews[i];
                    AddOption(option.Label, option.Interactable, option.DisabledReason, () => InvokeOptionFromSource(localIndex));
                }
            }

            if (optionActions.Count <= 0)
            {
                AddOption("离开", true, string.Empty, RequestClose);
            }

            UpdateOptionButtons();

            if (closeButton != null)
            {
                var showFallbackClose = CountActiveOptionButtons() == 0;
                closeButton.gameObject.SetActive(showFallbackClose);
                closeButton.interactable = true;
                SetButtonLabel(closeButton, "离开");
            }

            FocusDefaultOption();
        }

        private void AddOption(string label, bool interactable, string disabledReason, Action action)
        {
            var safeLabel = string.IsNullOrWhiteSpace(label) ? "选项" : label;
            optionActions.Add(action);

            EnsureOptionButton(optionActions.Count - 1, safeLabel, interactable, disabledReason);
        }

        private void EnsureOptionButton(int index, string label, bool interactable, string disabledReason)
        {
            while (optionButtons.Count <= index)
            {
                optionButtons.Add(CreateOptionButton(optionButtons.Count));
            }

            var button = optionButtons[index];
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(true);
            button.interactable = interactable;
            SetButtonLabel(button, $"{index + 1}. {label}");
            SetButtonState(button, interactable, disabledReason);
            button.onClick.RemoveAllListeners();
            var localIndex = index;
            button.onClick.AddListener(() => TryInvokeOption(localIndex));
        }

        private void UpdateOptionButtons()
        {
            for (int i = optionActions.Count; i < optionButtons.Count; i++)
            {
                var button = optionButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.gameObject.SetActive(false);
            }
        }

        private Button CreateOptionButton(int index)
        {
            EnsureOptionTemplate();
            if (optionTemplate == null)
            {
                return null;
            }

            Button button;
            if (index == 0)
            {
                button = optionTemplate;
            }
            else
            {
                button = Instantiate(optionTemplate, optionsRoot);
                button.name = "Option_" + (index + 1);
            }

            button.gameObject.SetActive(false);
            return button;
        }

        private void InvokeOptionFromSource(int optionIndex)
        {
            if (source == null)
            {
                RequestClose();
                return;
            }

            if (!source.InvokeInteractionOption(optionIndex, out var feedback, out var closeMenu))
            {
                ApplyFeedback(feedback);
                return;
            }

            ApplyFeedback(feedback);

            if (closeMenu)
            {
                CloseSelfIfNeeded();
                return;
            }

            BuildOptionList();
            FocusDefaultOption();
        }

        private void CloseSelfIfNeeded()
        {
            if (uiManager != null && uiManager.CurrentModal == this)
            {
                uiManager.CloseModal(this);
                return;
            }

            RequestClose();
        }

        private bool TryInvokeOption(int index)
        {
            if (index < 0 || index >= optionActions.Count)
            {
                return false;
            }

            if (index >= optionButtons.Count)
            {
                return false;
            }

            var button = optionButtons[index];
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
            {
                return false;
            }

            optionActions[index]?.Invoke();
            return true;
        }

        private int CountActiveOptionButtons()
        {
            var count = 0;
            for (int i = 0; i < optionButtons.Count; i++)
            {
                var button = optionButtons[i];
                if (button != null && button.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private void ApplyFeedback(string feedback)
        {
            if (descriptionText == null || string.IsNullOrWhiteSpace(feedback))
            {
                return;
            }

            descriptionText.text = feedback;
        }

        private static void SetButtonState(Button button, bool interactable, string disabledReason)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                if (!interactable && !string.IsNullOrWhiteSpace(disabledReason))
                {
                    text.text = text.text + " (不可用)";
                }

                text.color = interactable ? Color.white : new Color(0.65f, 0.69f, 0.74f, 1f);
            }

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = interactable
                    ? new Color(0.21f, 0.25f, 0.34f, 1f)
                    : new Color(0.16f, 0.19f, 0.26f, 0.92f);
            }
        }

        private void FocusDefaultOption()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            for (int i = 0; i < optionButtons.Count; i++)
            {
                var button = optionButtons[i];
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                {
                    continue;
                }

                eventSystem.SetSelectedGameObject(button.gameObject);
                return;
            }

            if (closeButton != null && closeButton.gameObject.activeInHierarchy && closeButton.interactable)
            {
                eventSystem.SetSelectedGameObject(closeButton.gameObject);
            }
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(RequestClose);
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(RequestClose);
            }

            subscribed = false;
        }

        private void EnsureOptionTemplate()
        {
            if (optionsRoot == null && optionTemplate != null)
            {
                optionsRoot = optionTemplate.transform.parent as RectTransform;
            }

            if (optionTemplate == null && optionsRoot != null)
            {
                optionTemplate = optionsRoot.GetComponentInChildren<Button>(true);
            }
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

        private static void TryEnableAction(InputActionReference reference)
        {
            var action = reference != null ? reference.action : null;
            if (action != null && !action.enabled)
            {
                action.Enable();
            }
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

            var worldAnchor = source.MenuAnchorWorldPosition + worldAnchorOffset;
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

        public static NpcInteractionMenuModal EnsureRuntimeModal(UIManager manager)
        {
            if (manager == null)
            {
                return null;
            }

            var existing = FindFirstObjectByType<NpcInteractionMenuModal>(FindObjectsInactive.Include);
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

            var modalGo = new GameObject("NpcInteractionMenuModal", typeof(RectTransform), typeof(CanvasGroup));
            var modalRect = modalGo.GetComponent<RectTransform>();
            modalRect.SetParent(parent, false);
            StretchRect(modalRect);

            var modal = modalGo.AddComponent<NpcInteractionMenuModal>();
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
            panelRect.sizeDelta = new Vector2(420f, 420f);

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

            var nameText = CreateRuntimeText(panel.transform, "NpcName", "NPC", font, 30, TextAnchor.MiddleLeft, Color.white, 44f);

            var descriptionText = CreateRuntimeText(panel.transform, "Description", "你好，想聊点什么？", font, 16, TextAnchor.UpperLeft, Color.white, 110f);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;

            var optionsPanel = new GameObject("OptionsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            optionsPanel.transform.SetParent(panel.transform, false);
            var optionsPanelImage = optionsPanel.GetComponent<Image>();
            optionsPanelImage.color = new Color(0.08f, 0.12f, 0.2f, 0.92f);
            var optionsLayout = optionsPanel.GetComponent<VerticalLayoutGroup>();
            optionsLayout.padding = new RectOffset(10, 10, 10, 10);
            optionsLayout.spacing = 8f;
            optionsLayout.childAlignment = TextAnchor.UpperCenter;
            optionsLayout.childControlHeight = true;
            optionsLayout.childControlWidth = true;
            optionsLayout.childForceExpandHeight = false;
            optionsLayout.childForceExpandWidth = true;
            var optionsElement = optionsPanel.GetComponent<LayoutElement>();
            optionsElement.preferredHeight = 186f;
            optionsElement.flexibleHeight = 1f;

            var optionTemplate = CreateRuntimeButton(optionsPanel.transform, "OptionTemplate", "1. 对话", font, -1f);
            optionTemplate.gameObject.SetActive(false);

            var closeButton = CreateRuntimeButton(panel.transform, "CloseButton", "离开", font, -1f);
            var closeLayout = closeButton.GetComponent<LayoutElement>();
            if (closeLayout != null)
            {
                closeLayout.preferredHeight = 44f;
                closeLayout.flexibleHeight = 0f;
            }

            CreateRuntimeText(panel.transform, "FooterHint", "E 选择  1-9 快捷选项  ESC 关闭", font, 13, TextAnchor.MiddleLeft, new Color(0.74f, 0.79f, 0.86f, 1f), 20f);

            modal.npcNameText = nameText;
            modal.descriptionText = descriptionText;
            modal.optionsRoot = optionsPanel.GetComponent<RectTransform>();
            modal.optionTemplate = optionTemplate;
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
    }
}
