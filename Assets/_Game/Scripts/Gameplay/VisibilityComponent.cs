using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 目标可见性组件：用于潜行/草丛等机制下的可见判定。
    /// </summary>
    public class VisibilityComponent : MonoBehaviour
    {
        [SerializeField] private TeamComponent ownerTeam;
        [SerializeField] private CombatStateComponent combatState;
        [SerializeField] private bool concealedAsCamouflage = true;

        private int concealmentStacks;

        private void Reset()
        {
            ownerTeam = GetComponent<TeamComponent>();
            combatState = GetComponent<CombatStateComponent>();
        }

        private void OnEnable()
        {
            if (ownerTeam == null)
            {
                ownerTeam = GetComponent<TeamComponent>();
            }

            if (combatState == null)
            {
                combatState = GetComponent<CombatStateComponent>();
            }
        }

        public void AddConcealment(bool camouflage)
        {
            concealmentStacks++;
            concealedAsCamouflage = camouflage;
            ApplyConcealFlags();
        }

        public void RemoveConcealment()
        {
            concealmentStacks = Mathf.Max(0, concealmentStacks - 1);
            ApplyConcealFlags();
        }

        public void RevealToTeam(int teamId, float durationSeconds)
        {
            combatState?.RevealToTeam(teamId, durationSeconds);
        }

        public bool IsVisibleTo(TeamComponent observerTeam)
        {
            if (concealmentStacks <= 0)
            {
                return true;
            }

            if (observerTeam != null && ownerTeam != null && observerTeam.IsSameTeam(ownerTeam))
            {
                return true;
            }

            if (combatState == null)
            {
                return false;
            }

            if (observerTeam == null)
            {
                return false;
            }

            return combatState.IsRevealedToTeam(observerTeam.TeamId);
        }

        private void ApplyConcealFlags()
        {
            if (combatState == null)
            {
                return;
            }

            if (concealmentStacks <= 0)
            {
                combatState.RemoveFlag(CombatStateFlags.Invisible);
                combatState.RemoveFlag(CombatStateFlags.Camouflaged);
                return;
            }

            if (concealedAsCamouflage)
            {
                combatState.AddFlag(CombatStateFlags.Camouflaged);
                combatState.RemoveFlag(CombatStateFlags.Invisible);
            }
            else
            {
                combatState.AddFlag(CombatStateFlags.Invisible);
                combatState.RemoveFlag(CombatStateFlags.Camouflaged);
            }
        }
    }
}
