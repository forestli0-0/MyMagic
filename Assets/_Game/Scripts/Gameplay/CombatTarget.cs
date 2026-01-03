using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 战斗目标数据结构，封装了目标实体的常用组件引用。
    /// </summary>
    /// <remarks>
    /// 设计说明：使用 struct 避免堆分配，适合在热路径中频繁创建。
    /// 
    /// 性能优化建议：
    /// - TryCreate 方法内部调用多次 GetComponent，在大量目标场景可能成为瓶颈
    /// - 后续可考虑在 UnitRoot 中缓存组件引用，通过属性直接访问
    /// - 或实现 CombatTarget 缓存池减少重复创建
    /// </remarks>
    public struct CombatTarget
    {
        public GameObject GameObject;
        public Transform Transform;
        public UnitRoot Unit;
        public StatsComponent Stats;
        public HealthComponent Health;
        public ResourceComponent Resource;
        public UnitTagsComponent Tags;
        public TeamComponent Team;
        /// <summary>
        /// Buff 控制器引用，用于条件判断等场景。
        /// </summary>
        public BuffController Buffs;

        /// <summary>
        /// 检查目标是否有效（GameObject 未被销毁）。
        /// </summary>
        public bool IsValid => GameObject != null;

        /// <summary>
        /// 从 GameObject 创建 CombatTarget 实例。
        /// </summary>
        /// <param name="source">源 GameObject</param>
        /// <param name="target">输出的 CombatTarget</param>
        /// <returns>若成功创建则返回 true</returns>
        /// <remarks>
        /// [性能] 此方法内部调用 5-6 次 GetComponent，在目标筛选热路径中
        /// 可能产生累积开销。优化方向：在 UnitRoot 中预缓存组件引用。
        /// </remarks>
        public static bool TryCreate(GameObject source, out CombatTarget target)
        {
            target = default;
            if (source == null)
            {
                return false;
            }

            var root = source.GetComponentInParent<UnitRoot>();
            if (root != null)
            {
                target.Unit = root;
                target.GameObject = root.gameObject;
                target.Transform = root.transform;
            }
            else
            {
                var health = source.GetComponentInParent<HealthComponent>();
                if (health == null)
                {
                    return false;
                }

                target.GameObject = health.gameObject;
                target.Transform = health.transform;
                target.Health = health;
                target.Unit = health.GetComponent<UnitRoot>();
            }

            if (target.Unit != null)
            {
                target.Stats = target.Unit.GetComponent<StatsComponent>();
                target.Health = target.Unit.GetComponent<HealthComponent>();
                target.Resource = target.Unit.GetComponent<ResourceComponent>();
                target.Tags = target.Unit.GetComponent<UnitTagsComponent>();
                target.Team = target.Unit.GetComponent<TeamComponent>();
                target.Buffs = target.Unit.GetComponent<BuffController>();
            }
            else
            {
                target.Stats = target.GameObject.GetComponent<StatsComponent>();
                target.Resource = target.GameObject.GetComponent<ResourceComponent>();
                target.Tags = target.GameObject.GetComponent<UnitTagsComponent>();
                target.Team = target.GameObject.GetComponent<TeamComponent>();
                target.Buffs = target.GameObject.GetComponent<BuffController>();
            }

            return target.IsValid;
        }
    }
}
