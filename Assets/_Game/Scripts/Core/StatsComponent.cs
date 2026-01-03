using System;
using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 属性组件，负责运行时的数值管理、修正器叠加以及变更事件分发。
    /// </summary>
    public class StatsComponent : MonoBehaviour
    {
        [Tooltip("初始单位配置")]
        [SerializeField] private UnitDefinition unitDefinition;
        [Tooltip("事件中心引用，用于分发全局变更事件")]
        [SerializeField] private CombatEventHub eventHub;
        [Tooltip("Buff 控制器，用于应用修正器")]
        [SerializeField] private BuffController buffController;
        [Tooltip("单位根组件")]
        [SerializeField] private UnitRoot unitRoot;
        [Tooltip("单位标签组件")]
        [SerializeField] private UnitTagsComponent unitTags;
        [Tooltip("是否在 Awake 时执行初始化")]
        [SerializeField] private bool initializeOnAwake = true;

        // 运行时属性数据存储，使用列表保证内存布局紧凑
        private readonly List<RuntimeStat> runtimeStats = new List<RuntimeStat>(16);
        // 通过定义查找索引的映射表
        private readonly Dictionary<StatDefinition, int> indexByStat = new Dictionary<StatDefinition, int>(16);

        /// <summary>
        /// 当属性发生变化时触发的本地事件。
        /// </summary>
        public event Action<StatChangedEvent> StatChanged;

        /// <summary>
        /// 获取当前的单位配置定义。
        /// </summary>
        public UnitDefinition Definition => unitDefinition;

        private void Reset()
        {
            unitRoot = GetComponent<UnitRoot>();
            buffController = GetComponent<BuffController>();
            unitTags = GetComponent<UnitTagsComponent>();
        }

        private void Awake()
        {
            if (initializeOnAwake && unitDefinition != null)
            {
                Initialize(unitDefinition);
            }
        }

        private void OnEnable()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (buffController == null)
            {
                buffController = GetComponent<BuffController>();
            }

            if (unitTags == null)
            {
                unitTags = GetComponent<UnitTagsComponent>();
            }

            if (buffController != null)
            {
                buffController.BuffsChanged += HandleBuffsChanged;
            }
        }

        private void OnDisable()
        {
            if (buffController != null)
            {
                buffController.BuffsChanged -= HandleBuffsChanged;
            }
        }

        /// <summary>
        /// 设置或更换事件中心。
        /// </summary>
        public void SetEventHub(CombatEventHub hub)
        {
            eventHub = hub;
        }

        /// <summary>
        /// 设置或更换 Buff 控制器。
        /// </summary>
        public void SetBuffController(BuffController controller)
        {
            if (buffController == controller)
            {
                return;
            }

            if (buffController != null)
            {
                buffController.BuffsChanged -= HandleBuffsChanged;
            }

            buffController = controller;

            if (buffController != null)
            {
                buffController.BuffsChanged += HandleBuffsChanged;
            }

            RefreshModifiers();
        }

        /// <summary>
        /// 根据单位定义初始化所有基础属性。
        /// </summary>
        /// <param name="definition">单位配置定义</param>
        public void Initialize(UnitDefinition definition)
        {
            Clear();
            unitDefinition = definition;
            if (definition == null)
            {
                return;
            }

            // 加载初始基础属性
            var baseStats = definition.BaseStats;
            for (int i = 0; i < baseStats.Count; i++)
            {
                AddOrSet(baseStats[i].stat, baseStats[i].value, false);
            }

            RefreshModifiers();
        }

        /// <summary>
        /// 清空所有运行时属性数据。
        /// </summary>
        public void Clear()
        {
            runtimeStats.Clear();
            indexByStat.Clear();
        }

        /// <summary>
        /// 尝试获取指定属性的当前值。
        /// </summary>
        /// <param name="stat">属性定义</param>
        /// <param name="value">获取到的值</param>
        /// <returns>若属性存在则返回 true</returns>
        public bool TryGetValue(StatDefinition stat, out float value)
        {
            if (stat == null)
            {
                value = 0f;
                return false;
            }

            if (indexByStat.TryGetValue(stat, out var index))
            {
                value = runtimeStats[index].value;
                return true;
            }

            value = 0f;
            return false;
        }

        /// <summary>
        /// 获取指定属性的当前值，若不存在则返回回退值。
        /// </summary>
        public float GetValue(StatDefinition stat, float fallback = 0f)
        {
            return TryGetValue(stat, out var value) ? value : fallback;
        }

        /// <summary>
        /// 直接设置某个属性的基础值。
        /// </summary>
        public void SetValue(StatDefinition stat, float value)
        {
            SetBaseValue(stat, value, true);
        }

        /// <summary>
        /// 修改某个属性的基础值（增量方式）。
        /// </summary>
        /// <param name="stat">目标属性</param>
        /// <param name="delta">变化量</param>
        public void ModifyValue(StatDefinition stat, float delta)
        {
            if (stat == null)
            {
                return;
            }

            if (indexByStat.TryGetValue(stat, out var index))
            {
                var data = runtimeStats[index];
                SetBaseValueInternal(index, data.baseValue + delta, true);
                return;
            }

            // 如果属性尚不存在，则以 delta 作为初始值添加
            AddOrSet(stat, delta, true);
        }

        /// <summary>
        /// 强制刷新所有属性修正器。
        /// </summary>
        public void RefreshModifiers()
        {
            if (runtimeStats.Count == 0)
            {
                return;
            }

            var context = CreateContext();
            var hasTarget = CombatTarget.TryCreate(gameObject, out var selfTarget);

            for (int i = 0; i < runtimeStats.Count; i++)
            {
                var data = runtimeStats[i];
                var oldValue = data.value;
                var newValue = CalculateModifiedValue(data.stat, data.baseValue, context, hasTarget ? selfTarget : default);

                if (!Mathf.Approximately(oldValue, newValue))
                {
                    data.value = newValue;
                    runtimeStats[i] = data;
                    RaiseStatChanged(data.stat, oldValue, newValue);
                }
            }
        }

        /// <summary>
        /// 内部方法：添加新属性或更新已有属性，可选是否派发事件。
        /// </summary>
        private void AddOrSet(StatDefinition stat, float baseValue, bool raiseEvent)
        {
            if (stat == null)
            {
                return;
            }

            if (indexByStat.TryGetValue(stat, out var index))
            {
                SetBaseValueInternal(index, baseValue, raiseEvent);
                return;
            }

            // 注册新属性
            indexByStat[stat] = runtimeStats.Count;
            runtimeStats.Add(new RuntimeStat(stat, baseValue, baseValue));

            if (raiseEvent)
            {
                RaiseStatChanged(stat, 0f, baseValue);
            }
        }

        /// <summary>
        /// 内部方法：更新基础值并重新计算最终值。
        /// </summary>
        private void SetBaseValueInternal(int index, float baseValue, bool raiseEvent)
        {
            var data = runtimeStats[index];
            var oldValue = data.value;
            data.baseValue = baseValue;
            data.value = CalculateModifiedValue(data.stat, baseValue, CreateContext(), GetSelfTarget());
            runtimeStats[index] = data;

            if (raiseEvent && !Mathf.Approximately(oldValue, data.value))
            {
                RaiseStatChanged(data.stat, oldValue, data.value);
            }
        }

        private void SetBaseValue(StatDefinition stat, float baseValue, bool raiseEvent)
        {
            if (stat == null)
            {
                return;
            }

            if (indexByStat.TryGetValue(stat, out var index))
            {
                SetBaseValueInternal(index, baseValue, raiseEvent);
                return;
            }

            AddOrSet(stat, baseValue, raiseEvent);
        }

        private float CalculateModifiedValue(StatDefinition stat, float baseValue, SkillRuntimeContext context, CombatTarget selfTarget)
        {
            if (buffController == null || buffController.ActiveBuffs.Count == 0)
            {
                return baseValue;
            }

            var add = 0f;
            var mul = 0f;
            var hasOverride = false;
            var overrideValue = baseValue;

            var buffs = buffController.ActiveBuffs;
            for (int i = 0; i < buffs.Count; i++)
            {
                var buffDef = buffs[i].Definition;
                if (buffDef == null)
                {
                    continue;
                }

                var modifiers = buffDef.Modifiers;
                if (modifiers == null || modifiers.Count == 0)
                {
                    continue;
                }

                var stacks = Mathf.Max(1, buffs[i].Stacks);

                for (int j = 0; j < modifiers.Count; j++)
                {
                    var modifier = modifiers[j];
                    if (modifier == null || modifier.Target != ModifierTargetType.Stat)
                    {
                        continue;
                    }

                    if (modifier.Stat != stat)
                    {
                        continue;
                    }

                    if (!TagsMatch(modifier.RequiredTags, modifier.BlockedTags))
                    {
                        continue;
                    }

                    if (modifier.Condition != null && !ConditionEvaluator.Evaluate(modifier.Condition, context, selfTarget))
                    {
                        continue;
                    }

                    var value = modifier.Value * stacks;
                    switch (modifier.Operation)
                    {
                        case ModifierOperation.Add:
                            add += value;
                            break;
                        case ModifierOperation.Multiply:
                            mul += value;
                            break;
                        case ModifierOperation.Override:
                            hasOverride = true;
                            overrideValue = value;
                            break;
                    }
                }
            }

            var result = (baseValue + add) * (1f + mul);
            if (hasOverride)
            {
                result = overrideValue;
            }

            return result;
        }

        private SkillRuntimeContext CreateContext()
        {
            return new SkillRuntimeContext(null, unitRoot, null, eventHub, null, null);
        }

        private CombatTarget GetSelfTarget()
        {
            CombatTarget.TryCreate(gameObject, out var selfTarget);
            return selfTarget;
        }

        private bool TagsMatch(IReadOnlyList<TagDefinition> required, IReadOnlyList<TagDefinition> blocked)
        {
            if (required != null && required.Count > 0)
            {
                if (unitTags == null)
                {
                    return false;
                }

                for (int i = 0; i < required.Count; i++)
                {
                    var tag = required[i];
                    if (tag != null && !unitTags.HasTag(tag))
                    {
                        return false;
                    }
                }
            }

            if (blocked != null && blocked.Count > 0 && unitTags != null)
            {
                for (int i = 0; i < blocked.Count; i++)
                {
                    var tag = blocked[i];
                    if (tag != null && unitTags.HasTag(tag))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void HandleBuffsChanged()
        {
            RefreshModifiers();
        }

        /// <summary>
        /// 派发属性变更事件至本地和全局事件中心。
        /// </summary>
        private void RaiseStatChanged(StatDefinition stat, float oldValue, float newValue)
        {
            var evt = new StatChangedEvent(this, stat, oldValue, newValue);
            StatChanged?.Invoke(evt);
            eventHub?.RaiseStatChanged(evt);
        }

        /// <summary>
        /// 运行时属性数据项结构。
        /// </summary>
        private struct RuntimeStat
        {
            public StatDefinition stat;
            public float baseValue;
            public float value;

            public RuntimeStat(StatDefinition stat, float baseValue, float value)
            {
                this.stat = stat;
                this.baseValue = baseValue;
                this.value = value;
            }
        }
    }
}
