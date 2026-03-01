using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 属性变更事件数据。
    /// </summary>
    public struct StatChangedEvent
    {
        public StatsComponent Source;   // 来源组件
        public StatDefinition Stat;      // 发生的变更属性定义
        public float OldValue;          // 变更前的值
        public float NewValue;          // 变更后的值
        public float Delta;             // 变化量

        public StatChangedEvent(StatsComponent source, StatDefinition stat, float oldValue, float newValue)
        {
            Source = source;
            Stat = stat;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
        }
    }

    /// <summary>
    /// 生命值变更事件数据。
    /// </summary>
    public struct HealthChangedEvent
    {
        public HealthComponent Source;   // 来源组件
        public float OldValue;          // 变更前的值
        public float NewValue;          // 变更后的值
        public float Delta;             // 变化量
        public bool IsAlive;            // 变更后是否存活

        public HealthChangedEvent(HealthComponent source, float oldValue, float newValue)
        {
            Source = source;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
            IsAlive = newValue > 0f;
        }
    }

    /// <summary>
    /// 护盾数值变更事件数据。
    /// </summary>
    public struct ShieldChangedEvent
    {
        public HealthComponent Source;  // 来源组件
        public float OldValue;          // 变更前的值
        public float NewValue;          // 变更后的值
        public float Delta;             // 变化量

        public ShieldChangedEvent(HealthComponent source, float oldValue, float newValue)
        {
            Source = source;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
        }
    }

    /// <summary>
    /// 伤害来源信息（用于击杀归属等）。
    /// </summary>
    public struct DamageSourceInfo
    {
        public UnitRoot SourceUnit;
        public SkillDefinition Skill;
        public EffectDefinition Effect;
        public SkillStepTrigger Trigger;

        public DamageSourceInfo(UnitRoot sourceUnit, SkillDefinition skill, EffectDefinition effect, SkillStepTrigger trigger)
        {
            SourceUnit = sourceUnit;
            Skill = skill;
            Effect = effect;
            Trigger = trigger;
        }
    }

    /// <summary>
    /// 单位死亡事件数据（包含伤害来源）。
    /// </summary>
    public struct UnitKilledEvent
    {
        public HealthComponent Victim;
        public DamageSourceInfo Source;

        public UnitKilledEvent(HealthComponent victim, DamageSourceInfo source)
        {
            Victim = victim;
            Source = source;
        }
    }

    /// <summary>
    /// 伤害结算事件数据（用于命中反馈、音效与镜头效果）。
    /// </summary>
    public struct DamageAppliedEvent
    {
        public UnitRoot Attacker;
        public HealthComponent Target;
        public float RequestedDamage;
        public float PostResistanceDamage;
        public float AppliedDamage;
        public float AbsorbedByShield;
        public bool IsCritical;
        public bool TargetKilled;
        public SkillDefinition Skill;
        public EffectDefinition Effect;
        public SkillStepTrigger Trigger;

        public float TotalImpact => AppliedDamage + AbsorbedByShield;

        public DamageAppliedEvent(
            UnitRoot attacker,
            HealthComponent target,
            float requestedDamage,
            float postResistanceDamage,
            float appliedDamage,
            float absorbedByShield,
            bool isCritical,
            bool targetKilled,
            SkillDefinition skill,
            EffectDefinition effect,
            SkillStepTrigger trigger)
        {
            Attacker = attacker;
            Target = target;
            RequestedDamage = requestedDamage;
            PostResistanceDamage = postResistanceDamage;
            AppliedDamage = appliedDamage;
            AbsorbedByShield = absorbedByShield;
            IsCritical = isCritical;
            TargetKilled = targetKilled;
            Skill = skill;
            Effect = effect;
            Trigger = trigger;
        }
    }

    /// <summary>
    /// 资源（法力/能量等）变更事件数据。
    /// </summary>
    public struct ResourceChangedEvent
    {
        public ResourceComponent Source; // 来源组件
        public ResourceType ResourceType; // 资源类型
        public float OldValue;          // 变更前的值
        public float NewValue;          // 变更后的值
        public float Delta;             // 变化量

        public ResourceChangedEvent(ResourceComponent source, ResourceType resourceType, float oldValue, float newValue)
        {
            Source = source;
            ResourceType = resourceType;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
        }
    }

    /// <summary>
    /// 技能冷却状态变更事件数据。
    /// </summary>
    public struct CooldownChangedEvent
    {
        public CooldownComponent Source; // 来源组件
        public SkillDefinition Skill;    // 目标技能
        public float Remaining;          // 剩余冷却时间
        public float Duration;           // 总冷却时长
        public bool IsCoolingDown;      // 当前是否正处于冷却中

        public CooldownChangedEvent(CooldownComponent source, SkillDefinition skill, float remaining, float duration, bool isCoolingDown)
        {
            Source = source;
            Skill = skill;
            Remaining = remaining;
            Duration = duration;
            IsCoolingDown = isCoolingDown;
        }
    }

    /// <summary>
    /// 技能施法事件数据。
    /// </summary>
    public struct SkillCastEvent
    {
        public UnitRoot Caster;          // 施法单位
        public SkillDefinition Skill;    // 施放技能
        public float CastTime;           // 施法时长
        public float ChannelTime;        // 引导时长
        public bool IsChannel;           // 是否为引导技能

        public SkillCastEvent(UnitRoot caster, SkillDefinition skill, float castTime, float channelTime, bool isChannel)
        {
            Caster = caster;
            Skill = skill;
            CastTime = castTime;
            ChannelTime = channelTime;
            IsChannel = isChannel;
        }
    }

    /// <summary>
    /// 技能步骤执行事件数据（用于表现系统消费）。
    /// </summary>
    public struct SkillStepExecutedEvent
    {
        public UnitRoot Caster;
        public SkillDefinition Skill;
        public SkillStep Step;
        public SkillStepTrigger Trigger;
        public CombatTarget PrimaryTarget;
        public GameObject ExplicitTarget;
        public bool HasAimPoint;
        public Vector3 AimPoint;
        public Vector3 AimDirection;
        public ulong CastId;
        public int StepIndex;

        public SkillStepExecutedEvent(
            UnitRoot caster,
            SkillDefinition skill,
            SkillStep step,
            SkillStepTrigger trigger,
            CombatTarget primaryTarget,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            ulong castId,
            int stepIndex)
        {
            Caster = caster;
            Skill = skill;
            Step = step;
            Trigger = trigger;
            PrimaryTarget = primaryTarget;
            ExplicitTarget = explicitTarget;
            HasAimPoint = hasAimPoint;
            AimPoint = aimPoint;
            AimDirection = aimDirection;
            CastId = castId;
            StepIndex = stepIndex;
        }
    }

    /// <summary>
    /// 技能效果执行阶段。
    /// </summary>
    public enum SkillEffectExecutionPhase
    {
        BeforeApply = 0,
        AfterApply = 1
    }

    /// <summary>
    /// 技能效果执行事件数据（用于表现系统消费）。
    /// </summary>
    public struct SkillEffectExecutedEvent
    {
        public UnitRoot Caster;
        public SkillDefinition Skill;
        public EffectDefinition Effect;
        public SkillStepTrigger Trigger;
        public SkillEffectExecutionPhase Phase;
        public CombatTarget Target;
        public ulong CastId;
        public int StepIndex;

        public SkillEffectExecutedEvent(
            UnitRoot caster,
            SkillDefinition skill,
            EffectDefinition effect,
            SkillStepTrigger trigger,
            SkillEffectExecutionPhase phase,
            CombatTarget target,
            ulong castId,
            int stepIndex)
        {
            Caster = caster;
            Skill = skill;
            Effect = effect;
            Trigger = trigger;
            Phase = phase;
            Target = target;
            CastId = castId;
            StepIndex = stepIndex;
        }
    }

    /// <summary>
    /// 投射物生命周期事件类型。
    /// </summary>
    public enum ProjectileLifecycleType
    {
        Spawn = 0,
        Hit = 1,
        Return = 2,
        Split = 3
    }

    /// <summary>
    /// 投射物生命周期事件数据（Spawn/Hit/Return/Split）。
    /// </summary>
    public struct ProjectileLifecycleEvent
    {
        public UnitRoot Caster;
        public SkillDefinition Skill;
        public ProjectileDefinition Projectile;
        public ProjectileLifecycleType LifecycleType;
        public CombatTarget Target;
        public Vector3 Position;
        public Vector3 Direction;
        public GameObject ProjectileObject;
        public ulong CastId;
        public int StepIndex;
        public int ProjectileInstanceId;
        public int RelatedProjectileInstanceId;

        public ProjectileLifecycleEvent(
            UnitRoot caster,
            SkillDefinition skill,
            ProjectileDefinition projectile,
            ProjectileLifecycleType lifecycleType,
            CombatTarget target,
            Vector3 position,
            Vector3 direction,
            GameObject projectileObject,
            ulong castId,
            int stepIndex,
            int projectileInstanceId,
            int relatedProjectileInstanceId = 0)
        {
            Caster = caster;
            Skill = skill;
            Projectile = projectile;
            LifecycleType = lifecycleType;
            Target = target;
            Position = position;
            Direction = direction;
            ProjectileObject = projectileObject;
            CastId = castId;
            StepIndex = stepIndex;
            ProjectileInstanceId = projectileInstanceId;
            RelatedProjectileInstanceId = relatedProjectileInstanceId;
        }
    }

    /// <summary>
    /// 经验值变更事件数据。
    /// </summary>
    public struct ExperienceChangedEvent
    {
        public PlayerProgression Source;
        public int OldValue;
        public int NewValue;
        public int Delta;
        public int Level;
        public int XpToNext;
        public float Normalized;

        public ExperienceChangedEvent(PlayerProgression source, int oldValue, int newValue, int level, int xpToNext)
        {
            Source = source;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
            Level = level;
            XpToNext = xpToNext;
            Normalized = xpToNext > 0 ? (float)newValue / xpToNext : 0f;
        }
    }

    /// <summary>
    /// 等级变更事件数据。
    /// </summary>
    public struct LevelChangedEvent
    {
        public PlayerProgression Source;
        public int OldLevel;
        public int NewLevel;
        public int Delta;

        public LevelChangedEvent(PlayerProgression source, int oldLevel, int newLevel)
        {
            Source = source;
            OldLevel = oldLevel;
            NewLevel = newLevel;
            Delta = newLevel - oldLevel;
        }
    }

    /// <summary>
    /// 属性点变更事件数据。
    /// </summary>
    public struct AttributePointsChangedEvent
    {
        public PlayerProgression Source;
        public int OldValue;
        public int NewValue;
        public int Delta;

        public AttributePointsChangedEvent(PlayerProgression source, int oldValue, int newValue)
        {
            Source = source;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
        }
    }
}
