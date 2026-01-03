using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// Buff 控制器，负责管理单位身上所有 Buff 实例的生命周期。
    /// </summary>
    /// <remarks>
    /// 核心职责：
    /// - 应用新 Buff（处理堆叠规则：刷新/延长/独立）
    /// - 追踪 Buff 剩余时间并自动过期移除
    /// - 提供 Buff 状态查询接口
    /// 
    /// 注意：当前版本尚未实现 Tick 触发器，后续需要扩展。
    /// </remarks>
    public class BuffController : MonoBehaviour
    {
        // 当前激活的 Buff 实例列表
        private readonly List<BuffInstance> activeBuffs = new List<BuffInstance>(16);
        
        // 通过 Buff 定义快速查找其在 activeBuffs 中的索引
        private readonly Dictionary<BuffDefinition, int> indexByBuff = new Dictionary<BuffDefinition, int>(16);
        
        // 用于收集本帧过期的 Buff 索引，避免在遍历时修改列表
        private readonly List<int> expiredIndices = new List<int>(8);

        /// <summary>
        /// 获取当前激活的 Buff 实例只读列表。
        /// </summary>
        public IReadOnlyList<BuffInstance> ActiveBuffs => activeBuffs;

        private void Update()
        {
            // 无激活 Buff 时直接跳过
            if (activeBuffs.Count == 0)
            {
                return;
            }

            var now = Time.time;
            expiredIndices.Clear();

            // 第一遍：收集所有已过期的 Buff 索引
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                var instance = activeBuffs[i];
                // EndTime <= 0 表示永久 Buff，不会过期
                if (instance.EndTime > 0f && instance.EndTime <= now)
                {
                    expiredIndices.Add(i);
                }
            }

            // 第二遍：从后向前移除过期 Buff（避免索引偏移问题）
            for (int i = expiredIndices.Count - 1; i >= 0; i--)
            {
                RemoveAt(expiredIndices[i]);
            }
        }

        /// <summary>
        /// 检查单位是否拥有指定的 Buff。
        /// </summary>
        /// <param name="buff">要检查的 Buff 定义</param>
        /// <returns>若拥有该 Buff 则返回 true</returns>
        public bool HasBuff(BuffDefinition buff)
        {
            return buff != null && indexByBuff.ContainsKey(buff);
        }

        /// <summary>
        /// 获取指定 Buff 的当前堆叠层数。
        /// </summary>
        /// <param name="buff">目标 Buff 定义</param>
        /// <returns>堆叠层数，若不存在该 Buff 则返回 0</returns>
        public int GetStacks(BuffDefinition buff)
        {
            if (buff == null)
            {
                return 0;
            }

            if (indexByBuff.TryGetValue(buff, out var index))
            {
                return activeBuffs[index].Stacks;
            }

            return 0;
        }

        /// <summary>
        /// 应用一个 Buff 到该单位。
        /// </summary>
        /// <remarks>
        /// 根据 Buff 的堆叠规则处理重复应用：
        /// - Refresh: 刷新持续时间到完整值
        /// - Extend: 在剩余时间基础上延长
        /// - Independent: 仅增加层数，不改变时间
        /// </remarks>
        /// <param name="buff">要应用的 Buff 定义</param>
        public void ApplyBuff(BuffDefinition buff)
        {
            if (buff == null)
            {
                return;
            }

            // 已存在该 Buff，根据堆叠规则处理
            if (indexByBuff.TryGetValue(buff, out var index))
            {
                var instance = activeBuffs[index];
                var duration = buff.Duration;
                var now = Time.time;

                // 增加层数（受 MaxStacks 限制）
                instance.Stacks = Mathf.Clamp(instance.Stacks + 1, 1, Mathf.Max(1, buff.MaxStacks));

                // 根据堆叠规则更新结束时间
                if (buff.StackingRule == BuffStackingRule.Refresh)
                {
                    // 刷新：重置为完整持续时间
                    instance.EndTime = duration > 0f ? now + duration : -1f;
                }
                else if (buff.StackingRule == BuffStackingRule.Extend)
                {
                    // 延长：在当前剩余时间基础上增加
                    if (duration > 0f)
                    {
                        instance.EndTime = instance.EndTime > 0f ? instance.EndTime + duration : now + duration;
                    }
                }
                else
                {
                    // Independent：仅更新层数，时间不变（除非原本是永久）
                    instance.EndTime = duration > 0f ? now + duration : instance.EndTime;
                }

                activeBuffs[index] = instance;
                return;
            }

            // 新增 Buff 实例
            var endTime = buff.Duration > 0f ? Time.time + buff.Duration : -1f;
            var newInstance = new BuffInstance(buff, 1, endTime);
            indexByBuff[buff] = activeBuffs.Count;
            activeBuffs.Add(newInstance);
        }

        /// <summary>
        /// 移除指定的 Buff。
        /// </summary>
        /// <param name="buff">要移除的 Buff 定义</param>
        /// <returns>若成功移除返回 true</returns>
        public bool RemoveBuff(BuffDefinition buff)
        {
            if (buff == null)
            {
                return false;
            }

            if (indexByBuff.TryGetValue(buff, out var index))
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 内部方法：按索引移除 Buff，使用"交换删除"策略保持性能。
        /// </summary>
        /// <remarks>
        /// 将最后一个元素移动到被删除位置，然后移除末尾元素。
        /// 这样可以避免数组元素整体移动，保持 O(1) 删除复杂度。
        /// </remarks>
        private void RemoveAt(int index)
        {
            var lastIndex = activeBuffs.Count - 1;
            var removed = activeBuffs[index];

            // 如果不是最后一个元素，需要交换
            if (index != lastIndex)
            {
                var last = activeBuffs[lastIndex];
                activeBuffs[index] = last;
                indexByBuff[last.Definition] = index;
            }

            // 移除末尾并更新索引映射
            activeBuffs.RemoveAt(lastIndex);
            indexByBuff.Remove(removed.Definition);
        }

        /// <summary>
        /// Buff 运行时实例数据结构。
        /// </summary>
        public struct BuffInstance
        {
            /// <summary>Buff 的配置定义</summary>
            public BuffDefinition Definition;
            /// <summary>当前堆叠层数</summary>
            public int Stacks;
            /// <summary>结束时间戳，-1 表示永久</summary>
            public float EndTime;

            public BuffInstance(BuffDefinition definition, int stacks, float endTime)
            {
                Definition = definition;
                Stacks = stacks;
                EndTime = endTime;
            }
        }
    }
}
