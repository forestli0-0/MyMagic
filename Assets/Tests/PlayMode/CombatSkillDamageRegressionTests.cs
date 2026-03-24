using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
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
        public void TryCast_CastConstraintBlocksCastAndSetsFailReason()
        {
            var caster = CreateUnit("Caster_CastConstraint");
            CallMethod(caster.Resource, "SetCurrent", 50f);

            var skill = CreateSkill(0f, 0f, null);
            var markerModifier = CreateSkillCostModifier(0f);
            var markerBuff = CreateBuffWithModifier(markerModifier);
            CallMethod(caster.Buffs, "ApplyBuff", markerBuff, caster.Unit);

            var notHasBuff = CreateCondition("NotHasBuff", "Caster", markerBuff);
            var castConstraint = CreateCastConstraint(notHasBuff, "CastConstraintFailed");
            SetPrivateField(
                skill,
                "castConstraints",
                CreateTypedList(RequireType("CombatSystem.Data.SkillCastConstraint"), castConstraint));

            Assert.IsFalse(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.AreEqual(
                ParseEnum("CombatSystem.Data.SkillCastFailReason", "CastConstraintFailed"),
                GetPropertyValue(caster.SkillUser, "LastCastFailReason"));
            Assert.AreEqual(50f, GetFloatProperty(caster.Resource, "Current"), 0.01f);

            CallMethod(caster.Buffs, "RemoveBuff", markerBuff);
            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.AreEqual(
                ParseEnum("CombatSystem.Data.SkillCastFailReason", "None"),
                GetPropertyValue(caster.SkillUser, "LastCastFailReason"));
        }

        [UnityTest]
        public IEnumerator TryCast_SpellShieldStopsRemainingStepEffects()
        {
            CreateSystems();
            var caster = CreateUnit("Caster_SpellShieldStep");
            var target = CreateUnit("Target_SpellShieldStep");
            CallMethod(caster.Team, "SetTeamId", 1);
            CallMethod(target.Team, "SetTeamId", 2);
            caster.GameObject.transform.position = Vector3.zero;
            target.GameObject.transform.position = new Vector3(2f, 0f, 0f);
            CallMethod(target.Health, "SetCurrent", 100f);
            CallMethod(target.State, "GrantSpellShield", 1);

            var markerModifier = CreateSkillCostModifier(0f);
            var markerBuff = CreateBuffWithModifier(markerModifier);

            var damage = CreateDamageEffect(20f, "True");
            var applyBuff = CreateApplyBuffEffect(markerBuff);

            var stepType = RequireType("CombatSystem.Data.SkillStep");
            var step = Activator.CreateInstance(stepType);
            SetField(step, "trigger", ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart"));
            SetField(step, "delay", 0f);
            SetField(step, "condition", null);
            SetField(step, "effects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition"), damage, applyBuff));
            SetField(step, "presentationCues", CreateTypedList(RequireType("CombatSystem.Data.SkillPresentationCue")));

            var targeting = CreateTargeting(true, false, "AliveOnly");
            var skill = CreateSkill(0f, 0f, targeting);
            SetPrivateField(skill, "steps", CreateTypedList(stepType, step));

            var firstCast = CallBoolMethod(caster.SkillUser, "TryCast", skill, target.GameObject);
            Assert.IsTrue(firstCast, $"First cast failed: {GetPropertyValue(caster.SkillUser, "LastCastFailReason")}");
            yield return null;
            Assert.AreEqual(100f, GetFloatProperty(target.Health, "Current"), 0.01f);
            Assert.IsFalse(CallBoolMethod(target.Buffs, "HasBuff", markerBuff));
            Assert.AreEqual(0, Convert.ToInt32(GetPropertyValue(target.State, "SpellShieldCharges")));

            var secondCast = CallBoolMethod(caster.SkillUser, "TryCast", skill, target.GameObject);
            Assert.IsTrue(secondCast, $"Second cast failed: {GetPropertyValue(caster.SkillUser, "LastCastFailReason")}");
            yield return null;
            Assert.AreEqual(80f, GetFloatProperty(target.Health, "Current"), 0.01f);
            Assert.IsTrue(CallBoolMethod(target.Buffs, "HasBuff", markerBuff));
        }

        [Test]
        public void CombatAI_IsRuleValid_UsesExplicitTargetForCanCast()
        {
            CreateSystems();
            var caster = CreateUnit("AI_CastRule_Caster");
            var target = CreateUnit("AI_CastRule_Target");

            var markerModifier = CreateSkillCostModifier(0f);
            var markerBuff = CreateBuffWithModifier(markerModifier);
            CallMethod(target.Buffs, "ApplyBuff", markerBuff, caster.Unit);

            var condition = CreateCondition("HasBuff", "Target", markerBuff);
            var castConstraint = CreateCastConstraint(condition, "CastConstraintFailed");

            var targeting = CreateTargeting(true, false, "AliveOnly");
            var skill = CreateSkill(0f, 0f, targeting);
            SetPrivateField(
                skill,
                "castConstraints",
                CreateTypedList(RequireType("CombatSystem.Data.SkillCastConstraint"), castConstraint));

            var ruleType = RequireType("CombatSystem.Data.AISkillRule");
            var rule = Activator.CreateInstance(ruleType);
            SetField(rule, "skill", skill);
            SetField(rule, "minRange", 0f);
            SetField(rule, "maxRange", 10f);
            SetField(rule, "weight", 1f);
            SetField(rule, "allowWhileMoving", true);
            SetField(rule, "condition", null);

            var aiController = caster.GameObject.AddComponent(RequireType("CombatSystem.AI.CombatAIController"));
            SetPrivateField(aiController, "unitRoot", caster.Unit);
            SetPrivateField(aiController, "skillUser", caster.SkillUser);
            SetPrivateField(aiController, "health", caster.Health);
            SetPrivateField(aiController, "team", caster.Team);
            SetPrivateField(aiController, "targetingSystem", targetingSystem);
            SetPrivateField(aiController, "currentTarget", CreateCombatTarget(target.GameObject));
            SetPrivateField(aiController, "hasTarget", true);

            Assert.IsTrue(InvokePrivateBoolMethod(aiController, "IsRuleValid", rule, 2f));

            CallMethod(target.Buffs, "RemoveBuff", markerBuff);
            Assert.IsFalse(InvokePrivateBoolMethod(aiController, "IsRuleValid", rule, 2f));
        }

        [UnityTest]
        public IEnumerator SkillUser_OnDisable_ClearsPendingStepsAndPreventsDelayedExecution()
        {
            CreateSystems();
            var caster = CreateUnit("DisablePending_Caster");
            CallMethod(caster.Health, "SetCurrent", 100f);

            var targeting = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.TargetingDefinition")));
            SetPrivateField(targeting, "mode", ParseEnum("CombatSystem.Data.TargetingMode", "Self"));
            SetPrivateField(targeting, "team", ParseEnum("CombatSystem.Data.TargetTeam", "Self"));
            SetPrivateField(targeting, "origin", ParseEnum("CombatSystem.Data.TargetingOrigin", "Caster"));
            SetPrivateField(targeting, "range", 0f);
            SetPrivateField(targeting, "radius", 0f);
            SetPrivateField(targeting, "maxTargets", 1);
            SetPrivateField(targeting, "includeSelf", true);
            SetPrivateField(targeting, "allowEmpty", true);
            SetPrivateField(targeting, "requireExplicitTarget", false);
            SetPrivateField(targeting, "hitValidation", ParseEnum("CombatSystem.Data.HitValidationPolicy", "None"));

            var damage = CreateDamageEffect(10f, "True");
            var stepType = RequireType("CombatSystem.Data.SkillStep");
            var step = Activator.CreateInstance(stepType);
            SetField(step, "trigger", ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart"));
            SetField(step, "delay", 0.05f);
            SetField(step, "condition", null);
            SetField(step, "effects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition"), damage));
            SetField(step, "presentationCues", CreateTypedList(RequireType("CombatSystem.Data.SkillPresentationCue")));

            var skill = CreateSkill(0f, 0f, targeting);
            SetPrivateField(skill, "steps", CreateTypedList(stepType, step));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            var pending = GetPrivateFieldValue(caster.SkillUser, "pendingSteps") as IList;
            Assert.NotNull(pending);
            Assert.Greater(pending.Count, 0);

            var behaviour = caster.SkillUser as Behaviour;
            Assert.NotNull(behaviour);
            behaviour.enabled = false;

            pending = GetPrivateFieldValue(caster.SkillUser, "pendingSteps") as IList;
            Assert.NotNull(pending);
            Assert.AreEqual(0, pending.Count);

            yield return new WaitForSeconds(0.08f);
            behaviour.enabled = true;
            yield return null;

            Assert.AreEqual(100f, GetFloatProperty(caster.Health, "Current"), 0.01f);
        }

        [Test]
        public void MoveEffect_ThroughExplicitTarget_MovesPastTarget()
        {
            CreateSystems();
            var caster = CreateUnit("MovePolicyCaster");
            var target = CreateUnit("MovePolicyTarget");
            caster.GameObject.transform.position = Vector3.zero;
            target.GameObject.transform.position = new Vector3(2f, 0f, 0f);

            var move = CreateMoveEffect(0.5f, 0f, "Dash", "ThroughExplicitTarget", "Default", 1f);
            var trigger = ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart");
            var casterTarget = CreateCombatTarget(caster.GameObject);
            var context = CreateRuntimeContext(
                caster,
                null,
                0UL,
                -1,
                null,
                target.GameObject,
                false,
                Vector3.zero,
                Vector3.right,
                1);

            CallMethod(effectExecutorSystem, "ExecuteEffect", move, context, casterTarget, trigger);
            Assert.Greater(caster.GameObject.transform.position.x, 2.5f);
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

        [UnityTest]
        public IEnumerator SequenceSkill_AutoAdvanceAndTimeoutReset()
        {
            var caster = CreateUnit("SequenceCaster");
            var skill = CreateSkill(0f, 0f, null);
            SetPrivateField(skill, "sequenceConfig", CreateSequenceConfig(true, 3, 0.12f, "LoopToStart", false));

            Assert.AreEqual(1, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentSequencePhase", skill)));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.AreEqual(2, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentSequencePhase", skill)));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.AreEqual(3, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentSequencePhase", skill)));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.AreEqual(1, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentSequencePhase", skill)));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            Assert.AreEqual(2, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentSequencePhase", skill)));

            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(1, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentSequencePhase", skill)));
        }

        [Test]
        public void SequenceSkill_ResetOnOtherSkillCast_Works()
        {
            var caster = CreateUnit("SequenceResetCaster");
            var sequenceSkill = CreateSkill(0f, 0f, null);
            var otherSkill = CreateSkill(0f, 0f, null);
            SetPrivateField(sequenceSkill, "sequenceConfig", CreateSequenceConfig(true, 3, 1f, "LoopToStart", true));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", sequenceSkill, null));
            Assert.AreEqual(2, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentSequencePhase", sequenceSkill)));

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", otherSkill, null));
            Assert.AreEqual(1, Convert.ToInt32(CallMethod(caster.SkillUser, "GetCurrentSequencePhase", sequenceSkill)));
        }

        [UnityTest]
        public IEnumerator SkillStepEvent_DispatchedWithCastId()
        {
            CreateSystems();
            var caster = CreateUnit("StepEventCaster");
            var eventHub = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Core.CombatEventHub")));
            SetPrivateField(caster.Unit, "eventHub", eventHub);
            SetPrivateField(caster.SkillUser, "eventHub", eventHub);
            SetPrivateField(caster.SkillUser, "targetingSystem", targetingSystem);
            SetPrivateField(caster.SkillUser, "effectExecutor", effectExecutorSystem);

            var targeting = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.TargetingDefinition")));
            SetPrivateField(targeting, "mode", ParseEnum("CombatSystem.Data.TargetingMode", "Self"));
            SetPrivateField(targeting, "team", ParseEnum("CombatSystem.Data.TargetTeam", "Self"));
            SetPrivateField(targeting, "origin", ParseEnum("CombatSystem.Data.TargetingOrigin", "Caster"));
            SetPrivateField(targeting, "range", 0f);
            SetPrivateField(targeting, "radius", 0f);
            SetPrivateField(targeting, "maxTargets", 1);
            SetPrivateField(targeting, "includeSelf", true);
            SetPrivateField(targeting, "allowEmpty", true);
            SetPrivateField(targeting, "requireExplicitTarget", false);
            SetPrivateField(targeting, "hitValidation", ParseEnum("CombatSystem.Data.HitValidationPolicy", "None"));

            var stepType = RequireType("CombatSystem.Data.SkillStep");
            var step = Activator.CreateInstance(stepType);
            SetField(step, "trigger", ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart"));
            SetField(step, "delay", 0f);
            SetField(step, "effects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition")));
            SetField(step, "presentationCues", CreateTypedList(RequireType("CombatSystem.Data.SkillPresentationCue")));

            var skill = CreateSkill(0f, 0f, targeting);
            SetPrivateField(skill, "steps", CreateTypedList(stepType, step));
            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryAddSkill", skill, -1));

            var eventCount = 0;
            ulong castId = 0UL;
            int stepIndex = -1;
            var subscription = AddEventListener(eventHub, "SkillStepExecuted", evt =>
            {
                eventCount++;
                castId = Convert.ToUInt64(GetFieldValue(evt, "CastId"));
                stepIndex = Convert.ToInt32(GetFieldValue(evt, "StepIndex"));
            });

            Assert.IsTrue(CallBoolMethod(caster.SkillUser, "TryCast", skill, null));
            yield return null;
            RemoveEventListener(eventHub, "SkillStepExecuted", subscription);

            Assert.GreaterOrEqual(eventCount, 1);
            Assert.Greater(castId, 0UL);
            Assert.AreEqual(0, stepIndex);
        }

        [Test]
        public void EffectEvent_DispatchedWithCorrectTarget()
        {
            CreateSystems();
            var caster = CreateUnit("EffectEventCaster");
            var target = CreateUnit("EffectEventTarget");
            CallMethod(target.Health, "SetCurrent", 100f);

            var eventHub = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Core.CombatEventHub")));
            SetPrivateField(caster.Unit, "eventHub", eventHub);
            SetPrivateField(caster.SkillUser, "eventHub", eventHub);

            var context = CreateRuntimeContext(caster, eventHub, 77UL, 3);
            var damage = CreateDamageEffect(15f, "True");
            var targetData = CreateCombatTarget(target.GameObject);
            var phases = new List<string>(2);
            ulong castId = 0UL;
            int stepIndex = -1;
            object firstTargetHealth = null;

            var subscription = AddEventListener(eventHub, "SkillEffectExecuted", evt =>
            {
                phases.Add(GetFieldValue(evt, "Phase").ToString());
                castId = Convert.ToUInt64(GetFieldValue(evt, "CastId"));
                stepIndex = Convert.ToInt32(GetFieldValue(evt, "StepIndex"));
                if (firstTargetHealth == null)
                {
                    var eventTarget = GetFieldValue(evt, "Target");
                    firstTargetHealth = GetFieldValue(eventTarget, "Health");
                }
            });

            CallMethod(effectExecutorSystem, "ExecuteEffect", damage, context, targetData, ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart"));
            RemoveEventListener(eventHub, "SkillEffectExecuted", subscription);

            Assert.AreEqual(2, phases.Count);
            Assert.AreEqual("BeforeApply", phases[0]);
            Assert.AreEqual("AfterApply", phases[1]);
            Assert.AreEqual(77UL, castId);
            Assert.AreEqual(3, stepIndex);
            Assert.AreEqual(target.Health, firstTargetHealth);
        }

        [Test]
        public void ProjectileLifecycle_ReturnAndSplit_RaisesEvents()
        {
            CreateSystems();
            var caster = CreateUnit("ProjectileLifecycleCaster");
            var target = CreateUnit("ProjectileLifecycleTarget");
            var targetData = CreateCombatTarget(target.GameObject);

            var eventHub = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Core.CombatEventHub")));
            SetPrivateField(caster.Unit, "eventHub", eventHub);
            SetPrivateField(caster.SkillUser, "eventHub", eventHub);

            var context = CreateRuntimeContext(caster, eventHub, 88UL, 5);
            var lifecycleTypes = new List<string>(8);
            var subscription = AddEventListener(eventHub, "ProjectileLifecycle", evt =>
            {
                lifecycleTypes.Add(GetFieldValue(evt, "LifecycleType").ToString());
            });

            var poolGo = Track(new GameObject("ProjectileLifecyclePool"));
            var pool = poolGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectilePool"));

            var prefab = Track(new GameObject("ProjectileLifecyclePrefab"));
            prefab.AddComponent<SphereCollider>().isTrigger = true;
            prefab.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));

            var returnDefinition = CreateProjectileDefinition(0.2f);
            SetPrivateField(returnDefinition, "prefab", prefab);
            SetPrivateField(returnDefinition, "behaviorType", ParseEnum("CombatSystem.Data.ProjectileBehaviorType", "Return"));

            var returnProjectileGo = Track(new GameObject("ProjectileLifecycle_Return"));
            returnProjectileGo.AddComponent<SphereCollider>().isTrigger = true;
            var returnProjectile = returnProjectileGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));
            CallMethod(returnProjectile, "SetPool", pool, prefab);
            CallMethod(returnProjectile, "Initialize", returnDefinition, context, targetData, Vector3.forward, null, targetingSystem);
            InvokePrivateBoolMethod(returnProjectile, "HandlePostHitBehavior", targetData);

            var splitDefinition = CreateProjectileDefinition(0.2f);
            SetPrivateField(splitDefinition, "prefab", prefab);
            SetPrivateField(splitDefinition, "behaviorType", ParseEnum("CombatSystem.Data.ProjectileBehaviorType", "Split"));
            SetPrivateField(splitDefinition, "splitCount", 2);
            SetPrivateField(splitDefinition, "maxSplitDepth", 1);
            SetPrivateField(splitDefinition, "onHitEffects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition")));

            var splitProjectileGo = Track(new GameObject("ProjectileLifecycle_Split"));
            splitProjectileGo.AddComponent<SphereCollider>().isTrigger = true;
            var splitProjectile = splitProjectileGo.AddComponent(RequireType("CombatSystem.Gameplay.ProjectileController"));
            CallMethod(splitProjectile, "SetPool", pool, prefab);
            CallMethod(splitProjectile, "Initialize", splitDefinition, context, CreateDefaultValue("CombatSystem.Gameplay.CombatTarget"), Vector3.forward, null, targetingSystem);
            InvokePrivateMethod(splitProjectile, "SpawnSplitProjectiles");

            RemoveEventListener(eventHub, "ProjectileLifecycle", subscription);

            Assert.IsTrue(lifecycleTypes.Contains("Spawn"));
            Assert.IsTrue(lifecycleTypes.Contains("Return"));
            Assert.IsTrue(lifecycleTypes.Contains("Split"));
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

        [UnityTest]
        public IEnumerator SummonEffect_WithDuration_DestroysSpawnedObject()
        {
            CreateSystems();
            var caster = CreateUnit("TimedSummonCaster");
            var summonPrefab = Track(new GameObject("TimedSummonPrefab_Source"));
            var effect = CreateSummonEffect(summonPrefab, 0.05f);
            var context = CreateRuntimeContext(
                caster,
                null,
                0UL,
                -1,
                null,
                null,
                true,
                new Vector3(1f, 0f, 0f),
                Vector3.forward,
                1);

            CallMethod(
                effectExecutorSystem,
                "ExecuteEffect",
                effect,
                context,
                CreateDefaultValue("CombatSystem.Gameplay.CombatTarget"),
                ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart"));

            yield return null;

            var spawned = GameObject.Find("TimedSummonPrefab_Source(Clone)");
            Assert.NotNull(spawned);

            yield return new WaitForSeconds(0.08f);
            Assert.IsTrue(spawned == null);
        }

        [Test]
        public void UnitRootInitialize_LoadsAndRemovesStartingPassiveWithoutDuplicateActivationBuff()
        {
            CreateSystems();

            var eventHub = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Core.CombatEventHub")));
            var attackStat = CreateStat("Stat_PassiveAttack");
            var attackModifier = CreateStatModifier(attackStat, 5f);
            var activationBuff = CreateBuffWithModifier(attackModifier);
            var passive = CreatePassiveDefinition(
                activationBuffs: new[] { activationBuff });
            var unitDefinition = CreateUnitDefinition(
                baseStats: new[] { CreateStatValueEntry(attackStat, 10f) },
                startingPassives: new[] { passive });
            var unit = CreateFrameworkUnit("PassiveInitUnit");

            InitializeUnit(unit, unitDefinition, eventHub);

            Assert.AreEqual(15f, Convert.ToSingle(CallMethod(unit.Stats, "GetValue", attackStat, 0f)), 0.01f);
            Assert.AreEqual(1, Convert.ToInt32(GetPropertyValue(unit.Passive, "ActivePassiveCount")));
            Assert.AreEqual(1, ((IList)GetPropertyValue(unit.Buffs, "ActiveBuffs")).Count);

            InitializeUnit(unit, unitDefinition, eventHub);

            Assert.AreEqual(15f, Convert.ToSingle(CallMethod(unit.Stats, "GetValue", attackStat, 0f)), 0.01f);
            Assert.AreEqual(1, Convert.ToInt32(GetPropertyValue(unit.Passive, "ActivePassiveCount")));
            Assert.AreEqual(1, ((IList)GetPropertyValue(unit.Buffs, "ActiveBuffs")).Count);

            Assert.IsTrue(CallBoolMethod(unit.Passive, "RemovePassive", passive));
            Assert.AreEqual(10f, Convert.ToSingle(CallMethod(unit.Stats, "GetValue", attackStat, 0f)), 0.01f);
            Assert.AreEqual(0, Convert.ToInt32(GetPropertyValue(unit.Passive, "ActivePassiveCount")));
            Assert.AreEqual(0, ((IList)GetPropertyValue(unit.Buffs, "ActiveBuffs")).Count);
        }

        [Test]
        public void ResourceComponent_AuxiliaryResourceStartsEmptyAndSupportsIndependentSetCurrent()
        {
            var unit = CreateFrameworkUnit("AuxiliaryResourceUnit");
            var flow = CreateResourceDefinition("Resource_TestFlow", 100f, false, true, 10);

            Assert.IsTrue(CallBoolMethod(unit.Resource, "EnsureResource", flow));
            Assert.AreEqual(0f, Convert.ToSingle(CallMethod(unit.Resource, "GetCurrent", flow)), 0.01f);
            Assert.AreEqual(100f, Convert.ToSingle(CallMethod(unit.Resource, "GetMax", flow)), 0.01f);

            CallMethod(unit.Resource, "SetCurrent", flow, 35f);
            Assert.AreEqual(35f, Convert.ToSingle(CallMethod(unit.Resource, "GetCurrent", flow)), 0.01f);
            Assert.AreEqual(0f, GetFloatProperty(unit.Resource, "Current"), 0.01f);
        }

        [Test]
        public void Passive_OnResourceChanged_UsesAuxiliaryResourcePercentCondition()
        {
            CreateSystems();

            var eventHub = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Core.CombatEventHub")));
            var flow = CreateResourceDefinition("Resource_ReactiveFlow", 100f, false, true, 10);
            var selfTargeting = CreateSelfTargeting();
            var shieldEffect = CreateShieldEffect(12f, 2f, selfTargeting);
            var flowThreshold = CreateResourceCondition("ResourcePercentAtLeast", "Caster", flow, 0.5f);
            var passive = CreatePassiveDefinition(
                triggers: new[]
                {
                    CreatePassiveTrigger("OnResourceChanged", flowThreshold, shieldEffect)
                },
                meterDrivers: new[]
                {
                    CreatePassiveMeterDriver(flow, "AddByMoveDistance", 0f)
                });
            var unitDefinition = CreateUnitDefinition(startingPassives: new[] { passive });
            var unit = CreateFrameworkUnit("PassiveResourceChangedUnit");

            InitializeUnit(unit, unitDefinition, eventHub);

            CallMethod(unit.Resource, "SetCurrent", flow, 40f);
            Assert.AreEqual(0f, GetFloatProperty(unit.Health, "Shield"), 0.01f);

            CallMethod(unit.Resource, "SetCurrent", flow, 60f);
            Assert.AreEqual(12f, GetFloatProperty(unit.Health, "Shield"), 0.01f);
        }

        [Test]
        public void Passive_OnDamaged_WithFullAuxiliaryResource_GrantsShieldAndClearsResource()
        {
            CreateSystems();

            var eventHub = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Core.CombatEventHub")));
            var flow = CreateResourceDefinition("Resource_DamageFlow", 100f, false, true, 10);
            var selfTargeting = CreateSelfTargeting();
            var shieldEffect = CreateShieldEffect(25f, 2f, selfTargeting);
            var clearFlow = CreateResourceEffect(0f, flow, "SetCurrent");
            var condition = CreateResourceCondition("ResourceAtLeast", "Caster", flow, 100f);
            var passive = CreatePassiveDefinition(
                triggers: new[]
                {
                    CreatePassiveTrigger("OnDamaged", condition, shieldEffect, clearFlow)
                },
                meterDrivers: new[]
                {
                    CreatePassiveMeterDriver(flow, "AddByMoveDistance", 0f)
                });
            var attacker = CreateFrameworkUnit("PassiveDamaged_Attacker");
            var victim = CreateFrameworkUnit("PassiveDamaged_Victim");
            var attackerDefinition = CreateUnitDefinition();
            var victimDefinition = CreateUnitDefinition(startingPassives: new[] { passive });
            var damage = CreateDamageEffect(10f, "True");

            CallMethod(attacker.Team, "SetTeamId", 1);
            CallMethod(victim.Team, "SetTeamId", 2);
            InitializeUnit(attacker, attackerDefinition, eventHub);
            InitializeUnit(victim, victimDefinition, eventHub);
            CallMethod(victim.Health, "SetCurrent", 100f);
            CallMethod(victim.Resource, "SetCurrent", flow, 100f);

            var targetData = CreateCombatTarget(victim.GameObject);
            var context = CreateRuntimeContext(attacker, eventHub);
            object damageApplied = null;
            var subscription = AddEventListener(eventHub, "DamageApplied", evt => damageApplied = evt);
            CallMethod(
                effectExecutorSystem,
                "ExecuteEffect",
                damage,
                context,
                targetData,
                ParseEnum("CombatSystem.Data.SkillStepTrigger", "OnCastStart"));
            RemoveEventListener(eventHub, "DamageApplied", subscription);

            Assert.NotNull(damageApplied);
            Assert.AreEqual(100f, GetFloatProperty(victim.Health, "Current"), 0.01f);
            Assert.AreEqual(15f, GetFloatProperty(victim.Health, "Shield"), 0.01f);
            Assert.AreEqual(0f, Convert.ToSingle(CallMethod(victim.Resource, "GetCurrent", flow)), 0.01f);
            Assert.AreEqual(0f, Convert.ToSingle(GetFieldValue(damageApplied, "AppliedDamage")), 0.01f);
            Assert.AreEqual(10f, Convert.ToSingle(GetFieldValue(damageApplied, "AbsorbedByShield")), 0.01f);
        }

        [UnityTest]
        public IEnumerator Passive_MeterDrivers_AddOnMoveAndDecayOnIdle()
        {
            CreateSystems();

            var eventHub = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Core.CombatEventHub")));
            var flow = CreateResourceDefinition("Resource_MovementFlow", 100f, false, true, 10);
            var passive = CreatePassiveDefinition(
                meterDrivers: new[]
                {
                    CreatePassiveMeterDriver(flow, "AddByMoveDistance", 2f),
                    CreatePassiveMeterDriver(flow, "DecayPerSecondWhileIdle", 20f)
                });
            var unitDefinition = CreateUnitDefinition(startingPassives: new[] { passive });
            var unit = CreateFrameworkUnit("PassiveMeterDriverUnit");

            InitializeUnit(unit, unitDefinition, eventHub);
            yield return null;

            var movementSampleType = RequireType("CombatSystem.Core.MovementSampleEvent");
            var movingEvent = Activator.CreateInstance(
                movementSampleType,
                null,
                new Vector3(3f, 0f, 0f),
                3f,
                true);
            InvokePrivateMethod(unit.Passive, "HandleMovementSampled", movingEvent);

            var afterMove = Convert.ToSingle(CallMethod(unit.Resource, "GetCurrent", flow));
            Assert.AreEqual(6f, afterMove, 0.01f);

            yield return null;

            var idleEvent = Activator.CreateInstance(
                movementSampleType,
                null,
                Vector3.zero,
                0f,
                false);
            InvokePrivateMethod(unit.Passive, "HandleMovementSampled", idleEvent);

            var afterIdle = Convert.ToSingle(CallMethod(unit.Resource, "GetCurrent", flow));
            Assert.Less(afterIdle, afterMove);
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
            CallMethod(stats, "SetBuffController", buffs);
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

        private UnitRig CreateFrameworkUnit(string name)
        {
            var go = Track(new GameObject(name));
            var unit = go.AddComponent(RequireType("CombatSystem.Core.UnitRoot"));
            var stats = go.AddComponent(RequireType("CombatSystem.Core.StatsComponent"));
            var health = go.AddComponent(RequireType("CombatSystem.Core.HealthComponent"));
            var resource = go.AddComponent(RequireType("CombatSystem.Core.ResourceComponent"));
            var cooldown = go.AddComponent(RequireType("CombatSystem.Core.CooldownComponent"));
            var tags = go.AddComponent(RequireType("CombatSystem.Core.UnitTagsComponent"));
            var buffs = go.AddComponent(RequireType("CombatSystem.Core.BuffController"));
            var state = go.AddComponent(RequireType("CombatSystem.Core.CombatStateComponent"));
            var visibility = go.AddComponent(RequireType("CombatSystem.Gameplay.VisibilityComponent"));
            var skillUser = go.AddComponent(RequireType("CombatSystem.Gameplay.SkillUserComponent"));
            var team = go.AddComponent(RequireType("CombatSystem.Core.TeamComponent"));
            var passive = go.GetComponent(RequireType("CombatSystem.Core.PassiveController"));
            if (passive == null)
            {
                passive = go.AddComponent(RequireType("CombatSystem.Core.PassiveController"));
            }

            CallMethod(team, "SetTeamId", 1);
            CallMethod(stats, "SetBuffController", buffs);

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
                SkillUser = skillUser,
                Passive = passive,
                Tags = tags
            };
        }

        private void InitializeUnit(UnitRig unit, object unitDefinition, object eventHub)
        {
            Assert.NotNull(unit);
            Assert.NotNull(unit.Unit);

            SetPrivateField(unit.Unit, "eventHub", eventHub);
            CallMethod(unit.Unit, "Initialize", unitDefinition);
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

        private object CreateResourceDefinition(
            string id,
            float baseMaxResource,
            bool initializeToMax,
            bool showInHud,
            int hudPriority)
        {
            var resource = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.ResourceDefinition")));
            SetPrivateField(resource, "id", id);
            SetPrivateField(resource, "displayName", id);
            SetPrivateField(resource, "baseMaxResource", baseMaxResource);
            SetPrivateField(resource, "initializeToMax", initializeToMax);
            SetPrivateField(resource, "showInHud", showInHud);
            SetPrivateField(resource, "hudPriority", hudPriority);
            SetPrivateField(resource, "clampToMax", true);
            SetPrivateField(resource, "useLegacyType", false);
            return resource;
        }

        private object CreateUnitDefinition(object[] baseStats = null, object[] startingPassives = null)
        {
            var unitDefinition = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.UnitDefinition")));
            SetPrivateField(unitDefinition, "baseStats", CreateTypedList(RequireType("CombatSystem.Data.StatValue"), baseStats ?? Array.Empty<object>()));
            SetPrivateField(unitDefinition, "tags", CreateTypedList(RequireType("CombatSystem.Data.TagDefinition")));
            SetPrivateField(unitDefinition, "startingSkills", CreateTypedList(RequireType("CombatSystem.Data.SkillDefinition")));
            SetPrivateField(unitDefinition, "startingPassives", CreateTypedList(RequireType("CombatSystem.Data.PassiveDefinition"), startingPassives ?? Array.Empty<object>()));
            SetPrivateField(unitDefinition, "basicAttack", null);
            return unitDefinition;
        }

        private object CreateStatValueEntry(object stat, float value)
        {
            var statValueType = RequireType("CombatSystem.Data.StatValue");
            var statValue = Activator.CreateInstance(statValueType);
            SetField(statValue, "stat", stat);
            SetField(statValue, "value", value);
            return statValue;
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

        private object CreateSequenceConfig(
            bool enabled,
            int maxPhases,
            float resetWindow,
            string overflowPolicy,
            bool resetOnOtherSkillCast)
        {
            var type = RequireType("CombatSystem.Data.SkillSequenceConfig");
            var config = Activator.CreateInstance(type);
            SetPrivateField(config, "enabled", enabled);
            SetPrivateField(config, "maxPhases", maxPhases);
            SetPrivateField(config, "resetWindow", resetWindow);
            SetPrivateField(config, "overflowPolicy", ParseEnum("CombatSystem.Data.SkillSequenceOverflowPolicy", overflowPolicy));
            SetPrivateField(config, "resetOnOtherSkillCast", resetOnOtherSkillCast);
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

        private object CreateSelfTargeting()
        {
            var targeting = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.TargetingDefinition")));
            SetPrivateField(targeting, "mode", ParseEnum("CombatSystem.Data.TargetingMode", "Self"));
            SetPrivateField(targeting, "team", ParseEnum("CombatSystem.Data.TargetTeam", "Self"));
            SetPrivateField(targeting, "origin", ParseEnum("CombatSystem.Data.TargetingOrigin", "Caster"));
            SetPrivateField(targeting, "range", 0f);
            SetPrivateField(targeting, "radius", 0f);
            SetPrivateField(targeting, "maxTargets", 1);
            SetPrivateField(targeting, "includeSelf", true);
            SetPrivateField(targeting, "allowEmpty", false);
            SetPrivateField(targeting, "requireExplicitTarget", false);
            SetPrivateField(targeting, "hitValidation", ParseEnum("CombatSystem.Data.HitValidationPolicy", "None"));
            return targeting;
        }

        private object CreateCondition(string conditionTypeName, string subjectName, object buff = null)
        {
            var condition = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.ConditionDefinition")));
            var entryType = RequireType("CombatSystem.Data.ConditionEntry");
            var entry = Activator.CreateInstance(entryType);
            SetField(entry, "type", ParseEnum("CombatSystem.Data.ConditionType", conditionTypeName));
            SetField(entry, "subject", ParseEnum("CombatSystem.Data.ConditionSubject", subjectName));
            SetField(entry, "buff", buff);
            SetPrivateField(condition, "op", ParseEnum("CombatSystem.Data.ConditionOperator", "All"));
            SetPrivateField(condition, "entries", CreateTypedList(entryType, entry));
            return condition;
        }

        private object CreateResourceCondition(string conditionTypeName, string subjectName, object resourceDefinition, float threshold)
        {
            var condition = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.ConditionDefinition")));
            var entryType = RequireType("CombatSystem.Data.ConditionEntry");
            var entry = Activator.CreateInstance(entryType);
            SetField(entry, "type", ParseEnum("CombatSystem.Data.ConditionType", conditionTypeName));
            SetField(entry, "subject", ParseEnum("CombatSystem.Data.ConditionSubject", subjectName));
            SetField(entry, "resource", resourceDefinition);
            SetField(entry, "threshold", threshold);
            SetPrivateField(condition, "op", ParseEnum("CombatSystem.Data.ConditionOperator", "All"));
            SetPrivateField(condition, "entries", CreateTypedList(entryType, entry));
            return condition;
        }

        private object CreateCastConstraint(object condition, string failReasonName)
        {
            var type = RequireType("CombatSystem.Data.SkillCastConstraint");
            var constraint = Activator.CreateInstance(type);
            SetField(constraint, "condition", condition);
            SetField(constraint, "failReason", ParseEnum("CombatSystem.Data.SkillCastFailReason", failReasonName));
            return constraint;
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

        private object CreateMoveEffect(
            float distance,
            float speed,
            string moveStyleName,
            string destinationPolicyName,
            string collisionPolicyName,
            float targetOffset = 0.8f)
        {
            var effect = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.EffectDefinition")));
            SetPrivateField(effect, "effectType", ParseEnum("CombatSystem.Data.EffectType", "Move"));
            SetPrivateField(effect, "moveStyle", ParseEnum("CombatSystem.Data.MoveStyle", moveStyleName));
            SetPrivateField(effect, "moveDistance", distance);
            SetPrivateField(effect, "moveSpeed", speed);
            SetPrivateField(effect, "moveDestinationPolicy", ParseEnum("CombatSystem.Data.MoveDestinationPolicy", destinationPolicyName));
            SetPrivateField(effect, "moveCollisionPolicy", ParseEnum("CombatSystem.Data.MoveCollisionPolicy", collisionPolicyName));
            SetPrivateField(effect, "moveTargetOffset", targetOffset);
            return effect;
        }

        private object CreateShieldEffect(float value, float duration, object overrideTargeting = null)
        {
            var effect = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.EffectDefinition")));
            SetPrivateField(effect, "effectType", ParseEnum("CombatSystem.Data.EffectType", "Shield"));
            SetPrivateField(effect, "value", value);
            SetPrivateField(effect, "duration", duration);
            SetPrivateField(effect, "overrideTargeting", overrideTargeting);
            return effect;
        }

        private object CreateResourceEffect(float value, object resourceDefinition, string operationName)
        {
            var effect = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.EffectDefinition")));
            SetPrivateField(effect, "effectType", ParseEnum("CombatSystem.Data.EffectType", "Resource"));
            SetPrivateField(effect, "value", value);
            SetPrivateField(effect, "resourceDefinition", resourceDefinition);
            SetPrivateField(effect, "resourceOperation", ParseEnum("CombatSystem.Data.ResourceOperation", operationName));
            return effect;
        }

        private object CreateSummonEffect(GameObject summonPrefab, float duration)
        {
            var effect = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.EffectDefinition")));
            SetPrivateField(effect, "effectType", ParseEnum("CombatSystem.Data.EffectType", "Summon"));
            SetPrivateField(effect, "summonPrefab", summonPrefab);
            SetPrivateField(effect, "duration", duration);
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

        private object CreateStatModifier(object stat, float value, string operationName = "Add")
        {
            var modifier = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.ModifierDefinition")));
            SetPrivateField(modifier, "target", ParseEnum("CombatSystem.Data.ModifierTargetType", "Stat"));
            SetPrivateField(modifier, "scope", ParseEnum("CombatSystem.Data.ModifierScope", "Caster"));
            SetPrivateField(modifier, "stat", stat);
            SetPrivateField(modifier, "operation", ParseEnum("CombatSystem.Data.ModifierOperation", operationName));
            SetPrivateField(modifier, "value", value);
            SetPrivateField(modifier, "requiredTags", CreateTypedList(RequireType("CombatSystem.Data.TagDefinition")));
            SetPrivateField(modifier, "blockedTags", CreateTypedList(RequireType("CombatSystem.Data.TagDefinition")));
            return modifier;
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

        private object CreatePassiveDefinition(
            object[] activationBuffs = null,
            object[] triggers = null,
            object[] meterDrivers = null)
        {
            var passive = Track(ScriptableObject.CreateInstance(RequireType("CombatSystem.Data.PassiveDefinition")));
            SetPrivateField(passive, "activationBuffs", CreateTypedList(RequireType("CombatSystem.Data.BuffDefinition"), activationBuffs ?? Array.Empty<object>()));
            SetPrivateField(passive, "triggers", CreateTypedList(RequireType("CombatSystem.Data.PassiveTrigger"), triggers ?? Array.Empty<object>()));
            SetPrivateField(passive, "meterDrivers", CreateTypedList(RequireType("CombatSystem.Data.PassiveMeterDriver"), meterDrivers ?? Array.Empty<object>()));
            SetPrivateField(passive, "tags", CreateTypedList(RequireType("CombatSystem.Data.TagDefinition")));
            return passive;
        }

        private object CreatePassiveTrigger(string triggerTypeName, object condition, params object[] effects)
        {
            var trigger = Activator.CreateInstance(RequireType("CombatSystem.Data.PassiveTrigger"));
            SetField(trigger, "triggerType", ParseEnum("CombatSystem.Data.PassiveTriggerType", triggerTypeName));
            SetField(trigger, "chance", 1f);
            SetField(trigger, "condition", condition);
            SetField(trigger, "effects", CreateTypedList(RequireType("CombatSystem.Data.EffectDefinition"), effects));
            return trigger;
        }

        private object CreatePassiveMeterDriver(object resourceDefinition, string driverTypeName, float rateOrAmount, object condition = null)
        {
            var driver = Activator.CreateInstance(RequireType("CombatSystem.Data.PassiveMeterDriver"));
            SetField(driver, "resource", resourceDefinition);
            SetField(driver, "driverType", ParseEnum("CombatSystem.Data.PassiveMeterDriverType", driverTypeName));
            SetField(driver, "rateOrAmount", rateOrAmount);
            SetField(driver, "condition", condition);
            return driver;
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

        private object CreateRuntimeContext(
            UnitRig caster,
            object eventHub = null,
            ulong castId = 0UL,
            int stepIndex = -1,
            object skill = null,
            GameObject explicitTarget = null,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default,
            int sequencePhase = 1)
        {
            var contextType = RequireType("CombatSystem.Gameplay.SkillRuntimeContext");
            var args = new object[]
            {
                caster != null ? caster.SkillUser : null,
                caster != null ? caster.Unit : null,
                skill,
                eventHub,
                targetingSystem,
                effectExecutorSystem,
                hasAimPoint,
                aimPoint,
                aimDirection,
                explicitTarget,
                0f,
                0f,
                1f,
                castId,
                stepIndex,
                sequencePhase
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

        private static Delegate AddEventListener(object source, string eventName, Action<object> callback)
        {
            Assert.NotNull(source);
            Assert.IsNotEmpty(eventName);
            Assert.NotNull(callback);

            var eventInfo = source.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(eventInfo, $"Event '{eventName}' not found on {source.GetType().Name}.");

            var handlerType = eventInfo.EventHandlerType;
            var invoke = handlerType.GetMethod("Invoke");
            Assert.NotNull(invoke, $"Event handler invoke not found for '{eventName}'.");
            var parameters = invoke.GetParameters();
            Assert.AreEqual(1, parameters.Length, $"Event '{eventName}' should have exactly one argument.");

            var argumentType = parameters[0].ParameterType;
            var callbackConstant = Expression.Constant(callback);
            var parameterExpr = Expression.Parameter(argumentType, "evt");
            var body = Expression.Call(
                callbackConstant,
                typeof(Action<object>).GetMethod("Invoke"),
                Expression.Convert(parameterExpr, typeof(object)));
            var lambda = Expression.Lambda(handlerType, body, parameterExpr);
            var handler = lambda.Compile();
            eventInfo.AddEventHandler(source, handler);
            return handler;
        }

        private static void RemoveEventListener(object source, string eventName, Delegate handler)
        {
            if (source == null || handler == null || string.IsNullOrEmpty(eventName))
            {
                return;
            }

            var eventInfo = source.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            if (eventInfo == null)
            {
                return;
            }

            eventInfo.RemoveEventHandler(source, handler);
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
            public Component Passive;
            public Component Tags;
        }
    }
}
