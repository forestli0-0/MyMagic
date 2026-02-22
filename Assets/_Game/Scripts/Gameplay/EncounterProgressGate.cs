using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 进度门：清完指定遭遇后解锁目标传送门。
    /// </summary>
    [DisallowMultipleComponent]
    public class EncounterProgressGate : MonoBehaviour
    {
        [Header("Gate Target")]
        [SerializeField] private LevelPortal gatedPortal;
        [SerializeField] private Collider gatedPortalCollider;
        [SerializeField] private bool lockPortalOnEnable = true;
        [SerializeField] private bool autoFindPortalFromChildren = true;

        [Header("Progress")]
        [SerializeField] private List<EncounterDirector> encounterDirectors = new List<EncounterDirector>(4);

        [Header("Debug")]
        [SerializeField] private bool verboseLogs;

        private bool isUnlocked;

        public bool IsUnlocked => isUnlocked;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeDirectors();

            if (lockPortalOnEnable)
            {
                SetPortalLocked(true);
            }

            EvaluateGate();
        }

        private void OnDisable()
        {
            UnsubscribeDirectors();
        }

        [ContextMenu("Force Unlock")]
        public void ForceUnlock()
        {
            SetPortalLocked(false);
        }

        [ContextMenu("Re-Evaluate Gate")]
        public void EvaluateGate()
        {
            if (isUnlocked)
            {
                return;
            }

            if (!AreAllEncountersCleared())
            {
                return;
            }

            SetPortalLocked(false);
        }

        private void HandleEncounterCleared(EncounterDirector _)
        {
            EvaluateGate();
        }

        private bool AreAllEncountersCleared()
        {
            var validCount = 0;
            for (var i = 0; i < encounterDirectors.Count; i++)
            {
                var director = encounterDirectors[i];
                if (director == null)
                {
                    continue;
                }

                validCount++;

                // 未激活过该遭遇（未进入区域）视为未完成。
                if (director.SpawnedCount <= 0)
                {
                    return false;
                }

                if (director.AliveCount > 0)
                {
                    return false;
                }
            }

            return validCount > 0;
        }

        private void SetPortalLocked(bool locked)
        {
            var unlocked = !locked;
            isUnlocked = unlocked;

            if (gatedPortal != null)
            {
                gatedPortal.enabled = unlocked;
            }

            if (gatedPortalCollider != null)
            {
                gatedPortalCollider.enabled = unlocked;
            }

            if (verboseLogs)
            {
                var state = unlocked ? "Unlocked" : "Locked";
                Debug.Log($"[EncounterProgressGate] {state} '{name}'.", this);
            }
        }

        private void SubscribeDirectors()
        {
            for (var i = 0; i < encounterDirectors.Count; i++)
            {
                var director = encounterDirectors[i];
                if (director == null)
                {
                    continue;
                }

                director.EncounterCleared -= HandleEncounterCleared;
                director.EncounterCleared += HandleEncounterCleared;
            }
        }

        private void UnsubscribeDirectors()
        {
            for (var i = 0; i < encounterDirectors.Count; i++)
            {
                var director = encounterDirectors[i];
                if (director == null)
                {
                    continue;
                }

                director.EncounterCleared -= HandleEncounterCleared;
            }
        }

        private void ResolveReferences()
        {
            if (gatedPortal == null && autoFindPortalFromChildren)
            {
                gatedPortal = GetComponentInChildren<LevelPortal>(true);
            }

            if (gatedPortalCollider == null && gatedPortal != null)
            {
                gatedPortalCollider = gatedPortal.GetComponent<Collider>();
            }
        }
    }
}
