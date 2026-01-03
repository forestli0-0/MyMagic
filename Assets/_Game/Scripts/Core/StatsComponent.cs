using System;
using System.Collections.Generic;
using CombatSystem.Data;
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

        private void Awake()
        {
            if (initializeOnAwake && unitDefinition != null)
            {
                Initialize(unitDefinition);
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
        /// 直接设置某个属性的值。
        /// </summary>
        public void SetValue(StatDefinition stat, float value)
        {
            AddOrSet(stat, value, true);
        }

        /// <summary>
        /// 修改某个属性的值（增量方式）。
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
                var current = runtimeStats[index].value;
                SetValueInternal(index, current + delta, true);
                return;
            }

            // 如果属性尚不存在，则以 delta 作为初始值添加
            AddOrSet(stat, delta, true);
        }

        /// <summary>
        /// 内部方法：添加新属性或更新已有属性，可选是否派发事件。
        /// </summary>
        private void AddOrSet(StatDefinition stat, float value, bool raiseEvent)
        {
            if (stat == null)
            {
                return;
            }

            if (indexByStat.TryGetValue(stat, out var index))
            {
                SetValueInternal(index, value, raiseEvent);
                return;
            }

            // 注册新属性
            indexByStat[stat] = runtimeStats.Count;
            runtimeStats.Add(new RuntimeStat(stat, value));

            if (raiseEvent)
            {
                RaiseStatChanged(stat, 0f, value);
            }
        }

        /// <summary>
        /// 内部方法：执行实际的数值赋值并处理变更逻辑。
        /// </summary>
        private void SetValueInternal(int index, float value, bool raiseEvent)
        {
            var data = runtimeStats[index];
            var oldValue = data.value;
            if (Mathf.Approximately(oldValue, value))
            {
                return;
            }

            data.value = value;
            runtimeStats[index] = data;

            if (raiseEvent)
            {
                RaiseStatChanged(data.stat, oldValue, value);
            }
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
            public float value;

            public RuntimeStat(StatDefinition stat, float value)
            {
                this.stat = stat;
                this.value = value;
            }
        }
    }
}
