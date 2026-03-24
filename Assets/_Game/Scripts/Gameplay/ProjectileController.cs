using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    public class ProjectileController : MonoBehaviour
    {
        private ProjectileDefinition definition;
        private SkillRuntimeContext context;
        private TargetingDefinition targetingDefinition;
        private TargetingSystem targetingSystem;
        private ProjectilePool pool;
        private GameObject prefabKey;
        private CombatTarget target;
        private Vector3 direction;
        private float expireTime;
        private int remainingPierce;
        private int splitDepth;
        private int projectileInstanceId;
        private int parentProjectileInstanceId;
        private bool active;
        private bool returning;
        private float orbitAngle;
        private Rigidbody body;
        private SphereCollider runtimeHitCollider;
        private TrailRenderer[] trailRenderers;
        private readonly List<int> hitIds = new List<int>(8);
        private static int nextRuntimeProjectileInstanceId = 1;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            runtimeHitCollider = GetComponent<SphereCollider>();
            trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        }

        public void SetPool(ProjectilePool projectilePool, GameObject prefab)
        {
            pool = projectilePool;
            prefabKey = prefab;
        }

        public void Initialize(
            ProjectileDefinition projectileDefinition,
            SkillRuntimeContext runtimeContext,
            CombatTarget initialTarget,
            Vector3 initialDirection,
            TargetingDefinition targeting,
            TargetingSystem targetingSystemRef)
        {
            Initialize(
                projectileDefinition,
                runtimeContext,
                initialTarget,
                initialDirection,
                targeting,
                targetingSystemRef,
                0);
        }

        public void Initialize(
            ProjectileDefinition projectileDefinition,
            SkillRuntimeContext runtimeContext,
            CombatTarget initialTarget,
            Vector3 initialDirection,
            TargetingDefinition targeting,
            TargetingSystem targetingSystemRef,
            int parentInstanceId)
        {
            definition = projectileDefinition;
            context = runtimeContext;
            target = initialTarget;
            targetingDefinition = targeting;
            targetingSystem = targetingSystemRef;
            direction = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : transform.forward;
            expireTime = definition.Lifetime > 0f ? Time.time + definition.Lifetime : float.PositiveInfinity;
            remainingPierce = definition.Pierce ? Mathf.Max(1, definition.MaxPierce) : 1;
            splitDepth = 0;
            active = true;
            returning = false;
            orbitAngle = 0f;
            hitIds.Clear();
            projectileInstanceId = GenerateProjectileInstanceId();
            parentProjectileInstanceId = Mathf.Max(0, parentInstanceId);
            ApplyHitRadius();
            ResetVisualTrails();
            RaiseLifecycleEvent(ProjectileLifecycleType.Spawn, target, parentProjectileInstanceId);
        }

        private void Update()
        {
            if (!active || definition == null)
            {
                return;
            }

            if (Time.time >= expireTime)
            {
                Despawn();
                return;
            }

            UpdateDirection();
            MoveProjectile();

            if (definition.BehaviorType == ProjectileBehaviorType.Return
                && returning
                && context.CasterUnit != null
                && (context.CasterUnit.transform.position - transform.position).sqrMagnitude <= 0.25f)
            {
                Despawn();
            }
        }

        private void UpdateDirection()
        {
            if (definition == null)
            {
                return;
            }

            switch (definition.BehaviorType)
            {
                case ProjectileBehaviorType.Homing:
                    if (target.Transform != null)
                    {
                        RotateTowards(target.Transform.position, definition.HomingTurnSpeed);
                    }
                    break;
                case ProjectileBehaviorType.Return:
                    if (returning && context.CasterUnit != null)
                    {
                        RotateTowards(context.CasterUnit.transform.position, definition.HomingTurnSpeed);
                    }
                    else if (definition.Homing && target.Transform != null)
                    {
                        RotateTowards(target.Transform.position, definition.HomingTurnSpeed);
                    }
                    break;
                default:
                    if (definition.Homing && target.Transform != null)
                    {
                        RotateTowards(target.Transform.position, definition.HomingTurnSpeed);
                    }
                    break;
            }
        }

        private void MoveProjectile()
        {
            if (definition == null)
            {
                return;
            }

            switch (definition.BehaviorType)
            {
                case ProjectileBehaviorType.Orbit:
                    if (context.CasterUnit == null)
                    {
                        MoveLinear(definition.Speed);
                        return;
                    }

                    orbitAngle += definition.OrbitAngularSpeed * Time.deltaTime;
                    var orbitOffset = Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward * definition.OrbitRadius;
                    var orbitPos = context.CasterUnit.transform.position + orbitOffset;
                    if (body != null)
                    {
                        body.MovePosition(orbitPos);
                    }
                    else
                    {
                        transform.position = orbitPos;
                    }

                    break;
                case ProjectileBehaviorType.BeamLike:
                    if (context.CasterUnit == null)
                    {
                        MoveLinear(definition.Speed);
                        return;
                    }

                    var forward = direction.sqrMagnitude > 0.0001f
                        ? direction.normalized
                        : context.CasterUnit.transform.forward;
                    var beamPos = context.CasterUnit.transform.position + forward * definition.BeamLength;
                    if (body != null)
                    {
                        body.MovePosition(beamPos);
                    }
                    else
                    {
                        transform.position = beamPos;
                    }

                    break;
                case ProjectileBehaviorType.Return:
                    var returnSpeed = returning
                        ? definition.Speed * Mathf.Max(0.01f, definition.ReturnSpeedMultiplier)
                        : definition.Speed;
                    MoveLinear(returnSpeed);
                    break;
                default:
                    MoveLinear(definition.Speed);
                    break;
            }
        }

        private void MoveLinear(float speed)
        {
            var velocity = direction * speed * Time.deltaTime;
            if (body != null)
            {
                body.MovePosition(body.position + velocity);
                return;
            }

            transform.position += velocity;
        }

        private void RotateTowards(Vector3 worldTarget, float turnSpeedDegrees)
        {
            var desired = (worldTarget - transform.position).normalized;
            if (desired.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var maxRadians = Mathf.Deg2Rad * Mathf.Max(0f, turnSpeedDegrees) * Time.deltaTime;
            direction = Vector3.RotateTowards(direction, desired, maxRadians, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHit(collision.collider);
        }

        private void TryHit(Collider other)
        {
            if (!active || other == null)
            {
                return;
            }

            if (TryInterceptVolume(other))
            {
                return;
            }

            if (!CombatTarget.TryCreate(other.gameObject, out var hitTarget))
            {
                return;
            }

            if (!IsValidTarget(hitTarget))
            {
                return;
            }

            var id = hitTarget.GameObject.GetInstanceID();
            if (hitIds.Contains(id))
            {
                return;
            }

            hitIds.Add(id);
            RaiseLifecycleEvent(ProjectileLifecycleType.Hit, hitTarget);
            ApplyHitEffects(hitTarget);
            if (definition.ForceStopOnFirstHit && definition.BehaviorType != ProjectileBehaviorType.Return)
            {
                Despawn();
                return;
            }

            if (HandlePostHitBehavior(hitTarget))
            {
                return;
            }

            remainingPierce--;
            if (remainingPierce <= 0)
            {
                Despawn();
            }
        }

        private bool IsValidTarget(CombatTarget hitTarget)
        {
            if (hitTarget.Health != null && !hitTarget.Health.IsAlive)
            {
                return false;
            }

            if (!HitResolutionSystem.CanProjectileHit(context, hitTarget))
            {
                return false;
            }

            if (context.CasterUnit != null && hitTarget.Unit == context.CasterUnit)
            {
                var includeSelf = targetingDefinition != null && targetingDefinition.IncludeSelf;
                if (!includeSelf)
                {
                    return false;
                }
            }

            if (targetingSystem != null && targetingDefinition != null)
            {
                var includeSelf = targetingDefinition.IncludeSelf || targetingDefinition.Team == TargetTeam.Self;
                return targetingSystem.IsValidTarget(context.CasterUnit, targetingDefinition, hitTarget, includeSelf);
            }

            return true;
        }

        private void ApplyHitRadius()
        {
            if (definition == null)
            {
                return;
            }

            var radius = Mathf.Max(0f, definition.HitRadius);
            if (runtimeHitCollider == null)
            {
                runtimeHitCollider = GetComponent<SphereCollider>();
            }

            if (runtimeHitCollider != null)
            {
                runtimeHitCollider.radius = radius;
                return;
            }

            if (radius <= 0f)
            {
                return;
            }

            runtimeHitCollider = gameObject.AddComponent<SphereCollider>();
            runtimeHitCollider.isTrigger = true;
            runtimeHitCollider.radius = radius;
        }

        private void ApplyHitEffects(CombatTarget hitTarget)
        {
            if (definition.BehaviorType == ProjectileBehaviorType.Split && definition.SplitCount > 0)
            {
                SpawnSplitProjectiles();
            }

            if (definition.OnHitEffects == null || definition.OnHitEffects.Count == 0)
            {
                return;
            }

            var spellShieldChargesBefore = GetSpellShieldCharges(hitTarget);
            var blockedBySpellShield = false;
            for (int i = 0; i < definition.OnHitEffects.Count; i++)
            {
                context.Executor?.ExecuteEffect(definition.OnHitEffects[i], context, hitTarget, SkillStepTrigger.OnProjectileHit);
                if (HasSpellShieldConsumed(hitTarget, spellShieldChargesBefore))
                {
                    blockedBySpellShield = true;
                    break;
                }
            }

            if (blockedBySpellShield)
            {
                return;
            }

            context.Caster?.NotifyProjectileHit(context, hitTarget);
        }

        private static int GetSpellShieldCharges(CombatTarget target)
        {
            if (target.State == null || !target.State.HasFlag(CombatStateFlags.SpellShielded))
            {
                return 0;
            }

            return target.State.SpellShieldCharges;
        }

        private static bool HasSpellShieldConsumed(CombatTarget target, int chargesBefore)
        {
            if (chargesBefore <= 0 || target.State == null)
            {
                return false;
            }

            return target.State.SpellShieldCharges < chargesBefore;
        }

        private bool HandlePostHitBehavior(CombatTarget hitTarget)
        {
            if (definition == null)
            {
                return false;
            }

            if (definition.BehaviorType == ProjectileBehaviorType.Return && !returning && context.CasterUnit != null)
            {
                returning = true;
                target = default;
                hitIds.Clear();
                RaiseLifecycleEvent(ProjectileLifecycleType.Return, hitTarget);
                return true;
            }

            return false;
        }

        private void SpawnSplitProjectiles()
        {
            if (pool == null || definition == null || splitDepth >= definition.MaxSplitDepth)
            {
                return;
            }

            var count = definition.SplitCount;
            if (count <= 0)
            {
                return;
            }

            var centerAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var total = Mathf.Max(0f, definition.SplitAngle);
            var step = count > 1 ? total / (count - 1) : 0f;
            var start = centerAngle - total * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var angle = start + step * i;
                var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var rot = Quaternion.LookRotation(dir);
                var child = pool.Spawn(definition, transform.position, rot);
                if (child == null)
                {
                    continue;
                }

                child.Initialize(definition, context, default, dir, targetingDefinition, targetingSystem, projectileInstanceId);
                child.SetSplitDepth(splitDepth + 1);
                RaiseLifecycleEvent(ProjectileLifecycleType.Split, default, child.projectileInstanceId);
            }
        }

        private bool TryInterceptVolume(Collider other)
        {
            var interceptor = other.GetComponentInParent<ProjectileInterceptorVolume>();
            if (interceptor == null || !interceptor.ShouldIntercept(context.CasterUnit))
            {
                return false;
            }

            remainingPierce--;
            if (remainingPierce <= 0)
            {
                Despawn();
            }

            return true;
        }

        private void SetSplitDepth(int depth)
        {
            splitDepth = Mathf.Max(0, depth);
        }

        private void Despawn()
        {
            active = false;
            definition = null;
            target = default;
            context = default;
            targetingDefinition = null;
            targetingSystem = null;
            projectileInstanceId = 0;
            parentProjectileInstanceId = 0;
            hitIds.Clear();

            if (pool != null && prefabKey != null)
            {
                pool.Release(prefabKey, this);
                return;
            }

            gameObject.SetActive(false);
        }

        private void ResetVisualTrails()
        {
            if (trailRenderers == null || trailRenderers.Length == 0)
            {
                trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
            }

            for (int i = 0; i < trailRenderers.Length; i++)
            {
                var trail = trailRenderers[i];
                if (trail == null)
                {
                    continue;
                }

                trail.Clear();
                trail.emitting = true;
            }
        }

        private static int GenerateProjectileInstanceId()
        {
            if (nextRuntimeProjectileInstanceId <= 0)
            {
                nextRuntimeProjectileInstanceId = 1;
            }

            return nextRuntimeProjectileInstanceId++;
        }

        private void RaiseLifecycleEvent(ProjectileLifecycleType lifecycleType, CombatTarget lifecycleTarget, int relatedProjectileInstanceId = 0)
        {
            if (definition == null || context.EventHub == null)
            {
                return;
            }

            var evt = new ProjectileLifecycleEvent(
                context.CasterUnit,
                context.Skill,
                definition,
                lifecycleType,
                lifecycleTarget,
                transform.position,
                direction,
                gameObject,
                context.CastId,
                context.StepIndex,
                projectileInstanceId,
                relatedProjectileInstanceId);
            context.EventHub.RaiseProjectileLifecycle(evt);
        }
    }
}
