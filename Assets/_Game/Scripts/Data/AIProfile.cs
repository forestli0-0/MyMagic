using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// AI 配置文件定义，决定了单位在战斗中的感知范围、思考频率和技能释放策略。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/AI/AI Profile", fileName = "AIProfile_")]
    public class AIProfile : DefinitionBase
    {
        [Header("感知与触发")]
        [Tooltip("发现敌人的距离")]
        [SerializeField] private float aggroRange = 10f;
        [Tooltip("进行普通攻击的理想距离")]
        [SerializeField] private float attackRange = 2f;
        [Tooltip("AI 思考决策的间隔时间（秒）")]
        [SerializeField] private float thinkInterval = 0.2f;
        
        [Header("技能决策")]
        [Tooltip("技能使用规则列表")]
        [SerializeField] private List<AISkillRule> skillRules = new List<AISkillRule>();

        public float AggroRange => aggroRange;
        public float AttackRange => attackRange;
        public float ThinkInterval => thinkInterval;
        public IReadOnlyList<AISkillRule> SkillRules => skillRules;
    }

    /// <summary>
    /// AI 使用特定技能的规则。
    /// </summary>
    [Serializable]
    public class AISkillRule
    {
        [Tooltip("要释放的技能")]
        public SkillDefinition skill;
        [Tooltip("释放技能要求的最小距离")]
        public float minRange;
        [Tooltip("释放技能要求的最大距离")]
        public float maxRange = 10f;
        [Tooltip("在多个技能可选时的随机权重")]
        public float weight = 1f;
        [Tooltip("是否允许在移动中释放（如果技能本身支持）")]
        public bool allowWhileMoving;
        [Tooltip("释放该技能前必须满足的额外逻辑条件")]
        public ConditionDefinition condition;
    }
}
