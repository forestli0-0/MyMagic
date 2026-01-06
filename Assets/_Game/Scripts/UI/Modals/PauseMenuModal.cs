using CombatSystem.Persistence;
using UnityEngine;

namespace CombatSystem.UI
{
    public class PauseMenuModal : UIModalBase
    {
        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase mainMenuScreen;
        [SerializeField] private UIModalBase settingsModal;
        [SerializeField] private UIScreenBase settingsScreen;

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

        public void BackToMenu()
        {
            if (uiManager != null && mainMenuScreen != null)
            {
                uiManager.CloseAllModals();
                uiManager.ShowScreen(mainMenuScreen, true);
            }
        }
    }
}
