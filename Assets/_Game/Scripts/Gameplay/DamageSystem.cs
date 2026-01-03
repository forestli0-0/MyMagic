using CombatSystem.Core;
using CombatSystem.Data;

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

            if (amount <= 0f)
            {
                return;
            }

            var wasAlive = target.Health.IsAlive;
            target.Health.ApplyDamage(amount);

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
    }
}
