using System;
using CombatSystem.Gameplay;
using CombatSystem.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class PauseMenuModal : UIModalBase
    {
        private const string LegacyQuestButtonPath = "Panel/Button_Quests";

        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private UIScreenBase mainMenuScreen;
        [SerializeField] private UIModalBase settingsModal;
        [SerializeField] private UIScreenBase settingsScreen;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Save")]
        [SerializeField] private SaveGameManager saveManager;

        private void OnEnable()
        {
            RemoveLegacyQuestButtonIfPresent();
            EnsureNavigationBindings();
        }

        private void Reset()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        public void Resume()
        {
            RequestClose();
        }

        public void OpenSettings()
        {
            if (uiManager == null)
            {
                return;
            }

            if (settingsModal != null)
            {
                uiManager.PushModal(settingsModal);
                return;
            }

            if (settingsScreen != null)
            {
                uiManager.CloseAllModals();

                if (settingsScreen is SettingsScreen screen)
                {
                    screen.RequestPauseGameplay(true);
                    screen.RequestStackBack();
                    screen.RequestReturnToPauseMenu(true);
                }

                uiManager.PushScreen(settingsScreen);
            }
        }

        public void SaveGame()
        {
            if (saveManager == null)
            {
                saveManager = FindFirstObjectByType<SaveGameManager>();
            }

            if (saveManager == null)
            {
                UIToast.Warning("保存失败：未找到存档服务。");
                return;
            }

            if (!saveManager.SaveCurrent())
            {
                var created = saveManager.SaveCurrentOrNew(null);
                var displayName = created != null && !string.IsNullOrWhiteSpace(created.displayName)
                    ? created.displayName
                    : "快速存档";
                UIToast.Success($"已创建存档：{displayName}");
                return;
            }

            UIToast.Success("保存成功。");
        }

        public void BackToMenu()
        {
            if (uiManager != null)
            {
                uiManager.CloseAllModals();
            }

            if (Time.timeScale != 1f)
            {
                Time.timeScale = 1f;
            }

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                SceneManager.sceneLoaded -= HandleMainMenuLoaded;
                SceneManager.sceneLoaded += HandleMainMenuLoaded;
                SceneManager.LoadScene(mainMenuSceneName);
                return;
            }

            if (uiManager != null && mainMenuScreen != null)
            {
                uiManager.ShowScreen(mainMenuScreen, true);
            }
        }

        private void HandleMainMenuLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, mainMenuSceneName, StringComparison.Ordinal))
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleMainMenuLoaded;

            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            var targetScreen = mainMenuScreen;
            if (targetScreen == null)
            {
                var screens = Resources.FindObjectsOfTypeAll<MainMenuScreen>();
                if (screens != null && screens.Length > 0)
                {
                    targetScreen = screens[0];
                }
            }

            if (uiManager != null)
            {
                uiManager.CloseAllModals();
                if (targetScreen != null)
                {
                    uiManager.ShowScreen(targetScreen, true);
                }

                uiManager.SetHudVisible(false);
            }
        }

        private void RemoveLegacyQuestButtonIfPresent()
        {
            var legacyQuestButton = transform.Find(LegacyQuestButtonPath);
            if (legacyQuestButton == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(legacyQuestButton.gameObject);
            }
#if UNITY_EDITOR
            else
            {
                DestroyImmediate(legacyQuestButton.gameObject);
            }
#endif
        }

        public override string GetFooterHintText()
        {
            return "{PAUSE} 继续游戏    {NAV_VERTICAL} 切换选项    {CONFIRM} 选择";
        }

        public override void OnFocus()
        {
            EnsureNavigationBindings();
            FocusDefaultSelectable();
        }

        public override bool FocusDefaultSelectable()
        {
            EnsureNavigationBindings();

            var preferred = resumeButton != null && resumeButton.IsActive() && resumeButton.IsInteractable()
                ? resumeButton
                : GetFirstActionButton();
            return UIFocusUtility.FocusDefault(preferred, this);
        }

        private void EnsureNavigationBindings()
        {
            if (resumeButton == null)
            {
                resumeButton = FindButton("Button_继续游戏") ?? FindButton("Button_Resume");
            }

            if (saveButton == null)
            {
                saveButton = FindButton("Button_保存游戏") ?? FindButton("Button_SaveGame");
            }

            if (settingsButton == null)
            {
                settingsButton = FindButton("Button_设置") ?? FindButton("Button_Settings");
            }

            if (mainMenuButton == null)
            {
                mainMenuButton = FindButton("Button_返回主菜单") ?? FindButton("Button_MainMenu");
            }

            var buttons = new[] { resumeButton, saveButton, settingsButton, mainMenuButton };
            var validCount = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].IsActive() && buttons[i].IsInteractable())
                {
                    validCount++;
                }
            }

            if (validCount <= 1)
            {
                return;
            }

            var ordered = new Button[validCount];
            var write = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null || !button.IsActive() || !button.IsInteractable())
                {
                    continue;
                }

                ordered[write++] = button;
            }

            for (int i = 0; i < ordered.Length; i++)
            {
                var current = ordered[i];
                var up = ordered[(i - 1 + ordered.Length) % ordered.Length];
                var down = ordered[(i + 1) % ordered.Length];

                var navigation = current.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = up;
                navigation.selectOnDown = down;
                navigation.selectOnLeft = current;
                navigation.selectOnRight = current;
                current.navigation = navigation;
            }
        }

        private Button GetFirstActionButton()
        {
            var buttons = GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null || !button.IsActive() || !button.IsInteractable())
                {
                    continue;
                }

                var name = button.name;
                if (!string.IsNullOrEmpty(name) && name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                return button;
            }

            return null;
        }

        private Button FindButton(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var buttons = GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                if (string.Equals(button.name, name, StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 玩家死亡弹窗：提供“复活 / 返回主菜单”入口。
    /// </summary>
    public class PlayerDeathModal : UIModalBase
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private Button respawnButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        private PlayerDeathFlowController flowController;

        public void Bind(PlayerDeathFlowController controller)
        {
            flowController = controller;
            EnsureReferences();
        }

        public override void OnEnter()
        {
            EnsureReferences();
            ApplyText();
        }

        public override void OnFocus()
        {
            EnsureReferences();
            FocusDefaultSelectable();
        }

        public override bool FocusDefaultSelectable()
        {
            EnsureReferences();
            var preferred = respawnButton != null && respawnButton.IsActive() && respawnButton.IsInteractable()
                ? respawnButton
                : mainMenuButton;
            return UIFocusUtility.FocusDefault(preferred, this);
        }

        public override string GetFooterHintText()
        {
            return "{NAV_VERTICAL} 切换选项    {CONFIRM} 确认    {BACK} 返回主菜单";
        }

        public void Respawn()
        {
            flowController?.Respawn();
        }

        public void BackToMainMenu()
        {
            flowController?.BackToMainMenu();
        }

        private void Update()
        {
            if (uiManager == null || uiManager.CurrentModal != this)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                BackToMainMenu();
                return;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
            {
                BackToMainMenu();
            }
        }

        private void EnsureReferences()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (respawnButton == null)
            {
                respawnButton = FindButton("Button_Respawn") ?? FindButton("Button_复活");
            }

            if (mainMenuButton == null)
            {
                mainMenuButton = FindButton("Button_MainMenu") ?? FindButton("Button_返回主菜单");
            }

            if (titleText == null)
            {
                titleText = FindText("TitleText");
            }

            if (bodyText == null)
            {
                bodyText = FindText("BodyText");
            }

            if (respawnButton != null)
            {
                respawnButton.onClick.RemoveListener(Respawn);
                respawnButton.onClick.AddListener(Respawn);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(BackToMainMenu);
                mainMenuButton.onClick.AddListener(BackToMainMenu);
            }

            if (respawnButton != null && mainMenuButton != null)
            {
                var respawnNav = respawnButton.navigation;
                respawnNav.mode = Navigation.Mode.Explicit;
                respawnNav.selectOnDown = mainMenuButton;
                respawnNav.selectOnUp = mainMenuButton;
                respawnNav.selectOnLeft = respawnButton;
                respawnNav.selectOnRight = respawnButton;
                respawnButton.navigation = respawnNav;

                var menuNav = mainMenuButton.navigation;
                menuNav.mode = Navigation.Mode.Explicit;
                menuNav.selectOnUp = respawnButton;
                menuNav.selectOnDown = respawnButton;
                menuNav.selectOnLeft = mainMenuButton;
                menuNav.selectOnRight = mainMenuButton;
                mainMenuButton.navigation = menuNav;
            }
        }

        private void ApplyText()
        {
            if (titleText != null)
            {
                titleText.text = "你已阵亡";
                titleText.color = UIStyleKit.TabActiveTextColor;
            }

            if (bodyText != null)
            {
                bodyText.text = "请选择：复活回到当前出生点，或返回主菜单。";
                bodyText.color = UIStyleKit.TabInactiveTextColor;
            }
        }

        private Button FindButton(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button != null && string.Equals(button.name, objectName, StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }

        private Text FindText(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text != null && string.Equals(text.name, objectName, StringComparison.Ordinal))
                {
                    return text;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 运行时创建玩家死亡弹窗，避免必须手动搭场景 UI。
    /// </summary>
    public static class PlayerDeathModalFactory
    {
        public static PlayerDeathModal Create(Canvas modalCanvas)
        {
            if (modalCanvas == null)
            {
                return null;
            }

            var existing = modalCanvas.GetComponentInChildren<PlayerDeathModal>(true);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("PlayerDeathModal", typeof(RectTransform), typeof(CanvasGroup), typeof(PlayerDeathModal));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(modalCanvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var dim = CreateImage(root.transform, "Background", UIStyleKit.GameplayOverlayColor);
            dim.raycastTarget = true;
            var dimRect = dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            var panel = CreateImage(root.transform, "Panel", UIStyleKit.GameplayPanelColor);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 320f);
            panelRect.anchoredPosition = new Vector2(0f, 12f);

            var title = CreateText(panel.transform, "TitleText", "你已阵亡", 52f);
            title.fontSize = 42;
            title.alignment = TextAnchor.MiddleCenter;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.08f, 0.72f);
            titleRect.anchorMax = new Vector2(0.92f, 0.94f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var body = CreateText(panel.transform, "BodyText", "请选择后续操作。", 24f);
            body.fontSize = 22;
            body.alignment = TextAnchor.UpperCenter;
            body.color = UIStyleKit.TabInactiveTextColor;
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0.1f, 0.44f);
            bodyRect.anchorMax = new Vector2(0.9f, 0.72f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;

            var respawnButton = CreateButton(panel.transform, "Button_复活", "复活");
            var respawnRect = respawnButton.GetComponent<RectTransform>();
            respawnRect.anchorMin = new Vector2(0.1f, 0.14f);
            respawnRect.anchorMax = new Vector2(0.44f, 0.3f);
            respawnRect.offsetMin = Vector2.zero;
            respawnRect.offsetMax = Vector2.zero;

            var mainMenuButton = CreateButton(panel.transform, "Button_返回主菜单", "返回主菜单");
            var menuRect = mainMenuButton.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.56f, 0.14f);
            menuRect.anchorMax = new Vector2(0.9f, 0.3f);
            menuRect.offsetMin = Vector2.zero;
            menuRect.offsetMax = Vector2.zero;

            var modal = root.GetComponent<PlayerDeathModal>();
            modal.gameObject.SetActive(false);
            return modal;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = UIStyleKit.ThemeSprite;
            image.type = Image.Type.Sliced;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string content, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(0f, height);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = UIStyleKit.ThemeFont;
            text.color = UIStyleKit.TabActiveTextColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var image = CreateImage(parent, name, UIStyleKit.TabInactiveColor);
            var button = image.gameObject.AddComponent<Button>();
            UIStyleKit.ApplyButtonStateColors(button, UIStyleKit.TabInactiveColor, 0.08f, 0.16f, 0.5f, 0.08f);

            var text = CreateText(image.transform, "Text", label, 0f);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 28;
            text.color = UIStyleKit.TabActiveTextColor;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }
    }
}
