using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 伤害系统，负责计算最终伤害并应用效果。
    /// </summary>
    public static class DamageSystem
    {
        public static void ApplyDamage(float amount, EffectDefinition effect, SkillRuntimeContext context, CombatTarget target, SkillStepTrigger trigger)
        {
            if (effect == null || target.Health == null)
            {
                return;
            }

            var finalAmount = amount + GetScalingValue(effect, context);
            if (finalAmount <= 0f)
            {
                return;
            }

            if (TryRollCritical(effect, context, out var critMultiplier))
            {
                finalAmount *= critMultiplier;
            }

            finalAmount = ModifierResolver.ApplyTargetResistance(finalAmount, effect, context, target);
            if (finalAmount <= 0f)
            {
                return;
            }

            var wasAlive = target.Health.IsAlive;
            var appliedDamage = target.Health.ApplyDamage(finalAmount, out _);
            if (appliedDamage > 0f)
            {
                ApplyVamp(effect, context, appliedDamage);
            }

            var canTriggerOnHit = CanTriggerOnHit(effect, context);
            var notifySkillHit = canTriggerOnHit && trigger != SkillStepTrigger.OnHit && trigger != SkillStepTrigger.OnProjectileHit;
            var notifyBuffHit = trigger != SkillStepTrigger.OnHit;

            if (notifySkillHit)
            {
                context.Caster?.NotifyHit(context, target);
            }

            if (notifyBuffHit)
            {
                var casterBuffs = context.CasterBuffs;
                if (canTriggerOnHit)
                {
                    casterBuffs?.NotifyHit(context, target);
                }

                var attackerTarget = default(CombatTarget);
                if (context.CasterUnit != null)
                {
                    CombatTarget.TryCreate(context.CasterUnit.gameObject, out attackerTarget);
                }

                var targetBuffs = target.Buffs;
                targetBuffs?.NotifyDamaged(context, attackerTarget.IsValid ? attackerTarget : target);

                if (wasAlive && target.Health != null && !target.Health.IsAlive)
                {
                    casterBuffs?.NotifyKill(context, target);
                }
            }
        }

        private static void ApplyVamp(EffectDefinition effect, SkillRuntimeContext context, float appliedDamage)
        {
            if (effect == null || appliedDamage <= 0f)
            {
                return;
            }

            var stats = context.CasterStats;
            if (stats == null)
            {
                return;
            }

            var healRate = stats.GetValueById(CombatStatIds.Omnivamp, 0f);
            if (effect.DamageType == DamageType.Physical)
            {
                healRate += stats.GetValueById(CombatStatIds.Lifesteal, 0f);
            }

            if (healRate <= 0f)
            {
                return;
            }

            // 使用缓存的 Health 引用
            var health = context.CasterHealth;
            if (health == null)
            {
                return;
            }

            health.Heal(appliedDamage * healRate);
        }

        private static float GetScalingValue(EffectDefinition effect, SkillRuntimeContext context)
        {
            if (effect == null || effect.ScalingStat == null || Mathf.Approximately(effect.ScalingRatio, 0f))
            {
                return 0f;
            }

            var stats = context.CasterStats;
            if (stats == null)
            {
                return 0f;
            }

            return stats.GetValue(effect.ScalingStat, 0f) * effect.ScalingRatio;
        }

        private static bool TryRollCritical(EffectDefinition effect, SkillRuntimeContext context, out float critMultiplier)
        {
            critMultiplier = Mathf.Max(1f, effect.CritMultiplier);

            if (effect == null || !effect.CanCrit)
            {
                return false;
            }

            var critChance = effect.CritChance;
            var stats = context.CasterStats;
            if (stats != null)
            {
                if (effect.CritChanceStat != null)
                {
                    critChance += stats.GetValue(effect.CritChanceStat, 0f);
                }

                if (effect.CritMultiplierStat != null)
                {
                    var statMultiplier = stats.GetValue(effect.CritMultiplierStat, 0f);
                    if (statMultiplier > 0f)
                    {
                        critMultiplier = statMultiplier;
                    }
                }
            }

            critChance = Mathf.Clamp01(critChance);
            if (critChance <= 0f)
            {
                return false;
            }

            return Random.value < critChance;
        }

        private static bool CanTriggerOnHit(EffectDefinition effect, SkillRuntimeContext context)
        {
            if (effect == null)
            {
                return false;
            }


            if (context.Caster != null && context.Caster.IsBasicAttackSkill(context.Skill))
            {
                return true;
            }

            return effect.TriggersOnHit;
        }
    }
}
