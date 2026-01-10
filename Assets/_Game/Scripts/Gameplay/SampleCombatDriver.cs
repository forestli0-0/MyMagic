using CombatSystem.Data;
using CombatSystem.Input;
using CombatSystem.UI;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    public class SampleCombatDriver : MonoBehaviour
    {
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private Transform target;
        [SerializeField] private SkillDefinition primarySkill;
        [SerializeField] private SkillDefinition secondarySkill;
        [SerializeField] private bool autoCast = true;
        [SerializeField] private float autoInterval = 2.5f;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private bool autoFindInputReader = true;

        private float nextAutoTime;

        private void Reset()
        {
            skillUser = GetComponent<SkillUserComponent>();
        }

        private void OnEnable()
        {
            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            if (autoFindInputReader && inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }

            if (inputReader != null)
            {
                inputReader.SkillStarted += HandleSkillStarted;
            }

            if (autoCast)
            {
                nextAutoTime = Time.time + Mathf.Max(0.1f, autoInterval);
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.SkillStarted -= HandleSkillStarted;
            }
        }

        private void Update()
        {
            if (!UIRoot.IsGameplayInputAllowed())
            {
                return;
            }

            if (skillUser == null)
            {
                return;
            }

            if (autoCast)
            {
                if (Time.time >= nextAutoTime)
                {
                    TryCast(primarySkill);
                    nextAutoTime = Time.time + Mathf.Max(0.1f, autoInterval);
                }

                return;
            }
        }

        private void TryCast(SkillDefinition skill)
        {
            if (skill == null)
            {
                skill = skillUser.BasicAttack;
                if (skill == null)
                {
                    return;
                }
            }

            skillUser.TryCast(skill, target != null ? target.gameObject : null);
        }

        private void HandleSkillStarted(int slotIndex)
        {
            if (autoCast || skillUser == null)
            {
                return;
            }

            if (!UIRoot.IsGameplayInputAllowed())
            {
                return;
            }

            if (slotIndex == 0)
            {
                TryCast(primarySkill);
                return;
            }

            if (slotIndex == 1)
            {
                TryCast(secondarySkill);
            }
        }
    }
}
