using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.UI
{
    /// <summary>
    /// 游戏功能菜单热键（默认 Tab）：打开/关闭角色-背包-任务页签菜单。
    /// </summary>
    public class GameplayMenuHotkey : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase defaultMenuScreen;
        [SerializeField] private Key toggleKey = Key.Tab;
        [SerializeField] private bool onlyWhenGameplayScreen = true;
        [SerializeField] private bool closeIfMenuAlreadyOpen = true;

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

            ToggleMenu();
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

            if (defaultMenuScreen == null)
            {
                defaultMenuScreen = FindFirstObjectByType<InventoryScreen>(FindObjectsInactive.Include);
                if (defaultMenuScreen == null)
                {
                    defaultMenuScreen = FindFirstObjectByType<CharacterScreen>(FindObjectsInactive.Include);
                }
            }
        }

        private void ToggleMenu()
        {
            ResolveReferences();
            if (uiManager == null)
            {
                return;
            }

            if (uiManager.ModalCount > 0)
            {
                return;
            }

            var current = uiManager.CurrentScreen;
            if (closeIfMenuAlreadyOpen && GameplayMenuTabs.IsGameplayMenuScreen(current))
            {
                GameplayMenuTabs.CollapseGameplayMenuStack(uiManager);

                if (GameplayMenuTabs.IsGameplayMenuScreen(uiManager.CurrentScreen))
                {
                    var tabs = current.GetComponent<GameplayMenuTabs>();
                    var fallback = tabs != null ? tabs.ResolveFallbackGameplayScreen() : FindFallbackGameplayScreen(current);
                    if (fallback != null && fallback != current)
                    {
                        uiManager.ShowScreen(fallback, true);
                    }
                }

                return;
            }

            if (onlyWhenGameplayScreen && current != null && current.InputMode != UIInputMode.Gameplay)
            {
                return;
            }

            if (defaultMenuScreen == null)
            {
                return;
            }

            uiManager.PushScreen(defaultMenuScreen);
        }

        private static UIScreenBase FindFallbackGameplayScreen(UIScreenBase excludeScreen)
        {
            var inGame = FindFirstObjectByType<InGameScreen>(FindObjectsInactive.Include);
            if (inGame != null && inGame != excludeScreen)
            {
                return inGame;
            }

            var screens = FindObjectsByType<UIScreenBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                if (screen == null || screen == excludeScreen || GameplayMenuTabs.IsGameplayMenuScreen(screen))
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
