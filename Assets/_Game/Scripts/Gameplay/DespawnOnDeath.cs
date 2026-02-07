using System.Collections;
using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    public class DespawnOnDeath : MonoBehaviour
    {
        [SerializeField] private HealthComponent health;
        [SerializeField] private float delaySeconds;
        [SerializeField] private bool destroyAfterDelay;

        private void Reset()
        {
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        private void HandleDied(HealthComponent source)
        {
            if (delaySeconds > 0f)
            {
                StartCoroutine(DespawnAfterDelay());
                return;
            }

            DespawnNow();
        }

        private IEnumerator DespawnAfterDelay()
        {
            yield return new WaitForSeconds(delaySeconds);
            DespawnNow();
        }

        private void DespawnNow()
        {
            if (destroyAfterDelay)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
