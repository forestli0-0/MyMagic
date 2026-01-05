using UnityEngine;

namespace CombatSystem.UI
{
    public class MainMenuScreen : UIScreenBase
    {
        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase saveSelectScreen;
        [SerializeField] private UIScreenBase settingsScreen;
        [SerializeField] private UIScreenBase inGameScreen;

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        public override void OnEnter()
        {
            if (uiManager != null)
            {
                uiManager.SetHudVisible(false);
            }
        }

        public void OpenSaveSelect()
        {
            if (uiManager != null && saveSelectScreen != null)
            {
                uiManager.ShowScreen(saveSelectScreen, true);
            }
        }

        public void OpenSettings()
        {
            if (uiManager != null && settingsScreen != null)
            {
                uiManager.ShowScreen(settingsScreen, false);
            }
        }

        public void StartNewGame()
        {
            if (uiManager != null && inGameScreen != null)
            {
                uiManager.ShowScreen(inGameScreen, true);
            }
        }

        public void ContinueGame()
        {
            StartNewGame();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
