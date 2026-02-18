using CombatSystem.Gameplay;
using CombatSystem.Persistence;
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

        [Header("Save")]
        [SerializeField] private SaveGameManager saveManager;

        [Header("Flow")]
        [SerializeField] private LevelFlowController levelFlow;

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (saveManager == null)
            {
                saveManager = FindFirstObjectByType<SaveGameManager>();
            }

            if (levelFlow == null)
            {
                levelFlow = FindFirstObjectByType<LevelFlowController>();
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
            if (levelFlow == null)
            {
                levelFlow = FindFirstObjectByType<LevelFlowController>();
            }

            if (levelFlow != null)
            {
                levelFlow.StartNewGame();
                return;
            }

            if (uiManager != null && inGameScreen != null)
            {
                uiManager.ShowScreen(inGameScreen, true);
            }
        }

        public void ContinueGame()
        {
            if (saveManager == null)
            {
                saveManager = FindFirstObjectByType<SaveGameManager>();
            }

            if (saveManager != null && saveManager.TryLoadLatest())
            {
                return;
            }

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

        public override string GetFooterHintText()
        {
            return "鼠标左键 / Enter 选择    ESC 返回上一页";
        }
    }
}
