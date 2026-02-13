using System;
using CombatSystem.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.UI
{
    public class PauseMenuModal : UIModalBase
    {
        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase mainMenuScreen;
        [SerializeField] private UIModalBase settingsModal;
        [SerializeField] private UIScreenBase settingsScreen;
        [SerializeField] private UIScreenBase questJournalScreen;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Save")]
        [SerializeField] private SaveGameManager saveManager;

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

        public void OpenQuestJournal()
        {
            if (uiManager == null || questJournalScreen == null)
            {
                return;
            }

            uiManager.CloseAllModals();
            uiManager.PushScreen(questJournalScreen);
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
    }
}
