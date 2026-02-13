using System.Text;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class QuestGiverModal : UIModalBase
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;

        [Header("Widgets")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text objectivesText;
        [SerializeField] private Text rewardText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryButtonText;
        [SerializeField] private Button closeButton;

        private QuestGiverTrigger source;
        private bool subscribed;

        public void Bind(QuestGiverTrigger trigger)
        {
            source = trigger;
            RefreshFromSource();
        }

        public override void OnEnter()
        {
            Subscribe();
            RefreshFromSource();
        }

        public override void OnExit()
        {
            Unsubscribe();
            source = null;
        }

        public override void OnFocus()
        {
            RefreshFromSource();
        }

        public void RefreshFromSource()
        {
            if (source == null)
            {
                ApplyEmpty();
                return;
            }

            var quest = source.QuestDefinition;
            var state = source.GetQuestState();

            if (quest == null)
            {
                ApplyEmpty();
                return;
            }

            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(quest.DisplayName) ? quest.Id : quest.DisplayName;
            }

            if (summaryText != null)
            {
                summaryText.text = string.IsNullOrWhiteSpace(quest.Summary) ? "无任务描述。" : quest.Summary;
            }

            if (statusText != null)
            {
                statusText.text = BuildStatusText(state);
            }

            if (objectivesText != null)
            {
                objectivesText.text = BuildObjectiveText(quest, state);
            }

            if (rewardText != null)
            {
                rewardText.text = BuildRewardText(quest.Reward);
            }

            if (feedbackText != null && string.IsNullOrWhiteSpace(feedbackText.text))
            {
                feedbackText.text = BuildHintText(state);
            }

            var primaryLabel = BuildPrimaryButtonText(state);
            var primaryInteractable = !string.IsNullOrWhiteSpace(primaryLabel);
            if (primaryButton != null)
            {
                primaryButton.interactable = primaryInteractable;
            }

            if (primaryButtonText != null)
            {
                primaryButtonText.text = primaryInteractable ? primaryLabel : "关闭";
            }
        }

        private void HandlePrimaryClicked()
        {
            if (source == null)
            {
                RequestClose();
                return;
            }

            var changed = source.ExecuteInteractionForDialog(out var feedback);
            if (feedbackText != null)
            {
                feedbackText.text = string.IsNullOrWhiteSpace(feedback) ? (changed ? "已更新。" : "无变更。") : feedback;
            }

            RefreshFromSource();
        }

        private void HandleCloseClicked()
        {
            RequestClose();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.AddListener(HandlePrimaryClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(HandlePrimaryClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }

            subscribed = false;
        }

        private void ApplyEmpty()
        {
            if (titleText != null)
            {
                titleText.text = "任务";
            }

            if (summaryText != null)
            {
                summaryText.text = "没有可交互的任务。";
            }

            if (statusText != null)
            {
                statusText.text = string.Empty;
            }

            if (objectivesText != null)
            {
                objectivesText.text = string.Empty;
            }

            if (rewardText != null)
            {
                rewardText.text = string.Empty;
            }

            if (feedbackText != null)
            {
                feedbackText.text = string.Empty;
            }

            if (primaryButton != null)
            {
                primaryButton.interactable = false;
            }
        }

        private static string BuildStatusText(QuestRuntimeState state)
        {
            if (state == null)
            {
                return "状态: 未接取";
            }

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

        private static string BuildHintText(QuestRuntimeState state)
        {
            if (state == null)
            {
                return "点击“接取任务”开始。";
            }

            switch (state.Status)
            {
                case QuestStatus.ReadyToTurnIn:
                    return "目标已完成，点击“提交任务”。";
                case QuestStatus.InProgress:
                    return "与 NPC 互动可推进当前任务。";
                case QuestStatus.Completed:
                    return "任务已完成。";
                default:
                    return "点击按钮继续。";
            }
        }

        private static string BuildPrimaryButtonText(QuestRuntimeState state)
        {
            if (state == null || state.Status == QuestStatus.NotAccepted)
            {
                return "接取任务";
            }

            switch (state.Status)
            {
                case QuestStatus.InProgress:
                    return "继续交谈";
                case QuestStatus.ReadyToTurnIn:
                    return "提交任务";
                default:
                    return string.Empty;
            }
        }

        private static string BuildObjectiveText(QuestDefinition quest, QuestRuntimeState state)
        {
            if (quest == null || quest.Objectives == null || quest.Objectives.Count <= 0)
            {
                return "目标: 无";
            }

            var builder = new StringBuilder(256);
            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                var objective = quest.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                var progress = state != null ? state.GetObjectiveProgress(i) : 0;
                var required = objective.RequiredAmount;
                var done = progress >= required;
                if (objective.HiddenUntilProgress && progress <= 0 && !done)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(done ? "[x] " : "[ ] ");
                builder.Append(string.IsNullOrWhiteSpace(objective.Description) ? objective.ObjectiveId : objective.Description);
                builder.Append(" (");
                builder.Append(Mathf.Min(progress, required));
                builder.Append('/');
                builder.Append(required);
                builder.Append(')');
            }

            return builder.Length > 0 ? builder.ToString() : "目标: 无";
        }

        private static string BuildRewardText(QuestRewardDefinition reward)
        {
            if (reward == null)
            {
                return "奖励: 无";
            }

            var builder = new StringBuilder(96);
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
                var appended = false;
                for (int i = 0; i < reward.Items.Count; i++)
                {
                    var entry = reward.Items[i];
                    if (entry == null || entry.Item == null)
                    {
                        continue;
                    }

                    if (appended)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(string.IsNullOrWhiteSpace(entry.Item.DisplayName) ? entry.Item.Id : entry.Item.DisplayName);
                    builder.Append(" x");
                    builder.Append(entry.Stack);
                    appended = true;
                }

                hasAny = hasAny || appended;
            }

            return hasAny ? builder.ToString() : "奖励: 无";
        }
    }
}
