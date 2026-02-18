using System;
using CombatSystem.Persistence;
using UnityEngine;
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
                return;
            }

            if (!saveManager.SaveCurrent())
            {
                saveManager.SaveCurrentOrNew(null);
            }
        }

        // Legacy compatibility hook: quest journal moved out of pause menu.
        public void OpenQuestJournal()
        {
            RequestClose();
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
            return "ESC 继续游戏    ↑↓ 切换选项    Enter 选择";
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
}
