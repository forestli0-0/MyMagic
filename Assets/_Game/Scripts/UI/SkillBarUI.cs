using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private bool keepEmptySlotsVisible = true;

        [Header("外观")]
        [SerializeField] private Image barBackground;
        [SerializeField] private bool autoCreateBarBackground = true;
        [SerializeField] private Color barBackgroundColor = new Color(0.03f, 0.05f, 0.09f, 0.82f);
        [SerializeField] private bool addBarOutline = true;
        [SerializeField] private Color barOutlineColor = new Color(0.26f, 0.46f, 0.82f, 0.85f);
        [SerializeField] private bool autoApplyLayoutPadding = true;
        [SerializeField] private Vector2 layoutPadding = new Vector2(10f, 8f);

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
        private static Sprite fallbackSprite;

        private void Awake()
        {
            // 初始化时收集子节点中的槽位组件
            CollectSlots();
            EnsureBarVisual();
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
            var reserveBasicSlot = showBasicAttackSlot && displayLimit > 0;

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

            if (reserveBasicSlot)
            {
                var basicSlot = slots[0];
                if (basicSlot != null)
                {
                    basicSlot.SetSlotVisible(true);
                    basicSlot.BindSkill(basicAttack, basicAttackKeyLabel);
                }
            }

            var nonBasicBaseIndex = reserveBasicSlot ? 1 : 0;
            var nonBasicVisibleCount = Mathf.Max(0, displayLimit - nonBasicBaseIndex);
            for (var i = 0; i < nonBasicVisibleCount; i++)
            {
                var slotIndex = i + nonBasicBaseIndex;
                if (slotIndex < 0 || slotIndex >= slots.Count)
                {
                    break;
                }

                var slot = slots[slotIndex];
                if (slot == null)
                {
                    continue;
                }

                slot.SetSlotVisible(true);
                var keyLabel = ResolveSkillSlotKeyLabel(i);
                var skill = i < nonBasicSkills.Count ? nonBasicSkills[i] : null;
                slot.BindSkill(skill, keyLabel);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                var shouldShow = keepEmptySlotsVisible
                    ? i < displayLimit
                    : (i < displayLimit && slot.Skill != null);
                slot.SetSlotVisible(shouldShow);

                if (shouldShow)
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
            var displayLimit = Mathf.Min(limit, slots.Count);
            var count = Mathf.Min(displayLimit, skills.Count);

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                var shouldShow = keepEmptySlotsVisible ? i < displayLimit : i < count;
                slot.SetSlotVisible(shouldShow);
                if (!shouldShow)
                {
                    slot.BindSkill(null, string.Empty);
                    continue;
                }

                var label = i < keyLabels.Length ? keyLabels[i] : string.Empty;
                if (i < count)
                {
                    slot.BindSkill(skills[i], label);
                }
                else
                {
                    slot.BindSkill(null, label);
                }
            }
        }

        private string ResolveSkillSlotKeyLabel(int nonBasicIndex)
        {
            if (nonBasicIndex < 0 || nonBasicIndex >= keyLabels.Length)
            {
                return string.Empty;
            }

            return keyLabels[nonBasicIndex] ?? string.Empty;
        }

        /// <summary>
        /// 检查指定技能是否可装配到给定的可视化槽位。
        /// </summary>
        public bool CanAssignSkillToSlot(SkillDefinition skill, SkillSlotUI targetSlot)
        {
            if (targetSlot == null)
            {
                return false;
            }

            var slotIndex = slots.IndexOf(targetSlot);
            return CanAssignSkillToSlotIndex(skill, slotIndex);
        }

        /// <summary>
        /// 将指定技能装配到给定的可视化槽位。
        /// 会自动处理“普攻首槽保留”的索引映射。
        /// </summary>
        public bool TryAssignSkillToSlot(SkillDefinition skill, SkillSlotUI targetSlot)
        {
            if (targetSlot == null)
            {
                return false;
            }

            var slotIndex = slots.IndexOf(targetSlot);
            return TryAssignSkillToSlotIndex(skill, slotIndex);
        }

        /// <summary>
        /// 查询某技能当前装配在技能栏的第几号技能槽（不含普攻保留槽）。
        /// </summary>
        /// <param name="skill">技能定义</param>
        /// <param name="slotNumber">返回 1-based 槽位编号；未装配时为 0</param>
        public bool TryGetSkillSlotNumber(SkillDefinition skill, out int slotNumber)
        {
            slotNumber = 0;
            if (skill == null || slots == null || slots.Count == 0)
            {
                return false;
            }

            var displayLimit = GetDisplayLimit();
            var searchStart = showBasicAttackSlot ? 1 : 0;
            var searchEnd = Mathf.Min(displayLimit, slots.Count);
            for (int visualIndex = searchStart; visualIndex < searchEnd; visualIndex++)
            {
                var slot = slots[visualIndex];
                if (slot == null || slot.Skill != skill)
                {
                    continue;
                }

                slotNumber = showBasicAttackSlot ? visualIndex : visualIndex + 1;
                return slotNumber > 0;
            }

            return false;
        }

        /// <summary>
        /// 根据屏幕坐标解析当前命中的技能槽位。
        /// 用于拖拽结束时的兜底命中（避免 pointerEnter 丢失）。
        /// </summary>
        public SkillSlotUI ResolveSlotAtScreenPoint(Vector2 screenPosition, Camera eventCamera)
        {
            if (slots == null || slots.Count == 0)
            {
                return null;
            }

            var displayLimit = GetDisplayLimit();
            var upperBound = Mathf.Min(displayLimit, slots.Count);
            for (int i = 0; i < upperBound; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var rect = slot.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
                {
                    return slot;
                }
            }

            return null;
        }

        private bool CanAssignSkillToSlotIndex(SkillDefinition skill, int slotIndex)
        {
            if (skillUser == null || skill == null || slotIndex < 0)
            {
                return false;
            }

            if (skillUser.IsBasicAttackSkill(skill))
            {
                return false;
            }

            var displayLimit = GetDisplayLimit();
            if (slotIndex >= displayLimit)
            {
                return false;
            }

            if (showBasicAttackSlot && slotIndex == 0)
            {
                return false;
            }

            var nonBasicCapacity = Mathf.Max(0, displayLimit - (showBasicAttackSlot ? 1 : 0));
            var targetNonBasicIndex = showBasicAttackSlot ? slotIndex - 1 : slotIndex;
            return nonBasicCapacity > 0 && targetNonBasicIndex >= 0 && targetNonBasicIndex < nonBasicCapacity;
        }

        private bool TryAssignSkillToSlotIndex(SkillDefinition skill, int slotIndex)
        {
            if (!CanAssignSkillToSlotIndex(skill, slotIndex))
            {
                return false;
            }

            var displayLimit = GetDisplayLimit();
            var nonBasicCapacity = Mathf.Max(0, displayLimit - (showBasicAttackSlot ? 1 : 0));
            var targetNonBasicIndex = showBasicAttackSlot ? slotIndex - 1 : slotIndex;
            if (targetNonBasicIndex < 0 || targetNonBasicIndex >= nonBasicCapacity)
            {
                return false;
            }

            nonBasicSkills.Clear();
            var skills = skillUser.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                var current = skills[i];
                if (current == null || skillUser.IsBasicAttackSkill(current))
                {
                    continue;
                }

                nonBasicSkills.Add(current);
            }

            // 先去重，避免同一个技能同时占用多个槽位。
            nonBasicSkills.Remove(skill);

            if (targetNonBasicIndex < nonBasicSkills.Count)
            {
                nonBasicSkills[targetNonBasicIndex] = skill;
            }
            else
            {
                // 当前工程空槽位是连续排列，索引超出时按“追加到最后已占槽位”处理。
                nonBasicSkills.Add(skill);
            }

            if (nonBasicSkills.Count > nonBasicCapacity)
            {
                nonBasicSkills.RemoveRange(nonBasicCapacity, nonBasicSkills.Count - nonBasicCapacity);
            }

            skillUser.SetSkills(nonBasicSkills, true);
            return true;
        }

        private int GetDisplayLimit()
        {
            var baseLimit = maxSlotsOverride >= 0
                ? maxSlotsOverride
                : (lastMaxSlots > 0 ? lastMaxSlots : slots.Count);
            return Mathf.Clamp(baseLimit, 0, slots.Count);
        }

        private void EnsureBarVisual()
        {
            if (barBackground == null)
            {
                barBackground = GetComponent<Image>();
            }

            if (barBackground == null && autoCreateBarBackground)
            {
                barBackground = gameObject.AddComponent<Image>();
            }

            if (barBackground != null)
            {
                if (barBackground.sprite == null)
                {
                    barBackground.sprite = GetDefaultSprite();
                }

                barBackground.type = Image.Type.Sliced;
                barBackground.color = barBackgroundColor;
                barBackground.raycastTarget = false;
            }

            if (addBarOutline)
            {
                var outline = GetComponent<Outline>();
                if (outline == null)
                {
                    outline = gameObject.AddComponent<Outline>();
                }

                outline.effectColor = barOutlineColor;
                outline.effectDistance = new Vector2(1.2f, -1.2f);
                outline.useGraphicAlpha = true;
            }

            if (!autoApplyLayoutPadding || slotRoot == null)
            {
                return;
            }

            var layout = slotRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                return;
            }

            var horizontal = Mathf.Max(0, Mathf.RoundToInt(layoutPadding.x));
            var vertical = Mathf.Max(0, Mathf.RoundToInt(layoutPadding.y));
            layout.padding = new RectOffset(horizontal, horizontal, vertical, vertical);
            EnsureSkillBarSize(layout);
        }

        private void EnsureSkillBarSize(HorizontalLayoutGroup layout)
        {
            var barRect = transform as RectTransform;
            if (barRect == null || slots == null || slots.Count == 0)
            {
                return;
            }

            var firstSlotRect = slots[0] != null ? slots[0].transform as RectTransform : null;
            if (firstSlotRect == null)
            {
                return;
            }

            var slotSize = firstSlotRect.rect.size;
            if (slotSize.x <= 0f || slotSize.y <= 0f)
            {
                return;
            }

            var slotCount = slots.Count;
            var requiredWidth = slotCount * slotSize.x
                + Mathf.Max(0, slotCount - 1) * layout.spacing
                + layout.padding.left
                + layout.padding.right;
            var requiredHeight = slotSize.y + layout.padding.top + layout.padding.bottom;

            var current = barRect.sizeDelta;
            if (current.x + 0.1f < requiredWidth || current.y + 0.1f < requiredHeight)
            {
                barRect.sizeDelta = new Vector2(
                    Mathf.Max(current.x, requiredWidth),
                    Mathf.Max(current.y, requiredHeight));
            }
        }

        private static Sprite GetDefaultSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            var texture = Texture2D.whiteTexture;
            if (texture != null)
            {
                fallbackSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            return fallbackSprite;
        }
    }
}
