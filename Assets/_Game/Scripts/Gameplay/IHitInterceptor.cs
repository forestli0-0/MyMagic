using CombatSystem.Data;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 命中拦截器扩展点：返回 true 表示拦截并阻止后续效果。
    /// </summary>
    public interface IHitInterceptor
    {
        bool TryIntercept(EffectDefinition effect, SkillRuntimeContext context, CombatTarget target, SkillStepTrigger trigger);
    }
}
