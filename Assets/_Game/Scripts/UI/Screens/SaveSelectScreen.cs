using UnityEngine;

namespace CombatSystem.UI
{
    public class SaveSelectScreen : UIScreenBase
    {
        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase mainMenuScreen;
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

        public void BackToMenu()
        {
            if (uiManager != null && mainMenuScreen != null)
            {
                uiManager.ShowScreen(mainMenuScreen, true);
            }
        }

        public void StartGame()
        {
            if (uiManager != null && inGameScreen != null)
            {
                uiManager.ShowScreen(inGameScreen, true);
            }
        }
    }
}
