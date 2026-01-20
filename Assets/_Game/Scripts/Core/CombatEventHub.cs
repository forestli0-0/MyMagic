using System;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 战斗系统运行时事件中心（基于 ScriptableObject），作为全局的观察者中心实现解耦。
    /// 所有核心组件的变更都会在此处汇聚并分发给 UI 或其他监听系统。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Runtime/Combat Event Hub", fileName = "CombatEventHub")]
    public class CombatEventHub : ScriptableObject
    {
        // --- 核心战斗事件 ---
        
        /// <summary> 当任何单位的属性发生变化时触发 </summary>
        public event Action<StatChangedEvent> StatChanged;
        /// <summary> 当单位血量发生变化时触发 </summary>
        public event Action<HealthChangedEvent> HealthChanged;
        /// <summary> 当单位护盾发生变化时触发 </summary>
        public event Action<ShieldChangedEvent> ShieldChanged;
        /// <summary> 当单位死亡时触发 </summary>
        public event Action<HealthComponent> UnitDied;
        /// <summary> 当单位死亡时触发（包含击杀来源） </summary>
        public event Action<UnitKilledEvent> UnitKilled;
        /// <summary> 当单位资源（如法力）发生变化时触发 </summary>
        public event Action<ResourceChangedEvent> ResourceChanged;
        /// <summary> 当技能冷却状态发生变化时触发 </summary>
        public event Action<CooldownChangedEvent> CooldownChanged;
        /// <summary> 当技能开始施放时触发 </summary>
        public event Action<SkillCastEvent> SkillCastStarted;
        /// <summary> 当技能完成施放时触发 </summary>
        public event Action<SkillCastEvent> SkillCastCompleted;
        /// <summary> 当技能施法被打断时触发 </summary>
        public event Action<SkillCastEvent> SkillCastInterrupted;
        /// <summary> 当经验值发生变化时触发 </summary>
        public event Action<ExperienceChangedEvent> ExperienceChanged;
        /// <summary> 当等级发生变化时触发 </summary>
        public event Action<LevelChangedEvent> LevelChanged;
        /// <summary> 当属性点发生变化时触发 </summary>
        public event Action<AttributePointsChangedEvent> AttributePointsChanged;
        /// <summary> 当玩家或系统切换当前目标时触发 </summary>
        public event Action<HealthComponent> TargetChanged;
        /// <summary> 当玩家或系统清除当前目标时触发 </summary>
        public event Action TargetCleared;

        // --- 广播方法 (由 Component 调用) ---

        public void RaiseStatChanged(StatChangedEvent evt) => StatChanged?.Invoke(evt);
        public void RaiseHealthChanged(HealthChangedEvent evt) => HealthChanged?.Invoke(evt);
        public void RaiseShieldChanged(ShieldChangedEvent evt) => ShieldChanged?.Invoke(evt);
        public void RaiseUnitDied(HealthComponent source) => UnitDied?.Invoke(source);
        public void RaiseUnitKilled(UnitKilledEvent evt) => UnitKilled?.Invoke(evt);
        public void RaiseResourceChanged(ResourceChangedEvent evt) => ResourceChanged?.Invoke(evt);
        public void RaiseCooldownChanged(CooldownChangedEvent evt) => CooldownChanged?.Invoke(evt);
        public void RaiseSkillCastStarted(SkillCastEvent evt) => SkillCastStarted?.Invoke(evt);
        public void RaiseSkillCastCompleted(SkillCastEvent evt) => SkillCastCompleted?.Invoke(evt);
        public void RaiseSkillCastInterrupted(SkillCastEvent evt) => SkillCastInterrupted?.Invoke(evt);
        public void RaiseExperienceChanged(ExperienceChangedEvent evt) => ExperienceChanged?.Invoke(evt);
        public void RaiseLevelChanged(LevelChangedEvent evt) => LevelChanged?.Invoke(evt);
        public void RaiseAttributePointsChanged(AttributePointsChangedEvent evt) => AttributePointsChanged?.Invoke(evt);
        public void RaiseTargetChanged(HealthComponent target) => TargetChanged?.Invoke(target);
        public void RaiseTargetCleared() => TargetCleared?.Invoke();

        private void OnDisable()
        {
            // 运行时清理，防止在编辑器切换或关闭时产生事件残留导致 NullReference
            StatChanged = null;
            HealthChanged = null;
            ShieldChanged = null;
            UnitDied = null;
            UnitKilled = null;
            ResourceChanged = null;
            CooldownChanged = null;
            SkillCastStarted = null;
            SkillCastCompleted = null;
            SkillCastInterrupted = null;
            ExperienceChanged = null;
            LevelChanged = null;
            AttributePointsChanged = null;
            TargetChanged = null;
            TargetCleared = null;
        }
    }
}
