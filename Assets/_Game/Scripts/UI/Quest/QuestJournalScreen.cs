using System;
using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class QuestJournalScreen : UIScreenBase
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private QuestTracker questTracker;
        [SerializeField] private GameDatabase database;

        [Header("List")]
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private QuestJournalEntryUI entryTemplate;
        [SerializeField] private Text emptyLabel;

        [Header("Detail")]
        [SerializeField] private Text detailTitleText;
        [SerializeField] private Text detailSummaryText;
        [SerializeField] private Text detailObjectivesText;
        [SerializeField] private Text detailStatusText;
        [SerializeField] private Text detailRewardText;
        [SerializeField] private Button backButton;

        private readonly List<QuestJournalEntryUI> entries = new List<QuestJournalEntryUI>(16);
        private bool subscribed;
        private string selectedQuestId;

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
            EnsureReferences();
            Subscribe();
            Refresh();
            if (uiManager != null)
            {
                uiManager.SetHudVisible(false);
            }
        }

        public override void OnExit()
        {
            Unsubscribe();
            if (uiManager != null)
            {
                uiManager.SetHudVisible(true);
            }
        }

        public override void OnFocus()
        {
            Refresh();
        }

        public void Back()
        {
            EnsureReferences();
            if (uiManager == null)
            {
                return;
            }

            var current = uiManager.CurrentScreen;
            uiManager.PopScreen();

            // 栈异常兜底：若未成功弹栈（例如 Journal 成为栈底），
            // 直接切回一个 Gameplay 屏幕，避免“Back 看起来不可点击”。
            if (uiManager.CurrentScreen == current)
            {
                var fallback = FindFallbackGameplayScreen();
                if (fallback != null && fallback != this)
                {
                    uiManager.ShowScreen(fallback, true);
                }
            }
        }

        private void EnsureReferences()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (questTracker == null)
            {
                questTracker = QuestTracker.Instance != null ? QuestTracker.Instance : FindFirstObjectByType<QuestTracker>();
            }

            if (database == null && questTracker != null)
            {
                database = FindFirstObjectByType<GameDatabase>();
                if (database == null)
                {
                    var assets = Resources.FindObjectsOfTypeAll<GameDatabase>();
                    if (assets != null && assets.Length > 0)
                    {
                        database = assets[0];
                    }
                }
            }

            ResolveBackButton();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (questTracker != null)
            {
                questTracker.QuestListChanged += HandleQuestListChanged;
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(Back);
                backButton.onClick.AddListener(Back);
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (questTracker != null)
            {
                questTracker.QuestListChanged -= HandleQuestListChanged;
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(Back);
            }

            subscribed = false;
        }

        private void ResolveBackButton()
        {
            if (backButton != null)
            {
                return;
            }

            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                var name = button.gameObject.name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    backButton = button;
                    break;
                }
            }
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
                if (screen == null || screen == this)
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

        private void HandleQuestListChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            EnsureReferences();
            RebuildEntries();
            RefreshDetail();
        }

        private void RebuildEntries()
        {
            if (listRoot == null || entryTemplate == null)
            {
                return;
            }

            var states = questTracker != null ? questTracker.States : null;
            var hasQuest = states != null && states.Count > 0;

            while (entries.Count < (hasQuest ? states.Count : 0))
            {
                var instance = Instantiate(entryTemplate, listRoot);
                instance.gameObject.SetActive(true);
                entries.Add(instance);
            }

            var validSelection = false;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!hasQuest || i >= states.Count)
                {
                    entry.gameObject.SetActive(false);
                    continue;
                }

                var state = states[i];
                var definition = ResolveQuestDefinition(state != null ? state.QuestId : string.Empty);
                if (definition == null)
                {
                    entry.gameObject.SetActive(false);
                    continue;
                }

                var selected = string.Equals(selectedQuestId, definition.Id, System.StringComparison.Ordinal);
                if (selected)
                {
                    validSelection = true;
                }

                entry.Bind(definition, state, selected, HandleEntryClicked);
                entry.gameObject.SetActive(true);
            }

            if (!hasQuest)
            {
                selectedQuestId = string.Empty;
            }
            else if (!validSelection)
            {
                selectedQuestId = states[0] != null ? states[0].QuestId : string.Empty;
                UpdateEntrySelectionVisual();
            }

            if (emptyLabel != null)
            {
                emptyLabel.gameObject.SetActive(!hasQuest);
            }
        }

        private void HandleEntryClicked(QuestJournalEntryUI entry)
        {
            if (entry == null)
            {
                return;
            }

            selectedQuestId = entry.QuestId;
            UpdateEntrySelectionVisual();
            RefreshDetail();
        }

        private void UpdateEntrySelectionVisual()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.gameObject.activeSelf)
                {
                    continue;
                }

                var selected = string.Equals(selectedQuestId, entry.QuestId, System.StringComparison.Ordinal);
                entry.SetSelected(selected);
            }
        }

        private void RefreshDetail()
        {
            if (questTracker == null || string.IsNullOrWhiteSpace(selectedQuestId))
            {
                ApplyDetailEmpty();
                return;
            }

            var state = questTracker.GetState(selectedQuestId);
            var definition = ResolveQuestDefinition(selectedQuestId);
            if (state == null || definition == null)
            {
                ApplyDetailEmpty();
                return;
            }

            if (detailTitleText != null)
            {
                detailTitleText.text = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Id : definition.DisplayName;
            }

            if (detailSummaryText != null)
            {
                detailSummaryText.text = string.IsNullOrWhiteSpace(definition.Summary) ? "No summary." : definition.Summary;
            }

            if (detailStatusText != null)
            {
                detailStatusText.text = BuildStatusText(state);
            }

            if (detailObjectivesText != null)
            {
                detailObjectivesText.text = BuildObjectiveText(definition, state);
            }

            if (detailRewardText != null)
            {
                detailRewardText.text = BuildRewardText(definition.Reward);
            }
        }

        private void ApplyDetailEmpty()
        {
            if (detailTitleText != null)
            {
                detailTitleText.text = "任务详情";
            }

            if (detailSummaryText != null)
            {
                detailSummaryText.text = "当前没有可查看的任务。";
            }

            if (detailStatusText != null)
            {
                detailStatusText.text = string.Empty;
            }

            if (detailObjectivesText != null)
            {
                detailObjectivesText.text = string.Empty;
            }

            if (detailRewardText != null)
            {
                detailRewardText.text = string.Empty;
            }
        }

        private QuestDefinition ResolveQuestDefinition(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return null;
            }

            if (questTracker != null)
            {
                var fromTracker = questTracker.GetDefinition(questId);
                if (fromTracker != null)
                {
                    return fromTracker;
                }
            }

            return database != null ? database.GetQuest(questId) : null;
        }

        private static string BuildStatusText(QuestRuntimeState state)
        {
            switch (state.Status)
            {
                case QuestStatus.InProgress:
                    return "状态: 进行中";
                case QuestStatus.ReadyToTurnIn:
                    return "状态: 可提交";
                case QuestStatus.Completed:
                    return "状态: 已完成";
                default:
                    return "状态: 未接取";
            }
        }

        private static string BuildObjectiveText(QuestDefinition definition, QuestRuntimeState state)
        {
            if (definition == null || definition.Objectives == null || definition.Objectives.Count <= 0)
            {
                return "目标: 无";
            }

            var builder = new System.Text.StringBuilder(256);
            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                var objective = definition.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                var progress = state != null ? state.GetObjectiveProgress(i) : 0;
                var required = objective.RequiredAmount;
                var done = progress >= required;

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(done ? "[x] " : "[ ] ");
                if (string.IsNullOrWhiteSpace(objective.Description))
                {
                    builder.Append(objective.ObjectiveId);
                }
                else
                {
                    builder.Append(objective.Description);
                }

                builder.Append(" (");
                builder.Append(Mathf.Min(progress, required));
                builder.Append('/');
                builder.Append(required);
                builder.Append(')');
            }

            return builder.ToString();
        }

        private static string BuildRewardText(QuestRewardDefinition reward)
        {
            if (reward == null)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(128);
            builder.Append("奖励: ");
            var hasAny = false;

            if (reward.Currency > 0)
            {
                builder.Append(reward.Currency);
                builder.Append("G");
                hasAny = true;
            }

            if (reward.Experience > 0)
            {
                if (hasAny)
                {
                    builder.Append("  ");
                }

                builder.Append("XP ");
                builder.Append(reward.Experience);
                hasAny = true;
            }

            if (reward.Items != null && reward.Items.Count > 0)
            {
                if (hasAny)
                {
                    builder.Append("  ");
                }

                builder.Append("物品 ");
                var hasItems = false;
                for (int i = 0; i < reward.Items.Count; i++)
                {
                    var entry = reward.Items[i];
                    if (entry == null || entry.Item == null)
                    {
                        continue;
                    }

                    if (hasItems)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(string.IsNullOrWhiteSpace(entry.Item.DisplayName) ? entry.Item.Id : entry.Item.DisplayName);
                    builder.Append(" x");
                    builder.Append(entry.Stack);
                    hasItems = true;
                }

                hasAny = hasAny || hasItems;
            }

            if (!hasAny)
            {
                return "奖励: 无";
            }

            return builder.ToString();
        }

        public override string GetFooterHintText()
        {
            return "TAB 关闭菜单    ESC 返回游戏    ←/→ 切换页签    鼠标左键 查看任务";
        }
    }
}
