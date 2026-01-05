using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 目标选择系统，负责根据 TargetingDefinition 配置筛选和收集战斗目标。
    /// </summary>
    /// <remarks>
    /// 核心功能：
    /// - 支持多种选择模式：Self（自身）、Single（单体）、Cone（锥形）、Sphere（球形）、Random（随机）、Chain（链式）
    /// - 支持阵营过滤：敌方、友方、任意
    /// - 支持标签过滤：必需标签、排除标签
    /// - 支持多种排序策略：最近、最远、血量最低/最高
    /// 
    /// 性能优化：
    /// - 使用预分配的 Collider 数组避免 GC
    /// - 使用 SimpleListPool 复用临时列表
    /// </remarks>
    public class TargetingSystem : MonoBehaviour
    {
        [Tooltip("目标检测的物理层掩码")]
        [SerializeField] private LayerMask targetLayers = ~0;
        
        [Tooltip("单次检测最大 Collider 数量")]
        [SerializeField] private int maxColliders = 64;

        // 预分配的碰撞体缓冲区，避免每帧分配
        private Collider[] overlapBuffer;

        private void Awake()
        {
            // 确保至少有 1 个槽位
            if (maxColliders < 1)
            {
                maxColliders = 1;
            }

            // 预分配碰撞体数组
            overlapBuffer = new Collider[maxColliders];
        }

        /// <summary>
        /// 根据目标定义收集有效的战斗目标。
        /// </summary>
        /// <param name="definition">目标选择配置</param>
        /// <param name="caster">施法者单位</param>
        /// <param name="explicitTarget">显式指定的目标（如玩家点击选中的敌人）</param>
        /// <param name="results">输出结果列表，调用前会被清空</param>
        public void CollectTargets(TargetingDefinition definition, UnitRoot caster, GameObject explicitTarget, List<CombatTarget> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            // 无目标定义时默认选择自身
            if (definition == null)
            {
                AddSelf(caster, results);
                return;
            }

            // 判断是否可以选中自身
            var includeSelf = definition.IncludeSelf || definition.Team == TargetTeam.Self;

            // 根据选择模式分发到不同的处理逻辑
            switch (definition.Mode)
            {
                case TargetingMode.Self:
                    // 自身模式：只选择施法者
                    AddSelf(caster, results);
                    return;
                case TargetingMode.Single:
                    // 单体模式：优先使用显式目标，否则自动选择最优目标
                    if (TryAddExplicitTarget(definition, caster, explicitTarget, includeSelf, results))
                    {
                        return;
                    }
                    SelectSingle(definition, caster, includeSelf, results);
                    return;
                case TargetingMode.Cone:
                    // 锥形模式：在前方锥形区域内选择目标
                    CollectArea(definition, caster, includeSelf, results, true);
                    return;
                case TargetingMode.Sphere:
                case TargetingMode.Random:
                    // 球形/随机/链式模式：在球形区域内选择目标
                    CollectArea(definition, caster, includeSelf, results, false);
                    return;
                case TargetingMode.Chain:
                    // 链式模式：先选定首目标，再依次跳跃
                    CollectChainTargets(definition, caster, explicitTarget, includeSelf, results);
                    return;
                default:
                    AddSelf(caster, results);
                    return;
            }
        }

        /// <summary>
        /// 收集技能范围内的所有有效候选目标（不限制数量）。
        /// 用于技能指示器预览时的目标选择。
        /// </summary>
        /// <param name="definition">目标选择配置</param>
        /// <param name="caster">施法者单位</param>
        /// <param name="results">输出结果列表，调用前会被清空</param>
        public void CollectAllCandidates(TargetingDefinition definition, UnitRoot caster, List<CombatTarget> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            if (definition == null || caster == null)
            {
                return;
            }

            // 自身模式不需要收集候选
            if (definition.Mode == TargetingMode.Self)
            {
                AddSelf(caster, results);
                return;
            }

            var includeSelf = definition.IncludeSelf || definition.Team == TargetTeam.Self;
            var origin = caster.transform.position;

            // 锥形模式使用角度过滤
            var useCone = definition.Mode == TargetingMode.Cone;

            // 获取范围
            var range = useCone || definition.Mode == TargetingMode.Sphere
                ? GetAreaRange(definition)
                : Mathf.Max(0f, definition.Range);

            // 收集所有候选目标
            CollectCandidates(definition, caster, includeSelf, origin, range, useCone, results);
        }

        /// <summary>
        /// 验证目标是否满足选择条件。
        /// </summary>
        /// <param name="caster">施法者单位</param>
        /// <param name="definition">目标选择配置</param>
        /// <param name="target">待验证的目标</param>
        /// <param name="includeSelf">是否允许选中自身</param>
        /// <returns>若目标有效则返回 true</returns>
        public bool IsValidTarget(UnitRoot caster, TargetingDefinition definition, CombatTarget target, bool includeSelf)
        {
            // 目标本身无效（已销毁或未正确初始化）
            if (!target.IsValid)
            {
                return false;
            }

            // 检查是否为自身且不允许选中自身
            if (!includeSelf && caster != null && target.Unit == caster)
            {
                return false;
            }

            // 无定义时跳过后续检查
            if (definition == null)
            {
                return true;
            }

            // 阵营检查
            if (!IsTeamMatch(caster, definition.Team, target))
            {
                return false;
            }

            // 必需标签检查
            if (!HasRequiredTags(definition.RequiredTags, target))
            {
                return false;
            }

            // 排除标签检查
            if (HasBlockedTags(definition.BlockedTags, target))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 将施法者自身添加到结果列表。
        /// </summary>
        private void AddSelf(UnitRoot caster, List<CombatTarget> results)
        {
            if (caster == null)
            {
                return;
            }

            if (CombatTarget.TryCreate(caster.gameObject, out var target))
            {
                results.Add(target);
            }
        }

        /// <summary>
        /// 尝试将显式指定的目标添加到结果中。
        /// </summary>
        /// <returns>若显式目标有效且成功添加则返回 true</returns>
        private bool TryAddExplicitTarget(TargetingDefinition definition, UnitRoot caster, GameObject explicitTarget, bool includeSelf, List<CombatTarget> results)
        {
            if (explicitTarget == null)
            {
                return false;
            }

            if (CombatTarget.TryCreate(explicitTarget, out var target) && IsValidTarget(caster, definition, target, includeSelf))
            {
                results.Add(target);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 单体模式：从候选目标中选择最优的一个。
        /// </summary>
        private void SelectSingle(TargetingDefinition definition, UnitRoot caster, bool includeSelf, List<CombatTarget> results)
        {
            var origin = caster != null ? caster.transform.position : transform.position;
            var range = Mathf.Max(0f, definition.Range);

            // 从对象池获取临时列表
            var candidates = SimpleListPool<CombatTarget>.Get();
            CollectCandidates(definition, caster, includeSelf, origin, range, false, candidates);

            if (candidates.Count > 0)
            {
                if (definition.Sort == TargetSort.Random)
                {
                    // 随机排序：随机选择一个
                    var index = Random.Range(0, candidates.Count);
                    results.Add(candidates[index]);
                }
                else
                {
                    // 其他排序：选择最优候选
                    var best = SelectBestCandidate(definition.Sort, origin, candidates);
                    if (best.IsValid)
                    {
                        results.Add(best);
                    }
                }
            }

            // 归还列表到对象池
            SimpleListPool<CombatTarget>.Release(candidates);
        }

        /// <summary>
        /// 区域模式：收集球形或锥形范围内的多个目标。
        /// </summary>
        /// <param name="useCone">是否使用锥形检测</param>
        private void CollectArea(TargetingDefinition definition, UnitRoot caster, bool includeSelf, List<CombatTarget> results, bool useCone)
        {
            var origin = caster != null ? caster.transform.position : transform.position;
            var range = GetAreaRange(definition);
            // 随机模式强制使用随机排序
            var sort = definition.Mode == TargetingMode.Random ? TargetSort.Random : definition.Sort;

            var candidates = SimpleListPool<CombatTarget>.Get();
            CollectCandidates(definition, caster, includeSelf, origin, range, useCone, candidates);
            SelectTargets(definition, sort, origin, candidates, results);
            SimpleListPool<CombatTarget>.Release(candidates);
        }

        private void CollectChainTargets(TargetingDefinition definition, UnitRoot caster, GameObject explicitTarget, bool includeSelf, List<CombatTarget> results)
        {
            var range = Mathf.Max(0f, definition.Range);
            if (range <= 0f)
            {
                return;
            }

            var origin = caster != null ? caster.transform.position : transform.position;
            var hasExplicit = false;
            var explicitCombatTarget = default(CombatTarget);

            if (explicitTarget != null && CombatTarget.TryCreate(explicitTarget, out explicitCombatTarget))
            {
                if (IsValidTarget(caster, definition, explicitCombatTarget, includeSelf))
                {
                    hasExplicit = true;
                    if (explicitCombatTarget.Transform != null)
                    {
                        origin = explicitCombatTarget.Transform.position;
                    }
                }
            }

            var candidates = SimpleListPool<CombatTarget>.Get();
            CollectCandidates(definition, caster, includeSelf, origin, range, false, candidates);

            var maxTargets = Mathf.Max(1, definition.MaxTargets);
            var first = default(CombatTarget);

            if (hasExplicit)
            {
                first = explicitCombatTarget;
            }
            else if (!TrySelectInitialChainTarget(definition.Sort, origin, candidates, out first))
            {
                SimpleListPool<CombatTarget>.Release(candidates);
                return;
            }

            if (!first.IsValid)
            {
                SimpleListPool<CombatTarget>.Release(candidates);
                return;
            }

            results.Add(first);
            RemoveCandidate(candidates, first);

            var chainRangeSqr = range * range;
            var last = first;

            while (results.Count < maxTargets)
            {
                if (!TrySelectNextChainTarget(definition.Sort, last.Transform != null ? last.Transform.position : origin, candidates, chainRangeSqr, out var next))
                {
                    break;
                }

                results.Add(next);
                RemoveCandidate(candidates, next);
                last = next;
            }

            SimpleListPool<CombatTarget>.Release(candidates);
        }

        /// <summary>
        /// 收集球形范围内的所有候选目标。
        /// </summary>
        /// <remarks>
        /// 使用 Physics.OverlapSphereNonAlloc 避免堆分配。
        /// 如果启用锥形检测，会额外检查目标是否在施法者前方的锥形角度内。
        /// </remarks>
        private void CollectCandidates(
            TargetingDefinition definition,
            UnitRoot caster,
            bool includeSelf,
            Vector3 origin,
            float range,
            bool useCone,
            List<CombatTarget> candidates)
        {
            if (range <= 0f)
            {
                return;
            }

            // 使用预分配缓冲区进行球形重叠检测
            var count = Physics.OverlapSphereNonAlloc(origin, range, overlapBuffer, targetLayers);
            var forward = caster != null ? caster.transform.forward : transform.forward;
            // 锥形检测的最小点积阈值（角度越小，阈值越高）
            var minDot = useCone ? Mathf.Cos(Mathf.Deg2Rad * Mathf.Clamp(definition.Angle, 0f, 180f) * 0.5f) : -1f;

            for (int i = 0; i < count; i++)
            {
                var collider = overlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                // 尝试创建战斗目标结构
                if (!CombatTarget.TryCreate(collider.gameObject, out var target))
                {
                    continue;
                }

                // 验证目标有效性（阵营、标签等）
                if (!IsValidTarget(caster, definition, target, includeSelf))
                {
                    continue;
                }

                // 锥形检测：检查目标是否在前方锥形角度内
                if (useCone)
                {
                    var dir = (target.Transform.position - origin).normalized;
                    if (Vector3.Dot(forward, dir) < minDot)
                    {
                        continue;
                    }
                }

                // 避免重复添加同一目标
                if (ContainsTarget(candidates, target))
                {
                    continue;
                }

                candidates.Add(target);
            }
        }

        /// <summary>
        /// 根据排序策略从候选列表中选择指定数量的目标。
        /// </summary>
        private void SelectTargets(TargetingDefinition definition, TargetSort sort, Vector3 origin, List<CombatTarget> candidates, List<CombatTarget> results)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            var maxTargets = Mathf.Max(1, definition.MaxTargets);

            // 随机排序：使用 Fisher-Yates 洗牌算法
            if (sort == TargetSort.Random)
            {
                SelectRandomTargets(candidates, maxTargets, results);
                return;
            }

            // 无排序：直接取前 N 个
            if (sort == TargetSort.None)
            {
                var count = Mathf.Min(maxTargets, candidates.Count);
                for (int i = 0; i < count; i++)
                {
                    results.Add(candidates[i]);
                }

                return;
            }

            // 其他排序：计算每个目标的排序指标并排序
            var candidateMetrics = SimpleListPool<TargetCandidate>.Get(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                var target = candidates[i];
                candidateMetrics.Add(new TargetCandidate(target, GetMetric(sort, origin, target)));
            }

            // 按指标升序排序
            candidateMetrics.Sort(TargetCandidateComparer.Instance);

            // 取前 N 个结果
            var take = Mathf.Min(maxTargets, candidateMetrics.Count);
            for (int i = 0; i < take; i++)
            {
                results.Add(candidateMetrics[i].Target);
            }

            SimpleListPool<TargetCandidate>.Release(candidateMetrics);
        }

        /// <summary>
        /// 随机选择指定数量的目标（Fisher-Yates 洗牌算法变体）。
        /// </summary>
        /// <remarks>
        /// 此算法不需要额外内存分配，通过交换已选元素到列表末尾实现。
        /// </remarks>
        private static void SelectRandomTargets(List<CombatTarget> candidates, int maxTargets, List<CombatTarget> results)
        {
            var available = candidates.Count;
            var count = Mathf.Min(maxTargets, available);

            for (int i = 0; i < count; i++)
            {
                var index = Random.Range(0, available);
                results.Add(candidates[index]);
                // 将已选元素交换到末尾，缩小可选范围
                available--;
                candidates[index] = candidates[available];
            }
        }

        /// <summary>
        /// 从候选列表中选择最优的单个目标。
        /// </summary>
        private static CombatTarget SelectBestCandidate(TargetSort sort, Vector3 origin, List<CombatTarget> candidates)
        {
            if (candidates.Count == 0)
            {
                return default;
            }

            var best = candidates[0];
            var bestMetric = GetMetric(sort, origin, best);

            // 线性遍历找到指标最小的目标
            for (int i = 1; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var metric = GetMetric(sort, origin, candidate);
                if (metric < bestMetric)
                {
                    bestMetric = metric;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool TrySelectInitialChainTarget(TargetSort sort, Vector3 origin, List<CombatTarget> candidates, out CombatTarget target)
        {
            target = default;
            if (candidates.Count == 0)
            {
                return false;
            }

            if (sort == TargetSort.Random)
            {
                target = candidates[Random.Range(0, candidates.Count)];
                return target.IsValid;
            }

            if (sort == TargetSort.None)
            {
                target = candidates[0];
                return target.IsValid;
            }

            target = SelectBestCandidate(sort, origin, candidates);
            return target.IsValid;
        }

        private static bool TrySelectNextChainTarget(TargetSort sort, Vector3 origin, List<CombatTarget> candidates, float rangeSqr, out CombatTarget target)
        {
            target = default;
            var found = false;
            var bestMetric = float.MaxValue;
            var randomCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Transform == null)
                {
                    continue;
                }

                var distSqr = (candidate.Transform.position - origin).sqrMagnitude;
                if (distSqr > rangeSqr)
                {
                    continue;
                }

                if (sort == TargetSort.Random)
                {
                    randomCount++;
                    if (Random.Range(0, randomCount) == 0)
                    {
                        target = candidate;
                        found = true;
                    }

                    continue;
                }

                if (sort == TargetSort.None)
                {
                    target = candidate;
                    return true;
                }

                var metric = GetMetric(sort, origin, candidate);
                if (!found || metric < bestMetric)
                {
                    bestMetric = metric;
                    target = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// 根据排序类型计算目标的排序指标。
        /// </summary>
        /// <remarks>
        /// 返回值越小优先级越高。对于“最远”和“血量最高”的情况，返回负值以实现逆序。
        /// </remarks>
        private static float GetMetric(TargetSort sort, Vector3 origin, CombatTarget target)
        {
            switch (sort)
            {
                case TargetSort.Farthest:
                    // 最远：返回距离的负值，远的指标更小
                    return -GetDistanceSqr(origin, target);
                case TargetSort.LowestHealth:
                    // 血量最低：直接返回血量值
                    return GetHealthValue(target);
                case TargetSort.HighestHealth:
                    // 血量最高：返回血量的负值
                    return -GetHealthValue(target);
                case TargetSort.Random:
                case TargetSort.None:
                case TargetSort.Closest:
                default:
                    // 最近/默认：返回距离的平方
                    return GetDistanceSqr(origin, target);
            }
        }

        /// <summary>
        /// 获取目标到原点的距离平方（避免 sqrt 计算）。
        /// </summary>
        private static float GetDistanceSqr(Vector3 origin, CombatTarget target)
        {
            return (target.Transform.position - origin).sqrMagnitude;
        }

        /// <summary>
        /// 获取目标的当前生命值。
        /// </summary>
        private static float GetHealthValue(CombatTarget target)
        {
            if (target.Health == null)
            {
                // 无 HealthComponent 的目标返回最大值，在“血量最低”排序时排在最后
                return float.MaxValue;
            }

            return target.Health.Current;
        }

        /// <summary>
        /// 检查候选列表中是否已包含该目标（通过 InstanceID 比较）。
        /// </summary>
        private static bool ContainsTarget(List<CombatTarget> list, CombatTarget target)
        {
            var instanceId = target.GameObject.GetInstanceID();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].GameObject.GetInstanceID() == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveCandidate(List<CombatTarget> list, CombatTarget target)
        {
            if (!target.IsValid)
            {
                return;
            }

            var instanceId = target.GameObject.GetInstanceID();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].GameObject.GetInstanceID() == instanceId)
                {
                    list.RemoveAt(i);
                    return;
                }
            }
        }

        private static float GetAreaRange(TargetingDefinition definition)
        {
            if (definition.Mode == TargetingMode.Sphere && definition.Radius > 0f)
            {
                return definition.Radius;
            }

            return Mathf.Max(0f, definition.Range);
        }

        /// <summary>
        /// 检查目标是否符合阵营条件。
        /// </summary>
        /// <remarks>
        /// [性能提示] 每次调用都会执行 GetComponent&lt;TeamComponent&gt;。
        /// 优化建议：在 UnitRoot 初始化时缓存 TeamComponent 引用，
        /// 或者将 TeamComponent 添加到 CombatTarget 结构体中（已实现）。
        /// </remarks>
        private static bool IsTeamMatch(UnitRoot caster, TargetTeam team, CombatTarget target)
        {
            if (team == TargetTeam.Any)
            {
                return true;
            }

            if (caster == null)
            {
                return team == TargetTeam.Any;
            }

            if (team == TargetTeam.Self)
            {
                return target.Unit == caster;
            }

            // [性能] 此处 GetComponent 可能在大量目标验证时产生开销
            // 建议：在 UnitRoot 中缓存 TeamComponent 引用
            var casterTeam = caster.GetComponent<TeamComponent>();
            if (casterTeam == null || target.Team == null)
            {
                return false;
            }

            var sameTeam = casterTeam.IsSameTeam(target.Team);
            return team == TargetTeam.Ally ? sameTeam : !sameTeam;
        }

        private static bool HasRequiredTags(IReadOnlyList<TagDefinition> requiredTags, CombatTarget target)
        {
            if (requiredTags == null || requiredTags.Count == 0)
            {
                return true;
            }

            if (target.Tags == null)
            {
                return false;
            }

            for (int i = 0; i < requiredTags.Count; i++)
            {
                var tag = requiredTags[i];
                if (tag != null && !target.Tags.HasTag(tag))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasBlockedTags(IReadOnlyList<TagDefinition> blockedTags, CombatTarget target)
        {
            if (blockedTags == null || blockedTags.Count == 0)
            {
                return false;
            }

            if (target.Tags == null)
            {
                return false;
            }

            for (int i = 0; i < blockedTags.Count; i++)
            {
                var tag = blockedTags[i];
                if (tag != null && target.Tags.HasTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct TargetCandidate
        {
            public readonly CombatTarget Target;
            public readonly float Metric;

            public TargetCandidate(CombatTarget target, float metric)
            {
                Target = target;
                Metric = metric;
            }
        }

        private sealed class TargetCandidateComparer : IComparer<TargetCandidate>
        {
            public static readonly TargetCandidateComparer Instance = new TargetCandidateComparer();

            public int Compare(TargetCandidate x, TargetCandidate y)
            {
                return x.Metric.CompareTo(y.Metric);
            }
        }
    }
}
