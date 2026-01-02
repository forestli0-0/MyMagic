using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 属性定义，规定了属性的默认值、范围以及显示格式。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Stats/Stat Definition", fileName = "Stat_")]
    public class StatDefinition : DefinitionBase
    {
        [Header("属性值设定")]
        [SerializeField] private float defaultValue = 0f;
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 9999f;
        
        [Header("显示格式")]
        [Tooltip("是否为整数（在界面显示时取整）")]
        [SerializeField] private bool isInteger;
        [Tooltip("是否以百分比形式显示")]
        [SerializeField] private bool isPercentage;

        public float DefaultValue => defaultValue;
        public float MinValue => minValue;
        public float MaxValue => maxValue;
        public bool IsInteger => isInteger;
        public bool IsPercentage => isPercentage;
    }

    /// <summary>
    /// 包含具体值的属性结构，常用于初始值配置。
    /// </summary>
    [System.Serializable]
    public struct StatValue
    {
        public StatDefinition stat; // 引用属性定义
        public float value;         // 具体数值
    }
}
