using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 被动配置定义。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Passives/Passive Definition", fileName = "Passive_")]
    public class PassiveDefinition : DefinitionBase
    {
        [Header("基本信息")]
        [SerializeField] private Sprite icon;
        [SerializeField] private List<TagDefinition> tags = new List<TagDefinition>();

        [Header("常驻效果")]
        [SerializeField] private List<BuffDefinition> activationBuffs = new List<BuffDefinition>();

        [Header("事件触发")]
        [SerializeField] private List<PassiveTrigger> triggers = new List<PassiveTrigger>();

        [Header("资源驱动")]
        [SerializeField] private List<PassiveMeterDriver> meterDrivers = new List<PassiveMeterDriver>();

        public Sprite Icon => icon;
        public IReadOnlyList<TagDefinition> Tags => tags;
        public IReadOnlyList<BuffDefinition> ActivationBuffs => activationBuffs;
        public IReadOnlyList<PassiveTrigger> Triggers => triggers;
        public IReadOnlyList<PassiveMeterDriver> MeterDrivers => meterDrivers;
    }

    /// <summary>
    /// 被动触发配置。
    /// </summary>
    [Serializable]
    public class PassiveTrigger
    {
        public PassiveTriggerType triggerType;
        public float chance = 1f;
        public ConditionDefinition condition;
        public List<EffectDefinition> effects = new List<EffectDefinition>();
    }

    /// <summary>
    /// 连续资源驱动配置。
    /// </summary>
    [Serializable]
    public class PassiveMeterDriver
    {
        public ResourceDefinition resource;
        public PassiveMeterDriverType driverType;
        public float rateOrAmount = 1f;
        public ConditionDefinition condition;
    }
}
