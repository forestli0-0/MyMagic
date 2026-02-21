using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    [DisallowMultipleComponent]
    public class UIFooterHintBar : MonoBehaviour
    {
        private const string LegacyGameplayMenuHint = "TAB 关闭菜单   ESC 返回游戏   ←/→ 切页   鼠标左键 选择";
        private const string LegacyGameplayHint = "WASD/右键 移动   E 交互   TAB 打开菜单   ESC 暂停";
        private const string GameplayMenuHintTemplate = "{MENU_CLOSE} 关闭菜单   {BACK} 返回游戏   {TAB_SWITCH} 切页   {CONFIRM} 选择";
        private const string GameplayHintTemplate = "{MOVE} 移动   {INTERACT} 交互   {MENU_TOGGLE} 打开菜单   {PAUSE} 暂停";

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text hintText;

        [Header("Defaults")]
        [SerializeField] private string gameplayMenuHint = GameplayMenuHintTemplate;
        [SerializeField] private bool showGameplayHint = false;
        [SerializeField] private string gameplayHint = GameplayHintTemplate;
        [SerializeField] private bool raiseForGameplayMenu = true;
        [SerializeField, Min(0f)] private float gameplayMenuYOffset = 70f;

        [Header("Input Prompt")]
        [SerializeField] private bool autoSwitchPromptByDevice = true;
        [SerializeField] private UIHintDeviceFamily forcedDeviceFamily = UIHintDeviceFamily.KeyboardMouse;

        private UIScreenBase cachedScreen;
        private UIModalBase cachedModal;
        private UIInputMode cachedInputMode;
        private UIHintDeviceFamily cachedDeviceFamily = UIHintDeviceFamily.KeyboardMouse;
        private string cachedHint = string.Empty;
        private RectTransform selfRect;
        private Vector2 baseOffsetMin;
        private Vector2 baseOffsetMax;
        private bool hasBaseOffsets;

        private void Awake()
        {
            EnsureReferences();
            ApplyThemeColors();
            RefreshNow(true);
        }

        private void OnEnable()
        {
            UIThemeRuntime.ThemeChanged += HandleThemeChanged;
            EnsureReferences();
            ApplyThemeColors();
            RefreshNow(true);
        }

        private void OnDisable()
        {
            UIThemeRuntime.ThemeChanged -= HandleThemeChanged;
        }

        private void Update()
        {
            RefreshNow(false);
        }

        private void EnsureReferences()
        {
            if (uiManager == null)
            {
                uiManager = GetComponentInParent<UIRoot>() != null
                    ? GetComponentInParent<UIRoot>().Manager
                    : FindFirstObjectByType<UIManager>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (selfRect == null)
            {
                selfRect = transform as RectTransform;
            }

            if (!hasBaseOffsets && selfRect != null)
            {
                baseOffsetMin = selfRect.offsetMin;
                baseOffsetMax = selfRect.offsetMax;
                hasBaseOffsets = true;
            }

            EnsureHintTemplates();
        }

        private void RefreshNow(bool force)
        {
            EnsureReferences();
            if (uiManager == null || hintText == null)
            {
                SetVisible(false);
                return;
            }

            var currentModal = uiManager.CurrentModal;
            var currentScreen = uiManager.CurrentScreen;
            var currentInputMode = uiManager.CurrentInputMode;
            var currentDeviceFamily = ResolveDeviceFamily();
            if (!force &&
                currentModal == cachedModal &&
                currentScreen == cachedScreen &&
                currentInputMode == cachedInputMode &&
                currentDeviceFamily == cachedDeviceFamily)
            {
                return;
            }

            cachedModal = currentModal;
            cachedScreen = currentScreen;
            cachedInputMode = currentInputMode;
            cachedDeviceFamily = currentDeviceFamily;
            ApplyLayoutOffset(currentModal, currentScreen);

            var rawHint = ResolveHint(currentModal, currentScreen, currentInputMode);
            var hint = UIInputPromptFormatter.Format(rawHint, currentDeviceFamily);
            if (!force && string.Equals(cachedHint, hint, System.StringComparison.Ordinal))
            {
                SetVisible(!string.IsNullOrWhiteSpace(hint));
                return;
            }

            cachedHint = hint;
            hintText.text = hint;
            SetVisible(!string.IsNullOrWhiteSpace(hint));
        }

        private string ResolveHint(UIModalBase modal, UIScreenBase screen, UIInputMode inputMode)
        {
            if (modal != null)
            {
                if (modal.HideGlobalFooterHint)
                {
                    return string.Empty;
                }

                var modalHint = modal.GetFooterHintText();
                if (!string.IsNullOrWhiteSpace(modalHint))
                {
                    return modalHint;
                }
            }

            if (screen != null)
            {
                if (GameplayMenuTabs.IsGameplayMenuScreen(screen))
                {
                    return string.Empty;
                }

                var screenHint = screen.GetFooterHintText();
                if (!string.IsNullOrWhiteSpace(screenHint))
                {
                    return screenHint;
                }
            }

            if (showGameplayHint && inputMode == UIInputMode.Gameplay)
            {
                return gameplayHint;
            }

            return string.Empty;
        }

        private UIHintDeviceFamily ResolveDeviceFamily()
        {
            if (!autoSwitchPromptByDevice)
            {
                return forcedDeviceFamily;
            }

            return UIInputPromptFormatter.ResolveCurrentDeviceFamily(cachedDeviceFamily);
        }

        private void EnsureHintTemplates()
        {
            if (string.IsNullOrWhiteSpace(gameplayMenuHint) || string.Equals(gameplayMenuHint, LegacyGameplayMenuHint, System.StringComparison.Ordinal))
            {
                gameplayMenuHint = GameplayMenuHintTemplate;
            }

            if (string.IsNullOrWhiteSpace(gameplayHint) || string.Equals(gameplayHint, LegacyGameplayHint, System.StringComparison.Ordinal))
            {
                gameplayHint = GameplayHintTemplate;
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void ApplyLayoutOffset(UIModalBase modal, UIScreenBase screen)
        {
            if (!hasBaseOffsets || selfRect == null)
            {
                return;
            }

            var shouldRaise = raiseForGameplayMenu && modal == null && GameplayMenuTabs.IsGameplayMenuScreen(screen);
            var yOffset = shouldRaise ? Mathf.Max(0f, gameplayMenuYOffset) : 0f;

            var targetMin = new Vector2(baseOffsetMin.x, baseOffsetMin.y + yOffset);
            var targetMax = new Vector2(baseOffsetMax.x, baseOffsetMax.y + yOffset);

            if (selfRect.offsetMin != targetMin)
            {
                selfRect.offsetMin = targetMin;
            }

            if (selfRect.offsetMax != targetMax)
            {
                selfRect.offsetMax = targetMax;
            }
        }

        private void HandleThemeChanged(UIThemeConfig theme)
        {
            ApplyThemeColors();
        }

        private void ApplyThemeColors()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = UIStyleKit.FooterHintBackgroundColor;
            }

            if (hintText != null)
            {
                hintText.color = UIStyleKit.FooterHintTextColor;
            }
        }
    }
}
