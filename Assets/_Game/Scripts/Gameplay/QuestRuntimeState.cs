using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    public enum QuestStatus
    {
        NotAccepted = 0,
        InProgress = 1,
        ReadyToTurnIn = 2,
        Completed = 3
    }

    /// <summary>
    /// 任务运行时状态。
    /// </summary>
    [Serializable]
    public class QuestRuntimeState
    {
        [SerializeField] private string questId;
        [SerializeField] private QuestStatus status = QuestStatus.NotAccepted;
        [SerializeField] private List<int> objectiveProgress = new List<int>(4);

        public string QuestId => questId;
        public QuestStatus Status => status;
        public IReadOnlyList<int> ObjectiveProgress => objectiveProgress;
        public bool IsActive => status == QuestStatus.InProgress || status == QuestStatus.ReadyToTurnIn;
        public bool IsCompleted => status == QuestStatus.Completed;

        public QuestRuntimeState(string questId, int objectiveCount)
        {
            this.questId = questId;
            EnsureObjectiveCount(objectiveCount);
        }

        public int GetObjectiveProgress(int index)
        {
            if (index < 0 || index >= objectiveProgress.Count)
            {
                return 0;
            }

            return Mathf.Max(0, objectiveProgress[index]);
        }

        internal void SetStatus(QuestStatus value)
        {
            status = value;
        }

        internal void EnsureObjectiveCount(int count)
        {
            var safeCount = Mathf.Max(0, count);
            while (objectiveProgress.Count < safeCount)
            {
                objectiveProgress.Add(0);
            }

            if (objectiveProgress.Count > safeCount)
            {
                objectiveProgress.RemoveRange(safeCount, objectiveProgress.Count - safeCount);
            }
        }

        internal void ResetProgress()
        {
            for (int i = 0; i < objectiveProgress.Count; i++)
            {
                objectiveProgress[i] = 0;
            }
        }

        internal bool TrySetObjectiveProgress(int index, int value, out int oldValue)
        {
            oldValue = 0;
            if (index < 0 || index >= objectiveProgress.Count)
            {
                return false;
            }

            var clamped = Mathf.Max(0, value);
            oldValue = objectiveProgress[index];
            if (oldValue == clamped)
            {
                return false;
            }

            objectiveProgress[index] = clamped;
            return true;
        }
    }
}
