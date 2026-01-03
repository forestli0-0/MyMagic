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
    public class BuffController : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private UnitRoot unitRoot;
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private EffectExecutor effectExecutor;
        [SerializeField] private TargetingSystem targetingSystem;

        private readonly List<BuffInstance> activeBuffs = new List<BuffInstance>(16);
        private readonly Dictionary<BuffDefinition, int> indexByBuff = new Dictionary<BuffDefinition, int>(16);
        private readonly List<int> expiredIndices = new List<int>(8);

        private CombatTarget selfTarget;
        private bool hasSelfTarget;

        public event Action BuffsChanged;

        /// <summary>
        /// 获取当前激活的 Buff 实例只读列表。
        /// </summary>
        public IReadOnlyList<BuffInstance> ActiveBuffs => activeBuffs;

        private void Reset()
        {
            unitRoot = GetComponent<UnitRoot>();
            skillUser = GetComponent<SkillUserComponent>();
        }

        private void Awake()
        {
            EnsureReferences();
            RefreshSelfTarget();
        }

        private void OnEnable()
        {
            EnsureReferences();
            RefreshSelfTarget();
        }

        private void Update()
        {
            if (activeBuffs.Count == 0)
            {
                return;
            }

            var now = Time.time;
            expiredIndices.Clear();

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                var instance = activeBuffs[i];
                if (instance.Definition == null)
                {
                    expiredIndices.Add(i);
                    continue;
                }

                if (instance.EndTime > 0f && instance.EndTime <= now)
                {
                    expiredIndices.Add(i);
                    continue;
                }

                if (instance.NextTickTime > 0f && now >= instance.NextTickTime)
                {
                    TriggerBuff(instance, BuffTriggerType.OnTick, BuildContext(default), GetSelfTarget());

                    var interval = instance.Definition.TickInterval;
                    instance.NextTickTime = interval > 0f ? now + interval : -1f;
                    activeBuffs[i] = instance;
                }
            }

            for (int i = expiredIndices.Count - 1; i >= 0; i--)
            {
                RemoveAt(expiredIndices[i], true);
            }
        }

        /// <summary>
        /// 检查单位是否拥有指定的 Buff。
        /// </summary>
        public bool HasBuff(BuffDefinition buff)
        {
            return buff != null && indexByBuff.ContainsKey(buff);
        }

        /// <summary>
        /// 获取指定 Buff 的当前堆叠层数。
        /// </summary>
        public int GetStacks(BuffDefinition buff)
        {
            if (buff == null)
            {
                return 0;
            }

            if (indexByBuff.TryGetValue(buff, out var index))
            {
                return activeBuffs[index].Stacks;
            }

            return 0;
        }

        /// <summary>
        /// 应用一个 Buff 到该单位。
        /// </summary>
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

            if (indexByBuff.TryGetValue(buff, out var index))
            {
                var instance = activeBuffs[index];
                instance.Stacks = Mathf.Clamp(instance.Stacks + 1, 1, Mathf.Max(1, buff.MaxStacks));

                if (buff.StackingRule == BuffStackingRule.Refresh)
                {
                    instance.EndTime = duration > 0f ? now + duration : -1f;
                    instance.NextTickTime = tickInterval > 0f ? now + tickInterval : -1f;
                }
                else if (buff.StackingRule == BuffStackingRule.Extend)
                {
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
                    instance.EndTime = duration > 0f ? now + duration : instance.EndTime;
                }

                activeBuffs[index] = instance;
                TriggerBuff(instance, BuffTriggerType.OnApply, BuildContext(default), GetSelfTarget());
                BuffsChanged?.Invoke();
                return;
            }

            var endTime = duration > 0f ? now + duration : -1f;
            var nextTick = tickInterval > 0f ? now + tickInterval : -1f;
            var newInstance = new BuffInstance(buff, 1, endTime, nextTick);
            indexByBuff[buff] = activeBuffs.Count;
            activeBuffs.Add(newInstance);

            TriggerBuff(newInstance, BuffTriggerType.OnApply, BuildContext(default), GetSelfTarget());
            BuffsChanged?.Invoke();
        }

        /// <summary>
        /// 移除指定的 Buff。
        /// </summary>
        public bool RemoveBuff(BuffDefinition buff)
        {
            if (buff == null)
            {
                return false;
            }

            if (indexByBuff.TryGetValue(buff, out var index))
            {
                RemoveAt(index, true);
                return true;
            }

            return false;
        }

        public void NotifyHit(SkillRuntimeContext context, CombatTarget target)
        {
            TriggerBuffs(BuffTriggerType.OnHit, context, target);
        }

        public void NotifyDamaged(SkillRuntimeContext context, CombatTarget attacker)
        {
            TriggerBuffs(BuffTriggerType.OnDamaged, context, attacker);
        }

        public void NotifySkillCast(SkillRuntimeContext context, CombatTarget target)
        {
            TriggerBuffs(BuffTriggerType.OnSkillCast, context, target);
        }

        public void NotifyKill(SkillRuntimeContext context, CombatTarget target)
        {
            TriggerBuffs(BuffTriggerType.OnKill, context, target);
        }

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

            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null || trigger.triggerType != triggerType)
                {
                    continue;
                }

                var chance = Mathf.Clamp01(trigger.chance);
                if (chance <= 0f || (chance < 1f && UnityEngine.Random.value > chance))
                {
                    continue;
                }

                if (trigger.condition != null && !ConditionEvaluator.Evaluate(trigger.condition, execContext, target))
                {
                    continue;
                }

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

        private SkillRuntimeContext BuildContext(SkillRuntimeContext context)
        {
            var skill = context.Skill;
            if (context.CasterUnit != null && context.CasterUnit == unitRoot && context.Caster == skillUser)
            {
                return context;
            }

            return new SkillRuntimeContext(skillUser, unitRoot, skill, unitRoot != null ? unitRoot.EventHub : null, targetingSystem, effectExecutor);
        }

        private void RemoveAt(int index, bool invokeExpire)
        {
            var lastIndex = activeBuffs.Count - 1;
            var removed = activeBuffs[index];

            if (invokeExpire)
            {
                TriggerBuff(removed, BuffTriggerType.OnExpire, BuildContext(default), GetSelfTarget());
            }

            if (index != lastIndex)
            {
                var last = activeBuffs[lastIndex];
                activeBuffs[index] = last;
                indexByBuff[last.Definition] = index;
            }

            activeBuffs.RemoveAt(lastIndex);
            indexByBuff.Remove(removed.Definition);

            BuffsChanged?.Invoke();
        }

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

        private void RefreshSelfTarget()
        {
            hasSelfTarget = CombatTarget.TryCreate(gameObject, out selfTarget);
        }

        private CombatTarget GetSelfTarget()
        {
            if (!hasSelfTarget)
            {
                RefreshSelfTarget();
            }

            return selfTarget;
        }

        private static SkillStepTrigger MapTrigger(BuffTriggerType triggerType)
        {
            return triggerType == BuffTriggerType.OnHit || triggerType == BuffTriggerType.OnDamaged
                ? SkillStepTrigger.OnHit
                : SkillStepTrigger.OnCastStart;
        }

        /// <summary>
        /// Buff 运行时实例数据结构。
        /// </summary>
        public struct BuffInstance
        {
            public BuffDefinition Definition;
            public int Stacks;
            public float EndTime;
            public float NextTickTime;

            public BuffInstance(BuffDefinition definition, int stacks, float endTime, float nextTickTime)
            {
                Definition = definition;
                Stacks = stacks;
                EndTime = endTime;
                NextTickTime = nextTickTime;
            }
        }
    }
}
