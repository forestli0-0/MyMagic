using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 效果执行器，负责根据 EffectDefinition 配置执行各类战斗效果。
    /// </summary>
    /// <remarks>
    /// 支持的效果类型：
    /// - Damage: 造成伤害
    /// - Heal: 治疗
    /// - ApplyBuff: 施加 Buff
    /// - RemoveBuff: 移除 Buff
    /// - Projectile: 发射投射物
    /// - Move: 位移（击退/拉拽/冲刺）
    /// - Resource: 资源操作（回复/消耗）
    /// - Summon: 召唤单位
    /// - TriggerSkill: 触发另一个技能
    /// </remarks>
    public class EffectExecutor : MonoBehaviour
    {
        [Tooltip("目标选择系统，用于覆盖目标逻辑")]
        [SerializeField] private TargetingSystem targetingSystem;
        
        [Tooltip("投射物对象池")]
        [SerializeField] private ProjectilePool projectilePool;

        /// <summary>
        /// 执行单个效果。
        /// </summary>
        /// <param name="effect">效果配置定义</param>
        /// <param name="context">技能运行时上下文</param>
        /// <param name="target">目标</param>
        /// <param name="trigger">触发时机</param>
        public void ExecuteEffect(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target, SkillStepTrigger trigger)
        {
            if (effect == null)
            {
                return;
            }

            // 检查效果的前置条件
            if (effect.Condition != null && !ConditionEvaluator.Evaluate(effect.Condition, context, target))
            {
                return;
            }

            // 如果效果定义了覆盖目标逻辑，重新收集目标
            if (effect.OverrideTargeting != null && targetingSystem != null)
            {
                var overrideTargets = SimpleListPool<CombatTarget>.Get();
                targetingSystem.CollectTargets(effect.OverrideTargeting, context.CasterUnit, target.GameObject, overrideTargets);

                // 对每个新目标应用效果
                for (int i = 0; i < overrideTargets.Count; i++)
                {
                    ApplyEffectInternal(effect, context, overrideTargets[i], trigger);
                }

                SimpleListPool<CombatTarget>.Release(overrideTargets);
                return;
            }

            // 对原始目标应用效果
            ApplyEffectInternal(effect, context, target, trigger);
        }

        /// <summary>
        /// 内部方法：根据效果类型分发到具体的执行逻辑。
        /// </summary>
        private void ApplyEffectInternal(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target, SkillStepTrigger trigger)
        {
            var effectValue = ModifierResolver.ApplyEffectModifiers(effect.Value, effect, context, target, ModifierParameters.EffectValue);

            switch (effect.EffectType)
            {
                case EffectType.Damage:
                    // 伤害效果：通过 DamageSystem 处理伤害公式
                    DamageSystem.ApplyDamage(effectValue, effect, context, target, trigger);
                    break;
                case EffectType.Heal:
                    // 治疗效果：直接调用目标的 HealthComponent
                    if (target.Health != null)
                    {
                        target.Health.Heal(effectValue);
                    }
                    break;
                case EffectType.ApplyBuff:
                    // 施加 Buff
                    ApplyBuff(effect, target);
                    break;
                case EffectType.RemoveBuff:
                    // 移除 Buff
                    RemoveBuff(effect, target);
                    break;
                case EffectType.Projectile:
                    // 发射投射物
                    LaunchProjectile(effect, context, target);
                    break;
                case EffectType.Move:
                    // 位移效果
                    var moveDistance = ModifierResolver.ApplyEffectModifiers(effect.MoveDistance, effect, context, target, ModifierParameters.EffectMoveDistance);
                    ApplyMove(moveDistance, effect, context, target);
                    break;
                case EffectType.Resource:
                    // 资源操作（法力等）
                    ApplyResource(effectValue, effect, target);
                    break;
                case EffectType.Summon:
                    // 召唤单位
                    Summon(effect, context, target);
                    break;
                case EffectType.TriggerSkill:
                    // 触发另一个技能
                    TriggerSkill(effect, context, target);
                    break;
            }
        }

        /// <summary>
        /// 施加 Buff 到目标。
        /// </summary>
        private static void ApplyBuff(EffectDefinition effect, CombatTarget target)
        {
            if (!target.IsValid)
            {
                return;
            }

            if (effect.Buff == null)
            {
                return;
            }

            // 获取目标的 BuffController 并施加 Buff
            var controller = target.Unit != null ? target.Unit.GetComponent<BuffController>() : target.GameObject.GetComponent<BuffController>();
            controller?.ApplyBuff(effect.Buff);
        }

        /// <summary>
        /// 从目标移除 Buff。
        /// </summary>
        private static void RemoveBuff(EffectDefinition effect, CombatTarget target)
        {
            if (!target.IsValid)
            {
                return;
            }

            if (effect.Buff == null)
            {
                return;
            }

            var controller = target.Unit != null ? target.Unit.GetComponent<BuffController>() : target.GameObject.GetComponent<BuffController>();
            controller?.RemoveBuff(effect.Buff);
        }

        /// <summary>
        /// 发射投射物。
        /// </summary>
        private void LaunchProjectile(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target)
        {
            if (effect.Projectile == null)
            {
                return;
            }

            // 懒加载投射物池
            if (projectilePool == null)
            {
                projectilePool = FindObjectOfType<ProjectilePool>();
            }

            if (projectilePool == null)
            {
                return;
            }

            // 计算生成位置和方向
            var spawnPosition = context.CasterUnit != null ? context.CasterUnit.transform.position : transform.position;
            var direction = context.CasterUnit != null ? context.CasterUnit.transform.forward : transform.forward;

            // 如果有明确目标，朝向目标
            if (target.IsValid)
            {
                var toTarget = target.Transform.position - spawnPosition;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    direction = toTarget.normalized;
                }
            }

            // 从对象池生成投射物
            var rotation = direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            var instance = projectilePool.Spawn(effect.Projectile, spawnPosition, rotation);
            if (instance == null)
            {
                return;
            }

            // 初始化投射物参数
            var targeting = effect.OverrideTargeting != null ? effect.OverrideTargeting : context.Skill?.Targeting;
            instance.Initialize(effect.Projectile, context, target, direction, targeting, targetingSystem);
        }

        /// <summary>
        /// 应用位移效果（击退/拉拽/冲刺）。
        /// </summary>
        private static void ApplyMove(float moveDistance, EffectDefinition effect, SkillRuntimeContext context, CombatTarget target)
        {
            if (target.Transform == null || moveDistance == 0f)
            {
                return;
            }

            // 根据位移类型计算方向
            var direction = Vector3.zero;
            if (effect.MoveStyle == MoveStyle.Knockback || effect.MoveStyle == MoveStyle.Pull)
            {
                // 击退/拉拽：基于施法者到目标的方向
                if (context.CasterUnit != null)
                {
                    direction = (target.Transform.position - context.CasterUnit.transform.position).normalized;
                    if (effect.MoveStyle == MoveStyle.Pull)
                    {
                        direction = -direction; // 拉拽方向相反
                    }
                }
            }
            else
            {
                // 冲刺等：使用施法者朝向
                direction = context.CasterUnit != null ? context.CasterUnit.transform.forward : target.Transform.forward;
            }

            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            // 直接位移（简化实现，实际应该用 MovementComponent）
            target.Transform.position += direction * moveDistance;
        }

        /// <summary>
        /// 应用资源效果（回复或消耗法力等）。
        /// </summary>
        private static void ApplyResource(float value, EffectDefinition effect, CombatTarget target)
        {
            if (target.Resource == null)
            {
                return;
            }

            // 检查资源类型是否匹配
            if (target.Resource.ResourceType != effect.ResourceType)
            {
                return;
            }

            // 正值为回复，负值为消耗
            if (value >= 0f)
            {
                target.Resource.Restore(value);
            }
            else
            {
                target.Resource.Spend(-value);
            }
        }

        /// <summary>
        /// 召唤单位。
        /// </summary>
        /// <remarks>
        /// [性能提示] 当前使用 Object.Instantiate 直接创建实例。
        /// 对于频繁召唤的单位（如召唤物、陷阱），建议实现对象池化，
        /// 类似 ProjectilePool 的模式。
        /// </remarks>
        private static void Summon(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target)
        {
            // 优先使用直接配置的 Prefab，否则使用 UnitDefinition 的 Prefab
            var prefab = effect.SummonPrefab != null ? effect.SummonPrefab : effect.SummonUnit?.Prefab;
            if (prefab == null)
            {
                return;
            }

            // 在目标位置生成（无目标时在施法者位置）
            var spawnPosition = target.IsValid ? target.Transform.position : (context.CasterUnit != null ? context.CasterUnit.transform.position : Vector3.zero);
            
            // [性能] 直接 Instantiate 会产生 GC，高频召唤场景建议使用对象池
            Object.Instantiate(prefab, spawnPosition, Quaternion.identity);
        }

        /// <summary>
        /// 触发另一个技能。
        /// </summary>
        private static void TriggerSkill(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target)
        {
            if (effect.TriggeredSkill == null || context.Caster == null)
            {
                return;
            }

            // 使用当前施法者释放触发的技能
            context.Caster.TryCast(effect.TriggeredSkill, target.IsValid ? target.GameObject : null);
        }
    }
}
