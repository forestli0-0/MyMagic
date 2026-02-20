using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// 技能栏 UI 组件 - 负责管理和显示所有已装备的技能槽位。
    /// 绑定 SkillUserComponent 和 CooldownComponent 以同步技能冷却状态。
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        [Header("布局设置")]
        [Tooltip("技能槽位的父容器，用于收集子节点槽位")]
        [SerializeField] private Transform slotRoot;
        
        [Tooltip("预分配的技能槽位列表")]
        [SerializeField] private List<SkillSlotUI> slots = new List<SkillSlotUI>(8);
        
        [Tooltip("最大显示槽位数量覆盖(-1 表示使用外部传入的值)")]
        [SerializeField] private int maxSlotsOverride = -1;
        
        [Tooltip("每个槽位对应的快捷键标签")]
        [SerializeField] private string[] keyLabels = { "1", "2", "3", "4", "5", "6" };

        [Header("普攻槽位")]
        [Tooltip("是否在技能栏中预留首格用于显示普攻")]
        [SerializeField] private bool showBasicAttackSlot = true;
        [Tooltip("普攻槽位的快捷键标签（仅显示用途）")]
        [SerializeField] private string basicAttackKeyLabel = "RMB";

        /// <summary>
        /// 绑定的技能使用者组件（提供装备的技能列表）
        /// </summary>
        private SkillUserComponent skillUser;
        
        /// <summary>
        /// 绑定的冷却组件（提供技能冷却状态）
        /// </summary>
        private CooldownComponent cooldown;

        private int lastMaxSlots;
        private readonly List<SkillDefinition> nonBasicSkills = new List<SkillDefinition>(8);

        private void Awake()
        {
            // 初始化时收集子节点中的槽位组件
            CollectSlots();
        }

        /// <summary>
        /// 绑定技能使用者和冷却组件，并重建槽位显示。
        /// </summary>
        /// <param name="user">技能使用者组件</param>
        /// <param name="cooldownComponent">冷却组件</param>
        /// <param name="maxSlots">最大槽位数量</param>
        public void Bind(SkillUserComponent user, CooldownComponent cooldownComponent, int maxSlots)
        {
            if (skillUser != null)
            {
                skillUser.SkillsChanged -= HandleSkillsChanged;
            }

            skillUser = user;
            cooldown = cooldownComponent;
            lastMaxSlots = maxSlots;
            
            if (cooldown == null)
            {
                Debug.LogWarning($"[SkillBarUI] CooldownComponent 为空，技能冷却显示将无法正常工作", this);
            }

            if (skillUser != null)
            {
                skillUser.SkillsChanged += HandleSkillsChanged;
            }
            
            CollectSlots();
            RebuildSlots(maxSlots);
        }

        /// <summary>
        /// 响应冷却变化事件，更新对应技能槽位的冷却显示。
        /// </summary>
        /// <param name="evt">冷却变化事件数据</param>
        public void HandleCooldownChanged(CooldownChangedEvent evt)
        {
            if (evt.Skill == null)
            {
                return;
            }

            // 查找对应技能的槽位并更新冷却状态
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.Skill == evt.Skill)
                {
                    slot.NotifyCooldown(evt.Remaining, evt.Duration, evt.IsCoolingDown);
                    return;
                }
            }
        }

        public void PlaySkillCastPulse(SkillDefinition castedSkill)
        {
            if (castedSkill == null || slots == null || slots.Count == 0)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.Skill != castedSkill)
                {
                    continue;
                }

                slot.PlayCastPulse();
                return;
            }
        }

        private void Update()
        {
            if (cooldown == null || slots.Count == 0)
            {
                return;
            }

            // 每帧刷新所有槽位的冷却显示（用于平滑更新进度条）
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.Skill != null)
                {
                    slot.RefreshCooldown(cooldown);
                }
            }
        }

        private void OnDisable()
        {
            if (skillUser != null)
            {
                skillUser.SkillsChanged -= HandleSkillsChanged;
            }
        }

        private void HandleSkillsChanged()
        {
            RebuildSlots(lastMaxSlots);
        }

        /// <summary>
        /// 从 slotRoot 子节点中收集所有 SkillSlotUI 组件。
        /// 仅在 slots 列表为空时执行收集。
        /// </summary>
        private void CollectSlots()
        {
            if (slotRoot == null)
            {
                slotRoot = transform;
            }

            if (slots.Count == 0)
            {
                slotRoot.GetComponentsInChildren(true, slots);
            }
        }

        /// <summary>
        /// 根据当前装备的技能重建所有槽位的绑定。
        /// </summary>
        /// <param name="maxSlots">最大槽位数量</param>
        private void RebuildSlots(int maxSlots)
        {
            if (skillUser == null)
            {
                return;
            }

            if (!showBasicAttackSlot)
            {
                RebuildSlotsLegacy(maxSlots);
                return;
            }

            var skills = skillUser.Skills;
            var limit = maxSlotsOverride >= 0 ? maxSlotsOverride : maxSlots;
            var displayLimit = Mathf.Min(limit, slots.Count);
            var basicAttack = skillUser.BasicAttack;

            nonBasicSkills.Clear();
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null || skillUser.IsBasicAttackSkill(skill))
                {
                    continue;
                }

                nonBasicSkills.Add(skill);
            }

            var slotCursor = 0;
            var hasBasicSlot = basicAttack != null && displayLimit > 0;
            if (hasBasicSlot)
            {
                slots[slotCursor].BindSkill(basicAttack, basicAttackKeyLabel);
                slotCursor++;
            }

            var nonBasicDisplayCount = Mathf.Min(nonBasicSkills.Count, Mathf.Max(0, displayLimit - slotCursor));
            for (var i = 0; i < nonBasicDisplayCount; i++)
            {
                var slot = slots[slotCursor];
                if (slot == null)
                {
                    slotCursor++;
                    continue;
                }

                var labelIndex = hasBasicSlot ? i : slotCursor;
                var label = labelIndex >= 0 && labelIndex < keyLabels.Length ? keyLabels[labelIndex] : string.Empty;
                slot.BindSkill(nonBasicSkills[i], label);
                slotCursor++;
            }

            for (int i = slotCursor; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.BindSkill(null, string.Empty);
            }
        }

        private void RebuildSlotsLegacy(int maxSlots)
        {
            var skills = skillUser.Skills;
            var limit = maxSlotsOverride >= 0 ? maxSlotsOverride : maxSlots;
            var count = Mathf.Min(limit, skills.Count, slots.Count);

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (i < count)
                {
                    var label = i < keyLabels.Length ? keyLabels[i] : string.Empty;
                    slot.BindSkill(skills[i], label);
                }
                else
                {
                    slot.BindSkill(null, string.Empty);
                }
            }
        }
    }
}
