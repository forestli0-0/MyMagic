using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 命中裁决系统：统一处理可选取、可命中、法术护盾与无敌等规则。
    /// </summary>
    public class HitResolutionSystem : MonoBehaviour
    {
        private static HitResolutionSystem instance;

        [SerializeField] private bool blockInvisibleTargets = true;

        private readonly List<IHitInterceptor> interceptors = new List<IHitInterceptor>(8);

        private void Awake()
        {
            instance = this;
            RefreshInterceptors();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static void RegisterInterceptor(IHitInterceptor interceptor)
        {
            if (instance == null || interceptor == null || instance.interceptors.Contains(interceptor))
            {
                return;
            }

            instance.interceptors.Add(interceptor);
        }

        public static void UnregisterInterceptor(IHitInterceptor interceptor)
        {
            if (instance == null || interceptor == null)
            {
                return;
            }

            instance.interceptors.Remove(interceptor);
        }

        public static bool CanSelectTarget(UnitRoot caster, TargetingDefinition definition, CombatTarget target)
        {
            if (!target.IsValid)
            {
                return false;
            }

            if (IsUntargetableFor(caster, target))
            {
                return false;
            }

            if (ShouldBlockByVisibility(caster, target))
            {
                return false;
            }

            return true;
        }

        public static bool CanProjectileHit(SkillRuntimeContext context, CombatTarget target)
        {
            if (!target.IsValid)
            {
                return false;
            }

            if (IsUntargetableFor(context.CasterUnit, target))
            {
                return false;
            }

            if (ShouldBlockByVisibility(context.CasterUnit, target))
            {
                return false;
            }

            return true;
        }

        public static bool CanApplyEffect(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target, SkillStepTrigger trigger)
        {
            if (effect == null || !target.IsValid)
            {
                return true;
            }

            var isCombatStateRemoval = effect.EffectType == EffectType.CombatState
                && effect.CombatStateMode == CombatStateEffectMode.RemoveFlags;

            if (!isCombatStateRemoval && IsUntargetableFor(context.CasterUnit, target))
            {
                return false;
            }

            if (!isCombatStateRemoval && ShouldBlockByVisibility(context.CasterUnit, target))
            {
                return false;
            }

            if (effect.EffectType == EffectType.Damage && IsInvulnerable(target))
            {
                return false;
            }

            if (ShouldConsumeSpellShield(effect, context, target))
            {
                return false;
            }

            if (instance == null || instance.interceptors.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < instance.interceptors.Count; i++)
            {
                var interceptor = instance.interceptors[i];
                if (interceptor != null && interceptor.TryIntercept(effect, context, target, trigger))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsInvulnerable(CombatTarget target)
        {
            return target.State != null && target.State.HasFlag(CombatStateFlags.Invulnerable);
        }

        private static bool IsUntargetableFor(UnitRoot caster, CombatTarget target)
        {
            if (target.State == null || !target.State.HasFlag(CombatStateFlags.Untargetable))
            {
                return false;
            }

            // 自身逻辑允许通过。
            if (caster != null && target.Unit != null && caster == target.Unit)
            {
                return false;
            }

            return true;
        }

        private static bool ShouldBlockByVisibility(UnitRoot caster, CombatTarget target)
        {
            if (!target.IsValid || target.Visibility == null)
            {
                return false;
            }

            if (instance != null && !instance.blockInvisibleTargets)
            {
                return false;
            }

            return !VisionSystem.IsTargetVisible(caster, target);
        }

        private static bool ShouldConsumeSpellShield(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target)
        {
            if (target.State == null || !target.State.HasFlag(CombatStateFlags.SpellShielded))
            {
                return false;
            }

            if (!IsHostile(context, target))
            {
                return false;
            }

            switch (effect.EffectType)
            {
                case EffectType.Heal:
                case EffectType.Shield:
                case EffectType.Cleanse:
                case EffectType.RemoveBuff:
                    return false;
                default:
                    return target.State.ConsumeSpellShield();
            }
        }

        private static bool IsHostile(SkillRuntimeContext context, CombatTarget target)
        {
            if (context.CasterUnit == null)
            {
                return true;
            }

            if (target.Unit != null && context.CasterUnit == target.Unit)
            {
                return false;
            }

            var casterTeam = context.CasterUnit.Team;
            var targetTeam = target.Team;
            if (casterTeam == null || targetTeam == null)
            {
                return true;
            }

            return !casterTeam.IsSameTeam(targetTeam);
        }

        private void RefreshInterceptors()
        {
            interceptors.Clear();
            var components = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IHitInterceptor interceptor)
                {
                    interceptors.Add(interceptor);
                }
            }
        }
    }
}
