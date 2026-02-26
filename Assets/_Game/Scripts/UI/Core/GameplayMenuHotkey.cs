using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CombatSystem.UI
{
    /// <summary>
    /// 游戏功能菜单热键：
    /// - Tab 打开/关闭角色-背包-任务页签菜单
    /// - 手柄 View 打开/关闭角色-背包-任务页签菜单
    /// - Esc 在菜单打开时关闭菜单
    /// - 左右方向键在菜单内切换页签
    /// - 手柄 LB/RB 或十字键左右切页，B 关闭
    /// </summary>
    public class GameplayMenuHotkey : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase defaultMenuScreen;
        [SerializeField] private UIScreenBase characterMenuScreen;
        [SerializeField] private UIScreenBase inventoryMenuScreen;
        [SerializeField] private UIScreenBase questMenuScreen;
        [SerializeField] private Key toggleKey = Key.Tab;
        [SerializeField] private Key closeKey = Key.Escape;
        [SerializeField] private Key previousTabKey = Key.LeftArrow;
        [SerializeField] private Key nextTabKey = Key.RightArrow;
        [SerializeField] private bool onlyWhenGameplayScreen = true;
        [SerializeField] private bool closeIfMenuAlreadyOpen = true;
        [SerializeField] private bool allowArrowTabSwitch = true;
        [SerializeField] private bool allowGamepadToggle = true;
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

            if (TryHandleToggleKey(keyboard))
            {
                return;
            }

            TryHandleGamepadToggle(gamepad);
        }

        private void ResolveReferences()
        {
            uiManager = UIHotkeyUtility.ResolveUiManager(this, uiManager);

            if (characterMenuScreen == null)
            {
                characterMenuScreen = FindFirstObjectByType<CharacterScreen>(FindObjectsInactive.Include);
            }

            if (inventoryMenuScreen == null)
            {
                inventoryMenuScreen = FindFirstObjectByType<InventoryScreen>(FindObjectsInactive.Include);
            }

            if (questMenuScreen == null)
            {
                questMenuScreen = FindFirstObjectByType<QuestJournalScreen>(FindObjectsInactive.Include);
            }

            if (defaultMenuScreen == null)
            {
                defaultMenuScreen = inventoryMenuScreen;
                if (defaultMenuScreen == null)
                {
                    defaultMenuScreen = characterMenuScreen;
                }

                if (defaultMenuScreen == null)
                {
                    defaultMenuScreen = questMenuScreen;
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

            var current = uiManager.CurrentScreen;
            if (closeIfMenuAlreadyOpen && GameplayMenuTabs.IsGameplayMenuScreen(current))
            {
                if (uiManager.ModalCount > 0)
                {
                    uiManager.CloseAllModals();
                }

                CloseGameplayMenu(current);
                return;
            }

            if (uiManager.ModalCount > 0)
            {
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

            if (current == null)
            {
                var fallback = UIHotkeyUtility.FindFallbackGameplayScreen(defaultMenuScreen, true);
                if (fallback != null && fallback != defaultMenuScreen)
                {
                    uiManager.ShowScreen(fallback, true);
                }
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
            var tabFallback = keyboard[Key.Tab];
            var togglePressed = (toggle != null && toggle.wasPressedThisFrame) ||
                                (tabFallback != null && tabFallback.wasPressedThisFrame);
            if (!togglePressed)
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
            if (uiManager == null)
            {
                return false;
            }

            var close = keyboard[closeKey];
            var escFallback = keyboard[Key.Escape];
            var closePressed = (close != null && close.wasPressedThisFrame) ||
                               (escFallback != null && escFallback.wasPressedThisFrame);
            if (!closePressed)
            {
                return false;
            }

            var current = uiManager.CurrentScreen;
            if (!GameplayMenuTabs.IsGameplayMenuScreen(current))
            {
                return false;
            }

            if (uiManager.ModalCount > 0)
            {
                uiManager.CloseAllModals();
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
                var fallbackDirection = ResolveTabSwitchDirection(
                    keyboard[previousTabKey] ?? keyboard[Key.LeftArrow],
                    keyboard[nextTabKey] ?? keyboard[Key.RightArrow]);
                return fallbackDirection != 0 && TryOpenRelativeTabFallback(current, fallbackDirection);
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
                var fallbackDirection = ResolveGamepadTabSwitchDirection(gamepad);
                return fallbackDirection != 0 && TryOpenRelativeTabFallback(current, fallbackDirection);
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
            if (uiManager == null)
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

            if (uiManager.ModalCount > 0)
            {
                uiManager.CloseAllModals();
            }

            CloseGameplayMenu(current);
            return true;
        }

        private bool TryHandleGamepadToggle(Gamepad gamepad)
        {
            if (!allowGamepadToggle || gamepad == null)
            {
                return false;
            }

            if (!gamepad.selectButton.wasPressedThisFrame)
            {
                return false;
            }

            ToggleMenu();
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
            var fallback = tabs != null
                ? tabs.ResolveFallbackGameplayScreen()
                : UIHotkeyUtility.FindFallbackGameplayScreen(current, true);
            if (fallback != null && fallback != current)
            {
                uiManager.ShowScreen(fallback, true);
                return;
            }

            // 兜底：没有可用 fallback 时，强制回到可操作的 gameplay 态，避免菜单卡死。
            uiManager.ForceReturnToGameplay();
        }

        private int ResolveTabSwitchDirection(KeyControl previous, KeyControl next)
        {
            if (previous != null && previous.wasPressedThisFrame)
            {
                return -1;
            }

            if (next != null && next.wasPressedThisFrame)
            {
                return 1;
            }

            return 0;
        }

        private static int ResolveGamepadTabSwitchDirection(Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return 0;
            }

            if (gamepad.leftShoulder.wasPressedThisFrame || gamepad.dpad.left.wasPressedThisFrame)
            {
                return -1;
            }

            if (gamepad.rightShoulder.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame)
            {
                return 1;
            }

            return 0;
        }

        private bool TryOpenRelativeTabFallback(UIScreenBase current, int direction)
        {
            if (uiManager == null || direction == 0)
            {
                return false;
            }

            ResolveReferences();

            var orderedScreens = new List<UIScreenBase>(3);
            AppendUniqueMenuScreen(orderedScreens, characterMenuScreen);
            AppendUniqueMenuScreen(orderedScreens, inventoryMenuScreen);
            AppendUniqueMenuScreen(orderedScreens, questMenuScreen);

            if (orderedScreens.Count <= 1)
            {
                return false;
            }

            var currentIndex = ResolveCurrentTabIndex(orderedScreens, current);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var step = direction > 0 ? 1 : -1;
            for (int i = 0; i < orderedScreens.Count; i++)
            {
                currentIndex = (currentIndex + step + orderedScreens.Count) % orderedScreens.Count;
                var target = orderedScreens[currentIndex];
                if (target == null || target == current)
                {
                    continue;
                }

                uiManager.PushScreen(target);
                return true;
            }

            return false;
        }

        private static void AppendUniqueMenuScreen(List<UIScreenBase> buffer, UIScreenBase screen)
        {
            if (screen == null || buffer == null)
            {
                return;
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i] == screen)
                {
                    return;
                }
            }

            buffer.Add(screen);
        }

        private static int ResolveCurrentTabIndex(List<UIScreenBase> orderedScreens, UIScreenBase current)
        {
            if (orderedScreens == null || orderedScreens.Count == 0 || current == null)
            {
                return -1;
            }

            for (int i = 0; i < orderedScreens.Count; i++)
            {
                if (orderedScreens[i] == current)
                {
                    return i;
                }
            }

            var type = current.GetType();
            for (int i = 0; i < orderedScreens.Count; i++)
            {
                var candidate = orderedScreens[i];
                if (candidate != null && candidate.GetType() == type)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
