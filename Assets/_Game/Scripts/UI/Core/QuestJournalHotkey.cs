using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.UI
{
    /// <summary>
    /// 任务日志热键（默认 J）：在游戏界面中打开/关闭任务日志界面。
    /// </summary>
    public class QuestJournalHotkey : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase questJournalScreen;
        [SerializeField] private Key toggleKey = Key.J;
        [SerializeField] private bool closeIfAlreadyOpen = true;
        [SerializeField] private bool onlyWhenGameplayScreen = true;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var key = keyboard[toggleKey];
            if (key == null || !key.wasPressedThisFrame)
            {
                return;
            }

            ToggleQuestJournal();
        }

        private void ResolveReferences()
        {
            if (uiManager == null)
            {
                uiManager = GetComponentInParent<UIManager>();
                if (uiManager == null)
                {
                    uiManager = FindFirstObjectByType<UIManager>();
                }
            }

            if (questJournalScreen == null)
            {
                questJournalScreen = FindFirstObjectByType<QuestJournalScreen>(FindObjectsInactive.Include);
            }
        }

        private void ToggleQuestJournal()
        {
            ResolveReferences();
            if (uiManager == null || questJournalScreen == null)
            {
                return;
            }

            if (uiManager.CurrentScreen == questJournalScreen)
            {
                if (closeIfAlreadyOpen)
                {
                    uiManager.PopScreen();
                }

                return;
            }

            if (uiManager.ModalCount > 0)
            {
                return;
            }

            // 兜底：若当前没有任何屏幕，先补一个 Gameplay 屏幕，
            // 避免 Journal 成为栈底导致 Back/Pop 无法返回。
            if (uiManager.CurrentScreen == null)
            {
                var fallbackGameplay = FindFallbackGameplayScreen();
                if (fallbackGameplay != null && fallbackGameplay != questJournalScreen)
                {
                    uiManager.ShowScreen(fallbackGameplay, true);
                }
            }

            if (onlyWhenGameplayScreen && uiManager.CurrentScreen != null && uiManager.CurrentScreen.InputMode != UIInputMode.Gameplay)
            {
                return;
            }

            uiManager.PushScreen(questJournalScreen);
        }

        private UIScreenBase FindFallbackGameplayScreen()
        {
            var inGame = FindFirstObjectByType<InGameScreen>(FindObjectsInactive.Include);
            if (inGame != null)
            {
                return inGame;
            }

            var screens = FindObjectsByType<UIScreenBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                if (screen == null || screen == questJournalScreen)
                {
                    continue;
                }

                if (screen.InputMode == UIInputMode.Gameplay)
                {
                    return screen;
                }
            }

            return null;
        }
    }
}
