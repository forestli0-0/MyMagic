using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    public static class ConditionEvaluator
    {
        public static bool Evaluate(ConditionDefinition condition, SkillRuntimeContext context, CombatTarget target)
        {
            if (condition == null)
            {
                return true;
            }

            var entries = condition.Entries;
            if (entries == null || entries.Count == 0)
            {
                return true;
            }

            if (condition.Operator == ConditionOperator.All)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!EvaluateEntry(entries[i], context, target))
                    {
                        return false;
                    }
                }

                return true;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (EvaluateEntry(entries[i], context, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EvaluateEntry(ConditionEntry entry, SkillRuntimeContext context, CombatTarget target)
        {
            if (!TryGetSubject(entry.subject, context, target, out var subject))
            {
                return false;
            }

            switch (entry.type)
            {
                case ConditionType.Always:
                    return true;
                case ConditionType.Chance:
                    return Random.value <= Mathf.Clamp01(entry.chance);
                case ConditionType.HasTag:
                    return subject.Tags != null && subject.Tags.HasTag(entry.tag);
                case ConditionType.HasBuff:
                    return subject.Unit != null && subject.Unit.GetComponent<BuffController>()?.HasBuff(entry.buff) == true;
                case ConditionType.HealthPercentBelow:
                    return GetHealthPercent(subject) <= entry.threshold;
                case ConditionType.HealthPercentAbove:
                    return GetHealthPercent(subject) >= entry.threshold;
                case ConditionType.IsTargetAlive:
                    return subject.Health != null && subject.Health.IsAlive;
                case ConditionType.IsTargetDead:
                    return subject.Health != null && !subject.Health.IsAlive;
                default:
                    return false;
            }
        }

        private static float GetHealthPercent(CombatTarget subject)
        {
            if (subject.Health == null || subject.Health.Max <= 0f)
            {
                return 0f;
            }

            return subject.Health.Current / subject.Health.Max;
        }

        private static bool TryGetSubject(ConditionSubject subject, SkillRuntimeContext context, CombatTarget target, out CombatTarget subjectTarget)
        {
            if (subject == ConditionSubject.Target)
            {
                subjectTarget = target;
                return target.IsValid;
            }

            if (context.CasterUnit != null)
            {
                return CombatTarget.TryCreate(context.CasterUnit.gameObject, out subjectTarget);
            }

            subjectTarget = default;
            return false;
        }
    }
}
