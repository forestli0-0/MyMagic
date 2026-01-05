using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace CombatSystem.AI
{
    /// <summary>
    /// 战斗 AI 控制器，负责管理单位的 AI 行为决策与状态机切换。
    /// </summary>
    /// <remarks>
    /// AI 状态机包含以下状态：
    /// - Idle: 空闲状态，无目标时待机
    /// - Chase: 追击状态，向目标移动
    /// - Attack: 攻击状态，尝试释放技能
    /// - CastSkill: 施法状态，等待技能施放完成
    /// - Retreat: 撤退状态，生命值过低时远离目标
    /// 
    /// 决策流程：
    /// 1. Think() 每隔 thinkInterval 执行一次决策
    /// 2. 验证当前目标有效性，必要时搜索新目标
    /// 3. 根据距离、冷却、条件选择最优技能
    /// 4. UpdateState() 每帧执行状态相关行为
    /// </remarks>
    public class CombatAIController : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("单位根组件")]
        [SerializeField] private UnitRoot unitRoot;
        [Tooltip("技能使用组件")]
        [SerializeField] private SkillUserComponent skillUser;
        [Tooltip("生命组件")]
        [SerializeField] private HealthComponent health;
        [Tooltip("队伍组件")]
        [SerializeField] private TeamComponent team;
        [Tooltip("目标选择系统")]
        [SerializeField] private TargetingSystem targetingSystem;
        [Tooltip("AI 配置文件")]
        [SerializeField] private AIProfile aiProfile;
        [Tooltip("导航代理")]
        [SerializeField] private NavMeshAgent navAgent;

        [Header("目标搜索")]
        [Tooltip("目标搜索的物理层")]
        [SerializeField] private LayerMask targetLayers = ~0;
        [Tooltip("搜索缓冲区大小")]
        [SerializeField] private int maxTargetColliders = 32;

        [Header("移动参数")]
        [Tooltip("是否使用 NavMesh 导航")]
        [SerializeField] private bool useNavMesh = true;
        [Tooltip("移动速度")]
        [SerializeField] private float moveSpeed = 3.5f;
        [Tooltip("停止距离")]
        [SerializeField] private float stoppingDistance = 1.5f;
        [Tooltip("转向速度（度/秒）")]
        [SerializeField] private float turnSpeed = 720f;

        [Header("撤退机制")]
        [Tooltip("是否启用撤退机制")]
        [SerializeField] private bool enableRetreat;
        [Tooltip("触发撤退的生命百分比阈值")]
        [Range(0f, 1f)]
        [SerializeField] private float retreatHealthPercent = 0.2f;
        [Tooltip("撤退距离")]
        [SerializeField] private float retreatDistance = 6f;

        [Header("调试")]
        [Tooltip("当前 AI 状态")]
        [SerializeField] private AIState currentState = AIState.Idle;
        [Tooltip("当前目标（调试用）")]
        [SerializeField] private Transform debugTarget;

        // 下次思考时间
        private float nextThinkTime;
        // 目标搜索缓冲区（避免 GC）
        private Collider[] overlapBuffer;
        // 当前锁定目标
        private CombatTarget currentTarget;
        // 是否有有效目标
        private bool hasTarget;
        // 当前选中的技能
        private SkillDefinition selectedSkill;
        // 选中技能的最小释放距离
        private float selectedMinRange;
        // 选中技能的最大释放距离
        private float selectedMaxRange;

        /// <summary>
        /// 编辑器重置时自动查找组件。
        /// </summary>
        private void Reset()
        {
            unitRoot = GetComponent<UnitRoot>();
            skillUser = GetComponent<SkillUserComponent>();
            health = GetComponent<HealthComponent>();
            team = GetComponent<TeamComponent>();
            navAgent = GetComponent<NavMeshAgent>();
        }

        private void Awake()
        {
            EnsureReferences();
            EnsureBuffer();
        }

        private void OnEnable()
        {
            EnsureReferences();
            EnsureBuffer();
            ScheduleNextThink(0f);
        }

        /// <summary>
        /// 每帧更新：检查存活状态、执行决策和状态更新。
        /// </summary>
        private void Update()
        {
            // 死亡时停止一切行为
            if (health != null && !health.IsAlive)
            {
                StopMovement();
                currentState = AIState.Idle;
                return;
            }

            // 无配置时不执行
            if (aiProfile == null)
            {
                return;
            }

            // 定时执行决策
            if (Time.time >= nextThinkTime)
            {
                Think();
                ScheduleNextThink(aiProfile.ThinkInterval);
            }

            // 每帧执行状态行为
            UpdateState();
        }

        /// <summary>
        /// AI 核心决策逻辑：目标获取、状态切换、技能选择。
        /// </summary>
        private void Think()
        {
            // 正在施法时保持施法状态
            if (skillUser != null && skillUser.IsCasting)
            {
                if (!skillUser.CanMoveWhileCasting)
                {
                    currentState = AIState.CastSkill;
                    return;
                }
            }

            // 验证并获取目标
            ValidateTarget();
            if (!hasTarget)
            {
                AcquireTarget();
                if (!hasTarget)
                {
                    selectedSkill = null;
                    currentState = AIState.Idle;
                    return;
                }
            }

            // 检查是否需要撤退
            if (ShouldRetreat())
            {
                selectedSkill = null;
                currentState = AIState.Retreat;
                return;
            }

            // 检查目标是否超出仇恨范围
            var distance = GetDistanceToTarget();
            if (distance > aiProfile.AggroRange)
            {
                ClearTarget();
                selectedSkill = null;
                currentState = AIState.Idle;
                return;
            }

            // 尝试选择技能
            if (TrySelectSkill(distance, out var skill, out var minRange, out var maxRange))
            {
                selectedSkill = skill;
                selectedMinRange = minRange;
                selectedMaxRange = maxRange;
                currentState = AIState.Attack;
                return;
            }

            // 使用普通攻击
            selectedSkill = skillUser != null ? skillUser.BasicAttack : null;
            selectedMinRange = 0f;
            selectedMaxRange = aiProfile.AttackRange;

            // 根据距离决定追击或攻击
            currentState = distance <= selectedMaxRange ? AIState.Attack : AIState.Chase;
        }

        /// <summary>
        /// 每帧状态行为更新。
        /// </summary>
        private void UpdateState()
        {
            if (skillUser != null && skillUser.IsCasting && !skillUser.CanMoveWhileCasting)
            {
                StopMovement();
                currentState = AIState.CastSkill;
                return;
            }

            switch (currentState)
            {
                case AIState.Idle:
                    // 空闲：停止移动
                    StopMovement();
                    break;

                case AIState.Chase:
                    // 追击：向目标移动
                    if (!hasTarget)
                    {
                        StopMovement();
                        return;
                    }

                    MoveTowardsTarget();
                    if (IsWithinDesiredRange())
                    {
                        StopMovement();
                        currentState = AIState.Attack;
                    }
                    break;

                case AIState.Attack:
                    // 攻击：尝试释放技能
                    if (!hasTarget)
                    {
                        StopMovement();
                        currentState = AIState.Idle;
                        return;
                    }

                    if (!IsWithinDesiredRange())
                    {
                        currentState = AIState.Chase;
                        return;
                    }

                    if (skillUser == null || !skillUser.IsCasting || !skillUser.CanMoveWhileCasting)
                    {
                        StopMovement();
                    }

                    TryCastSelectedSkill();
                    break;

                case AIState.CastSkill:
                    // 施法：等待施法完成
                    if (skillUser != null && skillUser.CanMoveWhileCasting)
                    {
                        currentState = AIState.Attack;
                        break;
                    }

                    StopMovement();
                    if (skillUser == null || !skillUser.IsCasting)
                    {
                        currentState = AIState.Attack;
                    }
                    break;

                case AIState.Retreat:
                    // 撤退：远离目标
                    if (!hasTarget)
                    {
                        StopMovement();
                        currentState = AIState.Idle;
                        return;
                    }

                    MoveAwayFromTarget();
                    if (!ShouldRetreat())
                    {
                        currentState = AIState.Chase;
                    }
                    break;
            }
        }

        #region 目标获取与验证

        /// <summary>
        /// 在仇恨范围内搜索最近的敌方单位。
        /// </summary>
        private void AcquireTarget()
        {
            if (aiProfile.AggroRange <= 0f)
            {
                return;
            }

            var origin = transform.position;
            // 使用 NonAlloc 版本避免 GC
            var count = Physics.OverlapSphereNonAlloc(origin, aiProfile.AggroRange, overlapBuffer, targetLayers);

            var bestDistance = float.MaxValue;
            CombatTarget bestTarget = default;

            for (int i = 0; i < count; i++)
            {
                var collider = overlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                if (!CombatTarget.TryCreate(collider.gameObject, out var candidate))
                {
                    continue;
                }

                if (!candidate.IsValid || candidate.Transform == null)
                {
                    continue;
                }

                // 跳过友方或已死亡单位
                if (!IsEnemy(candidate) || candidate.Health == null || !candidate.Health.IsAlive)
                {
                    continue;
                }

                // 选择最近的目标
                var distance = (candidate.Transform.position - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = candidate;
                }
            }

            if (bestTarget.IsValid)
            {
                SetTarget(bestTarget);
            }
        }

        /// <summary>
        /// 验证当前目标是否仍然有效。
        /// </summary>
        private void ValidateTarget()
        {
            if (!hasTarget)
            {
                return;
            }

            // 目标 GameObject 被销毁
            if (!currentTarget.IsValid || currentTarget.Transform == null)
            {
                ClearTarget();
                return;
            }

            // 目标已死亡
            if (currentTarget.Health != null && !currentTarget.Health.IsAlive)
            {
                ClearTarget();
                return;
            }
        }

        /// <summary>
        /// 清除当前目标。
        /// </summary>
        private void ClearTarget()
        {
            hasTarget = false;
            currentTarget = default;
            debugTarget = null;
        }

        /// <summary>
        /// 设置当前目标。
        /// </summary>
        private void SetTarget(CombatTarget target)
        {
            currentTarget = target;
            hasTarget = target.IsValid;
            debugTarget = target.Transform;
        }

        /// <summary>
        /// 获取到当前目标的距离。
        /// </summary>
        private float GetDistanceToTarget()
        {
            if (!hasTarget || currentTarget.Transform == null)
            {
                return float.MaxValue;
            }

            return Vector3.Distance(transform.position, currentTarget.Transform.position);
        }

        /// <summary>
        /// 检查是否在期望的技能释放范围内。
        /// </summary>
        private bool IsWithinDesiredRange()
        {
            if (!hasTarget)
            {
                return false;
            }

            var distance = GetDistanceToTarget();
            var minRange = selectedSkill != null ? selectedMinRange : 0f;
            var maxRange = selectedSkill != null && selectedMaxRange > 0f ? selectedMaxRange : aiProfile.AttackRange;
            if (distance > maxRange)
            {
                return false;
            }

            return distance >= minRange;
        }

        #endregion

        #region 移动控制

        /// <summary>
        /// 向目标移动。
        /// </summary>
        private void MoveTowardsTarget()
        {
            if (!hasTarget || currentTarget.Transform == null)
            {
                return;
            }

            var destination = currentTarget.Transform.position;
            if (useNavMesh && navAgent != null)
            {
                navAgent.updateRotation = CanRotateWhileCasting();
                navAgent.speed = moveSpeed;
                navAgent.stoppingDistance = Mathf.Max(stoppingDistance, selectedMinRange);
                navAgent.isStopped = false;
                navAgent.SetDestination(destination);
                return;
            }

            var stopDistance = stoppingDistance;
            if (selectedSkill != null)
            {
                stopDistance = Mathf.Max(stopDistance, selectedMinRange);
            }

            MoveDirectly(destination, stopDistance);
        }

        /// <summary>
        /// 远离目标移动（撤退）。
        /// </summary>
        private void MoveAwayFromTarget()
        {
            if (!hasTarget || currentTarget.Transform == null)
            {
                return;
            }

            var origin = transform.position;
            var direction = (origin - currentTarget.Transform.position).normalized;
            var destination = origin + direction * Mathf.Max(0.1f, retreatDistance);

            if (useNavMesh && navAgent != null)
            {
                navAgent.updateRotation = CanRotateWhileCasting();
                navAgent.speed = moveSpeed;
                navAgent.stoppingDistance = 0f;
                navAgent.isStopped = false;
                navAgent.SetDestination(destination);
                return;
            }

            MoveDirectly(destination, 0f);
        }

        /// <summary>
        /// 直接移动（不使用 NavMesh）。
        /// </summary>
        private void MoveDirectly(Vector3 destination, float stopDistance)
        {
            var origin = transform.position;
            var delta = destination - origin;
            delta.y = 0f;

            var distance = delta.magnitude;
            if (distance <= Mathf.Max(0.01f, stopDistance))
            {
                return;
            }

            var direction = delta / distance;
            transform.position = origin + direction * moveSpeed * Time.deltaTime;

            // 面向移动方向
            if (direction.sqrMagnitude > 0.001f && CanRotateWhileCasting())
            {
                var rotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, turnSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// 停止移动。
        /// </summary>
        private void StopMovement()
        {
            if (useNavMesh && navAgent != null)
            {
                if (!navAgent.isStopped)
                {
                    navAgent.isStopped = true;
                }

                if (navAgent.hasPath)
                {
                    navAgent.ResetPath();
                }
            }
        }

        private bool CanRotateWhileCasting()
        {
            return skillUser == null || !skillUser.IsCasting || skillUser.CanRotateWhileCasting;
        }

        #endregion

        #region 技能选择与释放

        /// <summary>
        /// 尝试释放选中的技能。
        /// </summary>
        private void TryCastSelectedSkill()
        {
            if (skillUser == null)
            {
                return;
            }

            if (!hasTarget || !currentTarget.IsValid)
            {
                return;
            }

            // 无选中技能时使用普通攻击
            if (selectedSkill == null)
            {
                selectedSkill = skillUser.BasicAttack;
            }

            if (selectedSkill == null)
            {
                return;
            }

            // 尝试释放技能
            if (skillUser.TryCast(selectedSkill, currentTarget.GameObject))
            {
                if (!skillUser.CanMoveWhileCasting)
                {
                    currentState = AIState.CastSkill;
                }

                selectedSkill = null;
            }
        }

        /// <summary>
        /// 根据距离、冷却、条件、权重选择要释放的技能。
        /// </summary>
        /// <param name="distance">到目标的距离</param>
        /// <param name="skill">输出选中的技能</param>
        /// <param name="minRange">输出技能最小距离</param>
        /// <param name="maxRange">输出技能最大距离</param>
        /// <returns>若成功选择技能则返回 true</returns>
        private bool TrySelectSkill(float distance, out SkillDefinition skill, out float minRange, out float maxRange)
        {
            skill = null;
            minRange = 0f;
            maxRange = 0f;

            if (skillUser == null || aiProfile == null)
            {
                return false;
            }

            var rules = aiProfile.SkillRules;
            if (rules == null || rules.Count == 0)
            {
                return false;
            }

            // 第一遍：计算有效规则的总权重
            var totalWeight = 0f;
            var hasValid = false;

            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (!IsRuleValid(rule, distance))
                {
                    continue;
                }

                hasValid = true;
                totalWeight += Mathf.Max(0f, rule.weight);
            }

            if (!hasValid)
            {
                return false;
            }

            // 权重为零时选择第一个有效规则
            if (totalWeight <= 0f)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (IsRuleValid(rule, distance))
                    {
                        skill = rule.skill;
                        minRange = rule.minRange;
                        maxRange = rule.maxRange;
                        return true;
                    }
                }

                return false;
            }

            // 加权随机选择
            var pick = Random.value * totalWeight;
            var cumulative = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (!IsRuleValid(rule, distance))
                {
                    continue;
                }

                cumulative += Mathf.Max(0f, rule.weight);
                if (pick <= cumulative)
                {
                    skill = rule.skill;
                    minRange = rule.minRange;
                    maxRange = rule.maxRange;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查技能规则是否当前可用。
        /// </summary>
        private bool IsRuleValid(AISkillRule rule, float distance)
        {
            if (rule == null || rule.skill == null)
            {
                return false;
            }

            // 距离检查
            if (!IsWithinRange(distance, rule.minRange, rule.maxRange))
            {
                return false;
            }

            // 移动中检查
            if (IsMoving() && !rule.allowWhileMoving)
            {
                return false;
            }

            // 冷却/资源检查
            if (!skillUser.CanCast(rule.skill))
            {
                return false;
            }

            // 条件检查
            if (rule.condition == null)
            {
                return true;
            }

            var context = new SkillRuntimeContext(
                skillUser,
                unitRoot,
                rule.skill,
                unitRoot != null ? unitRoot.EventHub : null,
                targetingSystem,
                null);

            return ConditionEvaluator.Evaluate(rule.condition, context, currentTarget);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查是否正在移动中。
        /// </summary>
        private bool IsMoving()
        {
            if (useNavMesh && navAgent != null)
            {
                return navAgent.hasPath && navAgent.remainingDistance > navAgent.stoppingDistance + 0.05f;
            }

            return currentState == AIState.Chase || currentState == AIState.Retreat;
        }

        /// <summary>
        /// 检查是否需要撤退。
        /// </summary>
        private bool ShouldRetreat()
        {
            if (!enableRetreat || health == null || health.Max <= 0f)
            {
                return false;
            }

            return (health.Current / health.Max) <= retreatHealthPercent;
        }

        /// <summary>
        /// 检查目标是否为敌方单位。
        /// </summary>
        private bool IsEnemy(CombatTarget target)
        {
            // 无队伍信息时视为敌人
            if (team == null || target.Team == null)
            {
                return true;
            }

            return !team.IsSameTeam(target.Team);
        }

        /// <summary>
        /// 检查距离是否在指定范围内。
        /// </summary>
        private static bool IsWithinRange(float distance, float minRange, float maxRange)
        {
            if (distance < minRange)
            {
                return false;
            }

            if (maxRange > 0f && distance > maxRange)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 确保所有组件引用有效。
        /// </summary>
        private void EnsureReferences()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (team == null)
            {
                team = GetComponent<TeamComponent>();
            }

            if (navAgent == null)
            {
                navAgent = GetComponent<NavMeshAgent>();
            }

            // 从 UnitDefinition 加载 AI 配置
            if (aiProfile == null && unitRoot != null && unitRoot.Definition != null)
            {
                aiProfile = unitRoot.Definition.AIProfile;
            }
        }

        /// <summary>
        /// 确保搜索缓冲区已初始化。
        /// </summary>
        private void EnsureBuffer()
        {
            var size = Mathf.Max(1, maxTargetColliders);
            if (overlapBuffer == null || overlapBuffer.Length != size)
            {
                overlapBuffer = new Collider[size];
            }
        }

        /// <summary>
        /// 安排下次思考时间。
        /// </summary>
        private void ScheduleNextThink(float interval)
        {
            var delay = Mathf.Max(0.05f, interval);
            nextThinkTime = Time.time + delay;
        }

        #endregion
    }

    /// <summary>
    /// AI 状态枚举。
    /// </summary>
    public enum AIState
    {
        /// <summary>空闲状态</summary>
        Idle,
        /// <summary>追击状态</summary>
        Chase,
        /// <summary>攻击状态</summary>
        Attack,
        /// <summary>施法状态</summary>
        CastSkill,
        /// <summary>撤退状态</summary>
        Retreat
    }
}
