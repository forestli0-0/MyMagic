using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 修正器配置定义，用于对属性、技能或效果参数进行数值上的偏移或乘算。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Modifiers/Modifier Definition", fileName = "Modifier_")]
    public class ModifierDefinition : DefinitionBase
    {
        [Header("目标设定")]
        [Tooltip("要修正的对象类型")]
        [SerializeField] private ModifierTargetType target = ModifierTargetType.Stat;
        [Tooltip("如果是修正属性，请指定具体属性")]
        [SerializeField] private StatDefinition stat;
        [Tooltip("如果是修正参数，请指定参数的唯一 ID")]
        [SerializeField] private string parameterId;
        
        [Header("运算逻辑")]
        [SerializeField] private ModifierOperation operation = ModifierOperation.Add;
        [Tooltip("修正的具体数值")]
        [SerializeField] private float value;
        
        [Header("限制条件")]
        [Tooltip("产生此修正的前提条件")]
        [SerializeField] private ConditionDefinition condition;
        [Tooltip("要求主体必须拥有的标签")]
        [SerializeField] private List<TagDefinition> requiredTags = new List<TagDefinition>();
        [Tooltip("逻辑上阻断此修正的标签")]
        [SerializeField] private List<TagDefinition> blockedTags = new List<TagDefinition>();

        public ModifierTargetType Target => target;
        public StatDefinition Stat => stat;
        public string ParameterId => parameterId;
        public ModifierOperation Operation => operation;
        public float Value => value;
        public ConditionDefinition Condition => condition;
        public IReadOnlyList<TagDefinition> RequiredTags => requiredTags;
        public IReadOnlyList<TagDefinition> BlockedTags => blockedTags;
    }
}

