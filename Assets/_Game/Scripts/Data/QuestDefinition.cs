using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 任务定义，包含任务目标与奖励配置。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Quests/Quest Definition", fileName = "Quest_")]
    public class QuestDefinition : DefinitionBase
    {
        private static readonly IReadOnlyList<QuestObjectiveDefinition> EmptyObjectives = Array.Empty<QuestObjectiveDefinition>();
        private static readonly QuestRewardDefinition EmptyReward = new QuestRewardDefinition();

        [Header("Meta")]
        [SerializeField] private QuestCategory category = QuestCategory.Side;
        [TextArea(2, 4)]
        [SerializeField] private string summary;
        [SerializeField] private bool requireTurnIn = true;
        [SerializeField] private bool autoTrackOnAccept = true;
        [SerializeField] private string nextQuestId;

        [Header("Content")]
        [SerializeField] private List<QuestObjectiveDefinition> objectives = new List<QuestObjectiveDefinition>();
        [SerializeField] private QuestRewardDefinition reward = new QuestRewardDefinition();

        public QuestCategory Category => category;
        public string Summary => summary;
        public bool RequireTurnIn => requireTurnIn;
        public bool AutoTrackOnAccept => autoTrackOnAccept;
        public string NextQuestId => nextQuestId;
        public IReadOnlyList<QuestObjectiveDefinition> Objectives => objectives ?? EmptyObjectives;
        public QuestRewardDefinition Reward => reward ?? EmptyReward;
    }

    public enum QuestCategory
    {
        Main = 0,
        Side = 1
    }

    public enum QuestObjectiveType
    {
        Trigger = 0,
        TalkToNpc = 1,
        BuyFromVendor = 2,
        SellToVendor = 3,
        CollectItem = 4,
        KillEnemy = 5
    }

    [Serializable]
    public class QuestObjectiveDefinition
    {
        [SerializeField] private string objectiveId;
        [TextArea(1, 3)]
        [SerializeField] private string description;
        [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.Trigger;
        [SerializeField] private string targetId;
        [SerializeField] private int requiredAmount = 1;
        [SerializeField] private bool optional;
        [SerializeField] private bool hiddenUntilProgress;

        public string ObjectiveId => string.IsNullOrWhiteSpace(objectiveId) ? targetId : objectiveId;
        public string Description => description;
        public QuestObjectiveType ObjectiveType => objectiveType;
        public string TargetId => targetId;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
        public bool Optional => optional;
        public bool HiddenUntilProgress => hiddenUntilProgress;
    }

    [Serializable]
    public class QuestRewardDefinition
    {
        private static readonly IReadOnlyList<QuestRewardItemEntry> EmptyItems = Array.Empty<QuestRewardItemEntry>();

        [SerializeField] private int currency;
        [SerializeField] private int experience;
        [SerializeField] private List<QuestRewardItemEntry> items = new List<QuestRewardItemEntry>();

        public int Currency => Mathf.Max(0, currency);
        public int Experience => Mathf.Max(0, experience);
        public IReadOnlyList<QuestRewardItemEntry> Items => items ?? EmptyItems;
    }

    [Serializable]
    public class QuestRewardItemEntry
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private int stack = 1;

        public ItemDefinition Item => item;
        public int Stack => Mathf.Max(1, stack);
    }
}
