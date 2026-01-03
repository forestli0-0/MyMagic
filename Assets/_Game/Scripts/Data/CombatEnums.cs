namespace CombatSystem.Data
{
    /// <summary>
    /// 资源类型（如法力、耐力等）。
    /// </summary>
    public enum ResourceType
    {
        Mana,       // 法力
        Stamina,    // 耐力
        Energy      // 能量
    }

    /// <summary>
    /// 伤害类型。
    /// </summary>
    public enum DamageType
    {
        Physical,   // 物理
        Magical,    // 魔法
        True        // 真相（无视防御）
    }

    /// <summary>
    /// 技能步骤触发时机。
    /// </summary>
    public enum SkillStepTrigger
    {
        OnCastStart,        // 施法开始
        OnCastComplete,     // 施法完成
        OnHit,              // 命中时（直接命中）
        OnProjectileHit     // 投射物命中时
    }

    /// <summary>
    /// 效果类型。
    /// </summary>
    public enum EffectType
    {
        Damage,         // 造成伤害
        Heal,           // 治疗
        ApplyBuff,      // 添加 Buff
        RemoveBuff,     // 移除 Buff
        Projectile,     // 发射投射物
        Move,           // 位移
        Resource,       // 资源变更（加/减法力等）
        Summon,         // 召唤
        TriggerSkill    // 触发另一个技能
    }

    /// <summary>
    /// 条件逻辑运算符（全部满足或任意满足）。
    /// </summary>
    public enum ConditionOperator
    {
        All,    // 且 (AND)
        Any     // 或 (OR)
    }

    /// <summary>
    /// 条件类型。
    /// </summary>
    public enum ConditionType
    {
        Always,                 // 始终满足
        Chance,                 // 概率触发
        HasTag,                 // 拥有指定标签
        HasBuff,                // 拥有指定 Buff
        HealthPercentBelow,     // 血量百分比低于
        HealthPercentAbove,     // 血量百分比高于
        IsTargetAlive,          // 目标存活
        IsTargetDead            // 目标死亡
    }

    /// <summary>
    /// 条件判断主体（施法者或目标）。
    /// </summary>
    public enum ConditionSubject
    {
        Caster, // 施法者
        Target  // 目标
    }

    /// <summary>
    /// 修正器目标类型。
    /// </summary>
    public enum ModifierTargetType
    {
        Stat,   // 基础属性
        Skill,  // 技能参数
        Effect  // 效果参数
    }

    /// <summary>
    /// 修正器作用域（施法者侧 or 目标侧）。
    /// </summary>
    public enum ModifierScope
    {
        Caster, // 作用于施法者侧（默认）
        Target, // 作用于目标侧（用于抗性/易伤等）
        Both    // 同时作用于施法者与目标
    }

    /// <summary>
    /// 修正器运算方式。
    /// </summary>
    public enum ModifierOperation
    {
        Add,        // 加法
        Multiply,   // 乘法
        Override    // 覆盖/重写
    }

    /// <summary>
    /// Buff 堆叠/共存规则。
    /// </summary>
    public enum BuffStackingRule
    {
        Refresh,    // 刷新持续时间
        Extend,     // 延长持续时间
        Independent // 独立存在（多层独立计时）
    }

    /// <summary>
    /// Buff 触发器类型。
    /// </summary>
    public enum BuffTriggerType
    {
        OnApply,        // 应用时
        OnExpire,       // 过期时
        OnTick,         // 周期性触发
        OnHit,          // 命中敌人时
        OnDamaged,      // 受到伤害时
        OnSkillCast,    // 释放技能时
        OnKill          // 击杀时
    }

    /// <summary>
    /// 目标选择模式。
    /// </summary>
    public enum TargetingMode
    {
        Self,       // 自身
        Single,     // 单体
        Sphere,     // 球形/圆形范围
        Cone,       // 锥形范围
        Chain,      // 链式（如连锁闪电）
        Random      // 范围随机
    }

    /// <summary>
    /// 目标阵营筛选。
    /// </summary>
    public enum TargetTeam
    {
        Any,    // 任何
        Self,   // 仅自身
        Ally,   // 盟友
        Enemy   // 敌人
    }

    /// <summary>
    /// 目标排序规则。
    /// </summary>
    public enum TargetSort
    {
        None,           // 无
        Closest,        // 最近
        Farthest,       // 最远
        LowestHealth,   // 血量最低
        HighestHealth,  // 血量最高
        Random          // 随机一个
    }

    /// <summary>
    /// 位移风格。
    /// </summary>
    public enum MoveStyle
    {
        Dash,       // 冲刺
        Leap,       // 跳跃
        Knockback,  // 击退
        Pull        // 拉取
    }
}
