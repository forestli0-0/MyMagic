using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 任务跟踪器：管理任务接取、目标推进、奖励发放与存读档。
    /// </summary>
    public class QuestTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameDatabase database;
        [SerializeField] private CombatEventHub combatEventHub;

        [Header("Player")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private InventoryComponent playerInventory;
        [SerializeField] private CurrencyComponent playerCurrency;
        [SerializeField] private PlayerProgression playerProgression;

        [Header("Settings")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private bool discoverOnSceneLoad = true;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool debugLogging;

        private readonly List<QuestRuntimeState> runtimeStates = new List<QuestRuntimeState>(16);
        private readonly Dictionary<string, QuestRuntimeState> statesByQuestId = new Dictionary<string, QuestRuntimeState>(StringComparer.Ordinal);

        private string trackedQuestId = string.Empty;
        private bool combatEventsSubscribed;

        public static QuestTracker Instance { get; private set; }

        public event Action QuestListChanged;
        public event Action<QuestRuntimeState> QuestAccepted;
        public event Action<QuestRuntimeState> QuestProgressed;
        public event Action<QuestRuntimeState> QuestReadyToTurnIn;
        public event Action<QuestRuntimeState> QuestCompleted;

        public IReadOnlyList<QuestRuntimeState> States => runtimeStates;
        public string TrackedQuestId => trackedQuestId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            ResolveDatabase();
            ResolvePlayerReferences();
            ResolveCombatEventHub();
        }

        private void OnEnable()
        {
            if (discoverOnSceneLoad)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }

            SubscribeCombatEvents();
        }

        private void OnDisable()
        {
            if (discoverOnSceneLoad)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }

            UnsubscribeCombatEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public QuestDefinition GetDefinition(string questId)
        {
            var resolved = ResolveDatabase();
            return resolved != null ? resolved.GetQuest(questId) : null;
        }

        public QuestRuntimeState GetState(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return null;
            }

            statesByQuestId.TryGetValue(questId, out var state);
            return state;
        }

        public QuestRuntimeState GetTrackedQuestState()
        {
            if (!string.IsNullOrWhiteSpace(trackedQuestId) && statesByQuestId.TryGetValue(trackedQuestId, out var tracked))
            {
                if (tracked.Status == QuestStatus.InProgress || tracked.Status == QuestStatus.ReadyToTurnIn)
                {
                    return tracked;
                }
            }

            for (int i = 0; i < runtimeStates.Count; i++)
            {
                if (runtimeStates[i].Status == QuestStatus.InProgress)
                {
                    return runtimeStates[i];
                }
            }

            for (int i = 0; i < runtimeStates.Count; i++)
            {
                if (runtimeStates[i].Status == QuestStatus.ReadyToTurnIn)
                {
                    return runtimeStates[i];
                }
            }

            return null;
        }

        public bool SetTrackedQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                trackedQuestId = string.Empty;
                QuestListChanged?.Invoke();
                return true;
            }

            if (!statesByQuestId.TryGetValue(questId, out var state))
            {
                return false;
            }

            if (state.Status == QuestStatus.NotAccepted)
            {
                return false;
            }

            trackedQuestId = questId;
            QuestListChanged?.Invoke();
            return true;
        }

        public bool AcceptQuest(string questId)
        {
            return AcceptQuest(questId, out _);
        }

        public bool AcceptQuest(string questId, out string failureReason)
        {
            failureReason = string.Empty;
            var definition = GetDefinition(questId);
            if (definition == null)
            {
                failureReason = "任务不存在。";
                return false;
            }

            var state = GetOrCreateState(definition);
            if (state.Status == QuestStatus.Completed)
            {
                failureReason = "任务已完成。";
                return false;
            }

            if (state.Status == QuestStatus.InProgress || state.Status == QuestStatus.ReadyToTurnIn)
            {
                failureReason = "任务已接取。";
                return false;
            }

            state.SetStatus(QuestStatus.InProgress);
            state.EnsureObjectiveCount(definition.Objectives.Count);
            state.ResetProgress();

            if (definition.AutoTrackOnAccept || string.IsNullOrWhiteSpace(trackedQuestId))
            {
                trackedQuestId = definition.Id;
            }

            QuestAccepted?.Invoke(state);

            if (AreRequiredObjectivesComplete(state, definition))
            {
                PromoteQuestCompletionState(state, definition, true);
            }

            NotifyStateChanged(state, false);
            return true;
        }

        public bool TryTurnInQuest(string questId)
        {
            return TryTurnInQuest(questId, out _);
        }

        public bool TryTurnInQuest(string questId, out string failureReason)
        {
            failureReason = string.Empty;
            var state = GetState(questId);
            if (state == null)
            {
                failureReason = "任务未接取。";
                return false;
            }

            if (state.Status == QuestStatus.Completed)
            {
                failureReason = "任务已完成。";
                return false;
            }

            if (state.Status != QuestStatus.ReadyToTurnIn)
            {
                failureReason = "任务目标尚未完成。";
                return false;
            }

            var definition = GetDefinition(questId);
            if (definition == null)
            {
                failureReason = "任务定义丢失。";
                return false;
            }

            GrantRewards(definition);
            state.SetStatus(QuestStatus.Completed);
            QuestCompleted?.Invoke(state);

            RefreshTrackedQuestId();
            NotifyStateChanged(state, false);

            if (!string.IsNullOrWhiteSpace(definition.NextQuestId))
            {
                AcceptQuest(definition.NextQuestId, out _);
            }

            return true;
        }

        public bool TryAdvanceObjective(string questId, string objectiveId, int amount = 1)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId))
            {
                return false;
            }

            var state = GetState(questId);
            if (state == null || state.Status != QuestStatus.InProgress)
            {
                return false;
            }

            var definition = GetDefinition(questId);
            if (definition == null || definition.Objectives == null || definition.Objectives.Count == 0)
            {
                return false;
            }

            var objectiveIndex = FindObjectiveIndex(definition, objectiveId);
            if (objectiveIndex < 0)
            {
                return false;
            }

            return TryAdvanceObjectiveInternal(state, definition, objectiveIndex, amount);
        }

        public int TryAdvanceObjectivesByTarget(string targetId, QuestObjectiveType objectiveType, int amount = 1)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var advanced = 0;
            for (int i = 0; i < runtimeStates.Count; i++)
            {
                var state = runtimeStates[i];
                if (state == null || state.Status != QuestStatus.InProgress)
                {
                    continue;
                }

                var definition = GetDefinition(state.QuestId);
                if (definition == null || definition.Objectives == null)
                {
                    continue;
                }

                for (int objectiveIndex = 0; objectiveIndex < definition.Objectives.Count; objectiveIndex++)
                {
                    var objective = definition.Objectives[objectiveIndex];
                    if (objective == null)
                    {
                        continue;
                    }

                    if (objective.ObjectiveType != objectiveType)
                    {
                        continue;
                    }

                    if (!IsTargetMatched(objective.TargetId, targetId))
                    {
                        continue;
                    }

                    if (TryAdvanceObjectiveInternal(state, definition, objectiveIndex, amount))
                    {
                        advanced++;
                    }
                }
            }

            return advanced;
        }

        public int TryAdvanceObjectivesByTargetForQuest(string questId, string targetId, QuestObjectiveType objectiveType, int amount = 1)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(questId))
            {
                return 0;
            }

            var state = GetState(questId);
            if (state == null || state.Status != QuestStatus.InProgress)
            {
                return 0;
            }

            var definition = GetDefinition(questId);
            if (definition == null || definition.Objectives == null)
            {
                return 0;
            }

            var advanced = 0;
            for (int objectiveIndex = 0; objectiveIndex < definition.Objectives.Count; objectiveIndex++)
            {
                var objective = definition.Objectives[objectiveIndex];
                if (objective == null)
                {
                    continue;
                }

                if (objective.ObjectiveType != objectiveType)
                {
                    continue;
                }

                if (!IsTargetMatched(objective.TargetId, targetId))
                {
                    continue;
                }

                if (TryAdvanceObjectiveInternal(state, definition, objectiveIndex, amount))
                {
                    advanced++;
                }
            }

            return advanced;
        }

        public void NotifyVendorBought(ItemDefinition item, int quantity)
        {
            var targetId = item != null ? item.Id : string.Empty;
            TryAdvanceObjectivesByTarget(targetId, QuestObjectiveType.BuyFromVendor, Mathf.Max(1, quantity));
        }

        public void NotifyVendorSold(ItemDefinition item, int quantity)
        {
            var targetId = item != null ? item.Id : string.Empty;
            TryAdvanceObjectivesByTarget(targetId, QuestObjectiveType.SellToVendor, Mathf.Max(1, quantity));
        }

        public void NotifyItemCollected(ItemDefinition item, int quantity)
        {
            var targetId = item != null ? item.Id : string.Empty;
            TryAdvanceObjectivesByTarget(targetId, QuestObjectiveType.CollectItem, Mathf.Max(1, quantity));
        }

        public QuestSaveData CaptureSaveData()
        {
            var data = new QuestSaveData
            {
                trackedQuestId = trackedQuestId
            };

            if (runtimeStates.Count <= 0)
            {
                return data;
            }

            data.quests = new QuestStateSaveData[runtimeStates.Count];
            for (int i = 0; i < runtimeStates.Count; i++)
            {
                var state = runtimeStates[i];
                var entry = new QuestStateSaveData
                {
                    questId = state.QuestId,
                    status = (int)state.Status
                };

                var progress = state.ObjectiveProgress;
                if (progress != null && progress.Count > 0)
                {
                    entry.objectiveProgress = new int[progress.Count];
                    for (int p = 0; p < progress.Count; p++)
                    {
                        entry.objectiveProgress[p] = Mathf.Max(0, progress[p]);
                    }
                }

                data.quests[i] = entry;
            }

            return data;
        }

        public void ApplySaveData(QuestSaveData data)
        {
            runtimeStates.Clear();
            statesByQuestId.Clear();
            trackedQuestId = string.Empty;

            if (data == null || data.quests == null || data.quests.Length == 0)
            {
                QuestListChanged?.Invoke();
                return;
            }

            for (int i = 0; i < data.quests.Length; i++)
            {
                var saved = data.quests[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.questId))
                {
                    continue;
                }

                var definition = GetDefinition(saved.questId);
                if (definition == null)
                {
                    continue;
                }

                var state = new QuestRuntimeState(saved.questId, definition.Objectives.Count);
                var status = ToQuestStatus(saved.status);
                state.SetStatus(status);

                if (saved.objectiveProgress != null)
                {
                    for (int objectiveIndex = 0; objectiveIndex < definition.Objectives.Count; objectiveIndex++)
                    {
                        var objective = definition.Objectives[objectiveIndex];
                        if (objective == null)
                        {
                            continue;
                        }

                        var value = objectiveIndex < saved.objectiveProgress.Length ? saved.objectiveProgress[objectiveIndex] : 0;
                        var clamped = Mathf.Clamp(value, 0, objective.RequiredAmount);
                        state.TrySetObjectiveProgress(objectiveIndex, clamped, out _);
                    }
                }

                if (state.Status == QuestStatus.ReadyToTurnIn && !AreRequiredObjectivesComplete(state, definition))
                {
                    state.SetStatus(QuestStatus.InProgress);
                }

                runtimeStates.Add(state);
                statesByQuestId[state.QuestId] = state;
            }

            if (!string.IsNullOrWhiteSpace(data.trackedQuestId) && statesByQuestId.ContainsKey(data.trackedQuestId))
            {
                trackedQuestId = data.trackedQuestId;
            }
            else
            {
                RefreshTrackedQuestId();
            }

            QuestListChanged?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnsubscribeCombatEvents();
            ResolveDatabase();
            ResolvePlayerReferences();
            ResolveCombatEventHub();
            SubscribeCombatEvents();
        }

        private bool TryAdvanceObjectiveInternal(QuestRuntimeState state, QuestDefinition definition, int objectiveIndex, int amount)
        {
            if (state == null || definition == null || amount <= 0)
            {
                return false;
            }

            if (state.Status != QuestStatus.InProgress)
            {
                return false;
            }

            if (objectiveIndex < 0 || objectiveIndex >= definition.Objectives.Count)
            {
                return false;
            }

            var objective = definition.Objectives[objectiveIndex];
            if (objective == null)
            {
                return false;
            }

            var current = state.GetObjectiveProgress(objectiveIndex);
            var next = Mathf.Clamp(current + amount, 0, objective.RequiredAmount);
            if (!state.TrySetObjectiveProgress(objectiveIndex, next, out var oldValue))
            {
                return false;
            }

            if (debugLogging)
            {
                Debug.Log($"[Quest] Progress quest={definition.Id} objective={objective.ObjectiveId} {oldValue}->{next}", this);
            }

            QuestProgressed?.Invoke(state);
            PromoteQuestCompletionState(state, definition, true);
            NotifyStateChanged(state, true);
            return true;
        }

        private void PromoteQuestCompletionState(QuestRuntimeState state, QuestDefinition definition, bool grantRewardsWhenCompleted)
        {
            if (!AreRequiredObjectivesComplete(state, definition))
            {
                return;
            }

            if (definition.RequireTurnIn)
            {
                if (state.Status != QuestStatus.ReadyToTurnIn)
                {
                    state.SetStatus(QuestStatus.ReadyToTurnIn);
                    QuestReadyToTurnIn?.Invoke(state);
                }

                return;
            }

            if (state.Status != QuestStatus.Completed)
            {
                if (grantRewardsWhenCompleted)
                {
                    GrantRewards(definition);
                }

                state.SetStatus(QuestStatus.Completed);
                QuestCompleted?.Invoke(state);
                RefreshTrackedQuestId();
            }
        }

        private void NotifyStateChanged(QuestRuntimeState state, bool keepTrackedQuest)
        {
            if (!keepTrackedQuest)
            {
                RefreshTrackedQuestId();
            }

            QuestListChanged?.Invoke();
        }

        private void GrantRewards(QuestDefinition definition)
        {
            if (definition == null || definition.Reward == null)
            {
                return;
            }

            ResolvePlayerReferences();
            var reward = definition.Reward;

            if (reward.Currency > 0 && playerCurrency != null)
            {
                playerCurrency.Add(reward.Currency);
            }

            if (reward.Experience > 0 && playerProgression != null)
            {
                playerProgression.AddExperience(reward.Experience);
            }

            if (reward.Items == null || reward.Items.Count == 0 || playerInventory == null)
            {
                return;
            }

            for (int i = 0; i < reward.Items.Count; i++)
            {
                var rewardEntry = reward.Items[i];
                if (rewardEntry == null || rewardEntry.Item == null)
                {
                    continue;
                }

                var instance = new ItemInstance(rewardEntry.Item, rewardEntry.Stack, rewardEntry.Item.Rarity, null);
                if (playerInventory.TryAddItem(instance, out var remainder))
                {
                    continue;
                }

                if (debugLogging && remainder != null)
                {
                    Debug.LogWarning($"[Quest] Reward item overflow quest={definition.Id} item={rewardEntry.Item.Id} remainder={remainder.Stack}", this);
                }
            }
        }

        private QuestRuntimeState GetOrCreateState(QuestDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (statesByQuestId.TryGetValue(definition.Id, out var existing))
            {
                existing.EnsureObjectiveCount(definition.Objectives.Count);
                return existing;
            }

            var state = new QuestRuntimeState(definition.Id, definition.Objectives.Count);
            runtimeStates.Add(state);
            statesByQuestId.Add(definition.Id, state);
            return state;
        }

        private static int FindObjectiveIndex(QuestDefinition definition, string objectiveId)
        {
            if (definition == null || definition.Objectives == null || string.IsNullOrWhiteSpace(objectiveId))
            {
                return -1;
            }

            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                var objective = definition.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                if (string.Equals(objective.ObjectiveId, objectiveId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsTargetMatched(string objectiveTargetId, string runtimeTargetId)
        {
            if (string.IsNullOrWhiteSpace(objectiveTargetId))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(runtimeTargetId))
            {
                return false;
            }

            return string.Equals(objectiveTargetId, runtimeTargetId, StringComparison.Ordinal);
        }

        private static bool AreRequiredObjectivesComplete(QuestRuntimeState state, QuestDefinition definition)
        {
            if (state == null || definition == null || definition.Objectives == null)
            {
                return false;
            }

            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                var objective = definition.Objectives[i];
                if (objective == null || objective.Optional)
                {
                    continue;
                }

                if (state.GetObjectiveProgress(i) < objective.RequiredAmount)
                {
                    return false;
                }
            }

            return true;
        }

        private void RefreshTrackedQuestId()
        {
            if (!string.IsNullOrWhiteSpace(trackedQuestId) && statesByQuestId.TryGetValue(trackedQuestId, out var tracked))
            {
                if (tracked.Status == QuestStatus.InProgress || tracked.Status == QuestStatus.ReadyToTurnIn)
                {
                    return;
                }
            }

            trackedQuestId = string.Empty;
            for (int i = 0; i < runtimeStates.Count; i++)
            {
                if (runtimeStates[i].Status == QuestStatus.InProgress)
                {
                    trackedQuestId = runtimeStates[i].QuestId;
                    return;
                }
            }

            for (int i = 0; i < runtimeStates.Count; i++)
            {
                if (runtimeStates[i].Status == QuestStatus.ReadyToTurnIn)
                {
                    trackedQuestId = runtimeStates[i].QuestId;
                    return;
                }
            }
        }

        private GameDatabase ResolveDatabase()
        {
            if (database != null)
            {
                return database;
            }

            database = FindFirstObjectByType<GameDatabase>();
            if (database != null)
            {
                return database;
            }

            var assets = Resources.FindObjectsOfTypeAll<GameDatabase>();
            if (assets != null && assets.Length > 0)
            {
                database = assets[0];
            }

            return database;
        }

        private void ResolvePlayerReferences()
        {
            if (!autoFindPlayer)
            {
                return;
            }

            if (playerInventory != null && playerCurrency != null && playerProgression != null)
            {
                return;
            }

            GameObject player = null;
            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                player = PlayerUnitLocator.FindGameObjectWithTagSafe(playerTag);
            }

            if (player == null)
            {
                var unitRoot = FindFirstObjectByType<UnitRoot>();
                if (unitRoot != null)
                {
                    player = unitRoot.gameObject;
                }
            }

            if (player != null)
            {
                if (playerInventory == null)
                {
                    playerInventory = player.GetComponent<InventoryComponent>();
                }

                if (playerCurrency == null)
                {
                    playerCurrency = player.GetComponent<CurrencyComponent>();
                }

                if (playerProgression == null)
                {
                    playerProgression = player.GetComponent<PlayerProgression>();
                }
            }

            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<InventoryComponent>();
            }

            if (playerCurrency == null)
            {
                playerCurrency = FindFirstObjectByType<CurrencyComponent>();
            }

            if (playerProgression == null)
            {
                playerProgression = FindFirstObjectByType<PlayerProgression>();
            }
        }

        private void ResolveCombatEventHub()
        {
            if (combatEventHub != null)
            {
                return;
            }

            GameObject player = null;
            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                player = PlayerUnitLocator.FindGameObjectWithTagSafe(playerTag);
            }

            var unitRoot = player != null ? player.GetComponent<UnitRoot>() : null;
            if (unitRoot == null)
            {
                unitRoot = FindFirstObjectByType<UnitRoot>();
            }

            if (unitRoot != null)
            {
                combatEventHub = unitRoot.EventHub;
            }
        }

        private void SubscribeCombatEvents()
        {
            if (combatEventsSubscribed)
            {
                return;
            }

            if (combatEventHub == null)
            {
                ResolveCombatEventHub();
            }

            if (combatEventHub == null)
            {
                return;
            }

            combatEventHub.UnitKilled += HandleUnitKilled;
            combatEventsSubscribed = true;
        }

        private void UnsubscribeCombatEvents()
        {
            if (!combatEventsSubscribed || combatEventHub == null)
            {
                combatEventsSubscribed = false;
                return;
            }

            combatEventHub.UnitKilled -= HandleUnitKilled;
            combatEventsSubscribed = false;
        }

        private void HandleUnitKilled(UnitKilledEvent evt)
        {
            if (evt.Victim == null || evt.Source.SourceUnit == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(playerTag) && !evt.Source.SourceUnit.CompareTag(playerTag))
            {
                return;
            }

            string targetId = null;
            var unitRoot = evt.Victim.GetComponent<UnitRoot>();
            if (unitRoot != null && unitRoot.Definition != null && !string.IsNullOrWhiteSpace(unitRoot.Definition.Id))
            {
                targetId = unitRoot.Definition.Id;
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                targetId = evt.Victim.gameObject.name;
            }

            TryAdvanceObjectivesByTarget(targetId, QuestObjectiveType.KillEnemy, 1);
        }

        private static QuestStatus ToQuestStatus(int value)
        {
            if (!Enum.IsDefined(typeof(QuestStatus), value))
            {
                return QuestStatus.NotAccepted;
            }

            return (QuestStatus)value;
        }
    }
}
