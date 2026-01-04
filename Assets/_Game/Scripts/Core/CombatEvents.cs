using CombatSystem.Data;

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
}
