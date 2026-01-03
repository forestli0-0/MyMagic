using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 效果配置定义，是战斗逻辑执行的最小单元（如：造成 X 点伤害，位移 Y 距离）。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Effects/Effect Definition", fileName = "Effect_")]
    public class EffectDefinition : DefinitionBase
    {
        [Header("通用设定")]
        [Tooltip("效果的分类类型")]
        [SerializeField] private EffectType effectType;
        [Tooltip("执行此效果的前提条件（可选）")]
        [SerializeField] private ConditionDefinition condition;
        [Tooltip("如果设置，将覆盖技能的默认目标选择逻辑")]
        [SerializeField] private TargetingDefinition overrideTargeting;

        [Header("数值/伤害相关")]
        [Tooltip("通用数值参数（伤害值、治疗值等）")]
        [SerializeField] private float value;
        [Tooltip("资源类型（资源效果时使用）")]
        [SerializeField] private ResourceType resourceType = ResourceType.Mana;
        [Tooltip("伤害属性分类")]
        [SerializeField] private DamageType damageType;

        [Header("位移相关")]
        [SerializeField] private MoveStyle moveStyle;
        [SerializeField] private float moveDistance;
        [SerializeField] private float moveSpeed;

        [Header("对象引用")]
        [Tooltip("要应用的 Buff 定义")]
        [SerializeField] private BuffDefinition buff;
        [Tooltip("发射的投射物定义")]
        [SerializeField] private ProjectileDefinition projectile;
        [Tooltip("触发的另一个技能定义")]
        [SerializeField] private SkillDefinition triggeredSkill;
        
        [Header("持续与频率")]
        [Tooltip("对于持续型效果的总持续时间")]
        [SerializeField] private float duration;
        [Tooltip("对于持续型效果的 Tick 间隔")]
        [SerializeField] private float interval;

        [Header("召唤相关")]
        [SerializeField] private GameObject summonPrefab;
        [SerializeField] private UnitDefinition summonUnit;

        public EffectType EffectType => effectType;
        public DamageType DamageType => damageType;
        public float Value => value;
        public ResourceType ResourceType => resourceType;
        public float Duration => duration;
        public float Interval => interval;
        public MoveStyle MoveStyle => moveStyle;
        public float MoveDistance => moveDistance;
        public float MoveSpeed => moveSpeed;
        public BuffDefinition Buff => buff;
        public ProjectileDefinition Projectile => projectile;
        public SkillDefinition TriggeredSkill => triggeredSkill;
        public ConditionDefinition Condition => condition;
        public TargetingDefinition OverrideTargeting => overrideTargeting;
        public GameObject SummonPrefab => summonPrefab;
        public UnitDefinition SummonUnit => summonUnit;
    }
}
