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

        public float DelaySeconds
        {
            get => delaySeconds;
            set => delaySeconds = Mathf.Max(0f, value);
        }

        public bool DestroyAfterDelay
        {
            get => destroyAfterDelay;
            set => destroyAfterDelay = value;
        }

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

        public void Configure(float delay, bool destroyAfter, HealthComponent explicitHealth = null)
        {
            delaySeconds = Mathf.Max(0f, delay);
            destroyAfterDelay = destroyAfter;

            if (explicitHealth != null)
            {
                health = explicitHealth;
                return;
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
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
