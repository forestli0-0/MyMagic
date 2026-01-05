using UnityEngine;

namespace CombatSystem.UI
{
    public class PauseMenuModal : UIModalBase
    {
        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase mainMenuScreen;
        [SerializeField] private UIModalBase settingsModal;

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
            if (uiManager != null && settingsModal != null)
            {
                uiManager.PushModal(settingsModal);
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
