using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.Tests.EditMode
{
    public class SkillPresentationPlanTests
    {
        private const string BindMenuPath = "Combat/Tools/Skill Presentation/Bind Cues For Player Skills";
        private const int ProjectileSpawnEventValue = 3;
        private const int ProjectileHitEventValue = 4;

        [Test]
        public void PlayerSkills_HavePresentationCueOrLegacyPayload()
        {
            Assert.IsTrue(
                EditorApplication.ExecuteMenuItem(BindMenuPath),
                $"Menu not found: {BindMenuPath}");

            var player = FindPlayerUnitDefinition();
            Assert.NotNull(player, "Unit_Player definition not found.");

            var skills = CollectPlayerSkills(player);
            Assert.IsNotEmpty(skills, "Player skill list is empty.");

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                Assert.NotNull(skill, "Skill reference is null.");

                var skillSo = new SerializedObject(skill);
                var steps = skillSo.FindProperty("steps");
                Assert.NotNull(steps, $"{skill.name} has null steps.");
                Assert.Greater(steps.arraySize, 0, $"{skill.name} has no steps.");

                var skillHasCuePayload = false;
                for (int s = 0; s < steps.arraySize; s++)
                {
                    var step = steps.GetArrayElementAtIndex(s);
                    Assert.NotNull(step, $"{skill.name}.Step[{s}] is null.");

                    var hasCuePayload = StepHasCuePayload(step);
                    var hasLegacyPayload = StepHasLegacyPayload(step);
                    var hasEffects = StepHasEffects(step);

                    Assert.IsTrue(
                        hasEffects || hasCuePayload || hasLegacyPayload,
                        $"{skill.name}.Step[{s}] has no effects and no presentation payload.");

                    skillHasCuePayload |= hasCuePayload || hasLegacyPayload;
                }

                Assert.IsTrue(skillHasCuePayload, $"{skill.name} has no visual/audio cue payload.");
            }
        }

        [Test]
        public void PlayerSkills_ProjectileSkills_HaveSpawnAndHitCue()
        {
            Assert.IsTrue(
                EditorApplication.ExecuteMenuItem(BindMenuPath),
                $"Menu not found: {BindMenuPath}");

            var player = FindPlayerUnitDefinition();
            Assert.NotNull(player, "Unit_Player definition not found.");

            var skills = CollectPlayerSkills(player);
            Assert.IsNotEmpty(skills, "Player skill list is empty.");

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                var skillSo = new SerializedObject(skill);
                var steps = skillSo.FindProperty("steps");
                if (steps == null)
                {
                    continue;
                }

                var hasProjectile = false;
                var hasSpawnCue = false;
                var hasHitCue = false;
                for (int s = 0; s < steps.arraySize; s++)
                {
                    var step = steps.GetArrayElementAtIndex(s);
                    if (step == null)
                    {
                        continue;
                    }

                    hasProjectile |= StepHasProjectile(step);
                    var cues = step.FindPropertyRelative("presentationCues");
                    if (cues == null)
                    {
                        continue;
                    }

                    for (int c = 0; c < cues.arraySize; c++)
                    {
                        var cue = cues.GetArrayElementAtIndex(c);
                        if (cue == null || !CueHasPayload(cue))
                        {
                            continue;
                        }

                        var eventType = cue.FindPropertyRelative("eventType");
                        if (eventType == null)
                        {
                            continue;
                        }

                        if (eventType.enumValueIndex == ProjectileSpawnEventValue)
                        {
                            hasSpawnCue = true;
                        }

                        if (eventType.enumValueIndex == ProjectileHitEventValue)
                        {
                            hasHitCue = true;
                        }
                    }
                }

                if (!hasProjectile)
                {
                    continue;
                }

                Assert.IsTrue(hasSpawnCue, $"{skill.name} projectile skill missing ProjectileSpawn cue.");
                Assert.IsTrue(hasHitCue, $"{skill.name} projectile skill missing ProjectileHit cue.");
            }
        }

        private static bool StepHasCuePayload(SerializedProperty step)
        {
            if (step == null)
            {
                return false;
            }

            var cues = step.FindPropertyRelative("presentationCues");
            if (cues == null)
            {
                return false;
            }

            for (int i = 0; i < cues.arraySize; i++)
            {
                var cue = cues.GetArrayElementAtIndex(i);
                if (cue != null && CueHasPayload(cue))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StepHasLegacyPayload(SerializedProperty step)
        {
            if (step == null)
            {
                return false;
            }

            var animationTrigger = step.FindPropertyRelative("animationTrigger");
            var vfx = step.FindPropertyRelative("vfxPrefab");
            var sfx = step.FindPropertyRelative("sfx");
            return (animationTrigger != null && !string.IsNullOrWhiteSpace(animationTrigger.stringValue))
                   || (vfx != null && vfx.objectReferenceValue != null)
                   || (sfx != null && sfx.objectReferenceValue != null);
        }

        private static bool StepHasEffects(SerializedProperty step)
        {
            var effects = step != null ? step.FindPropertyRelative("effects") : null;
            return effects != null && effects.arraySize > 0;
        }

        private static bool StepHasProjectile(SerializedProperty step)
        {
            if (step == null)
            {
                return false;
            }

            var effects = step.FindPropertyRelative("effects");
            if (effects == null)
            {
                return false;
            }

            for (int i = 0; i < effects.arraySize; i++)
            {
                var effectRef = effects.GetArrayElementAtIndex(i);
                var effectObject = effectRef != null ? effectRef.objectReferenceValue : null;
                if (effectObject == null)
                {
                    continue;
                }

                var effectSo = new SerializedObject(effectObject);
                var effectType = effectSo.FindProperty("effectType");
                if (effectType != null && effectType.enumDisplayNames.Length > effectType.enumValueIndex)
                {
                    var effectTypeName = effectType.enumDisplayNames[effectType.enumValueIndex];
                    if (string.Equals(effectTypeName, "Projectile", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CueHasPayload(SerializedProperty cue)
        {
            if (cue == null)
            {
                return false;
            }

            var animationTrigger = cue.FindPropertyRelative("animationTrigger");
            var vfx = cue.FindPropertyRelative("vfxPrefab");
            var sfx = cue.FindPropertyRelative("sfx");
            return (animationTrigger != null && !string.IsNullOrWhiteSpace(animationTrigger.stringValue))
                   || (vfx != null && vfx.objectReferenceValue != null)
                   || (sfx != null && sfx.objectReferenceValue != null);
        }

        private static UnityEngine.Object FindPlayerUnitDefinition()
        {
            var guids = AssetDatabase.FindAssets("t:UnitDefinition");
            UnityEngine.Object fallback = null;
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var unit = AssetDatabase.LoadMainAssetAtPath(path);
                if (unit == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = unit;
                }

                var so = new SerializedObject(unit);
                var id = so.FindProperty("id");
                if (id != null && string.Equals(id.stringValue, "Unit_Player", StringComparison.OrdinalIgnoreCase))
                {
                    return unit;
                }

                if (unit.name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fallback = unit;
                }
            }

            return fallback;
        }

        private static List<UnityEngine.Object> CollectPlayerSkills(UnityEngine.Object player)
        {
            var result = new List<UnityEngine.Object>(16);
            var unique = new HashSet<UnityEngine.Object>();
            if (player == null)
            {
                return result;
            }

            var so = new SerializedObject(player);
            var basicAttack = so.FindProperty("basicAttack");
            if (basicAttack != null && basicAttack.objectReferenceValue != null && unique.Add(basicAttack.objectReferenceValue))
            {
                result.Add(basicAttack.objectReferenceValue);
            }

            var startingSkills = so.FindProperty("startingSkills");
            if (startingSkills == null || !startingSkills.isArray)
            {
                return result;
            }

            for (int i = 0; i < startingSkills.arraySize; i++)
            {
                var skill = startingSkills.GetArrayElementAtIndex(i).objectReferenceValue;
                if (skill != null && unique.Add(skill))
                {
                    result.Add(skill);
                }
            }

            return result;
        }
    }
}
