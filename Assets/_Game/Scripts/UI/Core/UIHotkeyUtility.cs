using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// UI 热键脚本共享辅助方法，集中处理常见查找与兜底逻辑。
    /// </summary>
    public static class UIHotkeyUtility
    {
        public static UIManager ResolveUiManager(Component owner, UIManager current)
        {
            if (current != null)
            {
                return current;
            }

            var resolved = owner != null ? owner.GetComponentInParent<UIManager>() : null;
            if (resolved == null)
            {
                resolved = Object.FindFirstObjectByType<UIManager>();
            }

            return resolved;
        }

        public static UIScreenBase FindFallbackGameplayScreen(UIScreenBase excludeScreen, bool excludeGameplayMenuScreens)
        {
            var inGame = Object.FindFirstObjectByType<InGameScreen>(FindObjectsInactive.Include);
            if (inGame != null && inGame != excludeScreen)
            {
                if (!excludeGameplayMenuScreens || !GameplayMenuTabs.IsGameplayMenuScreen(inGame))
                {
                    return inGame;
                }
            }

            var screens = Object.FindObjectsByType<UIScreenBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                if (screen == null || screen == excludeScreen)
                {
                    continue;
                }

                if (excludeGameplayMenuScreens && GameplayMenuTabs.IsGameplayMenuScreen(screen))
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
