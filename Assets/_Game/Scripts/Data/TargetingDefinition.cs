using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 目标选择配置定义，规定了技能或效果如何寻找和筛选目标。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Targeting/Targeting Definition", fileName = "Targeting_")]
    public class TargetingDefinition : DefinitionBase
    {
        [Header("核心模式")]
        [Tooltip("查找目标的几何模式（如单体、圆形等）")]
        [SerializeField] private TargetingMode mode = TargetingMode.Single;
        [Tooltip("筛选目标的团队阵营")]
        [SerializeField] private TargetTeam team = TargetTeam.Enemy;
        [Tooltip("目标选择的原点位置")]
        [SerializeField] private TargetingOrigin origin = TargetingOrigin.Caster;
        
        [Header("范围参数")]
        [Tooltip("最大寻找距离")]
        [SerializeField] private float range = 5f;
        [Tooltip("区域检测时的半径")]
        [SerializeField] private float radius = 2f;
        [Tooltip("锥形检测时的角度")]
        [SerializeField] private float angle = 45f;
        
        [Header("筛选要求")]
        [Tooltip("单次逻辑最多选中的目标数")]
        [SerializeField] private int maxTargets = 1;
        [Tooltip("选中多个目标时的优先级排序")]
        [SerializeField] private TargetSort sort = TargetSort.Closest;
        [Tooltip("是否可以选中施法者自己")]
        [SerializeField] private bool includeSelf;
        [Tooltip("没有目标时是否仍允许释放技能")]
        [SerializeField] private bool allowEmpty;
        
        [Header("标签过滤")]
        [Tooltip("目标必须具备的标签")]
        [SerializeField] private List<TagDefinition> requiredTags = new List<TagDefinition>();
        [Tooltip("如果目标具备这些标签，则会被排除在外")]
        [SerializeField] private List<TagDefinition> blockedTags = new List<TagDefinition>();

        public TargetingMode Mode => mode;
        public TargetTeam Team => team;
        public TargetingOrigin Origin => origin;
        public float Range => range;
        public float Radius => radius;
        public float Angle => angle;
        public int MaxTargets => maxTargets;
        public TargetSort Sort => sort;
        public bool IncludeSelf => includeSelf;
        public bool AllowEmpty => allowEmpty;
        public IReadOnlyList<TagDefinition> RequiredTags => requiredTags;
        public IReadOnlyList<TagDefinition> BlockedTags => blockedTags;
    }
}
