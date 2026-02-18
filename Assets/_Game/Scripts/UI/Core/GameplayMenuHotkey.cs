using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.UI
{
    /// <summary>
    /// 游戏功能菜单热键：
    /// - Tab 打开/关闭角色-背包-任务页签菜单
    /// - Esc 在菜单打开时关闭菜单
    /// - 左右方向键在菜单内切换页签
    /// - 手柄 LB/RB 或十字键左右切页，B 关闭
    /// </summary>
    public class GameplayMenuHotkey : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase defaultMenuScreen;
        [SerializeField] private Key toggleKey = Key.Tab;
        [SerializeField] private Key closeKey = Key.Escape;
        [SerializeField] private Key previousTabKey = Key.LeftArrow;
        [SerializeField] private Key nextTabKey = Key.RightArrow;
        [SerializeField] private bool onlyWhenGameplayScreen = true;
        [SerializeField] private bool closeIfMenuAlreadyOpen = true;
        [SerializeField] private bool allowArrowTabSwitch = true;
        [SerializeField] private bool allowGamepadTabSwitch = true;
        [SerializeField] private bool allowGamepadClose = true;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (TryHandleArrowTabSwitch(keyboard) || TryHandleGamepadTabSwitch(gamepad))
            {
                return;
            }

            if (TryHandleCloseKey(keyboard) || TryHandleGamepadClose(gamepad))
            {
                return;
            }

            TryHandleToggleKey(keyboard);
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
                CloseGameplayMenu(current);
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

        private bool TryHandleToggleKey(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return false;
            }

            var toggle = keyboard[toggleKey];
            if (toggle == null || !toggle.wasPressedThisFrame)
            {
                return false;
            }

            ToggleMenu();
            return true;
        }

        private bool TryHandleCloseKey(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return false;
            }

            ResolveReferences();
            if (uiManager == null || uiManager.ModalCount > 0)
            {
                return false;
            }

            var close = keyboard[closeKey];
            if (close == null || !close.wasPressedThisFrame)
            {
                return false;
            }

            var current = uiManager.CurrentScreen;
            if (!GameplayMenuTabs.IsGameplayMenuScreen(current))
            {
                return false;
            }

            CloseGameplayMenu(current);
            return true;
        }

        private bool TryHandleArrowTabSwitch(Keyboard keyboard)
        {
            if (!allowArrowTabSwitch || keyboard == null)
            {
                return false;
            }

            ResolveReferences();
            if (uiManager == null || uiManager.ModalCount > 0)
            {
                return false;
            }

            var current = uiManager.CurrentScreen;
            if (!GameplayMenuTabs.IsGameplayMenuScreen(current))
            {
                return false;
            }

            var tabs = current.GetComponent<GameplayMenuTabs>();
            if (tabs == null)
            {
                return false;
            }

            var left = keyboard[previousTabKey];
            if (left != null && left.wasPressedThisFrame)
            {
                return tabs.OpenRelativeTab(-1);
            }

            var right = keyboard[nextTabKey];
            if (right != null && right.wasPressedThisFrame)
            {
                return tabs.OpenRelativeTab(1);
            }

            return false;
        }

        private bool TryHandleGamepadTabSwitch(Gamepad gamepad)
        {
            if (!allowGamepadTabSwitch || gamepad == null)
            {
                return false;
            }

            ResolveReferences();
            if (uiManager == null || uiManager.ModalCount > 0)
            {
                return false;
            }

            var current = uiManager.CurrentScreen;
            if (!GameplayMenuTabs.IsGameplayMenuScreen(current))
            {
                return false;
            }

            var tabs = current.GetComponent<GameplayMenuTabs>();
            if (tabs == null)
            {
                return false;
            }

            if (gamepad.leftShoulder.wasPressedThisFrame || gamepad.dpad.left.wasPressedThisFrame)
            {
                return tabs.OpenRelativeTab(-1);
            }

            if (gamepad.rightShoulder.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame)
            {
                return tabs.OpenRelativeTab(1);
            }

            return false;
        }

        private bool TryHandleGamepadClose(Gamepad gamepad)
        {
            if (!allowGamepadClose || gamepad == null)
            {
                return false;
            }

            ResolveReferences();
            if (uiManager == null || uiManager.ModalCount > 0)
            {
                return false;
            }

            if (!gamepad.buttonEast.wasPressedThisFrame)
            {
                return false;
            }

            var current = uiManager.CurrentScreen;
            if (!GameplayMenuTabs.IsGameplayMenuScreen(current))
            {
                return false;
            }

            CloseGameplayMenu(current);
            return true;
        }

        private void CloseGameplayMenu(UIScreenBase current)
        {
            if (uiManager == null)
            {
                return;
            }

            GameplayMenuTabs.CollapseGameplayMenuStack(uiManager);

            if (!GameplayMenuTabs.IsGameplayMenuScreen(uiManager.CurrentScreen))
            {
                return;
            }

            var tabs = current != null ? current.GetComponent<GameplayMenuTabs>() : null;
            var fallback = tabs != null ? tabs.ResolveFallbackGameplayScreen() : FindFallbackGameplayScreen(current);
            if (fallback != null && fallback != current)
            {
                uiManager.ShowScreen(fallback, true);
            }
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
