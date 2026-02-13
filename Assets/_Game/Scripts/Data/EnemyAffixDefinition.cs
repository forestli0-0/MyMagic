using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 敌人词缀定义，用于构建精英单位的数值与表现强化。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Encounters/Enemy Affix", fileName = "EnemyAffix_")]
    public class EnemyAffixDefinition : DefinitionBase
    {
        [Header("数值强化")]
        [SerializeField] private List<EnemyAffixStatModifier> statModifiers = new List<EnemyAffixStatModifier>();
        [SerializeField] private List<SkillDefinition> bonusSkills = new List<SkillDefinition>();

        [Header("视觉强化")]
        [SerializeField] private Color tintColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private float scaleMultiplier = 1.1f;

        public IReadOnlyList<EnemyAffixStatModifier> StatModifiers => statModifiers;
        public IReadOnlyList<SkillDefinition> BonusSkills => bonusSkills;
        public Color TintColor => tintColor;
        public float ScaleMultiplier => Mathf.Max(0.1f, scaleMultiplier);
    }

    [System.Serializable]
    public class EnemyAffixStatModifier
    {
        [SerializeField] private StatDefinition stat;
        [SerializeField] private float flatBonus;
        [SerializeField] private float multiplier = 1f;

        public StatDefinition Stat => stat;
        public float FlatBonus => flatBonus;
        public float Multiplier => multiplier;
    }
}
