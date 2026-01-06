using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 技能配置定义，包含消耗、冷却、目标选择以及执行步骤。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Skills/Skill Definition", fileName = "Skill_")]
    public class SkillDefinition : DefinitionBase
    {
        [Header("基本信息")]
        [SerializeField] private Sprite icon;
        
        [Header("消耗与冷却")]
        [SerializeField] private ResourceType resourceType = ResourceType.Mana;
        [SerializeField] private float resourceCost;
        [SerializeField] private float cooldown;
        
        [Header("施法参数")]
        [Tooltip("施法前摇时间")]
        [SerializeField] private float castTime;
        [Tooltip("持续引导时间（如果为 0 则不是引导技能）")]
        [SerializeField] private float channelTime;
        [SerializeField] private bool canMoveWhileCasting;
        [SerializeField] private bool canRotateWhileCasting = true;

        // 施法节奏与输入缓冲配置
        [Header("Cast Flow")]
        [Tooltip("Post-cast recovery time")]
        [SerializeField] private float postCastTime;
        [Tooltip("Global cooldown duration")]
        [SerializeField] private float gcdDuration;
        [Tooltip("Channel tick interval (0 = no ticks)")]
        [SerializeField] private float channelTickInterval;
        [Tooltip("Input queue window before cast ends")]
        [SerializeField] private float queueWindow;
        [Tooltip("How to handle multiple queued inputs")]
        [SerializeField] private SkillQueuePolicy queuePolicy = SkillQueuePolicy.Replace;
        [Tooltip("Target snapshot behavior for this skill")]
        [SerializeField] private TargetSnapshotPolicy targetSnapshotPolicy = TargetSnapshotPolicy.AtCastStart;
        
        [Header("逻辑与筛选")]
        [Tooltip("该技能寻找目标的方式")]
        [SerializeField] private TargetingDefinition targeting;
        [Tooltip("技能所属标签")]
        [SerializeField] private List<TagDefinition> tags = new List<TagDefinition>();
        
        [Header("执行流程")]
        [Tooltip("技能触发后的具体执行步骤列表")]
        [SerializeField] private List<SkillStep> steps = new List<SkillStep>();

        public Sprite Icon => icon;
        public ResourceType ResourceType => resourceType;
        public float ResourceCost => resourceCost;
        public float Cooldown => cooldown;
        public float CastTime => castTime;
        public float ChannelTime => channelTime;
        public bool CanMoveWhileCasting => canMoveWhileCasting;
        public bool CanRotateWhileCasting => canRotateWhileCasting;
        public float PostCastTime => postCastTime;
        public float GcdDuration => gcdDuration;
        public float ChannelTickInterval => channelTickInterval;
        public float QueueWindow => queueWindow;
        public SkillQueuePolicy QueuePolicy => queuePolicy;
        public TargetSnapshotPolicy TargetSnapshotPolicy => targetSnapshotPolicy;
        public TargetingDefinition Targeting => targeting;
        public IReadOnlyList<TagDefinition> Tags => tags;
        public IReadOnlyList<SkillStep> Steps => steps;
    }

    /// <summary>
    /// 技能执行流程中的一个步骤。
    /// </summary>
    [Serializable]
    public class SkillStep
    {
        [Tooltip("何时触发此步骤")]
        public SkillStepTrigger trigger;
        [Tooltip("从触发时机点开始的延迟时间")]
        public float delay;
        [Tooltip("执行此步骤的前提条件")]
        public ConditionDefinition condition;
        
        [Header("表现效果")]
        public string animationTrigger; // 动画触发参数
        public GameObject vfxPrefab;    // 特效预制体
        public AudioClip sfx;           // 音效
        
        [Header("战斗效果")]
        public List<EffectDefinition> effects = new List<EffectDefinition>(); // 该步骤产生的所有直接效果
    }
}
