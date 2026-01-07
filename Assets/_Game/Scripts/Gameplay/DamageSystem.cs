using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
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
            target.Health.ApplyDamage(finalAmount, out _);

            var notifySkillHit = trigger != SkillStepTrigger.OnHit && trigger != SkillStepTrigger.OnProjectileHit;
            var notifyBuffHit = trigger != SkillStepTrigger.OnHit;

            if (notifySkillHit)
            {
                context.Caster?.NotifyHit(context, target);
            }

            if (notifyBuffHit)
            {
                var casterBuffs = context.CasterUnit != null ? context.CasterUnit.GetComponent<BuffController>() : null;
                casterBuffs?.NotifyHit(context, target);

                var attackerTarget = default(CombatTarget);
                if (context.CasterUnit != null)
                {
                    CombatTarget.TryCreate(context.CasterUnit.gameObject, out attackerTarget);
                }

                var targetBuffs = target.Unit != null ? target.Unit.GetComponent<BuffController>() : target.GameObject.GetComponent<BuffController>();
                targetBuffs?.NotifyDamaged(context, attackerTarget.IsValid ? attackerTarget : target);

                if (wasAlive && target.Health != null && !target.Health.IsAlive)
                {
                    casterBuffs?.NotifyKill(context, target);
                }
            }
        }

        private static float GetScalingValue(EffectDefinition effect, SkillRuntimeContext context)
        {
            if (effect == null || effect.ScalingStat == null || Mathf.Approximately(effect.ScalingRatio, 0f))
            {
                return 0f;
            }

            if (context.CasterUnit == null)
            {
                return 0f;
            }

            var stats = context.CasterUnit.GetComponent<StatsComponent>();
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
            var caster = context.CasterUnit;
            if (caster != null)
            {
                var stats = caster.GetComponent<StatsComponent>();
                if (stats != null && effect.CritChanceStat != null)
                {
                    critChance += stats.GetValue(effect.CritChanceStat, 0f);
                }

                if (stats != null && effect.CritMultiplierStat != null)
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
    }
}
