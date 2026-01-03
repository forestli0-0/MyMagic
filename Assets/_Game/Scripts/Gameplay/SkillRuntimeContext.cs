using CombatSystem.Core;
using CombatSystem.Data;

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

        public SkillRuntimeContext(
            SkillUserComponent caster,
            UnitRoot casterUnit,
            SkillDefinition skill,
            CombatEventHub eventHub,
            TargetingSystem targeting,
            EffectExecutor executor)
        {
            Caster = caster;
            CasterUnit = casterUnit;
            Skill = skill;
            EventHub = eventHub;
            Targeting = targeting;
            Executor = executor;
        }
    }
}
