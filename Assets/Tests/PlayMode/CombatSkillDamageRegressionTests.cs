using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    public class CombatSkillDamageRegressionTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>(64);

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
            {
                var item = cleanup[i];
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            cleanup.Clear();
        }

        [Test]
        public void BuffOnApplyDamage_UsesBuffSourceStats()
        {
            CreateSystems();
            var source = CreateUnit("BuffSource");
            var target = CreateUnit("BuffTarget");
            var powerStat = CreateStat("Stat_BuffApplyPower");

            CallMethod(source.Stats, "SetValue", powerStat, 30f);
            CallMethod(target.Stats, "SetValue", powerStat, 5f);
            CallMethod(target.Health, "SetCurrent", 100f);

            var damageEffect = CreateDamageEffect(0f, "True", powerStat, 1f);
            var buff = CreateBuffWithTrigger("OnApply", damageEffect, 0f);
            CallMethod(target.Buffs, "ApplyBuff", buff, source.Unit);

            Assert.AreEqual(70f, GetFloatProperty(target.Health, "Current"), 0.01f);
        }

        [Test]
        public void BuffTickDamage_AttributionUsesSourceUnit()
        {
            CreateSystems();
            var source = CreateUnit("DotSource");
            var victim = CreateUnit("DotVictim");
            CallMethod(victim.Health, "SetCurrent", 20f);

            var tickDamage = CreateDamageEffect(25f, "True");
            var dotBuff = CreateBuffWithTrigger("OnTick", tickDamage, 1f);
            CallMethod(victim.Buffs, "ApplyBuff", dotBuff, source.Unit);

            var activeBuffs = GetPropertyValue(victim.Buffs, "ActiveBuffs") as IList;
            Assert.IsNotNull(activeBuffs);
            Assert.Greater(activeBuffs.Count, 0);
            var instance = activeBuffs[0];
            InvokePrivateTriggerBuff(victim.Buffs, instance, "OnTick");

            var damageSource = GetPrivateFieldValue(victim.Health, "lastDamageSource");
            var sourceUnit = GetFieldValue(damageSource, "SourceUnit");
            Assert.AreSame(source.Unit, sourceUnit);
        }

        [Test]
        public void CanCast_UsesModifiedResourceCost()
        {
            var caster = CreateUnit("Caster_Cost");
            CallMethod(caster.Resource, "SetCurrent", 50f);

            var skill = CreateSkill(60f, 0f, null);
            var modifier = CreateSkillCostModifier(-20f);
            var buff = CreateBuffWithModifier(modifier);
            CallMethod(caster.Buffs, "ApplyBuff", buff, caster.Unit);

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "CanCast", skill));
            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.AreEqual(10f, GetFloatProperty(caster.Resource, "Current"), 0.01f);
        }

        [Test]
        public void TryCast_FailsOnDeadExplicitTarget_NoCostNoCooldown()
        {
            var caster = CreateUnit("Caster_DeadTarget");
            var deadTarget = CreateUnit("DeadTarget");
            CallMethod(deadTarget.Health, "SetCurrent", 0f);
            CallMethod(caster.Resource, "SetCurrent", 80f);

            var targeting = CreateTargeting(true, false, "AliveOnly");
            var skill = CreateSkill(30f, 5f, targeting);
            var before = GetFloatProperty(caster.Resource, "Current");

            var castResult = CallBoolMethod(caster.SkillUser, "TryCast", skill, deadTarget.GameObject);

            Assert.IsFalse(castResult);
            Assert.AreEqual(before, GetFloatProperty(caster.Resource, "Current"), 0.01f);
            Assert.IsTrue(CallBoolMethod(caster.Cooldown, "IsReady", skill));
            Assert.AreEqual(0f, Convert.ToSingle(CallMethod(caster.Cooldown, "GetRemaining", skill)), 0.0001f);
        }

        [Test]
        public void HealthApplyDamage_OnDeadTarget_NoEffect()
        {
            var unit = CreateUnit("HealthDead");
            CallMethod(unit.Health, "SetCurrent", 0f);

            var applied = InvokeApplyDamageWithAbsorb(unit.Health, 50f, out var absorbed);

            Assert.AreEqual(0f, applied, 0.0001f);
            Assert.AreEqual(0f, absorbed, 0.0001f);
            Assert.AreEqual(0f, GetFloatProperty(unit.Health, "Current"), 0.0001f);
        }

        [Test]
        public void ProjectileIgnoresDeadTarget()
        {
            var projectileGo = Track(new GameObject("Projectile_DeadFilter"));
            var projectile = projectileGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));
            var definition = CreateProjectileDefinition(0.2f);
            CallMethod(
                projectile,
                "Initialize",
                definition,
                CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"),
                CreateDefaultValue("CombatSystem.Gameplay.CombatTarget"),
                Vector3.forward,
                null,
                null);

            var deadTarget = CreateUnit("ProjectileDeadTarget");
            CallMethod(deadTarget.Health, "SetCurrent", 0f);
            var deadCombatTarget = CreateCombatTarget(deadTarget.GameObject);

            var isValid = InvokePrivateIsValidTarget(projectile, deadCombatTarget);
            Assert.IsFalse(isValid);
        }

        [Test]
        public void TriggerBuffs_SnapshotIteration_NoDuplicateOrSkip()
        {
            CreateSystems();
            var unit = CreateUnit("SnapshotTarget");
            CallMethod(unit.Health, "SetCurrent", 100f);

            var addDamage = CreateDamageEffect(3f, "True");
            var newBuffDamage = CreateDamageEffect(7f, "True");
            var buffC = CreateBuffWithTrigger("OnSkillCast", newBuffDamage, 0f);
            var applyBuffC = CreateApplyBuffEffect(buffC);
            var buffA = CreateBuffWithTrigger("OnSkillCast", applyBuffC, 0f);
            var buffB = CreateBuffWithTrigger("OnSkillCast", addDamage, 0f);

            CallMethod(unit.Buffs, "ApplyBuff", buffA, unit.Unit);
            CallMethod(unit.Buffs, "ApplyBuff", buffB, unit.Unit);

            var selfTarget = CreateCombatTarget(unit.GameObject);
            var defaultContext = CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext");
            CallMethod(unit.Buffs, "NotifySkillCast", defaultContext, selfTarget);
            var firstDamage = 100f - GetFloatProperty(unit.Health, "Current");

            CallMethod(unit.Buffs, "NotifySkillCast", defaultContext, selfTarget);
            var secondDamage = (100f - GetFloatProperty(unit.Health, "Current")) - firstDamage;

            Assert.AreEqual(3f, firstDamage, 0.01f);
            Assert.AreEqual(10f, secondDamage, 0.01f);
        }

        [Test]
        public void ProjectileHitRadius_AppliedToRuntimeCollider()
        {
            var definition = CreateProjectileDefinition(1.25f);

            var noColliderGo = Track(new GameObject("Projectile_NoCollider"));
            var noColliderController = noColliderGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));
            CallMethod(
                noColliderController,
                "Initialize",
                definition,
                CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"),
                CreateDefaultValue("CombatSystem.Gameplay.CombatTarget"),
                Vector3.forward,
                null,
                null);

            var createdCollider = noColliderGo.GetComponent<SphereCollider>();
            Assert.NotNull(createdCollider);
            Assert.IsTrue(createdCollider.isTrigger);
            Assert.AreEqual(1.25f, createdCollider.radius, 0.0001f);

            var existingColliderGo = Track(new GameObject("Projectile_ExistingCollider"));
            var existingCollider = existingColliderGo.AddComponent<SphereCollider>();
            existingCollider.radius = 0.1f;
            var existingController = existingColliderGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));
            CallMethod(
                existingController,
                "Initialize",
                definition,
                CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"),
                CreateDefaultValue("CombatSystem.Gameplay.CombatTarget"),
                Vector3.forward,
                null,
                null);

            Assert.AreEqual(1.25f, existingCollider.radius, 0.0001f);
        }

        private UnitRig CreateUnit(string name)
        {
            var go = Track(new GameObject(name));
            var unit = go.AddComponent(RequireType("CombatSystem.Core.UnitRoot"));
            var stats = go.AddComponent(RequireType("CombatSystem.Core.StatsComponent"));
            var health = go.AddComponent(RequireType("CombatSystem.Core.HealthComponent"));
            var resource = go.AddComponent(RequireType("CombatSystem.Core.ResourceComponent"));
            var cooldown = go.AddComponent(RequireType("CombatSystem.Core.CooldownComponent"));
            var buffs = go.AddComponent(RequireType("CombatSystem.Core.BuffController"));
            var skillUser = go.AddComponent(RequireType("CombatSystem.Gameplay.SkillUserComponent"));
            var team = go.AddComponent(RequireType("CombatSystem.Core.TeamComponent"));

            CallMethod(team, "SetTeamId", 1);
            CallMethod(health, "Initialize");
            CallMethod(resource, "Initialize");

            return new UnitRig
            {
                GameObject = go,
                Unit = unit,
                Stats = stats,
                Health = health,
                Resource = resource,
                Cooldown = cooldown,
                Buffs = buffs,
                SkillUser = skillUser
            };
        }

        private void CreateSystems()
        {
            var go = Track(new GameObject("Systems"));
            go.AddComponent(RequireType("CombatSystem.Gameplay.EffectExecutor"));
        }

        private object CreateStat(string id)
        {
            var stat = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.StatDefinition")));
            SetPrivateField(stat, "id", id);
            SetPrivateField(stat, "defaultValue", 0f);
            SetPrivateField(stat, "minValue", -9999f);
            SetPrivateField(stat, "maxValue", 9999f);
            return stat;
        }

        private object CreateSkill(float resourceCost, float cooldown, object targeting)
        {
            var skill = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.SkillDefinition")));
            SetPrivateField(skill, "resourceCost", resourceCost);
            SetPrivateField(skill, "cooldown", cooldown);
            SetPrivateField(skill, "castTime", 0f);
            SetPrivateField(skill, "channelTime", 0f);
            SetPrivateField(skill, "postCastTime", 0f);
            SetPrivateField(skill, "gcdDuration", 0f);
            SetPrivateField(skill, "targeting", targeting);
            return skill;
        }

        private object CreateTargeting(bool requireExplicitTarget, bool allowEmpty, string hitValidationName)
        {
            var targeting = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.TargetingDefinition")));
            SetPrivateField(targeting, "mode", ParseEnum("CombatSystem.Data.TargetingMode", "Single"));
            SetPrivateField(targeting, "team", ParseEnum("CombatSystem.Data.TargetTeam", "Enemy"));
            SetPrivateField(targeting, "origin", ParseEnum("CombatSystem.Data.TargetingOrigin", "Caster"));
            SetPrivateField(targeting, "range", 10f);
            SetPrivateField(targeting, "radius", 0f);
            SetPrivateField(targeting, "maxTargets", 1);
            SetPrivateField(targeting, "includeSelf", false);
            SetPrivateField(targeting, "allowEmpty", allowEmpty);
            SetPrivateField(targeting, "requireExplicitTarget", requireExplicitTarget);
            SetPrivateField(targeting, "hitValidation", ParseEnum("CombatSystem.Data.HitValidationPolicy", hitValidationName));
            return targeting;
        }

        private object CreateDamageEffect(float value, string damageTypeName, object scalingStat = null, float scalingRatio = 0f)
        {
            var effect = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.EffectDefinition")));
            SetPrivateField(effect, "effectType", ParseEnum("CombatSystem.Data.EffectType", "Damage"));
            SetPrivateField(effect, "value", value);
            SetPrivateField(effect, "damageType", ParseEnum("CombatSystem.Data.DamageType", damageTypeName));
            SetPrivateField(effect, "scalingStat", scalingStat);
            SetPrivateField(effect, "scalingRatio", scalingRatio);
            SetPrivateField(effect, "canCrit", false);
            SetPrivateField(effect, "critChance", 0f);
            SetPrivateField(effect, "critMultiplier", 1f);
            SetPrivateField(effect, "triggersOnHit", false);
            return effect;
        }

        private object CreateApplyBuffEffect(object buff)
        {
            var effect = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.EffectDefinition")));
            SetPrivateField(effect, "effectType", ParseEnum("CombatSystem.Data.EffectType", "ApplyBuff"));
            SetPrivateField(effect, "buff", buff);
            return effect;
        }

        private object CreateBuffWithTrigger(string triggerTypeName, object effect, float tickInterval)
        {
            var triggerType = RequireType("CombatSystem.Data.BuffTrigger");
            var trigger = Activator.CreateInstance(triggerType);
            SetField(trigger, "triggerType", ParseEnum("CombatSystem.Data.BuffTriggerType", triggerTypeName));
            SetField(trigger, "chance", 1f);
            SetField(trigger, "effects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition"), effect));

            var buff = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.BuffDefinition")));
            SetPrivateField(buff, "duration", 10f);
            SetPrivateField(buff, "tickInterval", tickInterval);
            SetPrivateField(buff, "stackingRule", ParseEnum("CombatSystem.Data.BuffStackingRule", "Refresh"));
            SetPrivateField(buff, "maxStacks", 1);
            SetPrivateField(buff, "modifiers", CreateTypedList(RequireType("CombatSystem.Data.ModifierDefinition")));
            SetPrivateField(buff, "triggers", CreateTypedList(triggerType, trigger));
            return buff;
        }

        private object CreateBuffWithModifier(object modifier)
        {
            var buff = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.BuffDefinition")));
            SetPrivateField(buff, "duration", 10f);
            SetPrivateField(buff, "tickInterval", 0f);
            SetPrivateField(buff, "stackingRule", ParseEnum("CombatSystem.Data.BuffStackingRule", "Refresh"));
            SetPrivateField(buff, "maxStacks", 1);
            SetPrivateField(buff, "modifiers", CreateTypedList(RequireType("CombatSystem.Data.ModifierDefinition"), modifier));
            SetPrivateField(buff, "triggers", CreateTypedList(RequireType("CombatSystem.Data.BuffTrigger")));
            return buff;
        }

        private object CreateSkillCostModifier(float value)
        {
            var modifier = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.ModifierDefinition")));
            SetPrivateField(modifier, "target", ParseEnum("CombatSystem.Data.ModifierTargetType", "Skill"));
            SetPrivateField(modifier, "scope", ParseEnum("CombatSystem.Data.ModifierScope", "Caster"));
            SetPrivateField(modifier, "parameterId", "Skill.ResourceCost");
            SetPrivateField(modifier, "operation", ParseEnum("CombatSystem.Data.ModifierOperation", "Add"));
            SetPrivateField(modifier, "value", value);
            return modifier;
        }

        private object CreateProjectileDefinition(float hitRadius)
        {
            var projectile = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.ProjectileDefinition")));
            SetPrivateField(projectile, "speed", 10f);
            SetPrivateField(projectile, "lifetime", 1f);
            SetPrivateField(projectile, "hitRadius", hitRadius);
            SetPrivateField(projectile, "pierce", false);
            SetPrivateField(projectile, "maxPierce", 1);
            return projectile;
        }

        private object CreateCombatTarget(GameObject go)
        {
            var type = RequireType("CombatSystem.Gameplay.CombatTarget");
            var tryCreate = type.GetMethod("TryCreate", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(tryCreate);

            var args = new object[] { go, Activator.CreateInstance(type) };
            var ok = tryCreate.Invoke(null, args);
            Assert.IsTrue(ok is bool value && value);
            return args[1];
        }

        private object CreateDefaultValue(string typeName)
        {
            return Activator.CreateInstance(RequireType(typeName));
        }

        private void InvokePrivateTriggerBuff(object buffController, object buffInstance, string triggerTypeName)
        {
            var method = buffController.GetType().GetMethod("TriggerBuff", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(
                buffController,
                new[]
                {
                    buffInstance,
                    ParseEnum("CombatSystem.Data.BuffTriggerType", triggerTypeName),
                    CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"),
                    CreateDefaultValue("CombatSystem.Gameplay.CombatTarget")
                });
        }

        private bool InvokePrivateIsValidTarget(object projectileController, object target)
        {
            var method = projectileController.GetType().GetMethod("IsValidTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = method.Invoke(projectileController, new[] { target });
            return result is bool value && value;
        }

        private static float InvokeApplyDamageWithAbsorb(object health, float amount, out float absorbed)
        {
            var method = health.GetType().GetMethod("ApplyDamage", new[] { typeof(float), typeof(float).MakeByRefType() });
            Assert.NotNull(method);
            var args = new object[] { amount, 0f };
            var applied = method.Invoke(health, args);
            absorbed = Convert.ToSingle(args[1]);
            return Convert.ToSingle(applied);
        }

        private static object CreateTypedList(Type elementType, params object[] entries)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType) as IList;
            Assert.NotNull(list);

            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    list.Add(entries[i]);
                }
            }

            return list;
        }

        private static object ParseEnum(string enumTypeName, string name)
        {
            var type = RequireType(enumTypeName);
            return Enum.Parse(type, name);
        }

        private static Type RequireType(string typeName)
        {
            var type = GetRuntimeType(typeName);
            Assert.IsNotNull(type, $"Runtime type not found: {typeName}");
            return type;
        }

        private static Type GetRuntimeType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            return type ?? Type.GetType(typeName);
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

        private static object GetPrivateFieldValue(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }

            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            while (field == null && type.BaseType != null)
            {
                type = type.BaseType;
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            return field.GetValue(target);
        }

        private static object GetFieldValue(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }

            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {type.Name}");
            return field.GetValue(target);
        }

        private static float GetFloatProperty(object target, string memberName)
        {
            return Convert.ToSingle(GetPropertyValue(target, memberName));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
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

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

            Assert.Fail($"Method '{methodName}' not found on {target.GetType().Name} for {safeArgs.Length} arguments.");
            return null;
        }

        private static bool CallBoolMethod(object target, string methodName, params object[] args)
        {
            var result = CallMethod(target, methodName, args);
            return result is bool value && value;
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

        private T Track<T>(T instance) where T : UnityEngine.Object
        {
            cleanup.Add(instance);
            return instance;
        }

        private sealed class UnitRig
        {
            public GameObject GameObject;
            public Component Unit;
            public Component Stats;
            public Component Health;
            public Component Resource;
            public Component Cooldown;
            public Component Buffs;
            public Component SkillUser;
        }
    }
}
