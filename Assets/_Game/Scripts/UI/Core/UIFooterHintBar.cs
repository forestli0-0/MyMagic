using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    [DisallowMultipleComponent]
    public class UIFooterHintBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text hintText;

        [Header("Defaults")]
        [SerializeField] private string gameplayMenuHint = "TAB 关闭菜单   ESC 返回游戏   ←/→ 切页   鼠标左键 选择";
        [SerializeField] private bool showGameplayHint = false;
        [SerializeField] private string gameplayHint = "WASD/右键 移动   E 交互   TAB 打开菜单   ESC 暂停";

        private UIScreenBase cachedScreen;
        private UIModalBase cachedModal;
        private UIInputMode cachedInputMode;
        private string cachedHint = string.Empty;

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
            if (!force &&
                currentModal == cachedModal &&
                currentScreen == cachedScreen &&
                currentInputMode == cachedInputMode)
            {
                return;
            }

            cachedModal = currentModal;
            cachedScreen = currentScreen;
            cachedInputMode = currentInputMode;

            var hint = ResolveHint(currentModal, currentScreen, currentInputMode);
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
                var screenHint = screen.GetFooterHintText();
                if (!string.IsNullOrWhiteSpace(screenHint))
                {
                    return screenHint;
                }

                if (GameplayMenuTabs.IsGameplayMenuScreen(screen))
                {
                    return gameplayMenuHint;
                }
            }

            if (showGameplayHint && inputMode == UIInputMode.Gameplay)
            {
                return gameplayHint;
            }

            return string.Empty;
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
