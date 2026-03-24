using System;
using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 资源组件，统一管理主资源与附加资源（如 Flow、怒气、弹仓等）。
    /// </summary>
    public class ResourceComponent : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("全局事件中心")]
        [SerializeField] private CombatEventHub eventHub;
        [Tooltip("关联的属性组件")]
        [SerializeField] private StatsComponent stats;

        [Header("主资源配置")]
        [Tooltip("主资源定义（为空时使用旧主资源配置）")]
        [SerializeField] private ResourceDefinition mainResourceDefinition;
        [Tooltip("旧主资源类型（兼容旧配置）")]
        [SerializeField] private ResourceType resourceType = ResourceType.Mana;
        [Tooltip("旧主资源最大值属性（兼容旧配置）")]
        [SerializeField] private StatDefinition maxResourceStat;
        [Tooltip("旧主资源回复属性（兼容旧配置）")]
        [SerializeField] private StatDefinition regenStat;

        [Header("基础设定")]
        [Tooltip("若无属性定义时的主资源基础最大值")]
        [SerializeField] private float baseMaxResource = 100f;
        [Tooltip("是否在 Awake 时执行初始化")]
        [SerializeField] private bool initializeOnAwake = true;
        [Tooltip("主资源初始化时是否填满")]
        [SerializeField] private bool initializeToMax = true;
        [Tooltip("是否限制资源不能超过最大值")]
        [SerializeField] private bool clampToMax = true;

        [Header("附加资源")]
        [Tooltip("预注册的附加资源定义")]
        [SerializeField] private List<ResourceDefinition> additionalResources = new List<ResourceDefinition>(2);

        private readonly ResourceChannel primaryChannel = new ResourceChannel();
        private readonly List<ResourceChannel> auxiliaryChannels = new List<ResourceChannel>(4);
        private readonly Dictionary<ResourceDefinition, ResourceChannel> auxiliaryByDefinition = new Dictionary<ResourceDefinition, ResourceChannel>(4);

        /// <summary>
        /// 当资源数值发生变化时派发。
        /// </summary>
        public event Action<ResourceChangedEvent> ResourceChanged;

        public ResourceDefinition PrimaryResource => primaryChannel.Definition;
        public ResourceType ResourceType => primaryChannel.LegacyType;
        public float Current => primaryChannel.Current;
        public float Max => primaryChannel.Max;

        private void Reset()
        {
            stats = GetComponent<StatsComponent>();
        }

        private void Awake()
        {
            if (initializeOnAwake && !ShouldDeferInitializationToUnitRoot())
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
            UpdateChannelRegen(primaryChannel);

            for (int i = 0; i < auxiliaryChannels.Count; i++)
            {
                UpdateChannelRegen(auxiliaryChannels[i]);
            }
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
            auxiliaryChannels.Clear();
            auxiliaryByDefinition.Clear();

            ConfigurePrimaryChannel();
            InitializeChannel(primaryChannel, false, true);

            for (int i = 0; i < additionalResources.Count; i++)
            {
                EnsureResource(additionalResources[i]);
            }
        }

        /// <summary>
        /// 重新计算主资源最大值。
        /// </summary>
        public void RefreshMaxResource(bool keepRatio)
        {
            RefreshChannelMax(primaryChannel, keepRatio);
        }

        public bool CanSpend(float amount)
        {
            return CanSpend((ResourceDefinition)null, amount);
        }

        public bool Spend(float amount)
        {
            return Spend((ResourceDefinition)null, amount);
        }

        public void Restore(float amount)
        {
            Restore((ResourceDefinition)null, amount);
        }

        public void SetCurrent(float value)
        {
            SetCurrent((ResourceDefinition)null, value);
        }

        public bool EnsureResource(ResourceDefinition resource)
        {
            if (resource == null)
            {
                return false;
            }

            if (ReferenceEquals(primaryChannel.Definition, resource))
            {
                return true;
            }

            if (auxiliaryByDefinition.ContainsKey(resource))
            {
                return true;
            }

            var channel = BuildChannelFromDefinition(resource, false);
            auxiliaryChannels.Add(channel);
            auxiliaryByDefinition[resource] = channel;
            InitializeChannel(channel, false, true);
            return true;
        }

        public bool HasResource(ResourceDefinition resource)
        {
            return ResolveChannel(resource) != null;
        }

        public float GetCurrent(ResourceDefinition resource)
        {
            var channel = ResolveChannel(resource);
            return channel != null ? channel.Current : 0f;
        }

        public float GetMax(ResourceDefinition resource)
        {
            var channel = ResolveChannel(resource);
            return channel != null ? channel.Max : 0f;
        }

        public bool CanSpend(ResourceDefinition resource, float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            var channel = ResolveChannel(resource);
            return channel != null && channel.Current >= amount;
        }

        public bool Spend(ResourceDefinition resource, float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            var channel = ResolveChannel(resource);
            if (channel == null || channel.Current < amount)
            {
                return false;
            }

            ModifyCurrent(channel, -amount);
            return true;
        }

        public void Restore(ResourceDefinition resource, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            var channel = ResolveOrCreateChannel(resource);
            if (channel == null)
            {
                return;
            }

            ModifyCurrent(channel, amount);
        }

        public void SetCurrent(ResourceDefinition resource, float value)
        {
            var channel = ResolveOrCreateChannel(resource);
            if (channel == null)
            {
                return;
            }

            SetCurrent(channel, value);
        }

        public bool TryGetHighestPriorityHudResource(out ResourceView view)
        {
            ResourceChannel best = null;
            for (int i = 0; i < auxiliaryChannels.Count; i++)
            {
                var channel = auxiliaryChannels[i];
                var definition = channel.Definition;
                if (definition == null || !definition.ShowInHud)
                {
                    continue;
                }

                if (best == null || definition.HudPriority > best.Definition.HudPriority)
                {
                    best = channel;
                }
            }

            if (best == null)
            {
                view = default;
                return false;
            }

            view = CreateView(best);
            return true;
        }

        public void GetResourceViews(List<ResourceView> output, bool includePrimary = true)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();

            if (includePrimary)
            {
                output.Add(CreateView(primaryChannel));
            }

            for (int i = 0; i < auxiliaryChannels.Count; i++)
            {
                output.Add(CreateView(auxiliaryChannels[i]));
            }
        }

        private void ConfigurePrimaryChannel()
        {
            primaryChannel.Definition = mainResourceDefinition;
            primaryChannel.IsPrimary = true;
            primaryChannel.LegacyType = mainResourceDefinition != null && mainResourceDefinition.UseLegacyType
                ? mainResourceDefinition.LegacyType
                : resourceType;
            primaryChannel.MaxStat = mainResourceDefinition != null && mainResourceDefinition.MaxResourceStat != null
                ? mainResourceDefinition.MaxResourceStat
                : maxResourceStat;
            primaryChannel.RegenStat = mainResourceDefinition != null && mainResourceDefinition.RegenStat != null
                ? mainResourceDefinition.RegenStat
                : regenStat;
            primaryChannel.BaseMax = mainResourceDefinition != null
                ? mainResourceDefinition.BaseMaxResource
                : Mathf.Max(0f, baseMaxResource);
            primaryChannel.InitializeToMax = mainResourceDefinition != null
                ? mainResourceDefinition.InitializeToMax
                : initializeToMax;
            primaryChannel.ClampToMax = mainResourceDefinition != null
                ? mainResourceDefinition.ClampToMax
                : clampToMax;
        }

        private ResourceChannel BuildChannelFromDefinition(ResourceDefinition definition, bool isPrimary)
        {
            return new ResourceChannel
            {
                Definition = definition,
                LegacyType = definition != null && definition.UseLegacyType ? definition.LegacyType : resourceType,
                MaxStat = definition != null ? definition.MaxResourceStat : null,
                RegenStat = definition != null ? definition.RegenStat : null,
                BaseMax = definition != null ? definition.BaseMaxResource : 0f,
                InitializeToMax = definition != null && definition.InitializeToMax,
                ClampToMax = definition == null || definition.ClampToMax,
                IsPrimary = isPrimary
            };
        }

        private void InitializeChannel(ResourceChannel channel, bool keepRatio, bool forceEvent)
        {
            if (channel == null)
            {
                return;
            }

            RefreshChannelMax(channel, keepRatio);

            if (channel.InitializeToMax)
            {
                channel.Current = channel.Max;
            }

            if (channel.ClampToMax)
            {
                channel.Current = Mathf.Clamp(channel.Current, 0f, channel.Max);
            }

            if (forceEvent)
            {
                RaiseResourceChanged(channel, 0f, channel.Current);
            }
        }

        private void RefreshChannelMax(ResourceChannel channel, bool keepRatio)
        {
            if (channel == null)
            {
                return;
            }

            var oldMax = channel.Max;
            var oldCurrent = channel.Current;
            channel.Max = GetMaxValue(channel);

            if (keepRatio && oldMax > 0f)
            {
                var ratio = channel.Current / oldMax;
                channel.Current = ratio * channel.Max;
            }

            if (channel.ClampToMax)
            {
                channel.Current = Mathf.Clamp(channel.Current, 0f, channel.Max);
            }

            if (!Mathf.Approximately(oldMax, channel.Max) || !Mathf.Approximately(oldCurrent, channel.Current))
            {
                RaiseResourceChanged(channel, oldCurrent, channel.Current);
            }
        }

        private float GetMaxValue(ResourceChannel channel)
        {
            if (channel.MaxStat != null && stats != null && stats.TryGetValue(channel.MaxStat, out var value))
            {
                return Mathf.Max(0f, value);
            }

            return Mathf.Max(0f, channel.BaseMax);
        }

        private void ModifyCurrent(ResourceChannel channel, float delta)
        {
            if (channel == null || Mathf.Approximately(delta, 0f))
            {
                return;
            }

            var oldValue = channel.Current;
            var next = oldValue + delta;
            channel.Current = channel.ClampToMax ? Mathf.Clamp(next, 0f, channel.Max) : Mathf.Max(0f, next);

            if (!Mathf.Approximately(oldValue, channel.Current))
            {
                RaiseResourceChanged(channel, oldValue, channel.Current);
            }
        }

        private void SetCurrent(ResourceChannel channel, float value)
        {
            if (channel == null)
            {
                return;
            }

            var oldValue = channel.Current;
            channel.Current = channel.ClampToMax
                ? Mathf.Clamp(value, 0f, channel.Max)
                : Mathf.Max(0f, value);

            if (!Mathf.Approximately(oldValue, channel.Current))
            {
                RaiseResourceChanged(channel, oldValue, channel.Current);
            }
        }

        private void UpdateChannelRegen(ResourceChannel channel)
        {
            if (channel == null || channel.RegenStat == null || stats == null || channel.Current >= channel.Max)
            {
                return;
            }

            var regen = stats.GetValue(channel.RegenStat, 0f);
            if (regen <= 0f)
            {
                return;
            }

            ModifyCurrent(channel, regen * Time.deltaTime);
        }

        private void HandleStatChanged(StatChangedEvent evt)
        {
            if (evt.Stat == null)
            {
                return;
            }

            if (evt.Stat == primaryChannel.MaxStat)
            {
                RefreshMaxResource(true);
                return;
            }

            for (int i = 0; i < auxiliaryChannels.Count; i++)
            {
                if (auxiliaryChannels[i].MaxStat == evt.Stat)
                {
                    RefreshChannelMax(auxiliaryChannels[i], true);
                }
            }
        }

        private ResourceChannel ResolveChannel(ResourceDefinition resource)
        {
            if (resource == null)
            {
                return primaryChannel;
            }

            if (ReferenceEquals(primaryChannel.Definition, resource))
            {
                return primaryChannel;
            }

            auxiliaryByDefinition.TryGetValue(resource, out var channel);
            return channel;
        }

        private ResourceChannel ResolveOrCreateChannel(ResourceDefinition resource)
        {
            if (resource == null)
            {
                return primaryChannel;
            }

            EnsureResource(resource);
            return ResolveChannel(resource);
        }

        private ResourceView CreateView(ResourceChannel channel)
        {
            return new ResourceView(
                channel.Definition,
                channel.LegacyType,
                channel.Current,
                channel.Max,
                channel.IsPrimary);
        }

        private void RaiseResourceChanged(ResourceChannel channel, float oldValue, float newValue)
        {
            var evt = new ResourceChangedEvent(this, channel.Definition, channel.LegacyType, oldValue, newValue);
            ResourceChanged?.Invoke(evt);
            eventHub?.RaiseResourceChanged(evt);
        }

        private bool ShouldDeferInitializationToUnitRoot()
        {
            return TryGetComponent<UnitRoot>(out _);
        }

        private sealed class ResourceChannel
        {
            public ResourceDefinition Definition;
            public ResourceType LegacyType;
            public StatDefinition MaxStat;
            public StatDefinition RegenStat;
            public float BaseMax;
            public bool InitializeToMax;
            public bool ClampToMax;
            public bool IsPrimary;
            public float Current;
            public float Max = 1f;
        }

        public readonly struct ResourceView
        {
            public readonly ResourceDefinition Definition;
            public readonly ResourceType ResourceType;
            public readonly float Current;
            public readonly float Max;
            public readonly bool IsPrimary;

            public ResourceView(
                ResourceDefinition definition,
                ResourceType resourceType,
                float current,
                float max,
                bool isPrimary)
            {
                Definition = definition;
                ResourceType = resourceType;
                Current = current;
                Max = max;
                IsPrimary = isPrimary;
            }

            public string DisplayName
            {
                get
                {
                    if (Definition != null && !string.IsNullOrWhiteSpace(Definition.DisplayName))
                    {
                        return Definition.DisplayName;
                    }

                    return ResourceType.ToString();
                }
            }
        }
    }
}
