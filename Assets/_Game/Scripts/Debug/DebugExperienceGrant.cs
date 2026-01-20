using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Debugging
{
    /// <summary>
    /// Simple debug helper to grant XP via key press.
    /// </summary>
    public class DebugExperienceGrant : MonoBehaviour
    {
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private KeyCode grantKey = KeyCode.F5;
        [SerializeField] private int grantAmount = 50;
        [SerializeField] private bool requireShift;

        private void Awake()
        {
            ResolveProgression();
        }

        private void Update()
        {
            if (grantAmount <= 0 || grantKey == KeyCode.None)
            {
                return;
            }

            if (!UnityEngine.Input.GetKeyDown(grantKey))
            {
                return;
            }

            if (requireShift && !UnityEngine.Input.GetKey(KeyCode.LeftShift) && !UnityEngine.Input.GetKey(KeyCode.RightShift))
            {
                return;
            }

            if (progression == null)
            {
                ResolveProgression();
            }

            progression?.AddExperience(grantAmount);
        }

        private void ResolveProgression()
        {
            if (progression != null)
            {
                return;
            }

            progression = GetComponent<PlayerProgression>();
            if (progression != null)
            {
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                progression = player.GetComponent<PlayerProgression>();
            }

            if (progression == null)
            {
                progression = FindFirstObjectByType<PlayerProgression>();
            }
        }
    }
}
