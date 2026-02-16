using CombatSystem.Data;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 任务发布/提交触发器。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class QuestGiverTrigger : MonoBehaviour
    {
        [Header("Quest")]
        [SerializeField] private QuestDefinition quest;
        [SerializeField] private QuestTracker questTracker;
        [SerializeField] private bool autoAcceptOnEnter;
        [SerializeField] private bool autoTurnInWhenReady = true;

        [Header("Interaction")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool allowInteractKey = true;
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private bool useDialogUi = true;
        [SerializeField] private bool closeDialogOnExit = true;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private QuestGiverModal questModal;

        [Header("Objective Progress")]
        [SerializeField] private bool advanceObjectiveOnInteract = true;
        [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.TalkToNpc;
        [SerializeField] private string objectiveTargetId;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private bool playerInRange;

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
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = true;
            if (autoAcceptOnEnter)
            {
                Interact();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = false;
            CloseDialogIfNeeded();
        }

        private void Update()
        {
            if (!allowInteractKey || !playerInRange)
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
                Interact();
            }
        }

        public void Interact()
        {
            if (quest == null)
            {
                return;
            }

            if (useDialogUi && TryOpenDialog())
            {
                return;
            }

            ExecuteInteraction(false, out _);
        }

        public QuestDefinition QuestDefinition => quest;

        public QuestRuntimeState GetQuestState()
        {
            var tracker = ResolveQuestTracker();
            if (tracker == null || quest == null)
            {
                return null;
            }

            return tracker.GetState(quest.Id);
        }

        public QuestTracker ResolveQuestTracker()
        {
            if (questTracker == null)
            {
                questTracker = QuestTracker.Instance != null ? QuestTracker.Instance : FindFirstObjectByType<QuestTracker>();
            }

            return questTracker;
        }

        public bool ExecuteInteractionForDialog(out string feedback)
        {
            return ExecuteInteraction(true, out feedback);
        }

        private bool ExecuteInteraction(bool forceTurnIn, out string feedback)
        {
            feedback = string.Empty;
            if (quest == null)
            {
                feedback = "任务未配置。";
                return false;
            }

            var tracker = ResolveQuestTracker();
            if (tracker == null)
            {
                feedback = "任务系统未就绪。";
                return false;
            }

            var questId = quest.Id;
            var state = tracker.GetState(questId);

            if (state == null || state.Status == QuestStatus.NotAccepted)
            {
                if (tracker.AcceptQuest(questId, out var acceptReason))
                {
                    var advanced = TryAdvanceTalkObjective(questId);
                    feedback = advanced > 0 ? "任务已接取，已更新交谈进度。" : "任务已接取。";
                    return true;
                }

                feedback = acceptReason;
                if (debugLogging)
                {
                    Debug.Log($"[QuestGiver] Accept failed quest={questId} reason={acceptReason}", this);
                }

                return false;
            }

            if (state.Status == QuestStatus.ReadyToTurnIn)
            {
                if (autoTurnInWhenReady || forceTurnIn)
                {
                    if (tracker.TryTurnInQuest(questId, out var turnInReason))
                    {
                        feedback = "任务已提交。";
                        return true;
                    }

                    feedback = turnInReason;
                    if (debugLogging)
                    {
                        Debug.Log($"[QuestGiver] Turn-in failed quest={questId} reason={turnInReason}", this);
                    }

                    return false;
                }

                feedback = "任务可提交。";
                return false;
            }

            if (state.Status == QuestStatus.InProgress)
            {
                var advanced = TryAdvanceTalkObjective(questId);
                feedback = advanced > 0 ? "任务进度已更新。" : "任务进行中。";
                return advanced > 0;
            }

            feedback = "任务已完成。";
            return false;
        }

        private int TryAdvanceTalkObjective(string questId)
        {
            if (!advanceObjectiveOnInteract || questTracker == null || string.IsNullOrWhiteSpace(questId))
            {
                return 0;
            }

            var targetId = string.IsNullOrWhiteSpace(objectiveTargetId) ? questId : objectiveTargetId;
            return questTracker.TryAdvanceObjectivesByTargetForQuest(questId, targetId, objectiveType, 1);
        }

        private bool TryOpenDialog()
        {
            if (quest == null)
            {
                return false;
            }

            ResolveQuestTracker();
            ResolveUiReferences();
            if (uiManager == null || questModal == null)
            {
                return false;
            }

            questModal.Bind(this);
            if (uiManager.CurrentModal == questModal)
            {
                return true;
            }

            uiManager.PushModal(questModal);
            return true;
        }

        private void ResolveUiReferences()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (questModal == null)
            {
                questModal = FindFirstObjectByType<QuestGiverModal>(FindObjectsInactive.Include);
                if (questModal == null && uiManager != null)
                {
                    questModal = QuestGiverModal.EnsureRuntimeModal(uiManager);
                }
            }
        }

        private void CloseDialogIfNeeded()
        {
            if (!closeDialogOnExit)
            {
                return;
            }

            ResolveUiReferences();
            if (uiManager == null || questModal == null)
            {
                return;
            }

            if (uiManager.CurrentModal == questModal)
            {
                uiManager.CloseModal(questModal);
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
