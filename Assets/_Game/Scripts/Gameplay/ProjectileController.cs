using System.Collections.Generic;
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
        private bool active;
        private Rigidbody body;
        private readonly List<int> hitIds = new List<int>(8);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
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
            definition = projectileDefinition;
            context = runtimeContext;
            target = initialTarget;
            targetingDefinition = targeting;
            targetingSystem = targetingSystemRef;
            direction = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : transform.forward;
            expireTime = definition.Lifetime > 0f ? Time.time + definition.Lifetime : float.PositiveInfinity;
            remainingPierce = definition.Pierce ? Mathf.Max(1, definition.MaxPierce) : 1;
            active = true;
            hitIds.Clear();
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
        }

        private void UpdateDirection()
        {
            if (!definition.Homing || target.Transform == null)
            {
                return;
            }

            var desired = (target.Transform.position - transform.position).normalized;
            var maxRadians = Mathf.Deg2Rad * Mathf.Max(0f, definition.HomingTurnSpeed) * Time.deltaTime;
            direction = Vector3.RotateTowards(direction, desired, maxRadians, 0f);
        }

        private void MoveProjectile()
        {
            var velocity = direction * definition.Speed * Time.deltaTime;
            if (body != null)
            {
                body.MovePosition(body.position + velocity);
                return;
            }

            transform.position += velocity;
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
            ApplyHitEffects(hitTarget);

            remainingPierce--;
            if (remainingPierce <= 0)
            {
                Despawn();
            }
        }

        private bool IsValidTarget(CombatTarget hitTarget)
        {
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

        private void ApplyHitEffects(CombatTarget hitTarget)
        {
            if (definition.OnHitEffects == null || definition.OnHitEffects.Count == 0)
            {
                return;
            }

            for (int i = 0; i < definition.OnHitEffects.Count; i++)
            {
                context.Executor?.ExecuteEffect(definition.OnHitEffects[i], context, hitTarget, SkillStepTrigger.OnProjectileHit);
            }

            context.Caster?.NotifyProjectileHit(context, hitTarget);
        }

        private void Despawn()
        {
            active = false;
            definition = null;
            target = default;
            context = default;
            targetingDefinition = null;
            targetingSystem = null;
            hitIds.Clear();

            if (pool != null && prefabKey != null)
            {
                pool.Release(prefabKey, this);
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
