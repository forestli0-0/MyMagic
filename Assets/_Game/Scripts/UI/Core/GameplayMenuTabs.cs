using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public enum GameplayMenuTab
    {
        Character = 0,
        Inventory = 1,
        Quest = 2
    }

    /// <summary>
    /// 顶部功能页签控制器：负责角色/背包/任务页之间的切换。
    /// </summary>
    public class GameplayMenuTabs : MonoBehaviour
    {
        [Serializable]
        private sealed class GameplayMenuTabEntry
        {
            public string id = string.Empty;
            public UIScreenBase targetScreen = null;
            public Button button = null;
        }

        private sealed class RuntimeTabBinding
        {
            public string id;
            public UIScreenBase targetScreen;
            public Button button;
            public UnityAction listener;
        }

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase ownerScreen;
        [SerializeField] private UIScreenBase characterScreen;
        [SerializeField] private UIScreenBase inventoryScreen;
        [SerializeField] private UIScreenBase questJournalScreen;

        [Header("Tab Buttons")]
        [SerializeField] private Button characterButton;
        [SerializeField] private Button inventoryButton;
        [SerializeField] private Button questButton;

        [Header("Extension Tabs")]
        [Tooltip("可选：额外页签配置，便于后续扩展新功能页。")]
        [SerializeField] private List<GameplayMenuTabEntry> additionalTabs = new List<GameplayMenuTabEntry>(4);

        [Header("State")]
        [SerializeField] private GameplayMenuTab activeTab = GameplayMenuTab.Inventory;
        [SerializeField] private string activeTabId = "Inventory";
        [SerializeField] private Color activeColor = new Color(0.29f, 0.45f, 0.66f, 1f);
        [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color activeTextColor = new Color(0.97f, 0.98f, 1f, 1f);
        [SerializeField] private Color inactiveTextColor = new Color(0.85f, 0.87f, 0.9f, 1f);

        private bool listenersBound;
        private readonly List<RuntimeTabBinding> runtimeBindings = new List<RuntimeTabBinding>(8);

        public static bool IsGameplayMenuScreen(UIScreenBase screen)
        {
            return screen != null && screen.GetComponent<GameplayMenuTabs>() != null;
        }

        public static void CollapseGameplayMenuStack(UIManager manager)
        {
            if (manager == null)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                var current = manager.CurrentScreen;
                if (!IsGameplayMenuScreen(current))
                {
                    return;
                }

                manager.PopScreen();
                if (manager.CurrentScreen == current)
                {
                    return;
                }
            }
        }

        private void OnEnable()
        {
            UIThemeRuntime.ThemeChanged += HandleThemeChanged;
            SyncThemeColors();
            ResolveReferences();
            SyncActiveStateFromOwner();
            CollectTabs();
            BindButtons();
            RefreshVisualState();
        }

        private void OnDisable()
        {
            UIThemeRuntime.ThemeChanged -= HandleThemeChanged;
            UnbindButtons();
        }

        public void OpenCharacter()
        {
            SwitchTo(characterScreen, "Character");
        }

        public void OpenInventory()
        {
            SwitchTo(inventoryScreen, "Inventory");
        }

        public void OpenQuest()
        {
            SwitchTo(questJournalScreen, "Quest");
        }

        public bool OpenRelativeTab(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            ResolveReferences();
            CollectTabs();
            if (runtimeBindings.Count <= 1)
            {
                return false;
            }

            var resolvedActiveId = string.IsNullOrWhiteSpace(activeTabId) ? ResolveLegacyTabId(activeTab) : activeTabId;
            var currentIndex = -1;
            for (int i = 0; i < runtimeBindings.Count; i++)
            {
                var binding = runtimeBindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.id))
                {
                    continue;
                }

                if (!string.Equals(binding.id, resolvedActiveId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                currentIndex = i;
                break;
            }

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var step = direction > 0 ? 1 : -1;
            for (int i = 0; i < runtimeBindings.Count; i++)
            {
                currentIndex = (currentIndex + step + runtimeBindings.Count) % runtimeBindings.Count;
                var binding = runtimeBindings[currentIndex];
                if (binding == null || binding.targetScreen == null || string.IsNullOrWhiteSpace(binding.id))
                {
                    continue;
                }

                SwitchTo(binding.targetScreen, binding.id);
                return true;
            }

            return false;
        }

        public UIScreenBase ResolveFallbackGameplayScreen()
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
                if (screen == null || screen == ownerScreen || IsGameplayMenuScreen(screen))
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

            if (ownerScreen == null)
            {
                ownerScreen = GetComponent<UIScreenBase>();
            }
        }

        private void SyncActiveStateFromOwner()
        {
            if (ownerScreen == null)
            {
                return;
            }

            if (ownerScreen == characterScreen)
            {
                activeTab = GameplayMenuTab.Character;
                activeTabId = "Character";
                return;
            }

            if (ownerScreen == inventoryScreen)
            {
                activeTab = GameplayMenuTab.Inventory;
                activeTabId = "Inventory";
                return;
            }

            if (ownerScreen == questJournalScreen)
            {
                activeTab = GameplayMenuTab.Quest;
                activeTabId = "Quest";
                return;
            }

            if (ownerScreen is CharacterScreen)
            {
                activeTab = GameplayMenuTab.Character;
                activeTabId = "Character";
                return;
            }

            if (ownerScreen is InventoryScreen)
            {
                activeTab = GameplayMenuTab.Inventory;
                activeTabId = "Inventory";
                return;
            }

            if (ownerScreen is QuestJournalScreen)
            {
                activeTab = GameplayMenuTab.Quest;
                activeTabId = "Quest";
                return;
            }

            if (string.IsNullOrWhiteSpace(activeTabId))
            {
                activeTabId = ResolveLegacyTabId(activeTab);
            }
        }

        private void BindButtons()
        {
            if (listenersBound)
            {
                return;
            }

            for (int i = 0; i < runtimeBindings.Count; i++)
            {
                var binding = runtimeBindings[i];
                if (binding == null || binding.button == null || binding.targetScreen == null)
                {
                    continue;
                }

                binding.listener = () => SwitchTo(binding.targetScreen, binding.id);
                binding.button.onClick.AddListener(binding.listener);
            }

            listenersBound = true;
        }

        private void UnbindButtons()
        {
            if (!listenersBound)
            {
                return;
            }

            for (int i = 0; i < runtimeBindings.Count; i++)
            {
                var binding = runtimeBindings[i];
                if (binding == null || binding.button == null || binding.listener == null)
                {
                    continue;
                }

                binding.button.onClick.RemoveListener(binding.listener);
                binding.listener = null;
            }

            listenersBound = false;
        }

        private void SwitchTo(UIScreenBase targetScreen, string tabId)
        {
            ResolveReferences();
            if (uiManager == null || ownerScreen == null || targetScreen == null)
            {
                return;
            }

            if (uiManager.ModalCount > 0)
            {
                return;
            }

            if (uiManager.CurrentScreen == targetScreen)
            {
                if (!string.IsNullOrWhiteSpace(tabId))
                {
                    activeTabId = tabId;
                }

                RefreshVisualState();
                return;
            }

            CollapseGameplayMenuStack(uiManager);

            if (IsGameplayMenuScreen(uiManager.CurrentScreen))
            {
                var fallback = ResolveFallbackGameplayScreen();
                if (fallback != null && fallback != ownerScreen)
                {
                    uiManager.ShowScreen(fallback, true);
                }
            }

            if (uiManager.CurrentScreen != targetScreen)
            {
                uiManager.PushScreen(targetScreen);
                if (!string.IsNullOrWhiteSpace(tabId))
                {
                    activeTabId = tabId;
                }
            }

            RefreshVisualState();
        }

        private void RefreshVisualState()
        {
            var resolvedActiveId = string.IsNullOrWhiteSpace(activeTabId) ? ResolveLegacyTabId(activeTab) : activeTabId;
            for (int i = 0; i < runtimeBindings.Count; i++)
            {
                var binding = runtimeBindings[i];
                if (binding == null)
                {
                    continue;
                }

                var isActive = !string.IsNullOrWhiteSpace(binding.id) &&
                    string.Equals(binding.id, resolvedActiveId, StringComparison.OrdinalIgnoreCase);
                ApplyButtonState(binding.button, isActive);
            }
        }

        private void ApplyButtonState(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            // Keep active tabs interactable so they stay in normal/selected visual states.
            // Re-click is already safely ignored in SwitchTo when target equals current screen.
            button.interactable = true;

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = isActive ? activeColor : inactiveColor;
            }
            UIStyleKit.ApplyButtonStateColors(
                button,
                isActive ? activeColor : inactiveColor,
                isActive ? 0.06f : 0.1f,
                0.18f,
                0.52f,
                0.08f);

            var texts = button.GetComponentsInChildren<Text>(true);
            if (texts == null)
            {
                return;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.color = isActive ? activeTextColor : inactiveTextColor;
            }
        }

        private void CollectTabs()
        {
            runtimeBindings.Clear();

            AddTab("Character", characterScreen, characterButton);
            AddTab("Inventory", inventoryScreen, inventoryButton);
            AddTab("Quest", questJournalScreen, questButton);

            if (additionalTabs != null)
            {
                for (int i = 0; i < additionalTabs.Count; i++)
                {
                    var tab = additionalTabs[i];
                    if (tab == null)
                    {
                        continue;
                    }

                    AddTab(tab.id, tab.targetScreen, tab.button);
                }
            }

            if (string.IsNullOrWhiteSpace(activeTabId))
            {
                activeTabId = ResolveLegacyTabId(activeTab);
            }
        }

        private void AddTab(string id, UIScreenBase targetScreen, Button button)
        {
            if (string.IsNullOrWhiteSpace(id) || targetScreen == null || button == null)
            {
                return;
            }

            for (int i = 0; i < runtimeBindings.Count; i++)
            {
                var existing = runtimeBindings[i];
                if (existing == null)
                {
                    continue;
                }

                if (!string.Equals(existing.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                existing.targetScreen = targetScreen;
                existing.button = button;
                return;
            }

            runtimeBindings.Add(new RuntimeTabBinding
            {
                id = id,
                targetScreen = targetScreen,
                button = button
            });
        }

        private static string ResolveLegacyTabId(GameplayMenuTab tab)
        {
            switch (tab)
            {
                case GameplayMenuTab.Character:
                    return "Character";
                case GameplayMenuTab.Inventory:
                    return "Inventory";
                case GameplayMenuTab.Quest:
                    return "Quest";
                default:
                    return "Inventory";
            }
        }

        private void SyncThemeColors()
        {
            activeColor = UIStyleKit.TabActiveColor;
            inactiveColor = UIStyleKit.TabInactiveColor;
            activeTextColor = UIStyleKit.TabActiveTextColor;
            inactiveTextColor = UIStyleKit.TabInactiveTextColor;
        }

        private void HandleThemeChanged(UIThemeConfig theme)
        {
            SyncThemeColors();
            RefreshVisualState();
        }
    }
}
