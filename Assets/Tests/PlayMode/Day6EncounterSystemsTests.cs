using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    public class Day6EncounterSystemsTests
    {
        [Test]
        public void EnemyAffixRuntime_AppliesStatAndEliteMarker()
        {
            var statType = GetRuntimeType("CombatSystem.Data.StatDefinition");
            var unitType = GetRuntimeType("CombatSystem.Data.UnitDefinition");
            var statValueType = GetRuntimeType("CombatSystem.Data.StatValue");
            var affixType = GetRuntimeType("CombatSystem.Data.EnemyAffixDefinition");
            var affixModifierType = GetRuntimeType("CombatSystem.Data.EnemyAffixStatModifier");
            var skillType = GetRuntimeType("CombatSystem.Data.SkillDefinition");
            var statsComponentType = GetRuntimeType("CombatSystem.Core.StatsComponent");
            var healthType = GetRuntimeType("CombatSystem.Core.HealthComponent");
            var markerType = GetRuntimeType("CombatSystem.Gameplay.EliteUnitMarker");
            var runtimeType = GetRuntimeType("CombatSystem.Gameplay.EnemyAffixRuntime");

            if (statType == null || unitType == null || statValueType == null || affixType == null ||
                affixModifierType == null || skillType == null || statsComponentType == null ||
                healthType == null || markerType == null || runtimeType == null)
            {
                Assert.Ignore("Day6 runtime types not found.");
                return;
            }

            var stat = ScriptableObject.CreateInstance(statType);
            SetPrivateField(stat, "id", "Stat_MaxHealth");
            SetPrivateField(stat, "displayName", "Max Health");
            SetPrivateField(stat, "defaultValue", 0f);
            SetPrivateField(stat, "minValue", 0f);
            SetPrivateField(stat, "maxValue", 9999f);

            var unit = ScriptableObject.CreateInstance(unitType);
            SetPrivateField(unit, "baseStats", CreateBaseStats(statValueType, stat, 100f));
            SetPrivateField(unit, "prefab", null);
            SetPrivateField(unit, "startingSkills", CreateList(skillType));
            SetPrivateField(unit, "tags", CreateList(GetRuntimeType("CombatSystem.Data.TagDefinition")));
            SetPrivateField(unit, "basicAttack", null);
            SetPrivateField(unit, "aiProfile", null);

            var affix = ScriptableObject.CreateInstance(affixType);
            var modifier = Activator.CreateInstance(affixModifierType);
            SetPrivateField(modifier, "stat", stat);
            SetPrivateField(modifier, "flatBonus", 0f);
            SetPrivateField(modifier, "multiplier", 2f);
            SetPrivateField(affix, "statModifiers", CreateList(affixModifierType, modifier));
            SetPrivateField(affix, "bonusSkills", CreateList(skillType));
            SetPrivateField(affix, "scaleMultiplier", 1.1f);
            SetPrivateField(affix, "tintColor", Color.yellow);

            var enemy = new GameObject("Enemy_Affix_Test");
            var stats = enemy.AddComponent(statsComponentType);
            var health = enemy.AddComponent(healthType);
            SetPrivateField(health, "maxHealthStat", stat);
            CallMethod(stats, "Initialize", unit);
            CallMethod(health, "Initialize");

            var apply = runtimeType.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(apply);
            var applied = apply.Invoke(null, new object[] { enemy, affix });
            Assert.IsTrue(applied is bool appliedBool && appliedBool);

            var healthValue = CallMethod(stats, "GetValue", stat, 0f);
            Assert.AreEqual(200f, Convert.ToSingle(healthValue), 0.01f);
            Assert.AreEqual(200f, GetFloatProperty(health, "Max"), 0.01f);
            Assert.NotNull(enemy.GetComponent(markerType));

            UnityEngine.Object.DestroyImmediate(enemy);
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)affix);
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)unit);
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)stat);
        }

        [Test]
        public void EncounterDirector_SpawnsConfiguredCountAndGuaranteedElite()
        {
            var statType = GetRuntimeType("CombatSystem.Data.StatDefinition");
            var unitType = GetRuntimeType("CombatSystem.Data.UnitDefinition");
            var statValueType = GetRuntimeType("CombatSystem.Data.StatValue");
            var encounterType = GetRuntimeType("CombatSystem.Data.EncounterDefinition");
            var waveType = GetRuntimeType("CombatSystem.Data.EncounterWaveDefinition");
            var affixType = GetRuntimeType("CombatSystem.Data.EnemyAffixDefinition");
            var affixModifierType = GetRuntimeType("CombatSystem.Data.EnemyAffixStatModifier");
            var skillType = GetRuntimeType("CombatSystem.Data.SkillDefinition");
            var statsComponentType = GetRuntimeType("CombatSystem.Core.StatsComponent");
            var healthType = GetRuntimeType("CombatSystem.Core.HealthComponent");
            var teamType = GetRuntimeType("CombatSystem.Core.TeamComponent");
            var directorType = GetRuntimeType("CombatSystem.Gameplay.EncounterDirector");
            var markerType = GetRuntimeType("CombatSystem.Gameplay.EliteUnitMarker");

            if (statType == null || unitType == null || statValueType == null || encounterType == null ||
                waveType == null || affixType == null || affixModifierType == null || skillType == null ||
                statsComponentType == null || healthType == null || teamType == null || directorType == null || markerType == null)
            {
                Assert.Ignore("Day6 runtime types not found.");
                return;
            }

            var stat = ScriptableObject.CreateInstance(statType);
            SetPrivateField(stat, "id", "Stat_MaxHealth");
            SetPrivateField(stat, "displayName", "Max Health");

            var enemyPrefab = new GameObject("EnemyPrefab_Test");
            enemyPrefab.AddComponent(statsComponentType);
            var prefabHealth = enemyPrefab.AddComponent(healthType);
            SetPrivateField(prefabHealth, "maxHealthStat", stat);
            enemyPrefab.AddComponent(teamType);

            var unit = ScriptableObject.CreateInstance(unitType);
            SetPrivateField(unit, "baseStats", CreateBaseStats(statValueType, stat, 80f));
            SetPrivateField(unit, "prefab", enemyPrefab);
            SetPrivateField(unit, "startingSkills", CreateList(skillType));
            SetPrivateField(unit, "tags", CreateList(GetRuntimeType("CombatSystem.Data.TagDefinition")));
            SetPrivateField(unit, "basicAttack", null);
            SetPrivateField(unit, "aiProfile", null);

            var affix = ScriptableObject.CreateInstance(affixType);
            var modifier = Activator.CreateInstance(affixModifierType);
            SetPrivateField(modifier, "stat", stat);
            SetPrivateField(modifier, "flatBonus", 20f);
            SetPrivateField(modifier, "multiplier", 1.5f);
            SetPrivateField(affix, "statModifiers", CreateList(affixModifierType, modifier));
            SetPrivateField(affix, "bonusSkills", CreateList(skillType));
            SetPrivateField(affix, "scaleMultiplier", 1.1f);
            SetPrivateField(affix, "tintColor", Color.yellow);

            var wave = Activator.CreateInstance(waveType);
            SetPrivateField(wave, "waveId", "wave_test");
            SetPrivateField(wave, "unit", unit);
            SetPrivateField(wave, "prefabOverride", null);
            SetPrivateField(wave, "minCount", 3);
            SetPrivateField(wave, "maxCount", 3);
            SetPrivateField(wave, "guaranteedEliteCount", 1);
            SetPrivateField(wave, "eliteChance", 0f);
            SetPrivateField(wave, "eliteAffixes", CreateList(affixType, affix));

            var encounter = ScriptableObject.CreateInstance(encounterType);
            SetPrivateField(encounter, "waves", CreateList(waveType, wave));
            SetPrivateField(encounter, "globalEliteAffixes", CreateList(affixType, affix));
            SetPrivateField(encounter, "spawnRadius", 2f);
            SetPrivateField(encounter, "randomSeed", 42);

            var root = new GameObject("Encounter_Root_Test");
            var director = root.AddComponent(directorType);
            CallMethod(director, "SetEncounter", encounter);
            CallMethod(director, "SpawnEncounter");

            Assert.AreEqual(3, GetIntProperty(director, "SpawnedCount"));
            Assert.AreEqual(3, GetIntProperty(director, "AliveCount"));

            var markers = root.GetComponentsInChildren(markerType, true);
            Assert.AreEqual(1, markers.Length);

            var teams = root.GetComponentsInChildren(teamType, true);
            Assert.GreaterOrEqual(teams.Length, 3);
            for (int i = 0; i < teams.Length; i++)
            {
                Assert.AreEqual(2, GetIntProperty(teams[i], "TeamId"));
            }

            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)encounter);
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)affix);
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)unit);
            UnityEngine.Object.DestroyImmediate(enemyPrefab);
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)stat);
        }

        private static Type GetRuntimeType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            return type ?? Type.GetType(typeName);
        }

        private static object CreateList(Type elementType, object firstItem = null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType);
            if (firstItem != null)
            {
                listType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public)?.Invoke(list, new[] { firstItem });
            }

            return list;
        }

        private static object CreateBaseStats(Type statValueType, object stat, float value)
        {
            var list = CreateList(statValueType);
            var item = Activator.CreateInstance(statValueType);
            SetField(item, "stat", stat);
            SetField(item, "value", value);
            list.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public)?.Invoke(list, new[] { item });
            return list;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            Assert.NotNull(target);
            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            Assert.NotNull(target);
            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            while (field == null && type.BaseType != null)
            {
                type = type.BaseType;
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object CallMethod(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var safeArgs = args ?? Array.Empty<object>();
            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != safeArgs.Length)
                {
                    continue;
                }

                var compatible = true;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (!IsArgumentCompatible(parameters[p].ParameterType, safeArgs[p]))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                {
                    continue;
                }

                return method.Invoke(target, safeArgs);
            }

            return null;
        }

        private static bool IsArgumentCompatible(Type parameterType, object arg)
        {
            if (parameterType == null)
            {
                return false;
            }

            if (arg == null)
            {
                return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;
            }

            var argType = arg.GetType();
            return parameterType.IsAssignableFrom(argType);
        }

        private static object GetPropertyValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            var type = target.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                return property.GetValue(target);
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(target) : null;
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value is int intValue ? intValue : 0;
        }

        private static float GetFloatProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value is float floatValue ? floatValue : 0f;
        }
    }
}
