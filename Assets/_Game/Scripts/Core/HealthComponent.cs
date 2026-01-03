using System;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 生命值组件，负责管理当前生命值、处理伤害/治疗、自动回血逻辑，并监测死亡状态。
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("全局事件中心")]
        [SerializeField] private CombatEventHub eventHub;
        [Tooltip("相关的属性组件")]
        [SerializeField] private StatsComponent stats;
        
        [Header("配置")]
        [Tooltip("关联的最大生命值属性定义")]
        [SerializeField] private StatDefinition maxHealthStat;
        [Tooltip("关联的生命恢复速率属性定义")]
        [SerializeField] private StatDefinition regenStat;
        [Tooltip("如果没有找到属性定义时的基础最大生命值")]
        [SerializeField] private float baseMaxHealth = 100f;
        
        [Header("逻辑选项")]
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool initializeOnAwake = true;
        [Tooltip("初始化时是否将血量填满")]
        [SerializeField] private bool initializeToMax = true;
        [Tooltip("是否限制生命值不能超过最大值")]
        [SerializeField] private bool clampToMax = true;

        private float currentHealth;
        private float maxHealth;

        /// <summary>
        /// 当生命值数值发生变化时触发（包括最大值变更引起的调整）。
        /// </summary>
        public event Action<HealthChangedEvent> HealthChanged;
        /// <summary>
        /// 当单位生命值归零时触发。
        /// </summary>
        public event Action<HealthComponent> Died;

        public float Current => currentHealth;
        public float Max => maxHealth;
        public bool IsAlive => currentHealth > 0f;

        private void Reset()
        {
            // 编辑器脚本：尝试自动寻找同名组件
            stats = GetComponent<StatsComponent>();
        }

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void OnEnable()
        {
            if (stats == null)
            {
                stats = GetComponent<StatsComponent>();
            }

            // 监听属性变更，特别是最大生命值的变更
            if (stats != null)
            {
                stats.StatChanged += HandleStatChanged;
            }
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.StatChanged -= HandleStatChanged;
            }
        }

        private void Update()
        {
            // 处理每秒自动回血
            if (regenStat == null || stats == null || currentHealth >= maxHealth)
            {
                return;
            }

            var regen = stats.GetValue(regenStat, 0f);
            if (regen <= 0f)
            {
                return;
            }

            ModifyCurrent(regen * Time.deltaTime);
        }

        /// <summary>
        /// 设置外部事件中心引用。
        /// </summary>
        public void SetEventHub(CombatEventHub hub)
        {
            eventHub = hub;
        }

        /// <summary>
        /// 初始化生命值状态。
        /// </summary>
        public void Initialize()
        {
            RefreshMaxHealth(false);

            if (initializeToMax || currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }

            if (clampToMax)
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            }
        }

        /// <summary>
        /// 重新从属性组件计算最大生命值。
        /// </summary>
        /// <param name="keepRatio">维持当前的血量百分比（例如升级时）</param>
        public void RefreshMaxHealth(bool keepRatio)
        {
            var oldMax = maxHealth;
            var oldCurrent = currentHealth;
            maxHealth = GetMaxHealthValue();

            if (keepRatio && oldMax > 0f)
            {
                var ratio = currentHealth / oldMax;
                currentHealth = ratio * maxHealth;
            }

            if (clampToMax)
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            }

            // 如果数值真实发生了变化，则通知
            if (!Mathf.Approximately(oldMax, maxHealth) || !Mathf.Approximately(oldCurrent, currentHealth))
            {
                RaiseHealthChanged(oldCurrent, currentHealth);
            }
        }

        /// <summary>
        /// 对该组件应用伤害。
        /// </summary>
        /// <param name="amount">伤害数值</param>
        public void ApplyDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            ModifyCurrent(-amount);
        }

        /// <summary>
        /// 对该组件应用治疗。
        /// </summary>
        /// <param name="amount">治疗数值</param>
        public void Heal(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            ModifyCurrent(amount);
        }

        /// <summary>
        /// 直接设置当前的生命值绝对值。
        /// </summary>
        public void SetCurrent(float value)
        {
            ModifyCurrent(value - currentHealth);
        }

        /// <summary>
        /// 核心数值修改方法。
        /// </summary>
        private void ModifyCurrent(float delta)
        {
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            var oldValue = currentHealth;
            currentHealth = clampToMax ? Mathf.Clamp(oldValue + delta, 0f, maxHealth) : oldValue + delta;

            if (!Mathf.Approximately(oldValue, currentHealth))
            {
                RaiseHealthChanged(oldValue, currentHealth);

                // 死亡判定：从有血到无血的临界点触发
                if (oldValue > 0f && currentHealth <= 0f)
                {
                    Died?.Invoke(this);
                    eventHub?.RaiseUnitDied(this);
                }
            }
        }

        /// <summary>
        /// 响应属性变化。
        /// </summary>
        private void HandleStatChanged(StatChangedEvent evt)
        {
            if (evt.Stat == maxHealthStat)
            {
                RefreshMaxHealth(true);
            }
        }

        /// <summary>
        /// 获取最大生命值的当前数值输入。
        /// </summary>
        private float GetMaxHealthValue()
        {
            if (maxHealthStat != null && stats != null && stats.TryGetValue(maxHealthStat, out var value))
            {
                return Mathf.Max(0f, value);
            }

            return Mathf.Max(0f, baseMaxHealth);
        }

        /// <summary>
        /// 内部方法：包装并派发血量变更事件。
        /// </summary>
        private void RaiseHealthChanged(float oldValue, float newValue)
        {
            var evt = new HealthChangedEvent(this, oldValue, newValue);
            HealthChanged?.Invoke(evt);
            eventHub?.RaiseHealthChanged(evt);
        }
    }
}
