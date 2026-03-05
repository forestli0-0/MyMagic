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
        OnCastStart = 0,        // 施法开始
        OnCastComplete = 1,     // 施法完成
        OnHit = 2,              // 命中时（直接命中）
        OnProjectileHit = 3,    // 投射物命中时
        OnChannelTick = 4       // 引导中周期触发
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
        TriggerSkill,   // 触发另一个技能
        Shield,         // 护盾
        ResetBasicAttack, // 重置普攻冷却/后摇
        Cleanse,        // 净化/驱散
        CombatState     // 战斗状态位操作（不可选取/无敌/法术护盾等）
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
        Always = 0,             // 始终满足
        Chance = 1,             // 概率触发
        HasTag = 2,             // 拥有指定标签
        HasBuff = 3,            // 拥有指定 Buff
        HealthPercentBelow = 4, // 血量百分比低于
        HealthPercentAbove = 5, // 血量百分比高于
        IsTargetAlive = 6,      // 目标存活
        IsTargetDead = 7,       // 目标死亡
        NotHasTag = 8,          // 不拥有指定标签
        NotHasBuff = 9,         // 不拥有指定 Buff
        BuffStacksAtLeast = 10, // 指定 Buff 层数 >= 阈值
        BuffStacksBelow = 11,   // 指定 Buff 层数 < 阈值
        SequencePhaseIs = 12,   // 技能连段阶段 == 阈值
        SequencePhaseAtLeast = 13 // 技能连段阶段 >= 阈值
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
        OnKill,         // 击杀时
        OnAttack        // 普攻开始时
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
        Random,     // 范围随机
        Line,       // 线性/矩形范围
        Box         // 盒形/矩形范围
    }

    /// <summary>
    /// 目标区域的原点类型。
    /// </summary>
    public enum TargetingOrigin
    {
        Caster,     // 施法者位置
        TargetPoint // 鼠标/指定点
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

    /// <summary>
    /// 位移目的地策略。
    /// </summary>
    public enum MoveDestinationPolicy
    {
        Legacy = 0,             // 兼容旧逻辑：按瞄准方向/朝向位移
        AimDirection = 1,       // 始终按瞄准方向位移
        ToAimPoint = 2,         // 朝瞄准点位移
        ToExplicitTarget = 3,   // 朝显式目标位移
        ThroughExplicitTarget = 4, // 穿过显式目标（落到目标后方）
        BehindExplicitTarget = 5   // 落到显式目标身后（由 Offset 控制）
    }

    /// <summary>
    /// 位移碰撞策略。
    /// </summary>
    public enum MoveCollisionPolicy
    {
        Default = 0,                            // 保持 CharacterController 碰撞
        IgnoreCharacterControllerCollisions = 1 // 强制位移期间忽略 CharacterController 碰撞
    }

    /// <summary>
    /// 控制效果类型。
    /// </summary>
    public enum ControlType
    {
        Stun,       // 眩晕
        Silence,    // 沉默
        Root,       // 禁锢
        Disarm,     // 缴械
        Fear,       // 恐惧
        Taunt,      // 嘲讽
        Knockup,    // 击飞
        Knockback,  // 击退
        Suppression,// 压制
        Charm,      // 魅惑
        Sleep,      // 睡眠
        Polymorph,  // 变形
        Blind,      // 致盲
        Slow,       // 减速
        All         // 用于免疫配置
    }

    /// <summary>
    /// 控制规则标记。
    /// </summary>
    /// <remarks>
    /// 使用位标记组合表示控制效果的行为限制：
    /// - BlocksXXX: 禁止玩家执行对应操作
    /// - ForcesMovement: 由 AI 接管移动（恐惧逃跑/嘲讽靠近等）
    /// - InterruptsCasting: 立即打断当前施法
    /// </remarks>
    [System.Flags]
    public enum ControlFlag
    {
        None = 0,
        /// <summary>禁止玩家控制移动</summary>
        BlocksMovement = 1 << 0,
        /// <summary>禁止旋转</summary>
        BlocksRotation = 1 << 1,
        /// <summary>禁止施放技能</summary>
        BlocksCasting = 1 << 2,
        /// <summary>禁止普通攻击</summary>
        BlocksBasicAttack = 1 << 3,
        /// <summary>立即打断当前施法</summary>
        InterruptsCasting = 1 << 4,
        /// <summary>强制移动（AI 接管，用于恐惧/嘲讽/魅惑）</summary>
        ForcesMovement = 1 << 5
    }

    /// <summary>
    /// 单位战斗状态标记。
    /// </summary>
    [System.Flags]
    public enum CombatStateFlags
    {
        None = 0,
        Untargetable = 1 << 0, // 不可被选中
        Invulnerable = 1 << 1, // 不会受到伤害
        Invisible = 1 << 2,    // 隐形
        Camouflaged = 1 << 3,  // 伪装/潜行
        SpellShielded = 1 << 4 // 法术护盾
    }

    /// <summary>
    /// 战斗状态效果执行模式。
    /// </summary>
    public enum CombatStateEffectMode
    {
        AddFlags,         // 添加状态位
        RemoveFlags,      // 移除状态位
        GrantSpellShield  // 授予法术护盾层数
    }

    /// <summary>
    /// 技能输入缓冲策略。
    /// </summary>
    public enum SkillQueuePolicy
    {
        Replace,    // 替换已有队列
        Ignore      // 忽略新的请求
    }

    /// <summary>
    /// 技能施放失败原因。
    /// </summary>
    public enum SkillCastFailReason
    {
        None = 0,
        InvalidSkill = 1,
        TauntRestricted = 2,
        LockedOut = 3,
        CastingBlocked = 4,
        BasicAttackBlocked = 5,
        CasterDead = 6,
        RecastTargetInvalid = 7,
        Cooldown = 8,
        TargetDead = 9,
        AmmoDepleted = 10,
        InsufficientResource = 11,
        CastConstraintFailed = 12,
        OutOfRange = 13,
        NoValidTargets = 14,
        NoExecutableStep = 15,
        ResourceSpendFailed = 16,
        Queued = 17
    }

    /// <summary>
    /// 目标快照策略。
    /// </summary>
    public enum TargetSnapshotPolicy
    {
        AtCastStart,     // 施法开始时快照
        AtCastComplete,  // 施法完成/引导阶段重新选择
        PerStep          // 每个步骤执行时重新选择
    }

    /// <summary>
    /// 命中校验策略。
    /// </summary>
    public enum HitValidationPolicy
    {
        None,               // 不校验
        AliveOnly,          // 只校验存活
        InRange,            // 校验范围/形状
        InRangeAndLoS       // 校验范围并检查视线
    }

    /// <summary>
    /// 技能重施目标策略。
    /// </summary>
    public enum RecastTargetPolicy
    {
        AnyValid,                // 任意有效目标
        KeepOriginalIfPossible,  // 优先原目标，原目标失效则允许切换
        RequireOriginal          // 必须原目标
    }

    /// <summary>
    /// 技能连段达到最大阶段后的处理策略。
    /// </summary>
    public enum SkillSequenceOverflowPolicy
    {
        LoopToStart = 0, // 回到第 1 段并继续循环
        HoldAtMax = 1,   // 停留在最大段位
        ResetAfterMax = 2 // 当前最大段释放后，下一次回到第 1 段
    }

    /// <summary>
    /// 投射物行为类型。
    /// </summary>
    public enum ProjectileBehaviorType
    {
        Straight,   // 直线飞行
        Homing,     // 追踪
        Return,     // 命中后回返
        Split,      // 命中后分裂
        Orbit,      // 围绕施法者旋转
        BeamLike    // 光束式（锚定长度）
    }
}
