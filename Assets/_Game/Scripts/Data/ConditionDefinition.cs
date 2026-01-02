using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 条件配置定义，用于判断特定逻辑是否应该执行。支持多条件的 AND/OR 组合。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Conditions/Condition Definition", fileName = "Condition_")]
    public class ConditionDefinition : DefinitionBase
    {
        [Tooltip("多个条件条目之间的逻辑运算方式")]
        [SerializeField] private ConditionOperator op = ConditionOperator.All;
        [Tooltip("具体的条件条目列表")]
        [SerializeField] private List<ConditionEntry> entries = new List<ConditionEntry>();

        public ConditionOperator Operator => op;
        public IReadOnlyList<ConditionEntry> Entries => entries;
    }

    /// <summary>
    /// 单条条件判断条目。
    /// </summary>
    [Serializable]
    public class ConditionEntry
    {
        [Tooltip("要判断的条件类型")]
        public ConditionType type = ConditionType.Always;
        [Tooltip("判断主体是谁（施法者还是目标）")]
        public ConditionSubject subject = ConditionSubject.Caster;
        
        [Header("参数配置")]
        [Tooltip("概率条件时的成功几率 (0-1)")]
        public float chance = 1f;
        [Tooltip("标签相关条件时的指定标签")]
        public TagDefinition tag;
        [Tooltip("Buff 相关条件时的指定 Buff")]
        public BuffDefinition buff;
        [Tooltip("数值比较（如血量阈值）时的参考值")]
        public float threshold;
    }
}
