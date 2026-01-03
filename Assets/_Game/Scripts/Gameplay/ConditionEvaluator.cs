using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 条件评估器，用于在技能/Buff/效果执行时判断条件是否满足。
    /// </summary>
    /// <remarks>
    /// 支持的条件类型包括：
    /// - 固定概率 (Chance)
    /// - 标签检测 (HasTag)
    /// - Buff 持有检测 (HasBuff)
    /// - 生命百分比阈值 (HealthPercentBelow/Above)
    /// - 存活状态检测 (IsTargetAlive/Dead)
    /// 
    /// 条件可通过 All（全部满足）或 Any（任一满足）逻辑组合。
    /// </remarks>
    public static class ConditionEvaluator
    {
        /// <summary>
        /// 评估条件定义是否满足。
        /// </summary>
        /// <param name="condition">条件定义，若为 null 则视为无条件通过</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">评估对象</param>
        /// <returns>若条件满足则返回 true</returns>
        public static bool Evaluate(ConditionDefinition condition, SkillRuntimeContext context, CombatTarget target)
        {
            // 无条件定义视为通过
            if (condition == null)
            {
                return true;
            }

            var entries = condition.Entries;
            // 无条件项视为通过
            if (entries == null || entries.Count == 0)
            {
                return true;
            }

            // All 模式：所有条件都必须满足
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

            // Any 模式：任一条件满足即可
            for (int i = 0; i < entries.Count; i++)
            {
                if (EvaluateEntry(entries[i], context, target))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 评估单个条件项。
        /// </summary>
        /// <param name="entry">条件项</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">评估对象</param>
        /// <returns>若条件满足则返回 true</returns>
        private static bool EvaluateEntry(ConditionEntry entry, SkillRuntimeContext context, CombatTarget target)
        {
            // 获取条件主体（施法者或目标）
            if (!TryGetSubject(entry.subject, context, target, out var subject))
            {
                return false;
            }

            switch (entry.type)
            {
                case ConditionType.Always:
                    // 始终满足
                    return true;

                case ConditionType.Chance:
                    // 概率判定：[0, chance] 区间内视为成功
                    return Random.value <= Mathf.Clamp01(entry.chance);

                case ConditionType.HasTag:
                    // 标签检测：主体必须拥有指定标签
                    return subject.Tags != null && subject.Tags.HasTag(entry.tag);

                case ConditionType.HasBuff:
                    // Buff 持有检测：检查主体是否持有指定的 Buff
                    return subject.Buffs != null && subject.Buffs.HasBuff(entry.buff);

                case ConditionType.HealthPercentBelow:
                    // 生命百分比低于阈值
                    return GetHealthPercent(subject) <= entry.threshold;

                case ConditionType.HealthPercentAbove:
                    // 生命百分比高于阈值
                    return GetHealthPercent(subject) >= entry.threshold;

                case ConditionType.IsTargetAlive:
                    // 目标存活检测
                    return subject.Health != null && subject.Health.IsAlive;

                case ConditionType.IsTargetDead:
                    // 目标死亡检测
                    return subject.Health != null && !subject.Health.IsAlive;

                default:
                    // 未知条件类型默认不满足
                    return false;
            }
        }

        /// <summary>
        /// 计算主体的生命百分比。
        /// </summary>
        /// <param name="subject">目标主体</param>
        /// <returns>生命百分比 [0, 1]，无效时返回 0</returns>
        private static float GetHealthPercent(CombatTarget subject)
        {
            if (subject.Health == null || subject.Health.Max <= 0f)
            {
                return 0f;
            }

            return subject.Health.Current / subject.Health.Max;
        }

        /// <summary>
        /// 根据条件主体类型获取对应的 CombatTarget。
        /// </summary>
        /// <param name="subject">条件主体类型（施法者或目标）</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">当前目标</param>
        /// <param name="subjectTarget">输出的主体 CombatTarget</param>
        /// <returns>若成功获取则返回 true</returns>
        private static bool TryGetSubject(ConditionSubject subject, SkillRuntimeContext context, CombatTarget target, out CombatTarget subjectTarget)
        {
            // 目标主体：直接使用传入的 target
            if (subject == ConditionSubject.Target)
            {
                subjectTarget = target;
                return target.IsValid;
            }

            // 施法者主体：从上下文中获取
            if (context.CasterUnit != null)
            {
                return CombatTarget.TryCreate(context.CasterUnit.gameObject, out subjectTarget);
            }

            subjectTarget = default;
            return false;
        }
    }
}
