using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 单位配置定义，包含预制体引用、基础属性、初始技能和 AI 逻辑。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Units/Unit Definition", fileName = "Unit_")]
    public class UnitDefinition : DefinitionBase
    {
        [Header("视觉表现")]
        [Tooltip("单位的 3D 预制体")]
        [SerializeField] private GameObject prefab;
        [Tooltip("界面显示的头像")]
        [SerializeField] private Sprite portrait;
        [Tooltip("单位表现配置（模型挂点、Animator 参数映射）")]
        [SerializeField] private UnitVisualProfile visualProfile;
        
        [Header("数据与分类")]
        [Tooltip("单位自带的标签（如：英雄、不死族等）")]
        [SerializeField] private List<TagDefinition> tags = new List<TagDefinition>();
        [Tooltip("单位的初始基础属性")]
        [SerializeField] private List<StatValue> baseStats = new List<StatValue>();
        
        [Header("技能配置")]
        [Tooltip("普攻技能配置")]
        [SerializeField] private SkillDefinition basicAttack;
        [Tooltip("单位初始拥有的技能列表")]
        [SerializeField] private List<SkillDefinition> startingSkills = new List<SkillDefinition>();
        
        [Header("AI 配置")]
        [Tooltip("该单位使用的 AI 决策逻辑")]
        [SerializeField] private AIProfile aiProfile;

        public GameObject Prefab => prefab;
        public Sprite Portrait => portrait;
        public UnitVisualProfile VisualProfile => visualProfile;
        public IReadOnlyList<TagDefinition> Tags => tags;
        public IReadOnlyList<StatValue> BaseStats => baseStats;
        public SkillDefinition BasicAttack => basicAttack;
        public IReadOnlyList<SkillDefinition> StartingSkills => startingSkills;
        public AIProfile AIProfile => aiProfile;
    }
}
