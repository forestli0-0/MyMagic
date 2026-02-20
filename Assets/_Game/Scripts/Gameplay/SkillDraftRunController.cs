using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.UI;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 局内技能招募控制器：
    /// - 玩家升级时触发技能三选一
    /// - 技能槽已满时进入替换流程
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillDraftRunController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameDatabase gameDatabase;
        [SerializeField] private SkillDraftPoolDefinition draftPool;

        [Header("Draft Rules")]
        [SerializeField] private bool enableLevelUpDraft = true;
        [SerializeField, Range(1, 5)] private int offersPerDraft = 3;
        [Tooltip("可携带的主动技能上限（锁定普攻槽位时不包含普攻）")]
        [SerializeField, Min(1)] private int maxActiveSkills = 5;
        [SerializeField] private bool lockBasicAttackSlot = true;
        [SerializeField] private bool waitUntilNoOtherModal = true;
        [SerializeField] private bool showToastOnPick = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog;

        private readonly List<SkillDraftChoice> currentOffers = new List<SkillDraftChoice>(4);
        private readonly List<int> replaceableSkillIndices = new List<int>(8);
        private readonly List<SkillDefinition> replaceableSkills = new List<SkillDefinition>(8);
        private readonly List<SkillDefinition> discoveredSkills = new List<SkillDefinition>(64);
        private readonly HashSet<SkillDefinition> discoveredSkillSet = new HashSet<SkillDefinition>();

        private int pendingDraftCount;
        private bool showingReplacementStep;
        private bool hasPendingReplacementChoice;
        private SkillDraftChoice pendingReplacementChoice;
        private SkillDraftModal draftModal;

        private void Reset()
        {
            progression = GetComponent<PlayerProgression>();
            skillUser = GetComponent<SkillUserComponent>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            TryOpenDraftIfNeeded();
        }

        private void Subscribe()
        {
            if (progression != null)
            {
                progression.LevelChanged -= HandleLevelChanged;
                progression.LevelChanged += HandleLevelChanged;
            }
        }

        private void Unsubscribe()
        {
            if (progression != null)
            {
                progression.LevelChanged -= HandleLevelChanged;
            }
        }

        private void HandleLevelChanged(LevelChangedEvent evt)
        {
            if (!enableLevelUpDraft || progression == null || evt.Source != progression)
            {
                return;
            }

            var gainedLevels = Mathf.Max(0, evt.NewLevel - evt.OldLevel);
            if (gainedLevels <= 0)
            {
                return;
            }

            pendingDraftCount += gainedLevels;
            Log($"玩家升级 {evt.OldLevel} -> {evt.NewLevel}，累计待选次数：{pendingDraftCount}");
            TryOpenDraftIfNeeded();
        }

        private void TryOpenDraftIfNeeded()
        {
            if (!enableLevelUpDraft || pendingDraftCount <= 0)
            {
                return;
            }

            ResolveReferences();
            if (skillUser == null || uiManager == null)
            {
                return;
            }

            if (waitUntilNoOtherModal && uiManager.ModalCount > 0 && uiManager.CurrentModal != draftModal)
            {
                return;
            }

            if (draftModal != null && uiManager.CurrentModal == draftModal)
            {
                return;
            }

            if (currentOffers.Count <= 0)
            {
                if (!GenerateOffers())
                {
                    pendingDraftCount = Mathf.Max(0, pendingDraftCount - 1);
                    UIToast.Warning("未找到可用技能，已跳过本次升级奖励。");
                    Log("候选池为空，自动跳过一次奖励。");
                    return;
                }
            }

            EnsureDraftModal();
            if (draftModal == null)
            {
                return;
            }

            showingReplacementStep = false;
            hasPendingReplacementChoice = false;
            draftModal.PresentOffers(currentOffers, pendingDraftCount, HandleOfferSelected);
            if (uiManager.CurrentModal != draftModal)
            {
                uiManager.PushModal(draftModal);
            }
        }

        private bool GenerateOffers()
        {
            currentOffers.Clear();
            var sourceSkills = ResolveSkillSource();
            var count = SkillDraftOfferGenerator.GenerateOffers(
                draftPool,
                sourceSkills,
                skillUser != null ? skillUser.Skills : null,
                skillUser != null ? skillUser.BasicAttack : null,
                offersPerDraft,
                currentOffers);
            return count > 0;
        }

        private IReadOnlyList<SkillDefinition> ResolveSkillSource()
        {
            if (gameDatabase != null && gameDatabase.Skills != null && gameDatabase.Skills.Count > 0)
            {
                return gameDatabase.Skills;
            }

            if (gameDatabase == null)
            {
                var databases = Resources.FindObjectsOfTypeAll<GameDatabase>();
                if (databases != null && databases.Length > 0)
                {
                    gameDatabase = databases[0];
                    if (gameDatabase.Skills != null && gameDatabase.Skills.Count > 0)
                    {
                        return gameDatabase.Skills;
                    }
                }
            }

            discoveredSkills.Clear();
            discoveredSkillSet.Clear();
            var loadedSkills = Resources.FindObjectsOfTypeAll<SkillDefinition>();
            for (var i = 0; i < loadedSkills.Length; i++)
            {
                var skill = loadedSkills[i];
                if (skill == null || !discoveredSkillSet.Add(skill))
                {
                    continue;
                }

                discoveredSkills.Add(skill);
            }

            return discoveredSkills;
        }

        private void HandleOfferSelected(int offerIndex)
        {
            if (offerIndex < 0 || offerIndex >= currentOffers.Count || skillUser == null)
            {
                return;
            }

            var choice = currentOffers[offerIndex];
            if (choice.Skill == null)
            {
                return;
            }

            if (skillUser.HasSkill(choice.Skill))
            {
                UIToast.Info($"已拥有技能：{ResolveSkillName(choice.Skill)}");
                return;
            }

            var full = maxActiveSkills > 0 && GetCurrentActiveSkillCountForLimit() >= maxActiveSkills;
            if (!full)
            {
                if (skillUser.TryAddSkill(choice.Skill, GetRuntimeSkillLimit()))
                {
                    FinalizeDraft(choice.Skill, false, null);
                    return;
                }

                // 理论上不应走到这里，兜底转替换流程。
                Log("直接添加技能失败，转入替换流程。");
            }

            StartReplacementStep(choice);
        }

        private void StartReplacementStep(SkillDraftChoice choice)
        {
            BuildReplaceableSkillList();
            if (replaceableSkillIndices.Count <= 0)
            {
                UIToast.Warning("当前没有可替换技能，已跳过本次奖励。");
                currentOffers.Clear();
                pendingDraftCount = Mathf.Max(0, pendingDraftCount - 1);
                CloseDraftModalIfOpen();
                return;
            }

            pendingReplacementChoice = choice;
            hasPendingReplacementChoice = true;
            showingReplacementStep = true;

            EnsureDraftModal();
            if (draftModal == null)
            {
                return;
            }

            draftModal.PresentReplacement(
                choice,
                replaceableSkills,
                pendingDraftCount,
                HandleReplacementSelected,
                HandleBackToOffers);
            if (uiManager != null && uiManager.CurrentModal != draftModal)
            {
                uiManager.PushModal(draftModal);
            }
        }

        private void HandleBackToOffers()
        {
            if (!showingReplacementStep || draftModal == null)
            {
                return;
            }

            showingReplacementStep = false;
            hasPendingReplacementChoice = false;
            draftModal.PresentOffers(currentOffers, pendingDraftCount, HandleOfferSelected);
        }

        private void HandleReplacementSelected(int replaceIndex)
        {
            if (!hasPendingReplacementChoice || skillUser == null)
            {
                return;
            }

            if (replaceIndex < 0 || replaceIndex >= replaceableSkillIndices.Count)
            {
                return;
            }

            var runtimeIndex = replaceableSkillIndices[replaceIndex];
            var replacedSkill = runtimeIndex >= 0 && runtimeIndex < skillUser.Skills.Count
                ? skillUser.Skills[runtimeIndex]
                : null;

            if (!skillUser.TryReplaceSkill(runtimeIndex, pendingReplacementChoice.Skill, lockBasicAttackSlot))
            {
                UIToast.Warning("技能替换失败，请重试。");
                return;
            }

            FinalizeDraft(pendingReplacementChoice.Skill, true, replacedSkill);
        }

        private void BuildReplaceableSkillList()
        {
            replaceableSkillIndices.Clear();
            replaceableSkills.Clear();
            if (skillUser == null || skillUser.Skills == null)
            {
                return;
            }

            var skills = skillUser.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                if (lockBasicAttackSlot && skillUser.IsBasicAttackSkill(skill))
                {
                    continue;
                }

                replaceableSkillIndices.Add(i);
                replaceableSkills.Add(skill);
            }
        }

        private void FinalizeDraft(SkillDefinition pickedSkill, bool replaced, SkillDefinition replacedSkill)
        {
            pendingDraftCount = Mathf.Max(0, pendingDraftCount - 1);
            showingReplacementStep = false;
            hasPendingReplacementChoice = false;
            currentOffers.Clear();
            replaceableSkillIndices.Clear();
            replaceableSkills.Clear();

            CloseDraftModalIfOpen();

            if (showToastOnPick && pickedSkill != null)
            {
                if (replaced && replacedSkill != null)
                {
                    UIToast.Success($"已替换技能：{ResolveSkillName(replacedSkill)} -> {ResolveSkillName(pickedSkill)}");
                }
                else
                {
                    UIToast.Success($"习得技能：{ResolveSkillName(pickedSkill)}");
                }
            }

            Log($"完成一次技能选择，剩余待选次数：{pendingDraftCount}");
            TryOpenDraftIfNeeded();
        }

        private void EnsureDraftModal()
        {
            if (draftModal != null)
            {
                return;
            }

            draftModal = SkillDraftModal.EnsureRuntimeModal(uiManager);
        }

        private void CloseDraftModalIfOpen()
        {
            if (uiManager == null || draftModal == null)
            {
                return;
            }

            uiManager.CloseModal(draftModal);
        }

        private static string ResolveSkillName(SkillDefinition skill)
        {
            if (skill == null)
            {
                return "未知技能";
            }

            if (!string.IsNullOrWhiteSpace(skill.DisplayName))
            {
                return skill.DisplayName;
            }

            return skill.name;
        }

        private void ResolveReferences()
        {
            if (progression == null)
            {
                progression = GetComponent<PlayerProgression>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            if (uiManager == null)
            {
                var root = UIRoot.Instance != null ? UIRoot.Instance : FindFirstObjectByType<UIRoot>(FindObjectsInactive.Include);
                uiManager = root != null ? root.Manager : FindFirstObjectByType<UIManager>();
            }
        }

        private void Log(string message)
        {
            if (!enableDebugLog)
            {
                return;
            }

            Debug.Log($"[SkillDraftRunController] {message}", this);
        }

        /// <summary>
        /// 获取用于“技能上限”判定的当前技能数量。
        /// 当锁定普攻槽位时，普攻不计入技能上限。
        /// </summary>
        private int GetCurrentActiveSkillCountForLimit()
        {
            if (skillUser == null || skillUser.Skills == null)
            {
                return 0;
            }

            if (!lockBasicAttackSlot)
            {
                return skillUser.Skills.Count;
            }

            var count = 0;
            var skills = skillUser.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null || skillUser.IsBasicAttackSkill(skill))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// SkillUserComponent 的运行时上限参数。
        /// 当锁定普攻槽位时，允许“普攻 + maxActiveSkills”。
        /// </summary>
        private int GetRuntimeSkillLimit()
        {
            if (maxActiveSkills <= 0)
            {
                return -1;
            }

            if (!lockBasicAttackSlot || skillUser == null || skillUser.BasicAttack == null)
            {
                return maxActiveSkills;
            }

            return maxActiveSkills + 1;
        }
    }
}
