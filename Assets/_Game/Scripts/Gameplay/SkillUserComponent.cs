using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 技能使用组件，负责管理单位的技能列表、校验释放条件、执行技能管线。
    /// </summary>
    /// <remarks>
    /// 技能管线流程：
    /// 1. 接收释放请求 (TryCast)
    /// 2. 校验冷却/资源/存活状态 (CanCast)
    /// 3. 收集目标 (TargetingSystem)
    /// 4. 扣除资源并开始冷却
    /// 5. 调度 SkillStep 并执行效果 (EffectExecutor)
    /// 6. 派发施法事件
    /// </remarks>
    public class SkillUserComponent : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("单位根组件")]
        [SerializeField] private UnitRoot unitRoot;
        [Tooltip("全局事件中心")]
        [SerializeField] private CombatEventHub eventHub;
        [Tooltip("属性组件")]
        [SerializeField] private StatsComponent stats;
        [Tooltip("生命组件")]
        [SerializeField] private HealthComponent health;
        [Tooltip("资源组件")]
        [SerializeField] private ResourceComponent resource;
        [Tooltip("冷却组件")]
        [SerializeField] private CooldownComponent cooldown;
        [Tooltip("目标选择系统")]
        [SerializeField] private TargetingSystem targetingSystem;
        [Tooltip("效果执行器")]
        [SerializeField] private EffectExecutor effectExecutor;
        
        [Header("配置")]
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool initializeOnAwake = true;
        [Tooltip("是否从 UnitDefinition 加载技能")]
        [SerializeField] private bool useUnitDefinitionSkills = true;
        [Tooltip("额外配置的技能列表")]
        [SerializeField] private List<SkillDefinition> skills = new List<SkillDefinition>();
        [Tooltip("覆盖默认普通攻击")]
        [SerializeField] private SkillDefinition basicAttackOverride;

        // 运行时技能列表
        private readonly List<SkillDefinition> runtimeSkills = new List<SkillDefinition>(8);
        // 等待执行的技能步骤队列
        private readonly List<PendingStep> pendingSteps = new List<PendingStep>(16);

        // 施法状态
        private bool isCasting;
        private float castEndTime;
        private float currentCastTime;
        private SkillDefinition currentSkill;

        /// <summary>当技能开始施法时触发</summary>
        public event Action<SkillCastEvent> SkillCastStarted;
        /// <summary>当技能施法完成时触发</summary>
        public event Action<SkillCastEvent> SkillCastCompleted;

        /// <summary>是否正在施法中</summary>
        public bool IsCasting => isCasting;
        /// <summary>当前正在施放的技能</summary>
        public SkillDefinition CurrentSkill => currentSkill;
        /// <summary>运行时技能列表</summary>
        public IReadOnlyList<SkillDefinition> Skills => runtimeSkills;
        /// <summary>获取普通攻击技能</summary>
        public SkillDefinition BasicAttack => basicAttackOverride != null ? basicAttackOverride : unitRoot?.Definition?.BasicAttack;

        private void Reset()
        {
            // 编辑器下自动查找组件
            unitRoot = GetComponent<UnitRoot>();
            stats = GetComponent<StatsComponent>();
            health = GetComponent<HealthComponent>();
            resource = GetComponent<ResourceComponent>();
            cooldown = GetComponent<CooldownComponent>();
        }

        private void Awake()
        {
            if (eventHub == null && unitRoot != null)
            {
                eventHub = unitRoot.EventHub;
            }

            if (initializeOnAwake)
            {
                Initialize(unitRoot != null ? unitRoot.Definition : null);
            }
        }

        private void OnEnable()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (stats == null)
            {
                stats = GetComponent<StatsComponent>();
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (resource == null)
            {
                resource = GetComponent<ResourceComponent>();
            }

            if (cooldown == null)
            {
                cooldown = GetComponent<CooldownComponent>();
            }

            if (eventHub == null && unitRoot != null)
            {
                eventHub = unitRoot.EventHub;
            }
        }

        /// <summary>
        /// 每帧更新：处理延迟执行的技能步骤和施法状态。
        /// </summary>
        private void Update()
        {
            // 处理延迟执行的技能步骤
            if (pendingSteps.Count > 0)
            {
                var now = Time.time;
                // 从后向前遍历，方便移除
                for (int i = pendingSteps.Count - 1; i >= 0; i--)
                {
                    var pending = pendingSteps[i];
                    if (pending.ExecuteAt > now)
                    {
                        continue;
                    }

                    // 执行步骤并释放目标列表
                    ExecuteStep(pending);
                    ReleaseHandle(pending.Targets);
                    pendingSteps.RemoveAt(i);
                }
            }

            // 检查施法是否完成
            if (isCasting && Time.time >= castEndTime)
            {
                isCasting = false;
                if (currentSkill != null)
                {
                    RaiseSkillCastCompleted(CreateContext(currentSkill), currentCastTime);
                }

                currentSkill = null;
                currentCastTime = 0f;
            }
        }

        /// <summary>
        /// 根据单位定义初始化技能列表。
        /// </summary>
        /// <param name="definition">单位配置定义</param>
        public void Initialize(UnitDefinition definition)
        {
            runtimeSkills.Clear();

            // 从 UnitDefinition 加载技能
            if (useUnitDefinitionSkills && definition != null)
            {
                // 添加普通攻击
                if (definition.BasicAttack != null)
                {
                    runtimeSkills.Add(definition.BasicAttack);
                }

                // 添加初始技能
                var startingSkills = definition.StartingSkills;
                for (int i = 0; i < startingSkills.Count; i++)
                {
                    var skill = startingSkills[i];
                    if (skill != null && !runtimeSkills.Contains(skill))
                    {
                        runtimeSkills.Add(skill);
                    }
                }
            }

            // 添加额外配置的技能
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill != null && !runtimeSkills.Contains(skill))
                {
                    runtimeSkills.Add(skill);
                }
            }
        }

        /// <summary>
        /// 尝试释放指定技能。
        /// </summary>
        /// <param name="skill">要释放的技能</param>
        /// <param name="explicitTarget">显式指定的目标（可选）</param>
        /// <returns>若成功开始施法则返回 true</returns>
        public bool TryCast(SkillDefinition skill, GameObject explicitTarget = null)
        {
            // 校验释放条件
            if (!CanCast(skill))
            {
                return false;
            }

            // 收集目标
            var targets = SimpleListPool<CombatTarget>.Get();
            if (!CollectTargets(skill, explicitTarget, targets))
            {
                SimpleListPool<CombatTarget>.Release(targets);
                return false;
            }

            // 扣除资源
            if (!SpendResource(skill))
            {
                SimpleListPool<CombatTarget>.Release(targets);
                return false;
            }

            // 开始冷却
            if (cooldown != null && skill.Cooldown > 0f)
            {
                cooldown.StartCooldown(skill, skill.Cooldown);
            }

            // 创建技能上下文
            var context = CreateContext(skill);
            var now = Time.time;
            var castTime = Mathf.Max(0f, skill.CastTime);

            // 调度技能步骤
            var targetHandle = GetTargetHandle(targets);
            ScheduleSteps(skill, SkillStepTrigger.OnCastStart, now, targetHandle, context);
            ScheduleSteps(skill, SkillStepTrigger.OnCastComplete, now + castTime, targetHandle, context);

            // 如果没有任何步骤引用该目标列表，立即释放
            if (targetHandle.RefCount == 0)
            {
                ReleaseHandleUnused(targetHandle);
            }

            // 派发施法开始事件
            RaiseSkillCastStarted(context, castTime);

            // 如果有施法时间，进入施法状态
            if (castTime > 0f)
            {
                isCasting = true;
                castEndTime = now + castTime;
                currentSkill = skill;
                currentCastTime = castTime;
            }
            else
            {
                // 瞬发技能立即完成
                RaiseSkillCastCompleted(context, castTime);
            }

            return true;
        }

        /// <summary>
        /// 检查指定技能是否可以释放。
        /// </summary>
        /// <param name="skill">要检查的技能</param>
        /// <returns>若满足所有释放条件则返回 true</returns>
        public bool CanCast(SkillDefinition skill)
        {
            // 技能不能为空
            if (skill == null)
            {
                return false;
            }

            // 不能在施法中释放其他技能
            if (isCasting)
            {
                return false;
            }

            // 必须存活
            if (health != null && !health.IsAlive)
            {
                return false;
            }

            // 检查冷却
            if (cooldown != null && !cooldown.IsReady(skill))
            {
                return false;
            }

            // 检查资源
            return HasResource(skill);
        }

        /// <summary>
        /// 内部方法：当技能直接命中目标时触发。
        /// </summary>
        internal void NotifyHit(SkillRuntimeContext context, CombatTarget target)
        {
            ScheduleTriggeredSteps(context, SkillStepTrigger.OnHit, target);
        }

        /// <summary>
        /// 内部方法：当投射物命中目标时触发。
        /// </summary>
        internal void NotifyProjectileHit(SkillRuntimeContext context, CombatTarget target)
        {
            ScheduleTriggeredSteps(context, SkillStepTrigger.OnProjectileHit, target);
        }

        /// <summary>
        /// 收集技能目标。
        /// </summary>
        /// <returns>若成功收集到至少一个目标则返回 true</returns>
        private bool CollectTargets(SkillDefinition skill, GameObject explicitTarget, List<CombatTarget> targets)
        {
            // 无目标系统或无目标定义时，默认选择自身
            if (targetingSystem == null || skill.Targeting == null)
            {
                if (unitRoot != null && CombatTarget.TryCreate(unitRoot.gameObject, out var selfTarget))
                {
                    targets.Add(selfTarget);
                    return true;
                }

                return false;
            }

            // 使用目标系统收集目标
            targetingSystem.CollectTargets(skill.Targeting, unitRoot, explicitTarget, targets);
            return targets.Count > 0;
        }

        /// <summary>
        /// 为单个目标调度触发型技能步骤（如 OnHit）。
        /// </summary>
        private void ScheduleTriggeredSteps(SkillRuntimeContext context, SkillStepTrigger trigger, CombatTarget target)
        {
            if (context.Skill == null)
            {
                return;
            }

            // 为单个目标创建临时列表
            var list = SimpleListPool<CombatTarget>.Get(1);
            list.Add(target);
            var handle = GetTargetHandle(list);
            ScheduleSteps(context.Skill, trigger, Time.time, handle, context);

            // 如果没有步骤引用该 Handle，立即释放
            if (handle.RefCount == 0)
            {
                ReleaseHandleUnused(handle);
            }
        }

        /// <summary>
        /// 根据技能定义调度指定触发时机的所有步骤。
        /// </summary>
        private void ScheduleSteps(SkillDefinition skill, SkillStepTrigger trigger, float baseTime, TargetListHandle targets, SkillRuntimeContext context)
        {
            var steps = skill.Steps;
            if (steps == null || steps.Count == 0)
            {
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                // 跳过不匹配的触发时机
                if (step == null || step.trigger != trigger)
                {
                    continue;
                }

                // 计算执行时间（基准时间 + 延迟）
                var executeAt = baseTime + Mathf.Max(0f, step.delay);
                pendingSteps.Add(new PendingStep(step, trigger, executeAt, targets, context));
                // 增加引用计数，防止过早释放
                targets.RefCount++;
            }
        }

        /// <summary>
        /// 执行单个技能步骤，对所有目标应用效果。
        /// </summary>
        private void ExecuteStep(PendingStep pending)
        {
            if (effectExecutor == null)
            {
                return;
            }

            var step = pending.Step;
            var targets = pending.Targets.Targets;
            if (targets == null || targets.Count == 0)
            {
                return;
            }

            var effects = step.effects;
            if (effects == null || effects.Count == 0)
            {
                return;
            }

            // 遍历所有目标
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                // 检查步骤级别的条件
                if (step.condition != null && !ConditionEvaluator.Evaluate(step.condition, pending.Context, target))
                {
                    continue;
                }

                // 对该目标执行所有效果
                for (int j = 0; j < effects.Count; j++)
                {
                    effectExecutor.ExecuteEffect(effects[j], pending.Context, target, pending.Trigger);
                }
            }
        }

        /// <summary>
        /// 检查当前资源是否足够释放技能。
        /// </summary>
        private bool HasResource(SkillDefinition skill)
        {
            // 无资源组件时，只有零消耗技能可以释放
            if (resource == null)
            {
                return skill.ResourceCost <= 0f;
            }

            // 资源类型必须匹配
            if (resource.ResourceType != skill.ResourceType)
            {
                return false;
            }

            return resource.Current >= skill.ResourceCost;
        }

        /// <summary>
        /// 扣除技能所需资源。
        /// </summary>
        /// <returns>若成功扣除则返回 true</returns>
        private bool SpendResource(SkillDefinition skill)
        {
            if (resource == null)
            {
                return skill.ResourceCost <= 0f;
            }

            if (resource.ResourceType != skill.ResourceType)
            {
                return false;
            }

            return resource.Spend(skill.ResourceCost);
        }

        /// <summary>
        /// 创建技能运行时上下文。
        /// </summary>
        private SkillRuntimeContext CreateContext(SkillDefinition skill)
        {
            return new SkillRuntimeContext(this, unitRoot, skill, eventHub, targetingSystem, effectExecutor);
        }

        /// <summary>
        /// 派发技能施法开始事件。
        /// </summary>
        private void RaiseSkillCastStarted(SkillRuntimeContext context, float castTime)
        {
            var evt = new SkillCastEvent(context.CasterUnit, context.Skill, castTime, context.Skill != null && context.Skill.ChannelTime > 0f);
            SkillCastStarted?.Invoke(evt);
            eventHub?.RaiseSkillCastStarted(evt);
        }

        /// <summary>
        /// 派发技能施法完成事件。
        /// </summary>
        private void RaiseSkillCastCompleted(SkillRuntimeContext context, float castTime)
        {
            var evt = new SkillCastEvent(context.CasterUnit, context.Skill, castTime, context.Skill != null && context.Skill.ChannelTime > 0f);
            SkillCastCompleted?.Invoke(evt);
            eventHub?.RaiseSkillCastCompleted(evt);
        }

        #region Target Handle Pool
        // 目标列表句柄池，用于复用 TargetListHandle 避免 GC
        private static readonly Stack<TargetListHandle> TargetHandlePool = new Stack<TargetListHandle>(16);

        /// <summary>
        /// 从池中获取目标列表句柄。
        /// </summary>
        private static TargetListHandle GetTargetHandle(List<CombatTarget> targets)
        {
            var handle = TargetHandlePool.Count > 0 ? TargetHandlePool.Pop() : new TargetListHandle();
            handle.Targets = targets;
            handle.RefCount = 0;
            return handle;
        }

        /// <summary>
        /// 减少句柄引用计数，归零时释放。
        /// </summary>
        private static void ReleaseHandle(TargetListHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            handle.RefCount--;
            if (handle.RefCount <= 0)
            {
                ReleaseHandleInternal(handle);
            }
        }

        /// <summary>
        /// 释放未被使用的句柄（引用计数为 0）。
        /// </summary>
        private static void ReleaseHandleUnused(TargetListHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            ReleaseHandleInternal(handle);
        }

        /// <summary>
        /// 内部释放逻辑：归还目标列表和句柄到池中。
        /// </summary>
        private static void ReleaseHandleInternal(TargetListHandle handle)
        {
            SimpleListPool<CombatTarget>.Release(handle.Targets);
            handle.Targets = null;
            handle.RefCount = 0;
            TargetHandlePool.Push(handle);
        }
        #endregion

        #region Internal Types
        /// <summary>
        /// 待执行的技能步骤数据。
        /// </summary>
        private struct PendingStep
        {
            /// <summary>技能步骤配置</summary>
            public SkillStep Step;
            /// <summary>触发时机</summary>
            public SkillStepTrigger Trigger;
            /// <summary>执行时间戳</summary>
            public float ExecuteAt;
            /// <summary>目标列表句柄</summary>
            public TargetListHandle Targets;
            /// <summary>技能运行时上下文</summary>
            public SkillRuntimeContext Context;

            public PendingStep(SkillStep step, SkillStepTrigger trigger, float executeAt, TargetListHandle targets, SkillRuntimeContext context)
            {
                Step = step;
                Trigger = trigger;
                ExecuteAt = executeAt;
                Targets = targets;
                Context = context;
            }
        }

        /// <summary>
        /// 目标列表句柄，用于引用计数管理目标列表的生命周期。
        /// </summary>
        /// <remarks>
        /// 多个 PendingStep 可能共享同一个目标列表，使用引用计数确保
        /// 在最后一个步骤执行完后才释放列表。
        /// </remarks>
        private sealed class TargetListHandle
        {
            /// <summary>目标列表</summary>
            public List<CombatTarget> Targets;
            /// <summary>引用计数</summary>
            public int RefCount;
        }
        #endregion
    }
}
