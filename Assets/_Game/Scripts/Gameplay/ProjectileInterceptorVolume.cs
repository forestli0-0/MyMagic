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

            if (team == null)
            {
                team = GetComponentInParent<TeamComponent>();
            }

            var casterTeam = caster != null ? caster.Team : null;
            if (casterTeam == null && caster != null)
            {
                casterTeam = caster.GetComponent<TeamComponent>();
            }

            if (team == null || caster == null || casterTeam == null)
            {
                return true;
            }

            return !team.IsSameTeam(casterTeam);
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
