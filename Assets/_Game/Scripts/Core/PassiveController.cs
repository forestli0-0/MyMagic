using System;
using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 被动控制器，负责加载、激活和驱动单位的被动效果。
    /// </summary>
    public class PassiveController : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private UnitRoot unitRoot;
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private BuffController buffController;
        [SerializeField] private ResourceComponent resource;
        [SerializeField] private MovementComponent movement;
        [SerializeField] private EffectExecutor effectExecutor;
        [SerializeField] private TargetingSystem targetingSystem;

        private readonly List<PassiveRuntime> activePassives = new List<PassiveRuntime>(8);
        private CombatEventHub eventHub;

        public int ActivePassiveCount => activePassives.Count;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Initialize(UnitDefinition definition)
        {
            CacheReferences();
            Subscribe();
            ClearPassives();

            var startingPassives = definition != null ? definition.StartingPassives : null;
            if (startingPassives == null)
            {
                return;
            }

            for (int i = 0; i < startingPassives.Count; i++)
            {
                GrantPassive(startingPassives[i]);
            }
        }

        public bool HasPassive(PassiveDefinition passive)
        {
            if (passive == null)
            {
                return false;
            }

            for (int i = 0; i < activePassives.Count; i++)
            {
                if (activePassives[i].Definition == passive)
                {
                    return true;
                }
            }

            return false;
        }

        public bool GrantPassive(PassiveDefinition passive)
        {
            if (passive == null || HasPassive(passive))
            {
                return false;
            }

            CacheReferences();
            Subscribe();

            var runtime = new PassiveRuntime(passive);
            activePassives.Add(runtime);

            var activationBuffs = passive.ActivationBuffs;
            if (buffController != null && activationBuffs != null)
            {
                for (int i = 0; i < activationBuffs.Count; i++)
                {
                    var buff = activationBuffs[i];
                    if (buff == null)
                    {
                        continue;
                    }

                    buffController.ApplyBuff(buff, unitRoot);
                }
            }

            var meterDrivers = passive.MeterDrivers;
            if (resource != null && meterDrivers != null)
            {
                for (int i = 0; i < meterDrivers.Count; i++)
                {
                    if (meterDrivers[i] != null && meterDrivers[i].resource != null)
                    {
                        resource.EnsureResource(meterDrivers[i].resource);
                    }
                }
            }

            TriggerPassive(runtime, PassiveTriggerType.OnActivated, GetSelfTarget(), null);
            return true;
        }

        public bool RemovePassive(PassiveDefinition passive)
        {
            if (passive == null)
            {
                return false;
            }

            for (int i = activePassives.Count - 1; i >= 0; i--)
            {
                if (activePassives[i].Definition != passive)
                {
                    continue;
                }

                RemoveRuntime(activePassives[i]);
                activePassives.RemoveAt(i);
                return true;
            }

            return false;
        }

        private void CacheReferences()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            if (buffController == null)
            {
                buffController = GetComponent<BuffController>();
            }

            if (resource == null)
            {
                resource = GetComponent<ResourceComponent>();
            }

            if (movement == null)
            {
                movement = GetComponent<MovementComponent>();
            }

            if (effectExecutor == null)
            {
                effectExecutor = FindObjectOfType<EffectExecutor>();
            }

            if (targetingSystem == null)
            {
                targetingSystem = FindObjectOfType<TargetingSystem>();
            }

            if (unitRoot != null)
            {
                eventHub = unitRoot.EventHub;
            }
        }

        private void Subscribe()
        {
            if (eventHub != null)
            {
                eventHub.SkillCastStarted -= HandleSkillCastStarted;
                eventHub.SkillCastStarted += HandleSkillCastStarted;
                eventHub.DamageApplying -= HandleDamageApplying;
                eventHub.DamageApplying += HandleDamageApplying;
                eventHub.DamageApplied -= HandleDamageApplied;
                eventHub.DamageApplied += HandleDamageApplied;
                eventHub.UnitKilled -= HandleUnitKilled;
                eventHub.UnitKilled += HandleUnitKilled;
                eventHub.ResourceChanged -= HandleResourceChanged;
                eventHub.ResourceChanged += HandleResourceChanged;
            }

            if (movement != null)
            {
                movement.MovementSampled -= HandleMovementSampled;
                movement.MovementSampled += HandleMovementSampled;
            }
        }

        private void Unsubscribe()
        {
            if (eventHub != null)
            {
                eventHub.SkillCastStarted -= HandleSkillCastStarted;
                eventHub.DamageApplying -= HandleDamageApplying;
                eventHub.DamageApplied -= HandleDamageApplied;
                eventHub.UnitKilled -= HandleUnitKilled;
                eventHub.ResourceChanged -= HandleResourceChanged;
            }

            if (movement != null)
            {
                movement.MovementSampled -= HandleMovementSampled;
            }
        }

        private void HandleSkillCastStarted(SkillCastEvent evt)
        {
            if (evt.Caster != unitRoot)
            {
                return;
            }

            TriggerPassives(PassiveTriggerType.OnSkillCast, GetSelfTarget(), evt.Skill);

            if (skillUser != null && skillUser.IsBasicAttackSkill(evt.Skill))
            {
                TriggerPassives(PassiveTriggerType.OnAttack, GetSelfTarget(), evt.Skill);
            }
        }

        private void HandleDamageApplying(DamageApplyingEvent evt)
        {
            if (evt.Target == null || evt.Target.gameObject != gameObject)
            {
                return;
            }

            var attackerTarget = evt.Attacker != null && CombatTarget.TryCreate(evt.Attacker.gameObject, out var created)
                ? created
                : GetSelfTarget();
            TriggerPassives(PassiveTriggerType.OnDamaged, attackerTarget, evt.Skill);
        }

        private void HandleDamageApplied(DamageAppliedEvent evt)
        {
            if (evt.Attacker == unitRoot && evt.Target != null)
            {
                if (CombatTarget.TryCreate(evt.Target.gameObject, out var victimTarget))
                {
                    TriggerPassives(PassiveTriggerType.OnHit, victimTarget, evt.Skill);
                }
            }
        }

        private void HandleUnitKilled(UnitKilledEvent evt)
        {
            if (evt.Source.SourceUnit != unitRoot || evt.Victim == null)
            {
                return;
            }

            if (CombatTarget.TryCreate(evt.Victim.gameObject, out var target))
            {
                TriggerPassives(PassiveTriggerType.OnKill, target, evt.Source.Skill);
            }
        }

        private void HandleResourceChanged(ResourceChangedEvent evt)
        {
            if (evt.Source != resource)
            {
                return;
            }

            TriggerPassives(PassiveTriggerType.OnResourceChanged, GetSelfTarget(), null);
        }

        private void HandleMovementSampled(MovementSampleEvent evt)
        {
            if (resource == null || activePassives.Count == 0)
            {
                return;
            }

            var self = GetSelfTarget();
            for (int i = 0; i < activePassives.Count; i++)
            {
                var drivers = activePassives[i].Definition != null ? activePassives[i].Definition.MeterDrivers : null;
                if (drivers == null || drivers.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < drivers.Count; j++)
                {
                    var driver = drivers[j];
                    if (driver == null || driver.resource == null)
                    {
                        continue;
                    }

                    var context = BuildContext(null);
                    if (driver.condition != null && !ConditionEvaluator.Evaluate(driver.condition, context, self))
                    {
                        continue;
                    }

                    var delta = ResolveDriverDelta(driver, evt);
                    if (Mathf.Approximately(delta, 0f))
                    {
                        continue;
                    }

                    resource.EnsureResource(driver.resource);
                    var current = resource.GetCurrent(driver.resource);
                    resource.SetCurrent(driver.resource, current + delta);
                }
            }
        }

        private float ResolveDriverDelta(PassiveMeterDriver driver, MovementSampleEvent evt)
        {
            switch (driver.driverType)
            {
                case PassiveMeterDriverType.AddByMoveDistance:
                    return evt.HorizontalDistance * driver.rateOrAmount;
                case PassiveMeterDriverType.AddPerSecondWhileMoving:
                    return evt.IsMoving ? driver.rateOrAmount * Time.deltaTime : 0f;
                case PassiveMeterDriverType.DecayPerSecondWhileIdle:
                    return evt.IsMoving ? 0f : -driver.rateOrAmount * Time.deltaTime;
                default:
                    return 0f;
            }
        }

        private void TriggerPassives(PassiveTriggerType triggerType, CombatTarget target, SkillDefinition skill)
        {
            for (int i = 0; i < activePassives.Count; i++)
            {
                TriggerPassive(activePassives[i], triggerType, target, skill);
            }
        }

        private void TriggerPassive(PassiveRuntime runtime, PassiveTriggerType triggerType, CombatTarget target, SkillDefinition skill)
        {
            if (effectExecutor == null || runtime.Definition == null)
            {
                return;
            }

            var triggers = runtime.Definition.Triggers;
            if (triggers == null || triggers.Count == 0)
            {
                return;
            }

            var resolvedTarget = target.IsValid ? target : GetSelfTarget();
            var context = BuildContext(skill);
            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null || trigger.triggerType != triggerType)
                {
                    continue;
                }

                var chance = Mathf.Clamp01(trigger.chance);
                if (chance <= 0f || (chance < 1f && UnityEngine.Random.value > chance))
                {
                    continue;
                }

                if (trigger.condition != null && !ConditionEvaluator.Evaluate(trigger.condition, context, resolvedTarget))
                {
                    continue;
                }

                if (trigger.effects == null || trigger.effects.Count == 0)
                {
                    continue;
                }

                var mappedTrigger = MapTrigger(triggerType);
                for (int j = 0; j < trigger.effects.Count; j++)
                {
                    effectExecutor.ExecuteEffect(trigger.effects[j], context, resolvedTarget, mappedTrigger);
                }
            }
        }

        private SkillRuntimeContext BuildContext(SkillDefinition skill)
        {
            return new SkillRuntimeContext(
                skillUser,
                unitRoot,
                skill,
                eventHub,
                targetingSystem,
                effectExecutor,
                false,
                default,
                default,
                null,
                0f,
                0f,
                1f,
                0UL,
                -1,
                1);
        }

        private CombatTarget GetSelfTarget()
        {
            return CombatTarget.TryCreate(gameObject, out var target) ? target : default;
        }

        private void ClearPassives()
        {
            for (int i = activePassives.Count - 1; i >= 0; i--)
            {
                RemoveRuntime(activePassives[i]);
            }

            activePassives.Clear();
        }

        private void RemoveRuntime(PassiveRuntime runtime)
        {
            if (runtime.Definition == null || buffController == null)
            {
                return;
            }

            var activationBuffs = runtime.Definition.ActivationBuffs;
            if (activationBuffs == null)
            {
                return;
            }

            for (int i = 0; i < activationBuffs.Count; i++)
            {
                if (activationBuffs[i] != null)
                {
                    buffController.RemoveBuff(activationBuffs[i]);
                }
            }
        }

        private static SkillStepTrigger MapTrigger(PassiveTriggerType triggerType)
        {
            switch (triggerType)
            {
                case PassiveTriggerType.OnHit:
                case PassiveTriggerType.OnDamaged:
                case PassiveTriggerType.OnKill:
                    return SkillStepTrigger.OnHit;
                default:
                    return SkillStepTrigger.OnCastStart;
            }
        }

        private readonly struct PassiveRuntime
        {
            public readonly PassiveDefinition Definition;

            public PassiveRuntime(PassiveDefinition definition)
            {
                Definition = definition;
            }
        }
    }
}
