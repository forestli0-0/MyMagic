using System.Text;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 任务 HUD：显示当前追踪任务与目标进度。
    /// </summary>
    public class QuestTrackerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuestTracker questTracker;
        [SerializeField] private GameDatabase database;

        [Header("Widgets")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text objectivesText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text rewardText;

        [Header("Settings")]
        [SerializeField] private bool hideWhenNoActiveQuest = true;
        [SerializeField] private string emptyTitle = "Quest";
        [SerializeField] private string emptyContent = "No active quest.";
        [SerializeField] private bool animateOnQuestChanged = true;
        [SerializeField] private float changePulseScale = 1.035f;
        [SerializeField] private float changePulseDuration = 0.18f;
        [SerializeField] private bool pulseUseUnscaledTime = true;

        private CanvasGroup canvasGroup;
        private bool subscribed;
        private readonly StringBuilder builder = new StringBuilder(256);
        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private Coroutine pulseRoutine;

        private void OnEnable()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
                if (rectTransform != null)
                {
                    baseScale = rectTransform.localScale;
                }
            }

            ResolveReferences();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Refresh()
        {
            if (questTracker == null)
            {
                ResolveReferences();
            }

            if (!subscribed && questTracker != null)
            {
                Subscribe();
            }

            if (questTracker == null)
            {
                ApplyEmpty();
                return;
            }

            var state = questTracker.GetTrackedQuestState();
            if (state == null)
            {
                ApplyEmpty();
                return;
            }

            var definition = ResolveQuestDefinition(state.QuestId);
            if (definition == null)
            {
                ApplyEmpty();
                return;
            }

            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Id : definition.DisplayName;
            }

            if (objectivesText != null)
            {
                builder.Clear();
                BuildObjectiveLines(builder, definition, state);
                objectivesText.text = builder.Length > 0 ? builder.ToString() : emptyContent;
            }

            if (statusText != null)
            {
                statusText.text = ResolveStatusLabel(state.Status);
            }

            if (rewardText != null)
            {
                rewardText.text = BuildRewardText(definition.Reward);
            }

            if (hideWhenNoActiveQuest)
            {
                SetVisibility(true);
            }
        }

        private void HandleQuestChanged()
        {
            Refresh();
            PlayChangePulse();
        }

        private void ResolveReferences()
        {
            if (questTracker == null)
            {
                questTracker = QuestTracker.Instance != null ? QuestTracker.Instance : FindFirstObjectByType<QuestTracker>();
            }

            if (database == null)
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
        }

        private void Subscribe()
        {
            if (subscribed || questTracker == null)
            {
                return;
            }

            questTracker.QuestListChanged += HandleQuestChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || questTracker == null)
            {
                subscribed = false;
                return;
            }

            questTracker.QuestListChanged -= HandleQuestChanged;
            subscribed = false;
        }

        private QuestDefinition ResolveQuestDefinition(string questId)
        {
            if (!string.IsNullOrWhiteSpace(questId))
            {
                var fromTracker = questTracker != null ? questTracker.GetDefinition(questId) : null;
                if (fromTracker != null)
                {
                    return fromTracker;
                }
            }

            if (database == null || string.IsNullOrWhiteSpace(questId))
            {
                return null;
            }

            return database.GetQuest(questId);
        }

        private void ApplyEmpty()
        {
            if (titleText != null)
            {
                titleText.text = emptyTitle;
            }

            if (objectivesText != null)
            {
                objectivesText.text = emptyContent;
            }

            if (statusText != null)
            {
                statusText.text = string.Empty;
            }

            if (rewardText != null)
            {
                rewardText.text = string.Empty;
            }

            if (hideWhenNoActiveQuest)
            {
                SetVisibility(false);
            }
        }

        private static void BuildObjectiveLines(StringBuilder builder, QuestDefinition definition, QuestRuntimeState state)
        {
            if (builder == null || definition == null || state == null || definition.Objectives == null)
            {
                return;
            }

            var hasVisibleObjective = false;
            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                var objective = definition.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                var progress = state.GetObjectiveProgress(i);
                var required = objective.RequiredAmount;
                var completed = progress >= required;
                if (objective.HiddenUntilProgress && progress <= 0 && !completed)
                {
                    continue;
                }

                if (hasVisibleObjective)
                {
                    builder.AppendLine();
                }

                var label = string.IsNullOrWhiteSpace(objective.Description) ? objective.ObjectiveId : objective.Description;
                builder.Append(completed ? "[x] " : "[ ] ");
                builder.Append(label);
                builder.Append(" (");
                builder.Append(Mathf.Min(progress, required));
                builder.Append('/');
                builder.Append(required);
                builder.Append(')');
                hasVisibleObjective = true;
            }
        }

        private static string ResolveStatusLabel(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.InProgress:
                    return "进行中";
                case QuestStatus.ReadyToTurnIn:
                    return "可提交";
                case QuestStatus.Completed:
                    return "已完成";
                default:
                    return string.Empty;
            }
        }

        private static string BuildRewardText(QuestRewardDefinition reward)
        {
            if (reward == null)
            {
                return string.Empty;
            }

            var hasCurrency = reward.Currency > 0;
            var hasExperience = reward.Experience > 0;
            var hasItems = reward.Items != null && reward.Items.Count > 0;
            if (!hasCurrency && !hasExperience && !hasItems)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(96);
            builder.Append("奖励: ");
            var appended = false;

            if (hasCurrency)
            {
                builder.Append(reward.Currency);
                builder.Append('G');
                appended = true;
            }

            if (hasExperience)
            {
                if (appended)
                {
                    builder.Append("  ");
                }

                builder.Append("XP ");
                builder.Append(reward.Experience);
                appended = true;
            }

            if (hasItems)
            {
                if (appended)
                {
                    builder.Append("  ");
                }

                builder.Append("物品 ");
                var appendedItem = false;
                for (int i = 0; i < reward.Items.Count; i++)
                {
                    var item = reward.Items[i];
                    if (item == null || item.Item == null)
                    {
                        continue;
                    }

                    if (appendedItem)
                    {
                        builder.Append(", ");
                    }

                    var itemName = string.IsNullOrWhiteSpace(item.Item.DisplayName) ? item.Item.Id : item.Item.DisplayName;
                    builder.Append(itemName);
                    builder.Append(" x");
                    builder.Append(item.Stack);
                    appendedItem = true;
                }
            }

            return builder.ToString();
        }

        private void SetVisibility(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void PlayChangePulse()
        {
            if (!animateOnQuestChanged || !isActiveAndEnabled || rectTransform == null)
            {
                return;
            }

            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            pulseRoutine = StartCoroutine(PlayChangePulseRoutine());
        }

        private IEnumerator PlayChangePulseRoutine()
        {
            var duration = Mathf.Max(0.08f, changePulseDuration);
            var half = duration * 0.5f;
            var elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += GetPulseDeltaTime();
                var t = Mathf.Clamp01(elapsed / half);
                ApplyPulseScale(t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += GetPulseDeltaTime();
                var t = 1f - Mathf.Clamp01(elapsed / half);
                ApplyPulseScale(t);
                yield return null;
            }

            if (rectTransform != null)
            {
                rectTransform.localScale = baseScale;
            }

            pulseRoutine = null;
        }

        private void ApplyPulseScale(float intensity)
        {
            if (rectTransform == null)
            {
                return;
            }

            var targetScale = baseScale * Mathf.Max(1f, changePulseScale);
            rectTransform.localScale = Vector3.LerpUnclamped(baseScale, targetScale, intensity);
        }

        private float GetPulseDeltaTime()
        {
            return pulseUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
