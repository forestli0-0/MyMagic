using System;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// 背包页内的技能装配面板。
    /// 与 HUD 技能栏共享同一套槽位逻辑，但仅负责菜单内的装配编辑。
    /// </summary>
    public class SkillLoadoutPanelUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SkillBarUI skillBar;
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private CooldownComponent cooldown;

        [Header("Behavior")]
        [SerializeField] private int maxVisibleSlots = 6;
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private string playerTag = "Player";

        private bool subscribed;

        public event Action LoadoutChanged;

        public SkillBarUI SkillBar => skillBar;

        private void OnEnable()
        {
            RefreshBinding();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void RefreshBinding()
        {
            EnsureReferences();

            if (skillBar == null)
            {
                return;
            }

            skillBar.Bind(skillUser, cooldown, maxVisibleSlots);
        }

        public bool CanAssignSkillToSlot(SkillDefinition skill, SkillSlotUI targetSlot)
        {
            EnsureReferences();
            return skillBar != null && skillBar.CanAssignSkillToSlot(skill, targetSlot);
        }

        public bool TryAssignSkillToSlot(SkillDefinition skill, SkillSlotUI targetSlot)
        {
            RefreshBinding();
            return skillBar != null && skillBar.TryAssignSkillToSlot(skill, targetSlot);
        }

        public bool TryGetSkillSlotNumber(SkillDefinition skill, out int slotNumber)
        {
            EnsureReferences();
            if (skillBar != null)
            {
                return skillBar.TryGetSkillSlotNumber(skill, out slotNumber);
            }

            slotNumber = 0;
            return false;
        }

        public SkillSlotUI ResolveSlotAtScreenPoint(Vector2 screenPosition, Camera eventCamera)
        {
            EnsureReferences();
            return skillBar != null ? skillBar.ResolveSlotAtScreenPoint(screenPosition, eventCamera) : null;
        }

        private void EnsureReferences()
        {
            if (skillBar == null)
            {
                skillBar = GetComponentInChildren<SkillBarUI>(true);
            }

            var resolvedSkillUser = skillUser;
            var resolvedCooldown = cooldown;

            if (resolvedSkillUser == null && autoFindPlayer)
            {
                var playerObject = PlayerUnitLocator.FindGameObjectWithTagSafe(playerTag);
                if (playerObject == null)
                {
                    var playerUnit = PlayerUnitLocator.FindPlayerUnit();
                    playerObject = playerUnit != null ? playerUnit.gameObject : null;
                }

                if (playerObject != null)
                {
                    resolvedSkillUser = playerObject.GetComponent<SkillUserComponent>();
                    if (resolvedCooldown == null)
                    {
                        resolvedCooldown = playerObject.GetComponent<CooldownComponent>();
                    }
                }
            }

            if (resolvedSkillUser == null)
            {
                resolvedSkillUser = FindFirstObjectByType<SkillUserComponent>();
            }

            if (resolvedCooldown == null && resolvedSkillUser != null)
            {
                resolvedCooldown = resolvedSkillUser.GetComponent<CooldownComponent>();
            }

            if (resolvedCooldown == null)
            {
                resolvedCooldown = FindFirstObjectByType<CooldownComponent>();
            }

            if (resolvedSkillUser != skillUser)
            {
                Unsubscribe();
                skillUser = resolvedSkillUser;
            }
            else
            {
                skillUser = resolvedSkillUser;
            }

            cooldown = resolvedCooldown;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Subscribe()
        {
            if (subscribed || skillUser == null)
            {
                return;
            }

            skillUser.SkillsChanged += HandleSkillsChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (skillUser != null)
            {
                skillUser.SkillsChanged -= HandleSkillsChanged;
            }

            subscribed = false;
        }

        private void HandleSkillsChanged()
        {
            RefreshBinding();
            LoadoutChanged?.Invoke();
        }
    }
}
