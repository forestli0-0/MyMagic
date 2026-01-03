using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 修正器参数标识符常量集合。
    /// </summary>
    /// <remarks>
    /// 这些常量用于标识修正器作用的具体参数类型。
    /// 修正器通过 ParameterId 精确匹配目标参数，实现对技能/效果属性的动态调整。
    /// 
    /// 命名规范：[目标类型].[参数名]
    /// - Skill.* : 技能级别参数（资源消耗、冷却、施法时间等）
    /// - Effect.* : 效果级别参数（数值、持续时间、间隔等）
    /// </remarks>
    public static class ModifierParameters
    {
        // ========== 技能参数 ==========
        /// <summary>技能资源消耗</summary>
        public const string SkillResourceCost = "Skill.ResourceCost";
        /// <summary>技能冷却时间</summary>
        public const string SkillCooldown = "Skill.Cooldown";
        /// <summary>技能施法时间</summary>
        public const string SkillCastTime = "Skill.CastTime";
        /// <summary>技能引导时间</summary>
        public const string SkillChannelTime = "Skill.ChannelTime";

        // ========== 效果参数 ==========
        /// <summary>效果数值（伤害/治疗量）</summary>
        public const string EffectValue = "Effect.Value";
        /// <summary>效果持续时间</summary>
        public const string EffectDuration = "Effect.Duration";
        /// <summary>效果触发间隔</summary>
        public const string EffectInterval = "Effect.Interval";
        /// <summary>位移距离</summary>
        public const string EffectMoveDistance = "Effect.MoveDistance";
        /// <summary>位移速度</summary>
        public const string EffectMoveSpeed = "Effect.MoveSpeed";
    }

    /// <summary>
    /// 修正器解析器，负责计算 Buff 修正器对技能和效果参数的影响。
    /// </summary>
    /// <remarks>
    /// 核心职责：
    /// - 收集施法者身上所有 Buff 的修正器
    /// - 根据修正器的目标类型、参数ID、标签过滤进行匹配
    /// - 按照 Add → Multiply → Override 的顺序计算最终值
    /// 
    /// 计算公式：
    /// <code>
    /// 最终值 = (基础值 + 加法修正之和) × (1 + 乘法修正之和)
    /// 若存在 Override 修正器，则最终值 = 覆盖值
    /// </code>
    /// 
    /// 修正器层叠规则：
    /// - Add: 所有加法修正累加
    /// - Multiply: 所有乘法修正累加后统一应用（加法叠加，不是连乘）
    /// - Override: 最后一个生效的覆盖值将替换计算结果
    /// </remarks>
    public static class ModifierResolver
    {
        /// <summary>
        /// 应用技能参数的修正器。
        /// </summary>
        /// <param name="baseValue">基础值</param>
        /// <param name="skill">技能定义</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">目标（用于条件判断）</param>
        /// <param name="parameterId">参数标识符（参见 ModifierParameters）</param>
        /// <returns>经过修正后的最终值</returns>
        public static float ApplySkillModifiers(
            float baseValue,
            SkillDefinition skill,
            SkillRuntimeContext context,
            CombatTarget target,
            string parameterId)
        {
            // 参数校验：必须有有效的参数ID、技能定义和施法者
            if (string.IsNullOrEmpty(parameterId) || skill == null || context.CasterUnit == null)
            {
                return baseValue;
            }

            // 获取施法者的 Buff 控制器
            var buffController = context.CasterUnit.GetComponent<BuffController>();
            if (buffController == null || buffController.ActiveBuffs.Count == 0)
            {
                return baseValue;
            }

            // 使用技能的标签列表进行修正器匹配
            return ApplyModifiers(
                baseValue,
                buffController.ActiveBuffs,
                ModifierTargetType.Skill,
                parameterId,
                null,
                skill.Tags,
                context,
                target);
        }

        /// <summary>
        /// 应用效果参数的修正器。
        /// </summary>
        /// <param name="baseValue">基础值</param>
        /// <param name="effect">效果定义</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">目标（用于条件判断）</param>
        /// <param name="parameterId">参数标识符（参见 ModifierParameters）</param>
        /// <returns>经过修正后的最终值</returns>
        public static float ApplyEffectModifiers(
            float baseValue,
            EffectDefinition effect,
            SkillRuntimeContext context,
            CombatTarget target,
            string parameterId)
        {
            // 参数校验
            if (effect == null || string.IsNullOrEmpty(parameterId) || context.CasterUnit == null)
            {
                return baseValue;
            }

            // 获取施法者的 Buff 控制器
            var buffController = context.CasterUnit.GetComponent<BuffController>();
            if (buffController == null || buffController.ActiveBuffs.Count == 0)
            {
                return baseValue;
            }

            // 效果的修正器匹配使用其所属技能的标签
            var tags = context.Skill != null ? context.Skill.Tags : null;

            return ApplyModifiers(
                baseValue,
                buffController.ActiveBuffs,
                ModifierTargetType.Effect,
                parameterId,
                null,
                tags,
                context,
                target);
        }

        /// <summary>
        /// 核心修正器计算方法。
        /// </summary>
        /// <param name="baseValue">基础值</param>
        /// <param name="buffs">当前激活的 Buff 列表</param>
        /// <param name="targetType">修正器目标类型</param>
        /// <param name="parameterId">参数ID（用于 Skill/Effect 类型）</param>
        /// <param name="stat">属性定义（用于 Stat 类型）</param>
        /// <param name="contextTags">上下文标签（技能/效果携带的标签）</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">目标</param>
        /// <returns>计算后的最终值</returns>
        private static float ApplyModifiers(
            float baseValue,
            IReadOnlyList<BuffController.BuffInstance> buffs,
            ModifierTargetType targetType,
            string parameterId,
            StatDefinition stat,
            IReadOnlyList<TagDefinition> contextTags,
            SkillRuntimeContext context,
            CombatTarget target)
        {
            // 累加器：分别收集加法、乘法和覆盖修正
            var add = 0f;
            var mul = 0f;
            var hasOverride = false;
            var overrideValue = baseValue;

            // 遍历所有激活的 Buff
            for (int i = 0; i < buffs.Count; i++)
            {
                var buffDef = buffs[i].Definition;
                if (buffDef == null)
                {
                    continue;
                }

                var modifiers = buffDef.Modifiers;
                if (modifiers == null || modifiers.Count == 0)
                {
                    continue;
                }

                // Buff 层数作为修正器值的倍率
                var stacks = Mathf.Max(1, buffs[i].Stacks);

                // 遍历该 Buff 的所有修正器
                for (int j = 0; j < modifiers.Count; j++)
                {
                    var modifier = modifiers[j];
                    
                    // 目标类型必须匹配
                    if (modifier == null || modifier.Target != targetType)
                    {
                        continue;
                    }

                    // 根据目标类型进行精确匹配
                    if (targetType == ModifierTargetType.Stat)
                    {
                        // Stat 类型：匹配属性定义
                        if (modifier.Stat != stat)
                        {
                            continue;
                        }
                    }
                    else if (!string.Equals(modifier.ParameterId, parameterId, StringComparison.Ordinal))
                    {
                        // Skill/Effect 类型：匹配参数ID
                        continue;
                    }

                    // 标签过滤：必须包含所有必需标签，不能包含任何阻止标签
                    if (!TagsMatch(modifier.RequiredTags, modifier.BlockedTags, contextTags))
                    {
                        continue;
                    }

                    // 条件判断：若有条件定义则必须满足
                    if (modifier.Condition != null && !ConditionEvaluator.Evaluate(modifier.Condition, context, target))
                    {
                        continue;
                    }

                    // 计算修正值（基础值 × 层数）
                    var value = modifier.Value * stacks;
                    
                    // 根据操作类型累加到对应的累加器
                    switch (modifier.Operation)
                    {
                        case ModifierOperation.Add:
                            add += value;
                            break;
                        case ModifierOperation.Multiply:
                            mul += value;
                            break;
                        case ModifierOperation.Override:
                            hasOverride = true;
                            overrideValue = value;
                            break;
                    }
                }
            }

            // 最终计算：(基础值 + 加法) × (1 + 乘法)
            var result = (baseValue + add) * (1f + mul);
            
            // Override 修正器会完全覆盖计算结果
            if (hasOverride)
            {
                result = overrideValue;
            }

            return result;
        }

        /// <summary>
        /// 检查上下文标签是否满足修正器的标签过滤条件。
        /// </summary>
        /// <param name="required">必需标签列表（必须全部包含）</param>
        /// <param name="blocked">阻止标签列表（不能包含任何一个）</param>
        /// <param name="contextTags">上下文标签列表</param>
        /// <returns>若满足条件返回 true</returns>
        private static bool TagsMatch(
            IReadOnlyList<TagDefinition> required,
            IReadOnlyList<TagDefinition> blocked,
            IReadOnlyList<TagDefinition> contextTags)
        {
            // 检查必需标签：全部必须存在
            if (required != null && required.Count > 0)
            {
                if (contextTags == null || contextTags.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < required.Count; i++)
                {
                    var tag = required[i];
                    if (tag == null || !ContainsTag(contextTags, tag))
                    {
                        return false;
                    }
                }
            }

            // 检查阻止标签：任何一个存在则不匹配
            if (blocked != null && blocked.Count > 0 && contextTags != null)
            {
                for (int i = 0; i < blocked.Count; i++)
                {
                    var tag = blocked[i];
                    if (tag != null && ContainsTag(contextTags, tag))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 检查标签列表中是否包含指定标签。
        /// </summary>
        /// <remarks>
        /// 使用线性查找，对于小型标签列表（通常 &lt; 10）足够高效。
        /// </remarks>
        private static bool ContainsTag(IReadOnlyList<TagDefinition> tags, TagDefinition tag)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

