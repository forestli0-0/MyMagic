using CombatSystem.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 用触发器推进任务目标。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class QuestObjectiveTrigger : MonoBehaviour
    {
        [Header("Match")]
        [SerializeField] private QuestTracker questTracker;
        [SerializeField] private string questId;
        [SerializeField] private string objectiveId;
        [SerializeField] private string targetId;
        [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.Trigger;
        [SerializeField] private int progressAmount = 1;

        [Header("Trigger")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool requireInteractKey;
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private bool destroyAfterTriggered;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private bool playerInRange;
        private bool consumed;

        private void Reset()
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other) || consumed)
            {
                return;
            }

            playerInRange = true;
            if (!requireInteractKey)
            {
                TryApplyProgress();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = false;
        }

        private void Update()
        {
            if (!requireInteractKey || consumed || !playerInRange)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var key = keyboard[interactKey];
            if (key != null && key.wasPressedThisFrame)
            {
                TryApplyProgress();
            }
        }

        private void TryApplyProgress()
        {
            if (consumed)
            {
                return;
            }

            if (questTracker == null)
            {
                questTracker = QuestTracker.Instance != null ? QuestTracker.Instance : FindFirstObjectByType<QuestTracker>();
            }

            if (questTracker == null)
            {
                return;
            }

            var amount = Mathf.Max(1, progressAmount);
            var success = false;

            if (!string.IsNullOrWhiteSpace(questId) && !string.IsNullOrWhiteSpace(objectiveId))
            {
                success = questTracker.TryAdvanceObjective(questId, objectiveId, amount);
            }
            else if (!string.IsNullOrWhiteSpace(questId))
            {
                var matched = questTracker.TryAdvanceObjectivesByTargetForQuest(questId, targetId, objectiveType, amount);
                success = matched > 0;
            }
            else
            {
                var matched = questTracker.TryAdvanceObjectivesByTarget(targetId, objectiveType, amount);
                success = matched > 0;
            }

            if (!success)
            {
                return;
            }

            if (debugLogging)
            {
                Debug.Log($"[QuestObjectiveTrigger] Progress applied quest={questId} objective={objectiveId} target={targetId}", this);
            }

            if (triggerOnce)
            {
                consumed = true;
            }

            if (destroyAfterTriggered)
            {
                Destroy(gameObject);
            }
        }

        private bool IsPlayer(Component other)
        {
            if (other == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(playerTag))
            {
                return true;
            }

            return other.CompareTag(playerTag);
        }
    }
}
