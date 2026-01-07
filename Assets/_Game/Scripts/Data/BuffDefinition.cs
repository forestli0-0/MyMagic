using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// Buff 配置定义，包含持续时间、堆叠规则、属性修正和触发效果。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Buffs/Buff Definition", fileName = "Buff_")]
    public class BuffDefinition : DefinitionBase
    {
        [Header("基本表现")]
        [SerializeField] private Sprite icon;
        [Tooltip("是否为负面效果")]
        [SerializeField] private bool isDebuff;
        
        [Header("时间与堆叠")]
        [Tooltip("基础持续时间")]
        [SerializeField] private float duration = 1f;
        [Tooltip("周期性触发（Tick）的间隔时间")]
        [SerializeField] private float tickInterval;
        [Tooltip("当再次获得此 Buff 时的处理规则")]
        [SerializeField] private BuffStackingRule stackingRule = BuffStackingRule.Refresh;
        [Tooltip("最大堆叠层数")]
        [SerializeField] private int maxStacks = 1;
        
        [Header("数据与效果")]
        [Tooltip("Buff 携带的标签")]
        [SerializeField] private List<TagDefinition> tags = new List<TagDefinition>();
        [Tooltip("该 Buff 提供的属性或参数修正")]
        [SerializeField] private List<ModifierDefinition> modifiers = new List<ModifierDefinition>();
        [Tooltip("Buff 在特定时机触发的效果")]
        [SerializeField] private List<BuffTrigger> triggers = new List<BuffTrigger>();

        [Header("控制状态")]
        [Tooltip("该 Buff 施加的控制效果")]
        [SerializeField] private List<ControlType> controlEffects = new List<ControlType>();
        [Tooltip("该 Buff 提供的控制免疫")]
        [SerializeField] private List<ControlType> controlImmunities = new List<ControlType>();

        public Sprite Icon => icon;
        public bool IsDebuff => isDebuff;
        public float Duration => duration;
        public float TickInterval => tickInterval;
        public BuffStackingRule StackingRule => stackingRule;
        public int MaxStacks => maxStacks;
        public IReadOnlyList<TagDefinition> Tags => tags;
        public IReadOnlyList<ModifierDefinition> Modifiers => modifiers;
        public IReadOnlyList<BuffTrigger> Triggers => triggers;
        public IReadOnlyList<ControlType> ControlEffects => controlEffects;
        public IReadOnlyList<ControlType> ControlImmunities => controlImmunities;
    }

    /// <summary>
    /// Buff 内置的触发配置。
    /// </summary>
    [Serializable]
    public class BuffTrigger
    {
        [Tooltip("触发时机")]
        public BuffTriggerType triggerType;
        [Tooltip("触发概率")]
        public float chance = 1f;
        [Tooltip("触发逻辑前提条件")]
        public ConditionDefinition condition;
        [Tooltip("触发后产生的一系列效果")]
        public List<EffectDefinition> effects = new List<EffectDefinition>();
    }
}
