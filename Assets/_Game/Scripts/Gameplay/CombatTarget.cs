using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
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

        public bool IsValid => GameObject != null;

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
            }
            else
            {
                target.Stats = target.GameObject.GetComponent<StatsComponent>();
                target.Resource = target.GameObject.GetComponent<ResourceComponent>();
                target.Tags = target.GameObject.GetComponent<UnitTagsComponent>();
                target.Team = target.GameObject.GetComponent<TeamComponent>();
            }

            return target.IsValid;
        }
    }
}
