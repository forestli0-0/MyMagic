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
        [Tooltip("移动组件")]
        [SerializeField] private MovementComponent movement;
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
        [Tooltip("停止距离")]
        [SerializeField] private float stoppingDistance = 1.5f;
        [Tooltip("攻击距离迟滞缓冲，避免 Attack/Chase 边界抖动")]
        [SerializeField] private float attackRangeBuffer = 0.25f;
        [Tooltip("转向速度（度/秒）")]
        [SerializeField] private float turnSpeed = 720f;
        [Tooltip("是否在目标周围分散站位，避免同点挤压玩家")]
        [SerializeField] private bool spreadAroundTarget = true;
        [Tooltip("分散站位额外半径")]
        [SerializeField] private float surroundRadiusOffset = 0.45f;
        [Tooltip("同一目标允许近身的最大敌人数")]
        [SerializeField] private int maxCloseAttackers = 3;
        [Tooltip("超出近身人数后，每层外圈增加的半径")]
        [SerializeField] private float queuedRingSpacing = 0.9f;
        [Tooltip("敌人之间分离检测半径")]
        [SerializeField] private float separationRadius = 1.2f;
        [Tooltip("分离强度，值越大越不容易挤在一起")]
        [SerializeField] private float separationStrength = 0.6f;

        [Header("导航平滑")]
        [Tooltip("SetDestination 最小刷新间隔（秒），避免每帧重算路径引发方向抖动。")]
        [SerializeField] private float destinationRefreshInterval = 0.08f;
        [Tooltip("目标点变化超过该距离才立即刷新路径。")]
        [SerializeField] private float destinationRefreshThreshold = 0.25f;
        [Tooltip("距目标足够近时才启用围攻分散计算，降低远距离追击时的方向抖动。")]
        [SerializeField] private float spreadResolveDistancePadding = 1.2f;
        [Tooltip("NavMesh 期望速度平滑强度，值越大响应越快。")]
        [SerializeField] private float desiredVelocitySmoothing = 12f;

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
        // 围攻排队/分离缓冲区（避免 GC）
        private Collider[] crowdBuffer;
        private int[] crowdIdBuffer;
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
        private Vector3 lastIssuedDestination;
        private bool hasIssuedDestination;
        private float nextDestinationRefreshTime;
        private Vector3 smoothedDesiredVelocity;
        private bool hasSmoothedDesiredVelocity;

        /// <summary>
        /// 编辑器重置时自动查找组件。
        /// </summary>
        private void Reset()
        {
            unitRoot = GetComponent<UnitRoot>();
            skillUser = GetComponent<SkillUserComponent>();
            health = GetComponent<HealthComponent>();
            movement = GetComponent<MovementComponent>();
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

            if (useNavMesh && navAgent != null && movement != null)
            {
                navAgent.nextPosition = transform.position;
            }
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
            selectedMaxRange = GetSkillMaxRange(selectedSkill, aiProfile.AttackRange);

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
            ResetNavSteeringCache();
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
            var buffer = currentState == AIState.Attack ? Mathf.Max(0f, attackRangeBuffer) : 0f;
            if (maxRange > 0f && distance > maxRange + buffer)
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

            var desiredStopDistance = selectedSkill != null
                ? Mathf.Max(stoppingDistance, selectedMinRange)
                : stoppingDistance;
            var destination = ResolveApproachDestination(currentTarget.Transform.position, desiredStopDistance);
            if (useNavMesh && navAgent != null)
            {
                navAgent.speed = movement != null ? movement.MoveSpeed : 3.5f;
                navAgent.stoppingDistance = spreadAroundTarget
                    ? Mathf.Max(0.35f, desiredStopDistance * 0.5f)
                    : desiredStopDistance;
                navAgent.isStopped = false;
                if (ShouldRefreshDestination(destination))
                {
                    navAgent.SetDestination(destination);
                }

                if (movement != null)
                {
                    navAgent.nextPosition = transform.position;
                    movement.SetMoveVelocity(GetSmoothedDesiredVelocity(navAgent.desiredVelocity));
                }
                else
                {
                    navAgent.updateRotation = CanRotateWhileCasting();
                }

                return;
            }

            var stopDistance = spreadAroundTarget ? 0.08f : desiredStopDistance;
            MoveDirectly(destination, stopDistance);
        }

        private Vector3 ResolveApproachDestination(Vector3 targetPosition, float desiredStopDistance)
        {
            if (!spreadAroundTarget || desiredStopDistance <= 0f || currentTarget.Transform == null)
            {
                return targetPosition;
            }

            var ringRadius = Mathf.Max(0.75f, desiredStopDistance + Mathf.Max(0f, surroundRadiusOffset));
            var distanceToTarget = Vector3.Distance(transform.position, targetPosition);
            var shouldResolveSpread = distanceToTarget <= ringRadius + Mathf.Max(0f, spreadResolveDistancePadding);
            if (shouldResolveSpread)
            {
                var outerRing = GetQueueRingDepth(currentTarget.Transform, ringRadius);
                if (outerRing > 0)
                {
                    ringRadius += outerRing * Mathf.Max(0.1f, queuedRingSpacing);
                }
            }

            var radial = transform.position - targetPosition;
            radial.y = 0f;
            if (radial.sqrMagnitude <= 0.0001f)
            {
                radial = GetFallbackRadial();
            }
            else
            {
                radial.Normalize();
            }

            var tangent = Vector3.Cross(Vector3.up, radial);
            var dir = radial + tangent * (GetStableSlotBias() * 0.6f);

            var separation = shouldResolveSpread ? ComputeSeparationOffset() : Vector3.zero;
            if (separation.sqrMagnitude > 0.0001f)
            {
                dir += separation * Mathf.Max(0f, separationStrength);
            }

            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                dir = radial;
            }

            dir.Normalize();
            return targetPosition + dir * ringRadius;
        }

        private int GetQueueRingDepth(Transform target, float baseRingRadius)
        {
            if (maxCloseAttackers <= 0 || target == null || crowdBuffer == null)
            {
                return 0;
            }

            var scanRadius = Mathf.Max(baseRingRadius + queuedRingSpacing * 3f, 4f);
            var count = Physics.OverlapSphereNonAlloc(target.position, scanRadius, crowdBuffer, targetLayers);
            var myId = GetInstanceID();
            var seenCount = 0;
            var rank = 0;

            for (int i = 0; i < count; i++)
            {
                var collider = crowdBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                var ally = collider.GetComponentInParent<CombatAIController>();
                if (ally == null || ally == this || !ally.hasTarget || ally.currentTarget.Transform != target)
                {
                    continue;
                }

                if (!IsAlly(ally))
                {
                    continue;
                }

                if (!TryRegisterCrowdId(ally.GetInstanceID(), ref seenCount))
                {
                    continue;
                }

                if (ally.GetInstanceID() < myId)
                {
                    rank++;
                }
            }

            if (rank < maxCloseAttackers)
            {
                return 0;
            }

            return rank - maxCloseAttackers + 1;
        }

        private Vector3 ComputeSeparationOffset()
        {
            if (separationRadius <= 0f || crowdBuffer == null)
            {
                return Vector3.zero;
            }

            var count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, crowdBuffer, targetLayers);
            if (count <= 0)
            {
                return Vector3.zero;
            }

            var accumulated = Vector3.zero;
            var seenCount = 0;
            for (int i = 0; i < count; i++)
            {
                var collider = crowdBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                var ally = collider.GetComponentInParent<CombatAIController>();
                if (ally == null || ally == this || !IsAlly(ally))
                {
                    continue;
                }

                if (!TryRegisterCrowdId(ally.GetInstanceID(), ref seenCount))
                {
                    continue;
                }

                var delta = transform.position - ally.transform.position;
                delta.y = 0f;
                var sqrMag = delta.sqrMagnitude;
                if (sqrMag <= 0.0001f)
                {
                    continue;
                }

                accumulated += delta.normalized * (1f / (1f + sqrMag));
            }

            if (accumulated.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return accumulated.normalized;
        }

        private Vector3 GetFallbackRadial()
        {
            var angle = Mathf.Abs(GetInstanceID() % 360);
            return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        }

        private float GetStableSlotBias()
        {
            unchecked
            {
                var hash = GetInstanceID() * 1103515245 + 12345;
                var normalized = (hash & 0x7fffffff) / (float)int.MaxValue;
                return Mathf.Lerp(-1f, 1f, normalized);
            }
        }

        private bool IsAlly(CombatAIController other)
        {
            if (other == null)
            {
                return false;
            }

            if (team == null || other.team == null)
            {
                return true;
            }

            return team.IsSameTeam(other.team);
        }

        private bool TryRegisterCrowdId(int id, ref int count)
        {
            if (crowdIdBuffer == null || crowdIdBuffer.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                if (crowdIdBuffer[i] == id)
                {
                    return false;
                }
            }

            if (count < crowdIdBuffer.Length)
            {
                crowdIdBuffer[count] = id;
                count++;
            }

            return true;
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
                navAgent.speed = movement != null ? movement.MoveSpeed : 3.5f;
                navAgent.stoppingDistance = 0f;
                navAgent.isStopped = false;
                navAgent.SetDestination(destination);

                if (movement != null)
                {
                    navAgent.nextPosition = transform.position;
                    movement.SetMoveVelocity(navAgent.desiredVelocity);
                }
                else
                {
                    navAgent.updateRotation = CanRotateWhileCasting();
                }

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
                if (movement != null)
                {
                    movement.Stop();
                }

                return;
            }

            var direction = delta / distance;
            if (movement != null)
            {
                movement.SetMoveInput(direction);
                return;
            }

            var speed = 3.5f; // 无 MovementComponent 时的回退速度
            transform.position = origin + direction * speed * Time.deltaTime;

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
            if (movement != null)
            {
                movement.Stop();
            }

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

                if (movement != null)
                {
                    navAgent.nextPosition = transform.position;
                }
            }

            ResetNavSteeringCache();
        }

        private bool ShouldRefreshDestination(Vector3 destination)
        {
            var interval = Mathf.Max(0.02f, destinationRefreshInterval);
            var threshold = Mathf.Max(0.05f, destinationRefreshThreshold);

            if (!hasIssuedDestination)
            {
                lastIssuedDestination = destination;
                hasIssuedDestination = true;
                nextDestinationRefreshTime = Time.time + interval;
                return true;
            }

            var moved = (destination - lastIssuedDestination).sqrMagnitude >= threshold * threshold;
            if (!moved && Time.time < nextDestinationRefreshTime)
            {
                return false;
            }

            lastIssuedDestination = destination;
            nextDestinationRefreshTime = Time.time + interval;
            return true;
        }

        private Vector3 GetSmoothedDesiredVelocity(Vector3 desiredVelocity)
        {
            var dt = Mathf.Max(0.0001f, Time.deltaTime);
            var smoothing = Mathf.Max(1f, desiredVelocitySmoothing);
            var t = 1f - Mathf.Exp(-smoothing * dt);

            if (!hasSmoothedDesiredVelocity)
            {
                smoothedDesiredVelocity = desiredVelocity;
                hasSmoothedDesiredVelocity = true;
                return smoothedDesiredVelocity;
            }

            smoothedDesiredVelocity = Vector3.Lerp(smoothedDesiredVelocity, desiredVelocity, t);
            return smoothedDesiredVelocity;
        }

        private void ResetNavSteeringCache()
        {
            hasIssuedDestination = false;
            nextDestinationRefreshTime = 0f;
            hasSmoothedDesiredVelocity = false;
            smoothedDesiredVelocity = Vector3.zero;
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
            EnsureBasicAttackSelection();

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

        private void EnsureBasicAttackSelection()
        {
            if (selectedSkill != null || skillUser == null)
            {
                return;
            }

            var basic = skillUser.BasicAttack;
            if (basic == null)
            {
                return;
            }

            selectedSkill = basic;
            selectedMinRange = 0f;
            selectedMaxRange = GetSkillMaxRange(basic, aiProfile != null ? aiProfile.AttackRange : 0f);
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

        private static float GetSkillMaxRange(SkillDefinition skill, float fallback)
        {
            if (skill == null || skill.Targeting == null)
            {
                return fallback;
            }

            var targeting = skill.Targeting;
            switch (targeting.Mode)
            {
                case TargetingMode.Self:
                    return 0f;
                case TargetingMode.Sphere:
                    if (targeting.Radius > 0f)
                    {
                        return targeting.Radius;
                    }

                    break;
            }

            return targeting.Range > 0f ? targeting.Range : fallback;
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

            if (movement == null)
            {
                movement = GetComponent<MovementComponent>();
            }

            if (team == null)
            {
                team = GetComponent<TeamComponent>();
            }

            if (navAgent == null)
            {
                navAgent = GetComponent<NavMeshAgent>();
            }

            if (navAgent != null && movement != null)
            {
                navAgent.updatePosition = false;
                navAgent.updateRotation = false;
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

            if (crowdBuffer == null || crowdBuffer.Length != size)
            {
                crowdBuffer = new Collider[size];
            }

            var idSize = Mathf.Max(8, size * 2);
            if (crowdIdBuffer == null || crowdIdBuffer.Length != idSize)
            {
                crowdIdBuffer = new int[idSize];
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
