using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 投射物拦截体积（风墙类机制）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ProjectileInterceptorVolume : MonoBehaviour
    {
        [SerializeField] private TeamComponent team;
        [SerializeField] private bool interceptEnemyProjectilesOnly = true;

        private void Reset()
        {
            team = GetComponentInParent<TeamComponent>();
            EnsureTriggerCollider();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
        }

        public bool ShouldIntercept(UnitRoot caster)
        {
            if (!interceptEnemyProjectilesOnly)
            {
                return true;
            }

            if (team == null || caster == null || caster.Team == null)
            {
                return true;
            }

            return !team.IsSameTeam(caster.Team);
        }

        private void EnsureTriggerCollider()
        {
            var colliderRef = GetComponent<Collider>();
            if (colliderRef != null)
            {
                colliderRef.isTrigger = true;
            }
        }
    }
}
