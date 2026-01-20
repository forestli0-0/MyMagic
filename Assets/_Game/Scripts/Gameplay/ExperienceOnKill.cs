using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// Grants player experience when an enemy unit dies.
    /// </summary>
    public class ExperienceOnKill : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatEventHub eventHub;
        [SerializeField] private PlayerProgression playerProgression;
        [SerializeField] private TeamComponent playerTeam;

        [Header("Rules")]
        [SerializeField] private int experiencePerKill = 25;
        [SerializeField] private bool requireEnemyTeam = true;
        [SerializeField] private string playerTag = "Player";

        private void OnEnable()
        {
            ResolveReferences();
            if (eventHub != null)
            {
                eventHub.UnitKilled += HandleUnitKilled;
            }
        }

        private void OnDisable()
        {
            if (eventHub != null)
            {
                eventHub.UnitKilled -= HandleUnitKilled;
            }
        }

        private void ResolveReferences()
        {
            if (playerProgression == null)
            {
                var player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    playerProgression = player.GetComponent<PlayerProgression>();
                    playerTeam = player.GetComponent<TeamComponent>();
                }
            }

            if (playerProgression == null)
            {
                playerProgression = FindFirstObjectByType<PlayerProgression>();
            }

            if (playerTeam == null && playerProgression != null)
            {
                playerTeam = playerProgression.GetComponent<TeamComponent>();
            }

            if (eventHub == null)
            {
                var unitRoot = playerProgression != null ? playerProgression.GetComponent<UnitRoot>() : null;
                if (unitRoot == null)
                {
                    unitRoot = FindFirstObjectByType<UnitRoot>();
                }

                if (unitRoot != null)
                {
                    eventHub = unitRoot.EventHub;
                }
            }
        }

        private void HandleUnitKilled(UnitKilledEvent evt)
        {
            if (evt.Victim == null || experiencePerKill <= 0)
            {
                return;
            }

            if (playerProgression == null)
            {
                ResolveReferences();
            }

            if (playerProgression == null)
            {
                return;
            }

            if (!IsPlayerOrAlly(evt.Source.SourceUnit))
            {
                return;
            }

            if (requireEnemyTeam && !IsEnemy(evt.Victim))
            {
                return;
            }

            playerProgression.AddExperience(experiencePerKill);
        }

        private bool IsPlayerOrAlly(UnitRoot unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(playerTag) && unit.CompareTag(playerTag))
            {
                return true;
            }

            if (playerTeam == null)
            {
                return false;
            }

            var unitTeam = unit.GetComponent<TeamComponent>();
            if (unitTeam == null)
            {
                return false;
            }

            return playerTeam.IsSameTeam(unitTeam);
        }

        private bool IsEnemy(HealthComponent target)
        {
            if (target == null)
            {
                return false;
            }

            if (playerTeam == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(playerTag) && target.CompareTag(playerTag))
            {
                return false;
            }

            var targetTeam = target.GetComponent<TeamComponent>();
            if (targetTeam == null)
            {
                return false;
            }

            return !playerTeam.IsSameTeam(targetTeam);
        }
    }
}
