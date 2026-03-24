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
        [SerializeField] private ResourceDefinition resourceDefinition;
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

        [Header("蓄力参数")]
        [Tooltip("是否启用蓄力（按住技能键提升强度，松开释放）")]
        [SerializeField] private bool enableCharge;
        [Tooltip("蓄力达到满值所需时长（秒）")]
        [SerializeField] private float maxChargeTime = 1.2f;
        [Tooltip("零蓄力时的效果倍率")]
        [SerializeField] private float minChargeMultiplier = 1f;
        [Tooltip("满蓄力时的效果倍率")]
        [SerializeField] private float maxChargeMultiplier = 1.8f;

        // 施法节奏与输入缓冲配置
        [Header("施法节奏")]
        [Tooltip("施法后摇时间（技能释放完毕后的恢复时间）")]
        [SerializeField] private float postCastTime;
        [Tooltip("公共冷却时间（GCD，释放后短暂锁定其他技能）")]
        [SerializeField] private float gcdDuration;
        [Tooltip("引导技能的跳动间隔（0 表示不跳动）")]
        [SerializeField] private float channelTickInterval;
        [Tooltip("输入队列窗口（施法结束前可提前输入下一个技能的时间）")]
        [SerializeField] private float queueWindow;
        [Tooltip("队列输入处理策略（多个输入时如何处理）")]
        [SerializeField] private SkillQueuePolicy queuePolicy = SkillQueuePolicy.Replace;
        [Tooltip("目标快照策略（决定何时锁定目标）")]
        [SerializeField] private TargetSnapshotPolicy targetSnapshotPolicy = TargetSnapshotPolicy.AtCastStart;

        [Header("弹药与重施")]
        [Tooltip("弹药配置（启用后技能使用弹药并自动回复）")]
        [SerializeField] private SkillAmmoConfig ammoConfig = new SkillAmmoConfig();
        [Tooltip("重施配置（启用后技能可在窗口内再次释放）")]
        [SerializeField] private SkillRecastConfig recastConfig = new SkillRecastConfig();

        [Header("连段序列")]
        [Tooltip("连段配置（启用后可实现 Q1/Q2/Q3 类自动进阶与超时重置）")]
        [SerializeField] private SkillSequenceConfig sequenceConfig = new SkillSequenceConfig();

        [Header("连段表现")]
        [Tooltip("按连段阶段覆盖技能图标（Phase=1 时默认使用基础 icon）")]
        [SerializeField] private List<SkillSequencePhaseVisual> sequencePhaseVisuals = new List<SkillSequencePhaseVisual>();
        
        [Header("逻辑与筛选")]
        [Tooltip("该技能寻找目标的方式")]
        [SerializeField] private TargetingDefinition targeting;
        [Tooltip("技能所属标签")]
        [SerializeField] private List<TagDefinition> tags = new List<TagDefinition>();
        [Tooltip("施法前置约束（未满足时直接阻止施法，不进入步骤执行）")]
        [SerializeField] private List<SkillCastConstraint> castConstraints = new List<SkillCastConstraint>();
        
        [Header("执行流程")]
        [Tooltip("技能触发后的具体执行步骤列表")]
        [SerializeField] private List<SkillStep> steps = new List<SkillStep>();

        public Sprite Icon => icon;
        public ResourceDefinition ResourceDefinition => resourceDefinition;
        public ResourceType ResourceType => resourceType;
        public float ResourceCost => resourceCost;
        public float Cooldown => cooldown;
        public float CastTime => castTime;
        public float ChannelTime => channelTime;
        public bool CanMoveWhileCasting => canMoveWhileCasting;
        public bool CanRotateWhileCasting => canRotateWhileCasting;
        public bool EnableCharge => enableCharge;
        public float MaxChargeTime => maxChargeTime;
        public float MinChargeMultiplier => minChargeMultiplier;
        public float MaxChargeMultiplier => maxChargeMultiplier;
        public bool SupportsCharge => enableCharge && maxChargeTime > 0.01f;
        public float PostCastTime => postCastTime;
        public float GcdDuration => gcdDuration;
        public float ChannelTickInterval => channelTickInterval;
        public float QueueWindow => queueWindow;
        public SkillQueuePolicy QueuePolicy => queuePolicy;
        public TargetSnapshotPolicy TargetSnapshotPolicy => targetSnapshotPolicy;
        public SkillAmmoConfig AmmoConfig => ammoConfig;
        public SkillRecastConfig RecastConfig => recastConfig;
        public SkillSequenceConfig SequenceConfig => sequenceConfig;
        public IReadOnlyList<SkillSequencePhaseVisual> SequencePhaseVisuals => sequencePhaseVisuals;
        public bool SupportsAmmo => ammoConfig != null && ammoConfig.Enabled && ammoConfig.MaxCharges > 0;
        public bool SupportsRecast => recastConfig != null && recastConfig.Enabled && recastConfig.MaxRecasts > 0 && recastConfig.RecastWindow > 0f;
        public bool SupportsSequence => sequenceConfig != null
                                        && sequenceConfig.Enabled
                                        && sequenceConfig.MaxPhases > 1
                                        && sequenceConfig.ResetWindow > 0f;
        public TargetingDefinition Targeting => targeting;
        public IReadOnlyList<TagDefinition> Tags => tags;
        public IReadOnlyList<SkillCastConstraint> CastConstraints => castConstraints;
        public IReadOnlyList<SkillStep> Steps => steps;

        /// <summary>
        /// 根据蓄力时长计算归一化蓄力比例（0~1）。
        /// </summary>
        public float ResolveChargeRatio(float chargeDurationSeconds)
        {
            if (!SupportsCharge)
            {
                return 0f;
            }

            var safeMax = Mathf.Max(0.01f, maxChargeTime);
            return Mathf.Clamp01(Mathf.Max(0f, chargeDurationSeconds) / safeMax);
        }

        /// <summary>
        /// 根据蓄力时长计算效果倍率。
        /// </summary>
        public float ResolveChargeMultiplier(float chargeDurationSeconds)
        {
            if (!SupportsCharge)
            {
                return 1f;
            }

            var ratio = ResolveChargeRatio(chargeDurationSeconds);
            var minMul = Mathf.Max(0f, minChargeMultiplier);
            var maxMul = Mathf.Max(minMul, maxChargeMultiplier);
            return Mathf.Lerp(minMul, maxMul, ratio);
        }

        /// <summary>
        /// 根据连段阶段返回展示图标。
        /// Phase=1 或未配置覆盖时返回基础图标。
        /// </summary>
        public Sprite ResolveIconForPhase(int phase)
        {
            var safePhase = Mathf.Max(1, phase);
            if (safePhase <= 1 || sequencePhaseVisuals == null || sequencePhaseVisuals.Count == 0)
            {
                return icon;
            }

            for (int i = 0; i < sequencePhaseVisuals.Count; i++)
            {
                var visual = sequencePhaseVisuals[i];
                if (visual == null || visual.Icon == null)
                {
                    continue;
                }

                if (visual.Phase == safePhase)
                {
                    return visual.Icon;
                }
            }

            return icon;
        }
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
        [Tooltip("若启用，则该步骤只执行一次，由效果自己的 OverrideTargeting 决定最终命中目标。")]
        public bool executeOnce;
        
        [Header("表现 Cue（新流程）")]
        [Tooltip("步骤级表现配置，供 SkillPresentationSystem 通过事件总线消费")]
        public List<SkillPresentationCue> presentationCues = new List<SkillPresentationCue>();
        
        [Header("战斗效果")]
        public List<EffectDefinition> effects = new List<EffectDefinition>(); // 该步骤产生的所有直接效果
    }

    /// <summary>
    /// 技能施法前置约束。用于在 CanCast 阶段提前阻断不满足条件的施法请求。
    /// </summary>
    [Serializable]
    public class SkillCastConstraint
    {
        [Tooltip("约束条件，返回 false 时阻止施法")]
        public ConditionDefinition condition;
        [Tooltip("约束失败时返回的错误码（供 UI/日志使用）")]
        public SkillCastFailReason failReason = SkillCastFailReason.CastConstraintFailed;
    }

    /// <summary>
    /// 技能弹药配置。
    /// </summary>
    [Serializable]
    public class SkillAmmoConfig
    {
        [SerializeField] private bool enabled;
        [SerializeField] private int maxCharges = 3;
        [SerializeField] private int initialCharges = 3;
        [SerializeField] private float rechargeTime = 4f;

        public bool Enabled => enabled;
        public int MaxCharges => Mathf.Max(0, maxCharges);
        public int InitialCharges => Mathf.Clamp(initialCharges, 0, MaxCharges);
        public float RechargeTime => Mathf.Max(0f, rechargeTime);
    }

    /// <summary>
    /// 技能重施配置。
    /// </summary>
    [Serializable]
    public class SkillRecastConfig
    {
        [SerializeField] private bool enabled;
        [SerializeField] private int maxRecasts = 1;
        [SerializeField] private float recastWindow = 3f;
        [SerializeField] private bool consumesResourceOnRecast = true;
        [SerializeField] private bool delayCooldownUntilRecastEnds = true;
        [SerializeField] private RecastTargetPolicy targetPolicy = RecastTargetPolicy.AnyValid;

        public bool Enabled => enabled;
        public int MaxRecasts => Mathf.Max(0, maxRecasts);
        public float RecastWindow => Mathf.Max(0f, recastWindow);
        public bool ConsumesResourceOnRecast => consumesResourceOnRecast;
        public bool DelayCooldownUntilRecastEnds => delayCooldownUntilRecastEnds;
        public RecastTargetPolicy TargetPolicy => targetPolicy;
    }

    /// <summary>
    /// 技能连段配置（如 Q1/Q2/Q3）。
    /// </summary>
    [Serializable]
    public class SkillSequenceConfig
    {
        [SerializeField] private bool enabled;
        [SerializeField] private int maxPhases = 3;
        [SerializeField] private float resetWindow = 4f;
        [SerializeField] private SkillSequenceOverflowPolicy overflowPolicy = SkillSequenceOverflowPolicy.LoopToStart;
        [SerializeField] private bool resetOnOtherSkillCast;
        [SerializeField] private bool advanceOnHit;

        public bool Enabled => enabled;
        public int MaxPhases => Mathf.Max(1, maxPhases);
        public float ResetWindow => Mathf.Max(0f, resetWindow);
        public SkillSequenceOverflowPolicy OverflowPolicy => overflowPolicy;
        public bool ResetOnOtherSkillCast => resetOnOtherSkillCast;
        public bool AdvanceOnHit => advanceOnHit;
    }

    /// <summary>
    /// 技能连段阶段表现配置。
    /// </summary>
    [Serializable]
    public class SkillSequencePhaseVisual
    {
        [SerializeField] private int phase = 2;
        [SerializeField] private Sprite icon;

        public int Phase => Mathf.Max(1, phase);
        public Sprite Icon => icon;
    }
}
