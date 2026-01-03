using CombatSystem.Data;

namespace CombatSystem.Gameplay
{
    public static class DamageSystem
    {
        public static void ApplyDamage(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target, SkillStepTrigger trigger)
        {
            if (effect == null || target.Health == null)
            {
                return;
            }

            var amount = effect.Value;
            if (amount <= 0f)
            {
                return;
            }

            target.Health.ApplyDamage(amount);

            if (trigger != SkillStepTrigger.OnHit && trigger != SkillStepTrigger.OnProjectileHit)
            {
                context.Caster?.NotifyHit(context, target);
            }
        }
    }
}
