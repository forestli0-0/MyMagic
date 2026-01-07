using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 技能运行时上下文，封装施法者信息和技能配置。
    /// </summary>
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
        public readonly GameObject ExplicitTarget;

        // 施法者组件缓存，避免热路径 GetComponent 调用
        /// <summary>施法者属性组件缓存</summary>
        public readonly StatsComponent CasterStats;
        /// <summary>施法者生命组件缓存</summary>
        public readonly HealthComponent CasterHealth;
        /// <summary>施法者 Buff 控制器缓存</summary>
        public readonly BuffController CasterBuffs;

        public SkillRuntimeContext(
            SkillUserComponent caster,
            UnitRoot casterUnit,
            SkillDefinition skill,
            CombatEventHub eventHub,
            TargetingSystem targeting,
            EffectExecutor executor,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default,
            GameObject explicitTarget = null)
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
            ExplicitTarget = explicitTarget;

            if (casterUnit != null)
            {
                CasterStats = casterUnit.GetComponent<StatsComponent>();
                CasterHealth = casterUnit.GetComponent<HealthComponent>();
                CasterBuffs = casterUnit.GetComponent<BuffController>();
            }
            else
            {
                CasterStats = null;
                CasterHealth = null;
                CasterBuffs = null;
            }
        }
    }
}
