using System.Text;
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
        [SerializeField] private bool autoUseUnifiedNpcMenu = true;
        [SerializeField] private bool useDialogUi = true;
        [SerializeField] private bool closeDialogOnExit = true;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private QuestGiverModal questModal;

        [Header("Objective Progress")]
        [SerializeField] private bool advanceObjectiveOnInteract = true;
        [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.TalkToNpc;
        [SerializeField] private string objectiveTargetId;

        [Header("Presentation")]
        [SerializeField] private bool showStateIndicator = true;
        [SerializeField] private bool showInteractHintInIndicator = true;
        [SerializeField] private bool billboardIndicatorToCamera = true;
        [SerializeField] private Vector3 stateIndicatorOffset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private Color notAcceptedColor = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color inProgressColor = new Color(0.65f, 0.85f, 1f, 1f);
        [SerializeField] private Color readyToTurnInColor = new Color(0.45f, 1f, 0.45f, 1f);
        [SerializeField] private Color completedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        [SerializeField] private float indicatorRefreshInterval = 0.25f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private bool playerInRange;
        private TextMesh stateIndicator;
        private QuestTracker observedTracker;
        private Camera cachedCamera;
        private float nextIndicatorRefreshTime;
        private float nextCameraResolveTime;

        private void OnEnable()
        {
            EnsureUnifiedInteractionController();
            ResolveQuestTracker();
            RebindTrackerEvents();
            RefreshStatePresentation(true);
        }

        private void OnDisable()
        {
            UnbindTrackerEvents();
            CloseDialogIfNeeded();
        }

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
                return;
            }

            RefreshStatePresentation(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = false;
            CloseDialogIfNeeded();
            RefreshStatePresentation(true);
        }

        private void Update()
        {
            if (!HasExternalInteractionController() && allowInteractKey && playerInRange)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    var key = keyboard[interactKey];
                    if (key != null && key.wasPressedThisFrame)
                    {
                        Interact();
                        return;
                    }
                }
            }

            if (!showStateIndicator || Time.unscaledTime < nextIndicatorRefreshTime)
            {
                return;
            }

            nextIndicatorRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, indicatorRefreshInterval);
            RebindTrackerEvents();
            RefreshStatePresentation(false);
        }

        private void LateUpdate()
        {
            UpdateIndicatorBillboard();
        }

        public void Interact()
        {
            if (quest == null)
            {
                return;
            }

            if (useDialogUi && TryOpenDialog())
            {
                RefreshStatePresentation(true);
                return;
            }

            ExecuteInteraction(false, out _);
            RefreshStatePresentation(true);
        }

        public QuestDefinition QuestDefinition => quest;
        public Vector3 DialogAnchorWorldPosition => transform.position + stateIndicatorOffset;

        public bool TryOpenDialogUi()
        {
            return TryOpenDialog();
        }

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
            var changed = ExecuteInteraction(true, out feedback);
            RefreshStatePresentation(true);
            return changed;
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
                    feedback = advanced > 0
                        ? "任务已接取，已记录首次交谈进度。按 J 可查看任务详情。"
                        : "任务已接取。按 J 可查看任务详情。";
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
                        feedback = BuildTurnInFeedback(quest, tracker);
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
                feedback = advanced > 0 ? "任务进度已更新。按 J 可查看目标详情。" : "任务进行中，按 J 可查看目标详情。";
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

        private void RebindTrackerEvents()
        {
            var tracker = ResolveQuestTracker();
            if (tracker == observedTracker)
            {
                return;
            }

            if (observedTracker != null)
            {
                observedTracker.QuestListChanged -= HandleQuestListChanged;
            }

            observedTracker = tracker;
            if (observedTracker != null)
            {
                observedTracker.QuestListChanged += HandleQuestListChanged;
            }
        }

        private void UnbindTrackerEvents()
        {
            if (observedTracker == null)
            {
                return;
            }

            observedTracker.QuestListChanged -= HandleQuestListChanged;
            observedTracker = null;
        }

        private void HandleQuestListChanged()
        {
            RefreshStatePresentation(true);
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

        private bool HasExternalInteractionController()
        {
            var interactionTrigger = GetComponent<NpcInteractionTrigger>();
            return interactionTrigger != null && interactionTrigger.enabled && interactionTrigger.HandlesInteractKey;
        }

        private void EnsureUnifiedInteractionController()
        {
            if (!autoUseUnifiedNpcMenu)
            {
                return;
            }

            var interactionTrigger = GetComponent<NpcInteractionTrigger>();
            if (interactionTrigger == null)
            {
                interactionTrigger = gameObject.AddComponent<NpcInteractionTrigger>();
            }

            interactionTrigger.AssignQuestGiver(this);
            interactionTrigger.AssignUiManager(uiManager);
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

        private void RefreshStatePresentation(bool force)
        {
            if (!showStateIndicator || quest == null)
            {
                if (stateIndicator != null)
                {
                    stateIndicator.gameObject.SetActive(false);
                }

                return;
            }

            EnsureStateIndicator();
            if (stateIndicator == null)
            {
                return;
            }

            var state = GetQuestState();
            var indicatorText = BuildIndicatorText(state);
            if (force || !string.Equals(stateIndicator.text, indicatorText, System.StringComparison.Ordinal))
            {
                stateIndicator.text = indicatorText;
            }

            stateIndicator.color = ResolveIndicatorColor(state);
            stateIndicator.transform.localPosition = stateIndicatorOffset;
            if (!stateIndicator.gameObject.activeSelf)
            {
                stateIndicator.gameObject.SetActive(true);
            }
        }

        private void EnsureStateIndicator()
        {
            if (stateIndicator != null)
            {
                return;
            }

            var existing = transform.Find("QuestStateIndicator");
            if (existing != null)
            {
                stateIndicator = existing.GetComponent<TextMesh>();
            }

            if (stateIndicator == null)
            {
                var indicatorGo = new GameObject("QuestStateIndicator", typeof(TextMesh));
                indicatorGo.transform.SetParent(transform, false);
                stateIndicator = indicatorGo.GetComponent<TextMesh>();
            }

            if (stateIndicator == null)
            {
                return;
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                stateIndicator.font = font;
                var renderer = stateIndicator.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = font.material;
                }
            }

            stateIndicator.anchor = TextAnchor.LowerCenter;
            stateIndicator.alignment = TextAlignment.Center;
            stateIndicator.fontSize = 64;
            stateIndicator.characterSize = 0.04f;
            stateIndicator.text = string.Empty;
        }

        private void UpdateIndicatorBillboard()
        {
            if (!billboardIndicatorToCamera || stateIndicator == null || !stateIndicator.gameObject.activeInHierarchy)
            {
                return;
            }

            var camera = ResolvePresentationCamera();
            if (camera == null)
            {
                return;
            }

            var indicatorTransform = stateIndicator.transform;
            // TextMesh 的正面方向与常规 Quad 相反，使用“相机->文本”的方向可避免看到镜像背面。
            var forward = indicatorTransform.position - camera.transform.position;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            indicatorTransform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private Camera ResolvePresentationCamera()
        {
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            {
                return cachedCamera;
            }

            if (Time.unscaledTime < nextCameraResolveTime)
            {
                return cachedCamera;
            }

            nextCameraResolveTime = Time.unscaledTime + 0.5f;
            cachedCamera = Camera.main;
            if (cachedCamera == null)
            {
                cachedCamera = FindFirstObjectByType<Camera>();
            }

            return cachedCamera;
        }

        private string BuildIndicatorText(QuestRuntimeState state)
        {
            var status = state != null ? state.Status : QuestStatus.NotAccepted;
            var marker = ResolveStatusMarker(status);
            if (!showInteractHintInIndicator || !playerInRange || !allowInteractKey)
            {
                return marker;
            }

            return marker + "\n[" + ResolveInteractKeyLabel() + "]";
        }

        private string ResolveInteractKeyLabel()
        {
            var raw = interactKey.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "E";
            }

            return raw.ToUpperInvariant();
        }

        private Color ResolveIndicatorColor(QuestRuntimeState state)
        {
            if (state == null)
            {
                return notAcceptedColor;
            }

            switch (state.Status)
            {
                case QuestStatus.InProgress:
                    return inProgressColor;
                case QuestStatus.ReadyToTurnIn:
                    return readyToTurnInColor;
                case QuestStatus.Completed:
                    return completedColor;
                default:
                    return notAcceptedColor;
            }
        }

        private static string ResolveStatusMarker(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.InProgress:
                    return "...";
                case QuestStatus.ReadyToTurnIn:
                    return "?";
                case QuestStatus.Completed:
                    return "v";
                default:
                    return "!";
            }
        }

        private static string BuildTurnInFeedback(QuestDefinition definition, QuestTracker tracker)
        {
            var builder = new StringBuilder(128);
            builder.Append("任务已提交");

            var rewardSummary = BuildRewardSummary(definition != null ? definition.Reward : null);
            if (!string.IsNullOrWhiteSpace(rewardSummary))
            {
                builder.Append("，获得：");
                builder.Append(rewardSummary);
            }

            builder.Append('。');

            if (definition != null && !string.IsNullOrWhiteSpace(definition.NextQuestId) && tracker != null)
            {
                var next = tracker.GetDefinition(definition.NextQuestId);
                var nextName = next != null && !string.IsNullOrWhiteSpace(next.DisplayName) ? next.DisplayName : definition.NextQuestId;
                builder.Append(" 后续任务已解锁：");
                builder.Append(nextName);
                builder.Append('。');
            }

            return builder.ToString();
        }

        private static string BuildRewardSummary(QuestRewardDefinition reward)
        {
            if (reward == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(64);
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
                    builder.Append(", ");
                }

                builder.Append("XP ");
                builder.Append(reward.Experience);
                hasAny = true;
            }

            if (reward.Items != null && reward.Items.Count > 0)
            {
                for (int i = 0; i < reward.Items.Count; i++)
                {
                    var item = reward.Items[i];
                    if (item == null || item.Item == null)
                    {
                        continue;
                    }

                    if (hasAny)
                    {
                        builder.Append(", ");
                    }

                    var name = string.IsNullOrWhiteSpace(item.Item.DisplayName) ? item.Item.Id : item.Item.DisplayName;
                    builder.Append(name);
                    builder.Append(" x");
                    builder.Append(Mathf.Max(1, item.Stack));
                    hasAny = true;
                }
            }

            return hasAny ? builder.ToString() : string.Empty;
        }
    }
}
