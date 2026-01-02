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
        [Tooltip("是否为追踪弹")]
        [SerializeField] private bool homing;
        [Tooltip("追踪弹的转向速度")]
        [SerializeField] private float homingTurnSpeed = 360f;
        [Tooltip("是否支持穿透")]
        [SerializeField] private bool pierce;
        [Tooltip("最大穿透目标数")]
        [SerializeField] private int maxPierce = 1;

        [Header("命中效果")]
        [Tooltip("当投射物命中目标时触发的效果列表")]
        [SerializeField] private List<EffectDefinition> onHitEffects = new List<EffectDefinition>();

        public GameObject Prefab => prefab;
        public float Speed => speed;
        public float Lifetime => lifetime;
        public bool Homing => homing;
        public float HomingTurnSpeed => homingTurnSpeed;
        public bool Pierce => pierce;
        public int MaxPierce => maxPierce;
        public float HitRadius => hitRadius;
        public IReadOnlyList<EffectDefinition> OnHitEffects => onHitEffects;
    }
}

