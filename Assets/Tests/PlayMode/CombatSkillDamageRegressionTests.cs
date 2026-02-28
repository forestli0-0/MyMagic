using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CombatSystem.Tests
{
    public class CombatSkillDamageRegressionTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>(64);
        private Component effectExecutorSystem;
        private Component targetingSystem;

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
            effectExecutorSystem = null;
            targetingSystem = null;
        }

        [Test]
        public void CombatStateEffect_AddRemoveUntargetable_ViaBuffTriggers()
        {
            CreateSystems();
            var target = CreateUnit("CombatState_Untargetable_Target");
            var addUntargetable = CreateCombatStateEffect("AddFlags", "Untargetable");
            var removeUntargetable = CreateCombatStateEffect("RemoveFlags", "Untargetable");
            var buff = CreateBuffWithApplyAndExpire(addUntargetable, removeUntargetable, 1f);

            CallMethod(target.Buffs, "ApplyBuff", buff, target.Unit);
            Assert.IsTrue(CallBoolMethod(target.State, "HasFlag", ParseEnum("CombatSystem.Data.CombatStateFlags", "Untargetable")));

            CallMethod(target.Buffs, "RemoveBuff", buff);
            Assert.IsFalse(CallBoolMethod(target.State, "HasFlag", ParseEnum("CombatSystem.Data.CombatStateFlags", "Untargetable")));
        }

        [Test]
        public void CombatStateEffect_AddRemoveInvulnerable_ViaBuffTriggers()
        {
            CreateSystems();
            var caster = CreateUnit("CombatState_Invulnerable_Caster");
            var target = CreateUnit("CombatState_Invulnerable_Target");
            CallMethod(target.Health, "SetCurrent", 100f);

            var addInvulnerable = CreateCombatStateEffect("AddFlags", "Invulnerable");
            var removeInvulnerable = CreateCombatStateEffect("RemoveFlags", "Invulnerable");
            var buff = CreateBuffWithApplyAndExpire(addInvulnerable, removeInvulnerable, 1f);
            CallMethod(target.Buffs, "ApplyBuff", buff, caster.Unit);

            var damage = CreateDamageEffect(20f, "True");
            var targetData = CreateCombatTarget(target.GameObject);
            var trigger = ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart");

            CallMethod(effectExecutorSystem, "ExecuteEffect", damage, CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"), targetData, trigger);
            Assert.AreEqual(100f, GetFloatProperty(target.Health, "Current"), 0.01f);

            CallMethod(target.Buffs, "RemoveBuff", buff);
            CallMethod(effectExecutorSystem, "ExecuteEffect", damage, CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"), targetData, trigger);
            Assert.AreEqual(80f, GetFloatProperty(target.Health, "Current"), 0.01f);
        }

        [Test]
        public void CombatStateEffect_GrantSpellShield_FromSkill()
        {
            CreateSystems();
            var target = CreateUnit("CombatState_SpellShield_Target");
            CallMethod(target.Health, "SetCurrent", 100f);

            var grantShield = CreateCombatStateEffect("GrantSpellShield", "None", 1);
            var damage = CreateDamageEffect(20f, "True");
            var targetData = CreateCombatTarget(target.GameObject);
            var trigger = ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart");

            CallMethod(effectExecutorSystem, "ExecuteEffect", grantShield, CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"), targetData, trigger);
            Assert.AreEqual(1, Convert.ToInt32(GetPropertyValue(target.State, "SpellShieldCharges")));
            Assert.IsTrue(CallBoolMethod(target.State, "HasFlag", ParseEnum("CombatSystem.Data.CombatStateFlags", "SpellShielded")));

            CallMethod(effectExecutorSystem, "ExecuteEffect", damage, CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"), targetData, trigger);
            Assert.AreEqual(100f, GetFloatProperty(target.Health, "Current"), 0.01f);
            Assert.AreEqual(0, Convert.ToInt32(GetPropertyValue(target.State, "SpellShieldCharges")));

            CallMethod(effectExecutorSystem, "ExecuteEffect", damage, CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"), targetData, trigger);
            Assert.AreEqual(80f, GetFloatProperty(target.Health, "Current"), 0.01f);
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

        [Test]
        public void Untargetable_TargetingAndProjectileBlocked()
        {
            CreateSystems();
            var caster = CreateUnit("UntargetableCaster");
            var target = CreateUnit("UntargetableTarget");
            CallMethod(caster.Team, "SetTeamId", 1);
            CallMethod(target.Team, "SetTeamId", 2);
            CallMethod(target.State, "AddFlag", ParseEnum("CombatSystem.Data.CombatStateFlags", "Untargetable"));

            var targeting = CreateTargeting(true, false, "AliveOnly");
            var targetData = CreateCombatTarget(target.GameObject);
            var valid = CallBoolMethod(targetingSystem, "IsValidTarget", caster.Unit, targeting, targetData, false);
            Assert.IsFalse(valid);

            var projectileGo = Track(new GameObject("Projectile_UntargetableFilter"));
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
                targetingSystem);

            var canHit = InvokePrivateIsValidTarget(projectile, targetData);
            Assert.IsFalse(canHit);
        }

        [Test]
        public void Invulnerable_DamageAndOnHitSuppressed()
        {
            CreateSystems();
            var caster = CreateUnit("InvulCaster");
            var target = CreateUnit("InvulTarget");
            CallMethod(target.Health, "SetCurrent", 100f);
            CallMethod(target.State, "AddFlag", ParseEnum("CombatSystem.Data.CombatStateFlags", "Invulnerable"));

            var damage = CreateDamageEffect(35f, "True");
            var targetData = CreateCombatTarget(target.GameObject);
            CallMethod(
                effectExecutorSystem,
                "ExecuteEffect",
                damage,
                CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"),
                targetData,
                ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart"));

            Assert.AreEqual(100f, GetFloatProperty(target.Health, "Current"), 0.01f);
        }

        [Test]
        public void SpellShield_ConsumesFirstSpell_SecondSpellApplies()
        {
            CreateSystems();
            var target = CreateUnit("SpellShieldTarget");
            CallMethod(target.Health, "SetCurrent", 100f);
            CallMethod(target.State, "GrantSpellShield", 1);

            var damage = CreateDamageEffect(20f, "True");
            var targetData = CreateCombatTarget(target.GameObject);
            var trigger = ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart");

            CallMethod(effectExecutorSystem, "ExecuteEffect", damage, CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"), targetData, trigger);
            Assert.AreEqual(100f, GetFloatProperty(target.Health, "Current"), 0.01f);
            Assert.AreEqual(0, Convert.ToInt32(GetPropertyValue(target.State, "SpellShieldCharges")));

            CallMethod(effectExecutorSystem, "ExecuteEffect", damage, CreateDefaultValue("CombatSystem.Gameplay.SkillRuntimeContext"), targetData, trigger);
            Assert.AreEqual(80f, GetFloatProperty(target.Health, "Current"), 0.01f);
        }

        [UnityTest]
        public IEnumerator AmmoSkill_ConsumeAndRechargeDeterministic()
        {
            var caster = CreateUnit("AmmoCaster");
            var skill = CreateSkill(0f, 0f, null);
            SetPrivateField(skill, "ammoConfig", CreateAmmoConfig(true, 2, 2, 0.05f));

            Assert.AreEqual(2, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentAmmo", skill)));
            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.IsFalse(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));

            yield return new WaitForSeconds(0.07f);
            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
        }

        [UnityTest]
        public IEnumerator RecastSkill_WindowValidAndTimeoutReset()
        {
            var caster = CreateUnit("RecastCaster");
            var skill = CreateSkill(0f, 0.5f, null);
            SetPrivateField(skill, "recastConfig", CreateRecastConfig(true, 1, 0.1f, false, true, "AnyValid"));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "HasActiveRecast", skill));
            Assert.IsTrue(CallBoolMethod(caster.Cooldown, "IsReady", skill));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.IsFalse(CallBoolMethod(caster.SkillUser, "HasActiveRecast", skill));
            Assert.IsFalse(CallBoolMethod(caster.Cooldown, "IsReady", skill));

            yield return new WaitForSeconds(0.12f);
            Assert.IsFalse(CallBoolMethod(caster.SkillUser, "HasActiveRecast", skill));
        }

        [Test]
        public void SampleSkill_ReturnAndSplitProjectile_BehaviorWorks()
        {
            CreateSystems();
            var caster = CreateUnit("ProjectileBehaviorCaster");
            var target = CreateUnit("ProjectileBehaviorTarget");
            var context = CreateRuntimeContext(caster);
            var targetData = CreateCombatTarget(target.GameObject);

            var returnDefinition = CreateProjectileDefinition(0.2f);
            SetPrivateField(returnDefinition, "behaviorType", ParseEnum("CombatSystem.Data.ProjectileBehaviorType", "Return"));
            SetPrivateField(returnDefinition, "returnSpeedMultiplier", 1.2f);

            var returnProjectileGo = Track(new GameObject("Projectile_ReturnBehavior"));
            returnProjectileGo.AddComponent<SphereCollider>().isTrigger = true;
            var returnProjectile = returnProjectileGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));
            CallMethod(returnProjectile, "Initialize", returnDefinition, context, targetData, Vector3.forward, null, targetingSystem);
            var handled = InvokePrivateBoolMethod(returnProjectile, "HandlePostHitBehavior", targetData);
            Assert.IsTrue(handled);
            Assert.IsTrue(Convert.ToBoolean(GetPrivateFieldValue(returnProjectile, "returning")));

            var splitDefinition = CreateProjectileDefinition(0.2f);
            SetPrivateField(splitDefinition, "behaviorType", ParseEnum("CombatSystem.Data.ProjectileBehaviorType", "Split"));
            SetPrivateField(splitDefinition, "splitCount", 3);
            SetPrivateField(splitDefinition, "maxSplitDepth", 1);
            SetPrivateField(splitDefinition, "onHitEffects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition")));

            var splitPrefab = Track(new GameObject("Projectile_SplitPrefab"));
            splitPrefab.AddComponent<SphereCollider>().isTrigger = true;
            splitPrefab.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));
            SetPrivateField(splitDefinition, "prefab", splitPrefab);

            var poolGo = Track(new GameObject("ProjectilePool"));
            var pool = poolGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectilePool"));

            var splitProjectileGo = Track(new GameObject("Projectile_SplitBehavior"));
            splitProjectileGo.AddComponent<SphereCollider>().isTrigger = true;
            var splitProjectile = splitProjectileGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));
            CallMethod(splitProjectile, "SetPool", pool, splitPrefab);
            CallMethod(splitProjectile, "Initialize", splitDefinition, context, CreateDefaultValue("CombatSystem.Gameplay.CombatTarget"), Vector3.forward, null, targetingSystem);

            InvokePrivateMethod(splitProjectile, "SpawnSplitProjectiles");
            Assert.GreaterOrEqual(Convert.ToInt32(GetPropertyValue(pool, "TotalCreated")), 3);
        }

        [Test]
        public void Stealth_NotVisibleCannotBeTargeted()
        {
            CreateSystems();
            var caster = CreateUnit("StealthCaster");
            var target = CreateUnit("StealthTarget");
            CallMethod(caster.Team, "SetTeamId", 1);
            CallMethod(target.Team, "SetTeamId", 2);
            CallMethod(target.Visibility, "AddConcealment", true);

            var targeting = CreateTargeting(true, false, "AliveOnly");
            var targetData = CreateCombatTarget(target.GameObject);
            var visible = CallBoolMethod(targetingSystem, "IsValidTarget", caster.Unit, targeting, targetData, false);
            Assert.IsFalse(visible);
        }

        [Test]
        public void Reveal_MakesStealthTargetableWithinDuration()
        {
            CreateSystems();
            var caster = CreateUnit("RevealCaster");
            var target = CreateUnit("RevealTarget");
            CallMethod(caster.Team, "SetTeamId", 1);
            CallMethod(target.Team, "SetTeamId", 2);
            CallMethod(target.Visibility, "AddConcealment", true);
            CallMethod(target.Visibility, "RevealToTeam", 1, 0.5f);

            var targeting = CreateTargeting(true, false, "AliveOnly");
            var targetData = CreateCombatTarget(target.GameObject);
            var visible = CallBoolMethod(targetingSystem, "IsValidTarget", caster.Unit, targeting, targetData, false);
            Assert.IsTrue(visible);
        }

        [Test]
        public void WindWall_InterceptsEnemyProjectilesOnly()
        {
            var wallGo = Track(new GameObject("WindWall"));
            var wallTeam = wallGo.AddComponent(RequireType("CombatSystem.Core.TeamComponent"));
            CallMethod(wallTeam, "SetTeamId", 1);
            wallGo.AddComponent<BoxCollider>().isTrigger = true;
            var wall = wallGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileInterceptorVolume"));
            SetPrivateField(wall, "team", wallTeam);

            var allyCaster = CreateUnit("WindWallAlly");
            var enemyCaster = CreateUnit("WindWallEnemy");
            CallMethod(allyCaster.Team, "SetTeamId", 1);
            CallMethod(enemyCaster.Team, "SetTeamId", 2);

            Assert.IsFalse(CallBoolMethod(wall, "ShouldIntercept", allyCaster.Unit));
            Assert.IsTrue(CallBoolMethod(wall, "ShouldIntercept", enemyCaster.Unit));
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
            var state = go.AddComponent(RequireType("CombatSystem.Core.CombatStateComponent"));
            var visibility = go.AddComponent(RequireType("CombatSystem.Gameplay.VisibilityComponent"));
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
                State = state,
                Visibility = visibility,
                Team = team,
                SkillUser = skillUser
            };
        }

        private void CreateSystems()
        {
            var go = Track(new GameObject("Systems"));
            targetingSystem = go.AddComponent(RequireType("CombatSystem.Gameplay.TargetingSystem"));
            effectExecutorSystem = go.AddComponent(RequireType("CombatSystem.Gameplay.EffectExecutor"));
            go.AddComponent(RequireType("CombatSystem.Gameplay.HitResolutionSystem"));
            go.AddComponent(RequireType("CombatSystem.Gameplay.VisionSystem"));
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

        private object CreateAmmoConfig(bool enabled, int maxCharges, int initialCharges, float rechargeTime)
        {
            var type = RequireType("CombatSystem.Data.SkillAmmoConfig");
            var config = Activator.CreateInstance(type);
            SetPrivateField(config, "enabled", enabled);
            SetPrivateField(config, "maxCharges", maxCharges);
            SetPrivateField(config, "initialCharges", initialCharges);
            SetPrivateField(config, "rechargeTime", rechargeTime);
            return config;
        }

        private object CreateRecastConfig(
            bool enabled,
            int maxRecasts,
            float recastWindow,
            bool consumesResourceOnRecast,
            bool delayCooldownUntilRecastEnds,
            string targetPolicy)
        {
            var type = RequireType("CombatSystem.Data.SkillRecastConfig");
            var config = Activator.CreateInstance(type);
            SetPrivateField(config, "enabled", enabled);
            SetPrivateField(config, "maxRecasts", maxRecasts);
            SetPrivateField(config, "recastWindow", recastWindow);
            SetPrivateField(config, "consumesResourceOnRecast", consumesResourceOnRecast);
            SetPrivateField(config, "delayCooldownUntilRecastEnds", delayCooldownUntilRecastEnds);
            SetPrivateField(config, "targetPolicy", ParseEnum("CombatSystem.Data.RecastTargetPolicy", targetPolicy));
            return config;
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

        private object CreateCombatStateEffect(string modeName, string flagsName, int spellShieldCharges = 0)
        {
            var effect = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.EffectDefinition")));
            SetPrivateField(effect, "effectType", ParseEnum("CombatSystem.Data.EffectType", "CombatState"));
            SetPrivateField(effect, "combatStateMode", ParseEnum("CombatSystem.Data.CombatStateEffectMode", modeName));
            SetPrivateField(effect, "combatStateFlags", ParseEnum("CombatSystem.Data.CombatStateFlags", flagsName));
            SetPrivateField(effect, "spellShieldCharges", spellShieldCharges);
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

        private object CreateBuffWithApplyAndExpire(object onApplyEffect, object onExpireEffect, float duration)
        {
            var triggerType = RequireType("CombatSystem.Data.BuffTrigger");
            var onApply = Activator.CreateInstance(triggerType);
            SetField(onApply, "triggerType", ParseEnum("CombatSystem.Data.BuffTriggerType", "OnApply"));
            SetField(onApply, "chance", 1f);
            SetField(onApply, "effects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition"), onApplyEffect));

            var onExpire = Activator.CreateInstance(triggerType);
            SetField(onExpire, "triggerType", ParseEnum("CombatSystem.Data.BuffTriggerType", "OnExpire"));
            SetField(onExpire, "chance", 1f);
            SetField(onExpire, "effects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition"), onExpireEffect));

            var buff = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.BuffDefinition")));
            SetPrivateField(buff, "duration", duration);
            SetPrivateField(buff, "tickInterval", 0f);
            SetPrivateField(buff, "stackingRule", ParseEnum("CombatSystem.Data.BuffStackingRule", "Refresh"));
            SetPrivateField(buff, "maxStacks", 1);
            SetPrivateField(buff, "modifiers", CreateTypedList(RequireType("CombatSystem.Data.ModifierDefinition")));
            SetPrivateField(buff, "triggers", CreateTypedList(triggerType, onApply, onExpire));
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

        private object CreateRuntimeContext(UnitRig caster)
        {
            var contextType = RequireType("CombatSystem.Gameplay.SkillRuntimeContext");
            var args = new object[]
            {
                caster != null ? caster.SkillUser : null,
                caster != null ? caster.Unit : null,
                null,
                null,
                targetingSystem,
                effectExecutorSystem,
                false,
                Vector3.zero,
                Vector3.forward,
                null,
                0f,
                0f,
                1f
            };

            return Activator.CreateInstance(contextType, args);
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

        private static object InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Private method '{methodName}' not found on {target.GetType().Name}");
            return method.Invoke(target, args);
        }

        private static bool InvokePrivateBoolMethod(object target, string methodName, params object[] args)
        {
            var result = InvokePrivateMethod(target, methodName, args);
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
            public Component State;
            public Component Visibility;
            public Component Team;
            public Component SkillUser;
        }
    }
}
