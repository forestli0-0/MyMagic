using System;
using System.Collections.Generic;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 通用 NPC 交互触发器：统一处理靠近 + 按键交互，并通过数据配置分发到任务/交易/对话/自定义动作。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NpcInteractionTrigger : MonoBehaviour
    {
        public enum NpcInteractionActionType
        {
            Dialogue = 0,
            Quest = 1,
            Trade = 2,
            CustomEvent = 3,
            Close = 4
        }

        public enum NpcInteractionTemplatePreset
        {
            Custom = 0,
            DialogueOnly = 1,
            QuestOnly = 2,
            TradeOnly = 3,
            DialogueAndQuest = 4,
            DialogueAndTrade = 5,
            QuestAndTrade = 6,
            DialogueQuestTrade = 7
        }

        [Serializable]
        public class NpcInteractionActionEntry
        {
            [SerializeField] private bool enabled = true;
            [SerializeField] private string label;
            [SerializeField] private NpcInteractionActionType actionType = NpcInteractionActionType.Dialogue;
            [SerializeField] private bool closeMenuBeforeInvoke = true;
            [SerializeField] [TextArea(2, 4)] private string overrideDialogueLine;
            [SerializeField] private UnityEvent onSelected = new UnityEvent();

            public bool Enabled => enabled;
            public string Label => label;
            public NpcInteractionActionType ActionType => actionType;
            public bool CloseMenuBeforeInvoke => closeMenuBeforeInvoke;
            public string OverrideDialogueLine => overrideDialogueLine;

            public void Configure(
                string configuredLabel,
                NpcInteractionActionType configuredActionType,
                bool configuredCloseMenuBeforeInvoke,
                string configuredOverrideDialogueLine = "")
            {
                enabled = true;
                label = configuredLabel;
                actionType = configuredActionType;
                closeMenuBeforeInvoke = configuredCloseMenuBeforeInvoke;
                overrideDialogueLine = configuredOverrideDialogueLine;
            }

            public void InvokeCustom()
            {
                onSelected?.Invoke();
            }
        }

        [Serializable]
        public class NpcCustomAction
        {
            [SerializeField] private string label = "自定义动作";
            [SerializeField] private bool closeMenuBeforeInvoke = true;
            [SerializeField] private UnityEvent onSelected = new UnityEvent();

            public string Label => label;
            public bool CloseMenuBeforeInvoke => closeMenuBeforeInvoke;
            public bool IsValid => !string.IsNullOrWhiteSpace(label);

            public void Invoke()
            {
                onSelected?.Invoke();
            }
        }

        public struct InteractionOptionView
        {
            public string Label;
            public bool Interactable;
            public string DisabledReason;

            public InteractionOptionView(string label, bool interactable, string disabledReason)
            {
                Label = label;
                Interactable = interactable;
                DisabledReason = disabledReason;
            }
        }

        private struct ResolvedAction
        {
            public string Label;
            public bool Interactable;
            public string DisabledReason;
            public NpcInteractionActionType ActionType;
            public bool CloseMenuBeforeInvoke;
            public string OverrideDialogueLine;
            public NpcInteractionActionEntry ConfigAction;
            public NpcCustomAction CustomAction;
        }

        [Header("Identity")]
        [SerializeField] private string npcDisplayName = "NPC";
        [SerializeField] [TextArea(2, 4)] private string greeting = "你好，想聊点什么？";
        [SerializeField] [TextArea(2, 4)] private string fallbackDialogue = "你好。";

        [Header("Interaction")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool allowInteractKey = true;
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private bool closeMenuOnExit = true;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private NpcInteractionMenuModal interactionModal;

        [Header("Capabilities")]
        [SerializeField] private QuestGiverTrigger questGiver;
        [SerializeField] private VendorTrigger vendorTrigger;

        [Header("Template")]
        [SerializeField] private NpcInteractionTemplatePreset interactionTemplatePreset = NpcInteractionTemplatePreset.Custom;
        [SerializeField] private bool autoSyncActionConfigWithTemplate = false;
        [SerializeField] [HideInInspector] private NpcInteractionTemplatePreset lastAppliedTemplatePreset = NpcInteractionTemplatePreset.Custom;

        [Header("Action Config")]
        [SerializeField] private bool useActionConfig = true;
        [SerializeField] private bool autoBuildDefaultActionsWhenEmpty = true;
        [SerializeField] private bool enableDialogueOption = true;
        [SerializeField] private bool includeLeaveOption = true;
        [SerializeField] private List<NpcInteractionActionEntry> interactionActions = new List<NpcInteractionActionEntry>();
        [SerializeField] private List<string> dialogueLines = new List<string>();
        [SerializeField] private List<NpcCustomAction> customActions = new List<NpcCustomAction>();

        [Header("Presentation")]
        [SerializeField] private Vector3 menuAnchorOffset = new Vector3(0f, 2.1f, 0f);

        private bool playerInRange;
        private int dialogueIndex;
        private readonly List<ResolvedAction> resolvedActions = new List<ResolvedAction>(12);

        public bool HandlesInteractKey => allowInteractKey;
        public string DisplayName => string.IsNullOrWhiteSpace(npcDisplayName) ? gameObject.name : npcDisplayName;
        public string Greeting => string.IsNullOrWhiteSpace(greeting) ? "..." : greeting;
        public bool HasQuestOption => questGiver != null && questGiver.QuestDefinition != null;
        public bool HasVendorOption => vendorTrigger != null;
        public bool HasDialogueOption => enableDialogueOption && (HasConfiguredDialogue() || !string.IsNullOrWhiteSpace(fallbackDialogue));
        public Vector3 MenuAnchorWorldPosition => questGiver != null ? questGiver.DialogAnchorWorldPosition : transform.position + menuAnchorOffset;

        private void Awake()
        {
            EnsureColliderTrigger();
            ApplyTemplateIfNeeded();
            ResolveReferences();
        }

        private void Reset()
        {
            EnsureColliderTrigger();
            ApplyTemplateIfNeeded();
            ResolveReferences();
        }

        private void OnValidate()
        {
            EnsureColliderTrigger();
            ApplyTemplateIfNeeded();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = false;
            CloseMenuIfNeeded();
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
                OpenInteractionMenu();
            }
        }

        public bool OpenInteractionMenu()
        {
            ResolveReferences();
            if (uiManager == null)
            {
                return false;
            }

            if (interactionModal == null)
            {
                interactionModal = FindFirstObjectByType<NpcInteractionMenuModal>(FindObjectsInactive.Include);
                if (interactionModal == null)
                {
                    interactionModal = NpcInteractionMenuModal.EnsureRuntimeModal(uiManager);
                }
            }

            if (interactionModal == null)
            {
                return false;
            }

            interactionModal.Bind(this);
            if (uiManager.CurrentModal == interactionModal)
            {
                return true;
            }

            uiManager.PushModal(interactionModal);
            return true;
        }

        public int BuildInteractionOptions(List<InteractionOptionView> output)
        {
            if (output == null)
            {
                return 0;
            }

            output.Clear();
            resolvedActions.Clear();
            ResolveReferences();

            if (useActionConfig && TryBuildConfiguredOptions(output))
            {
                return output.Count;
            }

            BuildDefaultOptions(output);
            return output.Count;
        }

        public bool InvokeInteractionOption(int index, out string feedback, out bool closeMenu)
        {
            feedback = string.Empty;
            closeMenu = false;

            if (index < 0 || index >= resolvedActions.Count)
            {
                return false;
            }

            var action = resolvedActions[index];
            if (!action.Interactable)
            {
                feedback = string.IsNullOrWhiteSpace(action.DisabledReason) ? "该选项当前不可用。" : action.DisabledReason;
                return false;
            }

            var requestedClose = action.CloseMenuBeforeInvoke;
            switch (action.ActionType)
            {
                case NpcInteractionActionType.Dialogue:
                {
                    if (!string.IsNullOrWhiteSpace(action.OverrideDialogueLine))
                    {
                        feedback = action.OverrideDialogueLine;
                        closeMenu = requestedClose;
                        return true;
                    }

                    if (TryGetNextDialogue(out var line))
                    {
                        feedback = line;
                        closeMenu = requestedClose;
                        return true;
                    }

                    feedback = "暂无可对话内容。";
                    closeMenu = false;
                    return false;
                }
                case NpcInteractionActionType.Quest:
                {
                    if (OpenQuestOption())
                    {
                        feedback = "已打开任务界面。";
                        closeMenu = requestedClose;
                        return true;
                    }

                    feedback = "任务系统未就绪。";
                    closeMenu = false;
                    return false;
                }
                case NpcInteractionActionType.Trade:
                {
                    if (OpenVendorOption())
                    {
                        feedback = "已打开交易界面。";
                        closeMenu = requestedClose;
                        return true;
                    }

                    feedback = "交易功能未就绪。";
                    closeMenu = false;
                    return false;
                }
                case NpcInteractionActionType.CustomEvent:
                {
                    if (action.ConfigAction != null)
                    {
                        action.ConfigAction.InvokeCustom();
                        closeMenu = requestedClose;
                        return true;
                    }

                    if (action.CustomAction != null)
                    {
                        action.CustomAction.Invoke();
                        closeMenu = requestedClose;
                        return true;
                    }

                    feedback = "自定义动作未配置。";
                    closeMenu = false;
                    return false;
                }
                case NpcInteractionActionType.Close:
                    closeMenu = true;
                    return true;
                default:
                    return false;
            }
        }

        public bool TryGetNextDialogue(out string line)
        {
            line = string.Empty;
            if (!HasDialogueOption)
            {
                return false;
            }

            if (HasConfiguredDialogue())
            {
                if (dialogueIndex < 0 || dialogueIndex >= dialogueLines.Count)
                {
                    dialogueIndex = 0;
                }

                line = dialogueLines[dialogueIndex];
                dialogueIndex = (dialogueIndex + 1) % dialogueLines.Count;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                line = fallbackDialogue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                line = "...";
            }

            return true;
        }

        public bool OpenQuestOption()
        {
            if (questGiver == null)
            {
                return false;
            }

            return questGiver.TryOpenDialogUi();
        }

        public bool OpenVendorOption()
        {
            if (vendorTrigger == null)
            {
                return false;
            }

            return vendorTrigger.TryOpenVendor();
        }

        public void AssignQuestGiver(QuestGiverTrigger trigger)
        {
            if (trigger != null)
            {
                questGiver = trigger;
            }
        }

        public void AssignVendorTrigger(VendorTrigger trigger)
        {
            if (trigger != null)
            {
                vendorTrigger = trigger;
            }
        }

        public void AssignUiManager(UIManager manager)
        {
            if (manager != null)
            {
                uiManager = manager;
            }
        }

        public void AssignInteractionModal(NpcInteractionMenuModal modal)
        {
            if (modal != null)
            {
                interactionModal = modal;
            }
        }

        [ContextMenu("NPC/Apply Selected Interaction Template")]
        public void ApplySelectedInteractionTemplate()
        {
            ApplyInteractionTemplate(interactionTemplatePreset);
        }

        private bool TryBuildConfiguredOptions(List<InteractionOptionView> output)
        {
            if (interactionActions == null || interactionActions.Count == 0)
            {
                if (!autoBuildDefaultActionsWhenEmpty)
                {
                    return false;
                }

                BuildDefaultOptions(output);
                return output.Count > 0;
            }

            var hasCloseAction = false;
            for (int i = 0; i < interactionActions.Count; i++)
            {
                var entry = interactionActions[i];
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                if (entry.ActionType == NpcInteractionActionType.Close)
                {
                    hasCloseAction = true;
                }

                AddResolvedAction(
                    entry.ActionType,
                    entry.Label,
                    entry.CloseMenuBeforeInvoke,
                    entry.OverrideDialogueLine,
                    entry,
                    null,
                    output);
            }

            if (includeLeaveOption && !hasCloseAction)
            {
                AddResolvedAction(NpcInteractionActionType.Close, "离开", true, string.Empty, null, null, output);
            }

            return output.Count > 0;
        }

        private void BuildDefaultOptions(List<InteractionOptionView> output)
        {
            if (enableDialogueOption)
            {
                AddResolvedAction(NpcInteractionActionType.Dialogue, "对话", false, string.Empty, null, null, output);
            }

            if (HasQuestOption)
            {
                AddResolvedAction(NpcInteractionActionType.Quest, "任务", true, string.Empty, null, null, output);
            }

            if (HasVendorOption)
            {
                AddResolvedAction(NpcInteractionActionType.Trade, "交易", true, string.Empty, null, null, output);
            }

            if (customActions != null)
            {
                for (int i = 0; i < customActions.Count; i++)
                {
                    var action = customActions[i];
                    if (action == null || !action.IsValid)
                    {
                        continue;
                    }

                    AddResolvedAction(
                        NpcInteractionActionType.CustomEvent,
                        action.Label,
                        action.CloseMenuBeforeInvoke,
                        string.Empty,
                        null,
                        action,
                        output);
                }
            }

            if (includeLeaveOption || output.Count == 0)
            {
                AddResolvedAction(NpcInteractionActionType.Close, "离开", true, string.Empty, null, null, output);
            }
        }

        private void AddResolvedAction(
            NpcInteractionActionType actionType,
            string label,
            bool closeMenuBeforeInvoke,
            string overrideDialogueLine,
            NpcInteractionActionEntry configEntry,
            NpcCustomAction customAction,
            List<InteractionOptionView> output)
        {
            var resolvedLabel = ResolveActionLabel(actionType, label);
            var interactable = true;
            var disabledReason = string.Empty;

            switch (actionType)
            {
                case NpcInteractionActionType.Dialogue:
                {
                    var hasOverride = !string.IsNullOrWhiteSpace(overrideDialogueLine);
                    interactable = hasOverride || HasDialogueOption;
                    if (!interactable)
                    {
                        disabledReason = "暂无可对话内容。";
                    }

                    break;
                }
                case NpcInteractionActionType.Quest:
                    interactable = HasQuestOption;
                    if (!interactable)
                    {
                        disabledReason = "当前没有可处理任务。";
                    }

                    break;
                case NpcInteractionActionType.Trade:
                    interactable = HasVendorOption;
                    if (!interactable)
                    {
                        disabledReason = "当前无法进行交易。";
                    }

                    break;
                case NpcInteractionActionType.CustomEvent:
                    interactable = configEntry != null || customAction != null;
                    if (!interactable)
                    {
                        disabledReason = "自定义交互未配置。";
                    }

                    break;
                case NpcInteractionActionType.Close:
                    closeMenuBeforeInvoke = true;
                    break;
            }

            if (actionType == NpcInteractionActionType.Dialogue && configEntry == null && customAction == null)
            {
                closeMenuBeforeInvoke = false;
            }

            var resolved = new ResolvedAction
            {
                Label = resolvedLabel,
                Interactable = interactable,
                DisabledReason = disabledReason,
                ActionType = actionType,
                CloseMenuBeforeInvoke = closeMenuBeforeInvoke,
                OverrideDialogueLine = overrideDialogueLine,
                ConfigAction = configEntry,
                CustomAction = customAction
            };

            resolvedActions.Add(resolved);
            output.Add(new InteractionOptionView(resolvedLabel, interactable, disabledReason));
        }

        private static string ResolveActionLabel(NpcInteractionActionType actionType, string label)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            switch (actionType)
            {
                case NpcInteractionActionType.Dialogue:
                    return "对话";
                case NpcInteractionActionType.Quest:
                    return "任务";
                case NpcInteractionActionType.Trade:
                    return "交易";
                case NpcInteractionActionType.CustomEvent:
                    return "自定义";
                case NpcInteractionActionType.Close:
                    return "离开";
                default:
                    return "选项";
            }
        }

        private void ResolveReferences()
        {
            if (questGiver == null)
            {
                questGiver = GetComponent<QuestGiverTrigger>();
            }

            if (vendorTrigger == null)
            {
                vendorTrigger = GetComponent<VendorTrigger>();
            }

            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (interactionModal == null)
            {
                interactionModal = FindFirstObjectByType<NpcInteractionMenuModal>(FindObjectsInactive.Include);
            }
        }

        private void ApplyTemplateIfNeeded()
        {
            if (!autoSyncActionConfigWithTemplate)
            {
                return;
            }

            if (interactionTemplatePreset == NpcInteractionTemplatePreset.Custom)
            {
                lastAppliedTemplatePreset = interactionTemplatePreset;
                return;
            }

            if (interactionTemplatePreset != lastAppliedTemplatePreset || interactionActions == null || interactionActions.Count <= 0)
            {
                ApplyInteractionTemplate(interactionTemplatePreset);
            }
        }

        private void ApplyInteractionTemplate(NpcInteractionTemplatePreset preset)
        {
            interactionTemplatePreset = preset;
            if (preset == NpcInteractionTemplatePreset.Custom)
            {
                lastAppliedTemplatePreset = preset;
                return;
            }

            if (interactionActions == null)
            {
                interactionActions = new List<NpcInteractionActionEntry>();
            }
            else
            {
                interactionActions.Clear();
            }

            useActionConfig = true;
            autoBuildDefaultActionsWhenEmpty = false;

            var hasDialogue = false;
            switch (preset)
            {
                case NpcInteractionTemplatePreset.DialogueOnly:
                    hasDialogue = true;
                    AddTemplateAction("继续交谈", NpcInteractionActionType.Dialogue, false);
                    break;
                case NpcInteractionTemplatePreset.QuestOnly:
                    AddTemplateAction("任务", NpcInteractionActionType.Quest, true);
                    break;
                case NpcInteractionTemplatePreset.TradeOnly:
                    AddTemplateAction("交易", NpcInteractionActionType.Trade, true);
                    break;
                case NpcInteractionTemplatePreset.DialogueAndQuest:
                    hasDialogue = true;
                    AddTemplateAction("对话", NpcInteractionActionType.Dialogue, false);
                    AddTemplateAction("任务", NpcInteractionActionType.Quest, true);
                    break;
                case NpcInteractionTemplatePreset.DialogueAndTrade:
                    hasDialogue = true;
                    AddTemplateAction("对话", NpcInteractionActionType.Dialogue, false);
                    AddTemplateAction("交易", NpcInteractionActionType.Trade, true);
                    break;
                case NpcInteractionTemplatePreset.QuestAndTrade:
                    AddTemplateAction("任务", NpcInteractionActionType.Quest, true);
                    AddTemplateAction("交易", NpcInteractionActionType.Trade, true);
                    break;
                case NpcInteractionTemplatePreset.DialogueQuestTrade:
                    hasDialogue = true;
                    AddTemplateAction("对话", NpcInteractionActionType.Dialogue, false);
                    AddTemplateAction("任务", NpcInteractionActionType.Quest, true);
                    AddTemplateAction("交易", NpcInteractionActionType.Trade, true);
                    break;
                default:
                    break;
            }

            enableDialogueOption = hasDialogue;
            if (includeLeaveOption)
            {
                AddTemplateAction("离开", NpcInteractionActionType.Close, true);
            }

            lastAppliedTemplatePreset = preset;
        }

        private void AddTemplateAction(string label, NpcInteractionActionType actionType, bool closeMenuBeforeInvoke)
        {
            var entry = new NpcInteractionActionEntry();
            entry.Configure(label, actionType, closeMenuBeforeInvoke);
            interactionActions.Add(entry);
        }

        private void EnsureColliderTrigger()
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private bool HasConfiguredDialogue()
        {
            if (dialogueLines == null || dialogueLines.Count <= 0)
            {
                return false;
            }

            for (int i = 0; i < dialogueLines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(dialogueLines[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void CloseMenuIfNeeded()
        {
            if (!closeMenuOnExit)
            {
                return;
            }

            ResolveReferences();
            if (uiManager == null || interactionModal == null)
            {
                return;
            }

            if (uiManager.CurrentModal == interactionModal)
            {
                uiManager.CloseModal(interactionModal);
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
