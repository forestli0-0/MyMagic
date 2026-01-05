using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    public readonly struct SkillRuntimeContext
    {
        public readonly SkillUserComponent Caster;
        public readonly UnitRoot CasterUnit;
        public readonly SkillDefinition Skill;
        public readonly CombatEventHub EventHub;
        public readonly TargetingSystem Targeting;
        public readonly EffectExecutor Executor;
        public readonly bool HasAimPoint;
        public readonly Vector3 AimPoint;
        public readonly Vector3 AimDirection;

        public SkillRuntimeContext(
            SkillUserComponent caster,
            UnitRoot casterUnit,
            SkillDefinition skill,
            CombatEventHub eventHub,
            TargetingSystem targeting,
            EffectExecutor executor,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default)
        {
            Caster = caster;
            CasterUnit = casterUnit;
            Skill = skill;
            EventHub = eventHub;
            Targeting = targeting;
            Executor = executor;
            HasAimPoint = hasAimPoint;
            AimPoint = aimPoint;
            AimDirection = aimDirection;
        }
    }
}
