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
        public readonly float ChargeDuration;
        public readonly float ChargeRatio;
        public readonly float ChargeMultiplier;
        public readonly ulong CastId;
        public readonly int StepIndex;
        public readonly int SequencePhase;

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
            GameObject explicitTarget = null,
            float chargeDuration = 0f,
            float chargeRatio = 0f,
            float chargeMultiplier = 1f)
            : this(
                caster,
                casterUnit,
                skill,
                eventHub,
                targeting,
                executor,
                hasAimPoint,
                aimPoint,
                aimDirection,
                explicitTarget,
                chargeDuration,
                chargeRatio,
                chargeMultiplier,
                0UL,
                -1)
        {
        }

        public SkillRuntimeContext(
            SkillUserComponent caster,
            UnitRoot casterUnit,
            SkillDefinition skill,
            CombatEventHub eventHub,
            TargetingSystem targeting,
            EffectExecutor executor,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            GameObject explicitTarget,
            float chargeDuration,
            float chargeRatio,
            float chargeMultiplier,
            ulong castId,
            int stepIndex)
            : this(
                caster,
                casterUnit,
                skill,
                eventHub,
                targeting,
                executor,
                hasAimPoint,
                aimPoint,
                aimDirection,
                explicitTarget,
                chargeDuration,
                chargeRatio,
                chargeMultiplier,
                castId,
                stepIndex,
                1)
        {
        }

        public SkillRuntimeContext(
            SkillUserComponent caster,
            UnitRoot casterUnit,
            SkillDefinition skill,
            CombatEventHub eventHub,
            TargetingSystem targeting,
            EffectExecutor executor,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            GameObject explicitTarget,
            float chargeDuration,
            float chargeRatio,
            float chargeMultiplier,
            ulong castId,
            int stepIndex,
            int sequencePhase)
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
            ChargeDuration = Mathf.Max(0f, chargeDuration);
            ChargeRatio = Mathf.Clamp01(chargeRatio);
            ChargeMultiplier = Mathf.Max(0f, chargeMultiplier);
            CastId = castId;
            StepIndex = stepIndex;
            SequencePhase = Mathf.Max(1, sequencePhase);

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

        public SkillRuntimeContext WithStepIndex(int stepIndex)
        {
            return new SkillRuntimeContext(
                Caster,
                CasterUnit,
                Skill,
                EventHub,
                Targeting,
                Executor,
                HasAimPoint,
                AimPoint,
                AimDirection,
                ExplicitTarget,
                ChargeDuration,
                ChargeRatio,
                ChargeMultiplier,
                CastId,
                stepIndex,
                SequencePhase);
        }
    }
}
