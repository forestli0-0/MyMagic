using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Editor
{
    public enum SkillValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct SkillValidationMessage
    {
        public readonly SkillValidationSeverity Severity;
        public readonly string Message;
        public readonly Object Context;

        public SkillValidationMessage(SkillValidationSeverity severity, string message, Object context = null)
        {
            Severity = severity;
            Message = message;
            Context = context;
        }
    }

    public static class SkillValidationService
    {
        public static List<SkillValidationMessage> ValidateSkill(SkillDefinition skill)
        {
            var results = new List<SkillValidationMessage>(32);
            if (skill == null)
            {
                results.Add(new SkillValidationMessage(SkillValidationSeverity.Error, "Skill is null."));
                return results;
            }

            ValidateCore(skill, results);
            ValidateSteps(skill, results);
            return results;
        }

        private static void ValidateCore(SkillDefinition skill, List<SkillValidationMessage> results)
        {
            if (skill.Targeting == null)
            {
                results.Add(new SkillValidationMessage(SkillValidationSeverity.Warning, "Skill.Targeting is not assigned.", skill));
            }

            var steps = skill.Steps;
            if (steps == null || steps.Count == 0)
            {
                results.Add(new SkillValidationMessage(SkillValidationSeverity.Error, "Skill has no steps.", skill));
            }
        }

        private static void ValidateSteps(SkillDefinition skill, List<SkillValidationMessage> results)
        {
            var steps = skill.Steps;
            if (steps == null)
            {
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null)
                {
                    results.Add(new SkillValidationMessage(SkillValidationSeverity.Error, $"Step[{i}] is null.", skill));
                    continue;
                }

                var hasCuePayload = HasCuePayload(step.presentationCues);
                var hasLegacyPayload = !string.IsNullOrWhiteSpace(step.animationTrigger)
                                       || step.vfxPrefab != null
                                       || step.sfx != null;
                var hasEffects = step.effects != null && step.effects.Count > 0;

                if (!hasEffects && !hasCuePayload && !hasLegacyPayload)
                {
                    results.Add(new SkillValidationMessage(
                        SkillValidationSeverity.Warning,
                        $"Step[{i}] has no effect and no presentation payload.",
                        skill));
                }

                ValidateCues(skill, i, step, results);
                ValidateEffects(skill, i, step.effects, results);
            }
        }

        private static bool HasCuePayload(List<SkillPresentationCue> cues)
        {
            if (cues == null || cues.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                if (cues[i] != null && cues[i].HasPayload)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateCues(
            SkillDefinition skill,
            int stepIndex,
            SkillStep step,
            List<SkillValidationMessage> results)
        {
            if (step.presentationCues == null)
            {
                return;
            }

            for (int i = 0; i < step.presentationCues.Count; i++)
            {
                var cue = step.presentationCues[i];
                if (cue == null)
                {
                    results.Add(new SkillValidationMessage(
                        SkillValidationSeverity.Warning,
                        $"Step[{stepIndex}].Cue[{i}] is null.",
                        skill));
                    continue;
                }

                if (!cue.HasPayload)
                {
                    results.Add(new SkillValidationMessage(
                        SkillValidationSeverity.Warning,
                        $"Step[{stepIndex}].Cue[{i}] has no animation/vfx/sfx payload.",
                        skill));
                }

                if (cue.maxLifetime < 0f)
                {
                    results.Add(new SkillValidationMessage(
                        SkillValidationSeverity.Warning,
                        $"Step[{stepIndex}].Cue[{i}] has negative maxLifetime.",
                        skill));
                }
            }
        }

        private static void ValidateEffects(
            SkillDefinition skill,
            int stepIndex,
            List<EffectDefinition> effects,
            List<SkillValidationMessage> results)
        {
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    results.Add(new SkillValidationMessage(
                        SkillValidationSeverity.Error,
                        $"Step[{stepIndex}].Effect[{i}] is null.",
                        skill));
                    continue;
                }

                switch (effect.EffectType)
                {
                    case EffectType.Projectile:
                        if (effect.Projectile == null)
                        {
                            results.Add(new SkillValidationMessage(
                                SkillValidationSeverity.Error,
                                $"Step[{stepIndex}].Effect[{i}] uses Projectile effect but Projectile reference is missing.",
                                effect));
                        }

                        break;
                    case EffectType.ApplyBuff:
                    case EffectType.RemoveBuff:
                        if (effect.Buff == null)
                        {
                            results.Add(new SkillValidationMessage(
                                SkillValidationSeverity.Error,
                                $"Step[{stepIndex}].Effect[{i}] uses Buff effect but Buff reference is missing.",
                                effect));
                        }

                        break;
                    case EffectType.TriggerSkill:
                        if (effect.TriggeredSkill == null)
                        {
                            results.Add(new SkillValidationMessage(
                                SkillValidationSeverity.Warning,
                                $"Step[{stepIndex}].Effect[{i}] triggers another skill but TriggeredSkill is missing.",
                                effect));
                        }

                        break;
                    case EffectType.Summon:
                        if (effect.SummonPrefab == null && effect.SummonUnit == null)
                        {
                            results.Add(new SkillValidationMessage(
                                SkillValidationSeverity.Error,
                                $"Step[{stepIndex}].Effect[{i}] summon has no prefab/unit reference.",
                                effect));
                        }

                        break;
                }
            }
        }
    }
}
