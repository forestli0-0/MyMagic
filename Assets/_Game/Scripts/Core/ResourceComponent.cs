using System;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 资源组件，负责管理单位的各类消耗性数值（如法力、能量、耐力等）及其自动恢复。
    /// </summary>
    public class ResourceComponent : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("全局事件中心")]
        [SerializeField] private CombatEventHub eventHub;
        [Tooltip("关联的属性组件")]
        [SerializeField] private StatsComponent stats;
        
        [Header("属性映射")]
        [Tooltip("该组件管理的资源类型")]
        [SerializeField] private ResourceType resourceType = ResourceType.Mana;
        [Tooltip("关联的最大资源量属性定义")]
        [SerializeField] private StatDefinition maxResourceStat;
        [Tooltip("关联的资源恢复速率属性定义")]
        [SerializeField] private StatDefinition regenStat;
        
        [Header("基础设定")]
        [Tooltip("若无属性定义时的基础最大值")]
        [SerializeField] private float baseMaxResource = 100f;
        [Tooltip("是否在 Awake 时执行初始化")]
        [SerializeField] private bool initializeOnAwake = true;
        [Tooltip("初始化时是否将资源填满")]
        [SerializeField] private bool initializeToMax = true;
        [Tooltip("是否限制资源不能超过最大值")]
        [SerializeField] private bool clampToMax = true;

        private float currentResource;
        private float maxResource;

        /// <summary>
        /// 当资源数值发生变化时派发。
        /// </summary>
        public event Action<ResourceChangedEvent> ResourceChanged;

        public ResourceType ResourceType => resourceType;
        public float Current => currentResource;
        public float Max => maxResource;

        private void Reset()
        {
            // 自动关联同物体下的属性组件
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

            // 监听最大资源量相关的属性变化
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
            // 处理自然回复逻辑
            if (regenStat == null || stats == null || currentResource >= maxResource)
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
        /// 设置中心事件分发器。
        /// </summary>
        public void SetEventHub(CombatEventHub hub)
        {
            eventHub = hub;
        }

        /// <summary>
        /// 初始化资源状态。
        /// </summary>
        public void Initialize()
        {
            RefreshMaxResource(false);

            if (initializeToMax || currentResource <= 0f)
            {
                currentResource = maxResource;
            }

            if (clampToMax)
            {
                currentResource = Mathf.Clamp(currentResource, 0f, maxResource);
            }
        }

        /// <summary>
        /// 重新计算最大资源量。
        /// </summary>
        /// <param name="keepRatio">是否按比例缩放当前资源量</param>
        public void RefreshMaxResource(bool keepRatio)
        {
            var oldMax = maxResource;
            var oldCurrent = currentResource;
            maxResource = GetMaxResourceValue();

            if (keepRatio && oldMax > 0f)
            {
                var ratio = currentResource / oldMax;
                currentResource = ratio * maxResource;
            }

            if (clampToMax)
            {
                currentResource = Mathf.Clamp(currentResource, 0f, maxResource);
            }

            if (!Mathf.Approximately(oldMax, maxResource) || !Mathf.Approximately(oldCurrent, currentResource))
            {
                RaiseResourceChanged(oldCurrent, currentResource);
            }
        }

        /// <summary>
        /// 检查资源是否足够。
        /// </summary>
        public bool CanSpend(float amount)
        {
            return amount <= currentResource;
        }

        /// <summary>
        /// 尝试消耗一定数量的资源。
        /// </summary>
        /// <returns>若消耗成功（资源足够）返回 true</returns>
        public bool Spend(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (currentResource < amount)
            {
                return false;
            }

            ModifyCurrent(-amount);
            return true;
        }

        /// <summary>
        /// 恢复一定数量的资源。
        /// </summary>
        public void Restore(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            ModifyCurrent(amount);
        }

        /// <summary>
        /// 直接设置当前的资源绝对值。
        /// </summary>
        public void SetCurrent(float value)
        {
            ModifyCurrent(value - currentResource);
        }

        /// <summary>
        /// 核数值修改方法。
        /// </summary>
        private void ModifyCurrent(float delta)
        {
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            var oldValue = currentResource;
            currentResource = clampToMax ? Mathf.Clamp(oldValue + delta, 0f, maxResource) : oldValue + delta;

            if (!Mathf.Approximately(oldValue, currentResource))
            {
                RaiseResourceChanged(oldValue, currentResource);
            }
        }

        /// <summary>
        /// 响应属性变化事件。
        /// </summary>
        private void HandleStatChanged(StatChangedEvent evt)
        {
            if (evt.Stat == maxResourceStat)
            {
                RefreshMaxResource(true);
            }
        }

        /// <summary>
        /// 获取最大资源量的基础值。
        /// </summary>
        private float GetMaxResourceValue()
        {
            if (maxResourceStat != null && stats != null && stats.TryGetValue(maxResourceStat, out var value))
            {
                return Mathf.Max(0f, value);
            }

            return Mathf.Max(0f, baseMaxResource);
        }

        /// <summary>
        /// 派发资源变更事件。
        /// </summary>
        private void RaiseResourceChanged(float oldValue, float newValue)
        {
            var evt = new ResourceChangedEvent(this, resourceType, oldValue, newValue);
            ResourceChanged?.Invoke(evt);
            eventHub?.RaiseResourceChanged(evt);
        }
    }
}
