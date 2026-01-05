using CombatSystem.Data;
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
        [SerializeField] private KeyCode primaryKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode secondaryKey = KeyCode.Alpha2;

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

            if (autoCast)
            {
                nextAutoTime = Time.time + Mathf.Max(0.1f, autoInterval);
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

            if (Input.GetKeyDown(primaryKey))
            {
                TryCast(primarySkill);
            }

            if (Input.GetKeyDown(secondaryKey))
            {
                TryCast(secondarySkill);
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
    }
}
