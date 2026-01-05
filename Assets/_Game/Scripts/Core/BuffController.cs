using System;
using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// Buff 控制器，负责管理单位身上所有 Buff 实例的生命周期、触发器与修正器。
    /// </summary>
    /// <remarks>
    /// 核心职责：
    /// - 管理 Buff 的添加、移除、堆叠
    /// - 处理 Buff 的持续时间和周期性 Tick
    /// - 响应战斗事件触发 Buff 效果
    /// 
    /// 堆叠规则：
    /// - Refresh: 刷新持续时间，层数+1
    /// - Extend: 延长持续时间，层数+1
    /// - Independent: 每层独立计时
    /// 
    /// 触发器类型：
    /// - OnApply: Buff 应用时
    /// - OnExpire: Buff 到期时
    /// - OnTick: 周期性触发
    /// - OnHit/OnDamaged/OnSkillCast/OnKill: 战斗事件触发
    /// </remarks>
    public class BuffController : MonoBehaviour
    {
        #region 序列化字段

        [Header("组件引用")]
        [Tooltip("单位根组件引用")]
        [SerializeField] private UnitRoot unitRoot;

        [Tooltip("技能使用组件引用")]
        [SerializeField] private SkillUserComponent skillUser;

        [Tooltip("效果执行器引用")]
        [SerializeField] private EffectExecutor effectExecutor;

        [Tooltip("目标选择系统引用")]
        [SerializeField] private TargetingSystem targetingSystem;

        #endregion

        #region 私有字段

        /// <summary>当前激活的 Buff 实例列表</summary>
        private readonly List<BuffInstance> activeBuffs = new List<BuffInstance>(16);

        /// <summary>临时存储已过期 Buff 索引的列表（避免 GC）</summary>
        private readonly List<int> expiredIndices = new List<int>(8);

        /// <summary>缓存的自身目标</summary>
        private CombatTarget selfTarget;

        /// <summary>是否已缓存自身目标</summary>
        private bool hasSelfTarget;

        #endregion

        #region 公开事件

        /// <summary>
        /// 当 Buff 列表发生变化时触发（添加、移除、堆叠变化）。
        /// UI 可订阅此事件来更新 Buff 图标显示。
        /// </summary>
        public event Action BuffsChanged;

        #endregion

        #region 公开属性

        /// <summary>
        /// 获取当前激活的 Buff 实例只读列表。
        /// </summary>
        public IReadOnlyList<BuffInstance> ActiveBuffs => activeBuffs;

        #endregion

        #region Unity 生命周期

        /// <summary>
        /// 编辑器重置时自动获取组件引用。
        /// </summary>
        private void Reset()
        {
            unitRoot = GetComponent<UnitRoot>();
            skillUser = GetComponent<SkillUserComponent>();
        }

        /// <summary>
        /// 初始化：确保组件引用并缓存自身目标。
        /// </summary>
        private void Awake()
        {
            EnsureReferences();
            RefreshSelfTarget();
        }

        /// <summary>
        /// 启用时确保组件引用并刷新自身目标。
        /// </summary>
        private void OnEnable()
        {
            EnsureReferences();
            RefreshSelfTarget();
        }

        /// <summary>
        /// 每帧更新：处理 Buff 的持续时间和周期性触发。
        /// </summary>
        private void Update()
        {
            // 没有激活的 Buff 则跳过
            if (activeBuffs.Count == 0)
            {
                return;
            }

            var now = Time.time;
            expiredIndices.Clear();

            // 遍历所有激活的 Buff
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                var instance = activeBuffs[i];

                // 无效的 Buff 定义，标记为过期
                if (instance.Definition == null)
                {
                    expiredIndices.Add(i);
                    continue;
                }

                // 检查是否已过期（EndTime > 0 表示有持续时间限制）
                if (instance.EndTime > 0f && instance.EndTime <= now)
                {
                    expiredIndices.Add(i);
                    continue;
                }

                // 检查是否需要触发 Tick（周期性效果）
                if (instance.NextTickTime > 0f && now >= instance.NextTickTime)
                {
                    // 触发 OnTick 效果
                    TriggerBuff(instance, BuffTriggerType.OnTick, BuildContext(default), GetSelfTarget());

                    // 计算下一次 Tick 时间
                    var interval = instance.Definition.TickInterval;
                    instance.NextTickTime = interval > 0f ? now + interval : -1f;
                    activeBuffs[i] = instance;
                }
            }

            // 从后向前移除过期的 Buff（避免索引错位）
            for (int i = expiredIndices.Count - 1; i >= 0; i--)
            {
                RemoveAt(expiredIndices[i], true);
            }
        }

        #endregion

        #region 公开方法 - 查询

        /// <summary>
        /// 检查单位是否拥有指定的 Buff。
        /// </summary>
        /// <param name="buff">要检查的 Buff 定义</param>
        /// <returns>如果拥有该 Buff 则返回 true</returns>
        public bool HasBuff(BuffDefinition buff)
        {
            if (buff == null)
            {
                return false;
            }

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                if (activeBuffs[i].Definition == buff)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取指定 Buff 的当前堆叠层数。
        /// </summary>
        /// <param name="buff">要查询的 Buff 定义</param>
        /// <returns>总堆叠层数（Independent 模式下会累加所有实例）</returns>
        public int GetStacks(BuffDefinition buff)
        {
            if (buff == null)
            {
                return 0;
            }

            var stacks = 0;
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                var instance = activeBuffs[i];
                if (instance.Definition == buff)
                {
                    stacks += Mathf.Max(1, instance.Stacks);
                }
            }

            return stacks;
        }

        #endregion

        #region 公开方法 - 操作

        /// <summary>
        /// 应用一个 Buff 到该单位。
        /// 根据 Buff 的堆叠规则处理已存在的实例。
        /// </summary>
        /// <param name="buff">要应用的 Buff 定义</param>
        public void ApplyBuff(BuffDefinition buff)
        {
            if (buff == null)
            {
                return;
            }

            EnsureReferences();

            var now = Time.time;
            var duration = buff.Duration;
            var tickInterval = buff.TickInterval;
            var maxStacks = Mathf.Max(1, buff.MaxStacks);

            // 非 Independent 模式：合并到已有实例
            if (buff.StackingRule != BuffStackingRule.Independent)
            {
                var index = FindBuffIndex(buff);
                if (index >= 0)
                {
                    var instance = activeBuffs[index];
                    // 增加层数（不超过最大层数）
                    instance.Stacks = Mathf.Clamp(instance.Stacks + 1, 1, maxStacks);

                    // 根据堆叠规则处理持续时间
                    if (buff.StackingRule == BuffStackingRule.Refresh)
                    {
                        // Refresh: 重置持续时间
                        instance.EndTime = duration > 0f ? now + duration : -1f;
                        instance.NextTickTime = tickInterval > 0f ? now + tickInterval : -1f;
                    }
                    else if (buff.StackingRule == BuffStackingRule.Extend)
                    {
                        // Extend: 延长持续时间
                        if (duration > 0f)
                        {
                            instance.EndTime = instance.EndTime > 0f ? instance.EndTime + duration : now + duration;
                        }

                        if (instance.NextTickTime <= 0f && tickInterval > 0f)
                        {
                            instance.NextTickTime = now + tickInterval;
                        }
                    }
                    else
                    {
                        // 默认：保持原有时间或更新
                        instance.EndTime = duration > 0f ? now + duration : instance.EndTime;
                    }

                    activeBuffs[index] = instance;
                    TriggerBuff(instance, BuffTriggerType.OnApply, BuildContext(default), GetSelfTarget());
                    BuffsChanged?.Invoke();
                    return;
                }
            }
            else
            {
                // Independent 模式：检查是否已达到最大实例数
                var instanceCount = CountBuffInstances(buff);
                if (instanceCount >= maxStacks)
                {
                    // 刷新最旧的实例
                    if (TryGetOldestIndependentIndex(buff, out var refreshIndex))
                    {
                        var instance = activeBuffs[refreshIndex];
                        instance.Stacks = 1;
                        instance.EndTime = duration > 0f ? now + duration : -1f;
                        instance.NextTickTime = tickInterval > 0f ? now + tickInterval : -1f;
                        activeBuffs[refreshIndex] = instance;
                        TriggerBuff(instance, BuffTriggerType.OnApply, BuildContext(default), GetSelfTarget());
                        BuffsChanged?.Invoke();
                    }

                    return;
                }
            }

            // 创建新的 Buff 实例
            var endTime = duration > 0f ? now + duration : -1f;
            var nextTick = tickInterval > 0f ? now + tickInterval : -1f;
            var newInstance = new BuffInstance(buff, 1, endTime, nextTick, now);
            activeBuffs.Add(newInstance);

            // 触发 OnApply 效果
            TriggerBuff(newInstance, BuffTriggerType.OnApply, BuildContext(default), GetSelfTarget());
            BuffsChanged?.Invoke();
        }

        /// <summary>
        /// 移除指定的 Buff（所有实例）。
        /// </summary>
        /// <param name="buff">要移除的 Buff 定义</param>
        /// <returns>如果成功移除至少一个实例则返回 true</returns>
        public bool RemoveBuff(BuffDefinition buff)
        {
            if (buff == null)
            {
                return false;
            }

            var removed = false;
            // 从后向前遍历以安全移除
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                if (activeBuffs[i].Definition == buff)
                {
                    RemoveAt(i, true);
                    removed = true;
                }
            }

            return removed;
        }

        #endregion

        #region 公开方法 - 战斗事件通知

        /// <summary>
        /// 通知：命中目标时触发。
        /// 由 EffectExecutor 在伤害结算时调用。
        /// </summary>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">被命中的目标</param>
        public void NotifyHit(SkillRuntimeContext context, CombatTarget target)
        {
            TriggerBuffs(BuffTriggerType.OnHit, context, target);
        }

        /// <summary>
        /// 通知：受到伤害时触发。
        /// 由 EffectExecutor 在伤害结算时调用。
        /// </summary>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="attacker">攻击者</param>
        public void NotifyDamaged(SkillRuntimeContext context, CombatTarget attacker)
        {
            TriggerBuffs(BuffTriggerType.OnDamaged, context, attacker);
        }

        /// <summary>
        /// 通知：释放技能时触发。
        /// 由 SkillUserComponent 在施法开始时调用。
        /// </summary>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">技能目标</param>
        public void NotifySkillCast(SkillRuntimeContext context, CombatTarget target)
        {
            TriggerBuffs(BuffTriggerType.OnSkillCast, context, target);
        }

        /// <summary>
        /// 通知：击杀目标时触发。
        /// 由 EffectExecutor 在目标死亡时调用。
        /// </summary>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">被击杀的目标</param>
        public void NotifyKill(SkillRuntimeContext context, CombatTarget target)
        {
            TriggerBuffs(BuffTriggerType.OnKill, context, target);
        }

        #endregion

        #region 私有方法 - 触发器

        /// <summary>
        /// 触发所有 Buff 的指定类型触发器。
        /// </summary>
        /// <param name="triggerType">触发器类型</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">相关目标</param>
        private void TriggerBuffs(BuffTriggerType triggerType, SkillRuntimeContext context, CombatTarget target)
        {
            if (activeBuffs.Count == 0)
            {
                return;
            }

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                TriggerBuff(activeBuffs[i], triggerType, context, target);
            }
        }

        /// <summary>
        /// 触发单个 Buff 实例的指定类型触发器。
        /// </summary>
        /// <param name="instance">Buff 实例</param>
        /// <param name="triggerType">触发器类型</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">相关目标</param>
        private void TriggerBuff(BuffInstance instance, BuffTriggerType triggerType, SkillRuntimeContext context, CombatTarget target)
        {
            if (effectExecutor == null || instance.Definition == null)
            {
                return;
            }

            var triggers = instance.Definition.Triggers;
            if (triggers == null || triggers.Count == 0)
            {
                return;
            }

            var execContext = BuildContext(context);
            if (!target.IsValid)
            {
                target = GetSelfTarget();
            }

            // 遍历 Buff 定义的所有触发器
            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null || trigger.triggerType != triggerType)
                {
                    continue;
                }

                // 概率检查
                var chance = Mathf.Clamp01(trigger.chance);
                if (chance <= 0f || (chance < 1f && UnityEngine.Random.value > chance))
                {
                    continue;
                }

                // 条件检查
                if (trigger.condition != null && !ConditionEvaluator.Evaluate(trigger.condition, execContext, target))
                {
                    continue;
                }

                // 执行触发器的效果列表
                if (trigger.effects == null || trigger.effects.Count == 0)
                {
                    continue;
                }

                var mappedTrigger = MapTrigger(triggerType);
                for (int j = 0; j < trigger.effects.Count; j++)
                {
                    effectExecutor.ExecuteEffect(trigger.effects[j], execContext, target, mappedTrigger);
                }
            }
        }

        /// <summary>
        /// 构建技能运行时上下文。
        /// 如果传入的上下文已经正确设置，则直接返回；否则创建新的上下文。
        /// </summary>
        /// <param name="context">原始上下文</param>
        /// <returns>有效的上下文</returns>
        private SkillRuntimeContext BuildContext(SkillRuntimeContext context)
        {
            var skill = context.Skill;
            if (context.CasterUnit != null && context.CasterUnit == unitRoot && context.Caster == skillUser)
            {
                return context;
            }

            return new SkillRuntimeContext(skillUser, unitRoot, skill, unitRoot != null ? unitRoot.EventHub : null, targetingSystem, effectExecutor);
        }

        #endregion

        #region 私有方法 - Buff 管理

        /// <summary>
        /// 移除指定索引的 Buff。
        /// 使用 swap-remove 技术避免列表移位开销。
        /// </summary>
        /// <param name="index">要移除的索引</param>
        /// <param name="invokeExpire">是否触发 OnExpire 效果</param>
        private void RemoveAt(int index, bool invokeExpire)
        {
            var lastIndex = activeBuffs.Count - 1;
            var removed = activeBuffs[index];

            // 触发过期效果
            if (invokeExpire)
            {
                TriggerBuff(removed, BuffTriggerType.OnExpire, BuildContext(default), GetSelfTarget());
            }

            // swap-remove：将最后一个元素移到被删除的位置
            if (index != lastIndex)
            {
                activeBuffs[index] = activeBuffs[lastIndex];
            }

            activeBuffs.RemoveAt(lastIndex);

            BuffsChanged?.Invoke();
        }

        /// <summary>
        /// 查找指定 Buff 在列表中的索引。
        /// </summary>
        /// <param name="buff">要查找的 Buff 定义</param>
        /// <returns>索引，未找到返回 -1</returns>
        private int FindBuffIndex(BuffDefinition buff)
        {
            if (buff == null)
            {
                return -1;
            }

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                if (activeBuffs[i].Definition == buff)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 统计指定 Buff 的实例数量（用于 Independent 模式）。
        /// </summary>
        /// <param name="buff">要统计的 Buff 定义</param>
        /// <returns>实例数量</returns>
        private int CountBuffInstances(BuffDefinition buff)
        {
            if (buff == null)
            {
                return 0;
            }

            var count = 0;
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                if (activeBuffs[i].Definition == buff)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 获取 Independent 模式下最旧的 Buff 实例索引。
        /// 优先选择有持续时间限制且剩余时间最短的实例。
        /// </summary>
        /// <param name="buff">要查找的 Buff 定义</param>
        /// <param name="index">输出的索引</param>
        /// <returns>如果找到则返回 true</returns>
        private bool TryGetOldestIndependentIndex(BuffDefinition buff, out int index)
        {
            index = -1;
            if (buff == null)
            {
                return false;
            }

            var bestEndTime = float.PositiveInfinity;
            var foundTimed = false;

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                var instance = activeBuffs[i];
                if (instance.Definition != buff)
                {
                    continue;
                }

                // 优先选择有持续时间的实例
                if (instance.EndTime > 0f)
                {
                    if (!foundTimed || instance.EndTime < bestEndTime)
                    {
                        bestEndTime = instance.EndTime;
                        index = i;
                        foundTimed = true;
                    }

                    continue;
                }

                // 永久 Buff 作为备选
                if (!foundTimed && index < 0)
                {
                    index = i;
                }
            }

            return index >= 0;
        }

        #endregion

        #region 私有方法 - 辅助

        /// <summary>
        /// 确保所有必需的组件引用已就绪。
        /// </summary>
        /// <remarks>
        /// 性能提示：FindObjectOfType 在大型场景中开销较高。
        /// 建议在 Inspector 中预先配置 effectExecutor 和 targetingSystem 的引用，
        /// 而非依赖运行时查找。
        /// </remarks>
        private void EnsureReferences()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            // [性能] FindObjectOfType 是全局搜索，首次调用时可能影响性能
            // 建议：通过 Inspector 注入或在场景初始化时配置
            if (effectExecutor == null)
            {
                effectExecutor = FindObjectOfType<EffectExecutor>();
            }

            if (targetingSystem == null)
            {
                targetingSystem = FindObjectOfType<TargetingSystem>();
            }
        }

        /// <summary>
        /// 刷新缓存的自身目标。
        /// </summary>
        private void RefreshSelfTarget()
        {
            hasSelfTarget = CombatTarget.TryCreate(gameObject, out selfTarget);
        }

        /// <summary>
        /// 获取缓存的自身目标。
        /// 如果未缓存则先刷新。
        /// </summary>
        /// <returns>自身的 CombatTarget</returns>
        private CombatTarget GetSelfTarget()
        {
            if (!hasSelfTarget)
            {
                RefreshSelfTarget();
            }

            return selfTarget;
        }

        /// <summary>
        /// 将 Buff 触发器类型映射到技能步骤触发器类型。
        /// 用于在执行 Buff 效果时确定触发阶段。
        /// </summary>
        /// <param name="triggerType">Buff 触发器类型</param>
        /// <returns>对应的技能步骤触发器类型</returns>
        private static SkillStepTrigger MapTrigger(BuffTriggerType triggerType)
        {
            return triggerType == BuffTriggerType.OnHit || triggerType == BuffTriggerType.OnDamaged
                ? SkillStepTrigger.OnHit
                : SkillStepTrigger.OnCastStart;
        }

        #endregion

        #region 内部类型

        /// <summary>
        /// Buff 运行时实例数据结构。
        /// 使用 struct 避免堆分配。
        /// </summary>
        public struct BuffInstance
        {
            /// <summary>Buff 定义（ScriptableObject）</summary>
            public BuffDefinition Definition;

            /// <summary>当前堆叠层数</summary>
            public int Stacks;

            /// <summary>结束时间（-1 表示永久）</summary>
            public float EndTime;

            /// <summary>下一次 Tick 时间（-1 表示无 Tick）</summary>
            public float NextTickTime;

            /// <summary>应用时间（用于排序）</summary>
            public float AppliedTime;

            /// <summary>
            /// 创建 Buff 实例。
            /// </summary>
            /// <param name="definition">Buff 定义</param>
            /// <param name="stacks">初始层数</param>
            /// <param name="endTime">结束时间</param>
            /// <param name="nextTickTime">下一次 Tick 时间</param>
            /// <param name="appliedTime">应用时间</param>
            public BuffInstance(BuffDefinition definition, int stacks, float endTime, float nextTickTime, float appliedTime)
            {
                Definition = definition;
                Stacks = stacks;
                EndTime = endTime;
                NextTickTime = nextTickTime;
                AppliedTime = appliedTime;
            }
        }

        #endregion
    }
}
