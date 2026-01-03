using System;
using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 冷却组件，负责跟踪所有技能的冷却状态。
    /// </summary>
    public class CooldownComponent : MonoBehaviour
    {
        [Tooltip("全局事件中心")]
        [SerializeField] private CombatEventHub eventHub;

        // 存储每个技能对应的冷却结束时间戳
        private readonly Dictionary<SkillDefinition, float> cooldownEndTimes = new Dictionary<SkillDefinition, float>(32);
        // 用于存储本帧过期的技能，避免在遍历时修改字典
        private readonly List<SkillDefinition> expired = new List<SkillDefinition>(16);

        /// <summary>
        /// 当某个技能的冷却状态发生变化（开始或结束）时派发。
        /// </summary>
        public event Action<CooldownChangedEvent> CooldownChanged;

        private void Update()
        {
            if (cooldownEndTimes.Count == 0)
            {
                return;
            }

            var now = Time.time;
            expired.Clear();

            // 查找已到达结束时间的技能
            foreach (var pair in cooldownEndTimes)
            {
                if (pair.Value <= now)
                {
                    expired.Add(pair.Key);
                }
            }

            // 执行过期逻辑并清理
            for (int i = 0; i < expired.Count; i++)
            {
                var skill = expired[i];
                cooldownEndTimes.Remove(skill);
                RaiseCooldownChanged(skill, 0f, 0f, false);
            }
        }

        /// <summary>
        /// 设置外部事件中心引用。
        /// </summary>
        public void SetEventHub(CombatEventHub hub)
        {
            eventHub = hub;
        }

        /// <summary>
        /// 检查指定技能是否已冷却完毕。
        /// </summary>
        public bool IsReady(SkillDefinition skill)
        {
            return GetRemaining(skill) <= 0f;
        }

        /// <summary>
        /// 获取指定技能的剩余冷却时间（秒）。
        /// </summary>
        public float GetRemaining(SkillDefinition skill)
        {
            if (skill == null)
            {
                return 0f;
            }

            if (!cooldownEndTimes.TryGetValue(skill, out var endTime))
            {
                return 0f;
            }

            return Mathf.Max(0f, endTime - Time.time);
        }

        /// <summary>
        /// 为指定技能开启冷却。
        /// </summary>
        /// <param name="skill">目标技能配置</param>
        /// <param name="duration">冷却持续时长</param>
        public void StartCooldown(SkillDefinition skill, float duration)
        {
            if (skill == null)
            {
                return;
            }

            var endTime = Time.time + Mathf.Max(0f, duration);
            cooldownEndTimes[skill] = endTime;
            RaiseCooldownChanged(skill, duration, duration, true);
        }

        /// <summary>
        /// 强制清除指定技能的冷却。
        /// </summary>
        public void ClearCooldown(SkillDefinition skill)
        {
            if (skill == null)
            {
                return;
            }

            if (cooldownEndTimes.Remove(skill))
            {
                RaiseCooldownChanged(skill, 0f, 0f, false);
            }
        }

        /// <summary>
        /// 清除所有正在进行的冷却计时。
        /// </summary>
        public void ClearAll()
        {
            if (cooldownEndTimes.Count == 0)
            {
                return;
            }

            expired.Clear();
            foreach (var pair in cooldownEndTimes)
            {
                expired.Add(pair.Key);
            }

            cooldownEndTimes.Clear();

            for (int i = 0; i < expired.Count; i++)
            {
                RaiseCooldownChanged(expired[i], 0f, 0f, false);
            }
        }

        /// <summary>
        /// 派发冷却状态变更事件。
        /// </summary>
        private void RaiseCooldownChanged(SkillDefinition skill, float remaining, float duration, bool isCoolingDown)
        {
            var evt = new CooldownChangedEvent(this, skill, remaining, duration, isCoolingDown);
            CooldownChanged?.Invoke(evt);
            eventHub?.RaiseCooldownChanged(evt);
        }
    }
}
