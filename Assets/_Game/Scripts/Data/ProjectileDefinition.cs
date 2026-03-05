using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 投射物配置定义，包含移动速度、寿命、穿透规则和碰撞效果。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Projectiles/Projectile Definition", fileName = "Projectile_")]
    public class ProjectileDefinition : DefinitionBase
    {
        [Header("表现与物理")]
        [Tooltip("投射物的视觉预制体")]
        [SerializeField] private GameObject prefab;
        [Tooltip("飞行速度")]
        [SerializeField] private float speed = 10f;
        [Tooltip("最大飞行时间，超时自动销毁")]
        [SerializeField] private float lifetime = 5f;
        [Tooltip("碰撞检测的半径")]
        [SerializeField] private float hitRadius = 0.1f;

        [Header("逻辑控制")]
        [Tooltip("投射物行为类型")]
        [SerializeField] private ProjectileBehaviorType behaviorType = ProjectileBehaviorType.Straight;
        [Tooltip("是否为追踪弹")]
        [SerializeField] private bool homing;
        [Tooltip("追踪弹的转向速度")]
        [SerializeField] private float homingTurnSpeed = 360f;
        [Tooltip("是否支持穿透")]
        [SerializeField] private bool pierce;
        [Tooltip("最大穿透目标数")]
        [SerializeField] private int maxPierce = 1;
        [Tooltip("命中第一个有效目标后是否立即结束投射物")]
        [SerializeField] private bool forceStopOnFirstHit;
        [Tooltip("回返时速度倍率")]
        [SerializeField] private float returnSpeedMultiplier = 1f;
        [Tooltip("命中后分裂数量（0=不分裂）")]
        [SerializeField] private int splitCount;
        [Tooltip("分裂扇形角度")]
        [SerializeField] private float splitAngle = 35f;
        [Tooltip("分裂最大层级（防止无限分裂）")]
        [SerializeField] private int maxSplitDepth = 1;
        [Tooltip("环绕半径（Orbit）")]
        [SerializeField] private float orbitRadius = 2.5f;
        [Tooltip("环绕角速度（度/秒）")]
        [SerializeField] private float orbitAngularSpeed = 240f;
        [Tooltip("光束长度（BeamLike）")]
        [SerializeField] private float beamLength = 6f;

        [Header("命中效果")]
        [Tooltip("当投射物命中目标时触发的效果列表")]
        [SerializeField] private List<EffectDefinition> onHitEffects = new List<EffectDefinition>();

        public GameObject Prefab => prefab;
        public float Speed => speed;
        public float Lifetime => lifetime;
        public ProjectileBehaviorType BehaviorType => behaviorType;
        public bool Homing => homing;
        public float HomingTurnSpeed => homingTurnSpeed;
        public bool Pierce => pierce;
        public int MaxPierce => maxPierce;
        public bool ForceStopOnFirstHit => forceStopOnFirstHit;
        public float ReturnSpeedMultiplier => Mathf.Max(0f, returnSpeedMultiplier);
        public int SplitCount => Mathf.Max(0, splitCount);
        public float SplitAngle => Mathf.Max(0f, splitAngle);
        public int MaxSplitDepth => Mathf.Max(0, maxSplitDepth);
        public float OrbitRadius => Mathf.Max(0f, orbitRadius);
        public float OrbitAngularSpeed => orbitAngularSpeed;
        public float BeamLength => Mathf.Max(0f, beamLength);
        public float HitRadius => hitRadius;
        public IReadOnlyList<EffectDefinition> OnHitEffects => onHitEffects;
    }
}
