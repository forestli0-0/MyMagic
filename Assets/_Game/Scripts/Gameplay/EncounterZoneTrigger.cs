using System;
using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 进入触发区后激活一次遭遇（避免场景加载即刷怪）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class EncounterZoneTrigger : MonoBehaviour
    {
        [Header("Encounter")]
        [SerializeField] private EncounterDirector encounterDirector;
        [SerializeField] private bool triggerOnce = true;

        [Header("Filter")]
        [SerializeField] private bool requirePlayerTag = true;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool disableColliderAfterTrigger = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs;

        private Collider cachedTriggerCollider;

        public event Action<EncounterZoneTrigger> Triggered;

        public EncounterDirector Director => encounterDirector;
        public bool IsTriggered { get; private set; }

        private void Awake()
        {
            cachedTriggerCollider = GetComponent<Collider>();
            if (cachedTriggerCollider != null && !cachedTriggerCollider.isTrigger)
            {
                cachedTriggerCollider.isTrigger = true;
            }

            if (encounterDirector == null)
            {
                encounterDirector = GetComponentInChildren<EncounterDirector>(true);
            }
        }

        private void Reset()
        {
            cachedTriggerCollider = GetComponent<Collider>();
            if (cachedTriggerCollider != null)
            {
                cachedTriggerCollider.isTrigger = true;
            }

            if (encounterDirector == null)
            {
                encounterDirector = GetComponentInChildren<EncounterDirector>(true);
            }
        }

        public void Activate()
        {
            if (triggerOnce && IsTriggered)
            {
                return;
            }

            IsTriggered = true;

            if (encounterDirector == null)
            {
                if (verboseLogs)
                {
                    Debug.LogWarning("[EncounterZoneTrigger] EncounterDirector is missing.", this);
                }

                return;
            }

            encounterDirector.SpawnEncounter();
            Triggered?.Invoke(this);

            if (verboseLogs)
            {
                Debug.Log($"[EncounterZoneTrigger] Triggered '{name}'.", this);
            }

            if (disableColliderAfterTrigger && cachedTriggerCollider != null)
            {
                cachedTriggerCollider.enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanTriggerBy(other))
            {
                return;
            }

            Activate();
        }

        private bool CanTriggerBy(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if (!requirePlayerTag || string.IsNullOrWhiteSpace(playerTag))
            {
                var unit = other.GetComponentInParent<UnitRoot>();
                return PlayerUnitLocator.IsPlayerUnit(unit);
            }

            return other.CompareTag(playerTag);
        }
    }
}
