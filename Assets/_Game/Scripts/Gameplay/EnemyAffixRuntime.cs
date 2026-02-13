using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 敌人词缀运行时应用器。
    /// </summary>
    public static class EnemyAffixRuntime
    {
        public static bool Apply(GameObject target, EnemyAffixDefinition affix)
        {
            if (target == null || affix == null)
            {
                return false;
            }

            ApplyStats(target, affix);
            ApplySkills(target, affix);
            ApplyVisual(target, affix);
            ApplyEliteMarker(target, affix);
            return true;
        }

        private static void ApplyStats(GameObject target, EnemyAffixDefinition affix)
        {
            var stats = target.GetComponent<StatsComponent>();
            if (stats == null)
            {
                return;
            }

            var modifiers = affix.StatModifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier == null || modifier.Stat == null)
                {
                    continue;
                }

                var oldValue = stats.GetValue(modifier.Stat, modifier.Stat.DefaultValue);
                var scaled = oldValue * modifier.Multiplier + modifier.FlatBonus;
                stats.SetValue(modifier.Stat, Mathf.Max(modifier.Stat.MinValue, scaled));
            }

            var health = target.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.RefreshMaxHealth(false);
                health.SetCurrent(health.Max);
            }
        }

        private static void ApplySkills(GameObject target, EnemyAffixDefinition affix)
        {
            var skillUser = target.GetComponent<SkillUserComponent>();
            if (skillUser == null)
            {
                return;
            }

            var combined = new List<SkillDefinition>(skillUser.Skills.Count + affix.BonusSkills.Count);
            for (int i = 0; i < skillUser.Skills.Count; i++)
            {
                var skill = skillUser.Skills[i];
                if (skill != null && !combined.Contains(skill))
                {
                    combined.Add(skill);
                }
            }

            var bonusSkills = affix.BonusSkills;
            for (int i = 0; i < bonusSkills.Count; i++)
            {
                var skill = bonusSkills[i];
                if (skill != null && !combined.Contains(skill))
                {
                    combined.Add(skill);
                }
            }

            skillUser.SetSkills(combined, true);
        }

        private static void ApplyVisual(GameObject target, EnemyAffixDefinition affix)
        {
            target.transform.localScale *= affix.ScaleMultiplier;

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            var tint = affix.TintColor;
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", tint);
                block.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(block);
            }
        }

        private static void ApplyEliteMarker(GameObject target, EnemyAffixDefinition affix)
        {
            var marker = target.GetComponent<EliteUnitMarker>();
            if (marker == null)
            {
                marker = target.AddComponent<EliteUnitMarker>();
            }

            marker.SetAffix(affix);
            target.name = $"{target.name} [Elite]";
        }
    }
}
