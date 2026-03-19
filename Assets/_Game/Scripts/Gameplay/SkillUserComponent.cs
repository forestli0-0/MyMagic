using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 技能使用组件，负责管理单位的技能列表、校验释放条件、执行技能管线。
    /// </summary>
    /// <remarks>
    /// 技能管线流程：
    /// 1. 接收释放请求 (TryCast)
    /// 2. 校验冷却/资源/存活状态 (CanCast)
    /// 3. 收集目标 (TargetingSystem)
    /// 4. 扣除资源并开始冷却
    /// 5. 调度 SkillStep 并执行效果 (EffectExecutor)
    /// 6. 派发施法事件
    /// </remarks>
    public class SkillUserComponent : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("单位根组件")]
        [SerializeField] private UnitRoot unitRoot;
        [Tooltip("全局事件中心")]
        [SerializeField] private CombatEventHub eventHub;
        [Tooltip("属性组件")]
        [SerializeField] private StatsComponent stats;
        [Tooltip("生命组件")]
        [SerializeField] private HealthComponent health;
        [Tooltip("资源组件")]
        [SerializeField] private ResourceComponent resource;
        [Tooltip("冷却组件")]
        [SerializeField] private CooldownComponent cooldown;
        [Tooltip("Buff 控制器")]
        [SerializeField] private BuffController buffController;
        [Tooltip("目标选择系统")]
        [SerializeField] private TargetingSystem targetingSystem;
        [Tooltip("效果执行器")]
        [SerializeField] private EffectExecutor effectExecutor;
        
        [Header("配置")]
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool initializeOnAwake = true;
        [Tooltip("是否从 UnitDefinition 加载技能")]
        [SerializeField] private bool useUnitDefinitionSkills = true;
        [Tooltip("额外配置的技能列表")]
        [SerializeField] private List<SkillDefinition> skills = new List<SkillDefinition>();
        [Tooltip("覆盖默认普通攻击")]
        [SerializeField] private SkillDefinition basicAttackOverride;

        // 运行时技能列表
        private readonly List<SkillDefinition> runtimeSkills = new List<SkillDefinition>(8);
        // 等待执行的技能步骤队列
        private readonly List<PendingStep> pendingSteps = new List<PendingStep>(16);
        // 技能弹药运行时状态
        private readonly Dictionary<SkillDefinition, AmmoRuntimeState> ammoStates = new Dictionary<SkillDefinition, AmmoRuntimeState>(16);
        // 技能重施运行时状态
        private readonly Dictionary<SkillDefinition, RecastRuntimeState> recastStates = new Dictionary<SkillDefinition, RecastRuntimeState>(16);
        // 技能连段运行时状态
        private readonly Dictionary<SkillDefinition, SequenceRuntimeState> sequenceStates = new Dictionary<SkillDefinition, SequenceRuntimeState>(16);

        // 施法状态
        private bool isCasting;
        private float castStartTime;
        private float castEndTime;
        private float currentCastTime;
        private float currentChannelTime;
        private SkillDefinition currentSkill;
        private bool currentIsChannel;
        private float currentPostCastTime;
        private float currentQueueWindow;
        private float currentChargeDuration;
        private float currentChargeRatio;
        private float currentChargeMultiplier = 1f;
        private int currentSequencePhase = 1;
        private ulong nextCastId = 1UL;
        private SkillRuntimeContext currentContext;
        private bool currentHasAimPoint;
        private Vector3 currentAimPoint;
        private Vector3 currentAimDirection;
        // 后摇结束时间，未到此时间不可继续施法
        private float recoveryEndTime;
        // 公共冷却结束时间，未到此时间不可继续施法
        private float gcdEndTime;
        // 是否存在已排队的技能请求
        private bool hasQueuedCast;
        // 单槽技能请求队列（用于输入缓冲）
        private QueuedCast queuedCast;
        // 最近一次施放失败原因（用于 UI/调试）
        private SkillCastFailReason lastCastFailReason = SkillCastFailReason.None;

        /// <summary>当技能开始施法时触发</summary>
        public event Action<SkillCastEvent> SkillCastStarted;
        /// <summary>当技能施法完成时触发</summary>
        public event Action<SkillCastEvent> SkillCastCompleted;
        /// <summary>当技能施法被打断时触发</summary>
        public event Action<SkillCastEvent> SkillCastInterrupted;
        /// <summary>当技能列表发生变化时触发</summary>
        public event Action SkillsChanged;

        /// <summary>是否正在施法中</summary>
        public bool IsCasting => isCasting;
        /// <summary>当前正在施放的技能</summary>
        public SkillDefinition CurrentSkill => currentSkill;
        /// <summary>运行时技能列表</summary>
        public IReadOnlyList<SkillDefinition> Skills => runtimeSkills;
        /// <summary>获取普通攻击技能</summary>
        public SkillDefinition BasicAttack => basicAttackOverride != null ? basicAttackOverride : unitRoot?.Definition?.BasicAttack;
        /// <summary>施法中是否允许移动</summary>
        public bool CanMoveWhileCasting => !isCasting || (currentSkill != null && currentSkill.CanMoveWhileCasting);
        /// <summary>施法中是否允许旋转</summary>
        public bool CanRotateWhileCasting => !isCasting || (currentSkill != null && currentSkill.CanRotateWhileCasting);
        /// <summary>是否处于引导阶段</summary>
        public bool IsChanneling => isCasting && currentChannelTime > 0f && Time.time >= castStartTime + currentCastTime;
        /// <summary>最近一次施放失败原因</summary>
        public SkillCastFailReason LastCastFailReason => lastCastFailReason;

        private void Reset()
        {
            // 编辑器下自动查找组件
            unitRoot = GetComponent<UnitRoot>();
            stats = GetComponent<StatsComponent>();
            health = GetComponent<HealthComponent>();
            resource = GetComponent<ResourceComponent>();
            cooldown = GetComponent<CooldownComponent>();
            buffController = GetComponent<BuffController>();
        }

        private void Awake()
        {
            if (eventHub == null && unitRoot != null)
            {
                eventHub = unitRoot.EventHub;
            }

            if (initializeOnAwake)
            {
                Initialize(unitRoot != null ? unitRoot.Definition : null);
            }
        }

        private void OnEnable()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (stats == null)
            {
                stats = GetComponent<StatsComponent>();
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (resource == null)
            {
                resource = GetComponent<ResourceComponent>();
            }

            if (cooldown == null)
            {
                cooldown = GetComponent<CooldownComponent>();
            }

            if (buffController == null)
            {
                buffController = GetComponent<BuffController>();
            }

            if (eventHub == null && unitRoot != null)
            {
                eventHub = unitRoot.EventHub;
            }

            if (targetingSystem == null)
            {
                // 优先检查 unitRoot 是否有注入，否则尝试全局查找（较耗时）
                targetingSystem = FindObjectOfType<TargetingSystem>();
            }

            if (effectExecutor == null)
            {
                // 优先检查 unitRoot 是否有注入，否则尝试全局查找（较耗时）
                effectExecutor = FindObjectOfType<EffectExecutor>();
            }

            if (health != null)
            {
                health.Died += HandleUnitDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleUnitDied;
            }

            ClearAllPendingSteps();
            hasQueuedCast = false;
            queuedCast = default;
            currentQueueWindow = 0f;
            recoveryEndTime = 0f;
            gcdEndTime = 0f;
            ClearCastState();
        }

        /// <summary>
        /// 每帧更新：处理延迟执行的技能步骤和施法状态。
        /// </summary>
        private void Update()
        {
            UpdateAmmoRecharge(Time.time);
            UpdateRecastExpiry(Time.time);
            UpdateSequenceExpiry(Time.time);

            // 处理延迟执行的技能步骤
            if (pendingSteps.Count > 0)
            {
                var now = Time.time;
                // 从后向前遍历，方便移除
                for (int i = pendingSteps.Count - 1; i >= 0; i--)
                {
                    var pending = pendingSteps[i];
                    if (pending.ExecuteAt > now)
                    {
                        continue;
                    }

                    // 执行步骤并释放目标列表
                    ExecuteStep(pending);
                    ReleaseHandle(pending.Targets);
                    pendingSteps.RemoveAt(i);
                }
            }

            // 检查施法是否完成
            if (isCasting && Time.time >= castEndTime)
            {
                if (currentSkill != null)
                {
                    var context = currentContext.Skill != null
                        ? currentContext
                        : CreateContext(
                            currentSkill,
                            currentHasAimPoint,
                            currentAimPoint,
                            currentAimDirection,
                            null,
                            currentChargeDuration,
                            currentChargeRatio,
                            currentChargeMultiplier,
                            0UL,
                            -1,
                            currentSequencePhase);
                    RaiseSkillCastCompleted(context, currentCastTime, currentChannelTime, currentIsChannel);
                }

                ClearCastState();
            }

            if (!isCasting)
            {
                if (TryHandleTaunt())
                {
                    return;
                }

                // 施法结束后尝试消耗输入缓冲
                TryConsumeQueuedCast();
            }
        }

        /// <summary>
        /// 根据单位定义初始化技能列表。
        /// </summary>
        /// <param name="definition">单位配置定义</param>
        public void Initialize(UnitDefinition definition)
        {
            runtimeSkills.Clear();

            // 从 UnitDefinition 加载技能
            if (useUnitDefinitionSkills && definition != null)
            {
                // 添加普通攻击
                if (definition.BasicAttack != null)
                {
                    runtimeSkills.Add(definition.BasicAttack);
                }

                // 添加初始技能
                var startingSkills = definition.StartingSkills;
                for (int i = 0; i < startingSkills.Count; i++)
                {
                    AddRuntimeSkillUnique(startingSkills[i]);
                }
            }

            // 添加额外配置的技能
            for (int i = 0; i < skills.Count; i++)
            {
                AddRuntimeSkillUnique(skills[i]);
            }

            RefreshSkillRuntimeStates();
            SkillsChanged?.Invoke();
        }

        /// <summary>
        /// 覆盖当前技能列表（用于读档恢复或运行时构筑重设）。
        /// </summary>
        /// <param name="newSkills">新的技能列表</param>
        /// <param name="includeBasicAttack">是否自动包含普攻</param>
        public void SetSkills(IReadOnlyList<SkillDefinition> newSkills, bool includeBasicAttack)
        {
            runtimeSkills.Clear();

            if (includeBasicAttack)
            {
                var basic = BasicAttack;
                if (basic != null)
                {
                    runtimeSkills.Add(basic);
                }
            }

            if (newSkills != null)
            {
                for (int i = 0; i < newSkills.Count; i++)
                {
                    AddRuntimeSkillUnique(newSkills[i]);
                }
            }

            RefreshSkillRuntimeStates();
            SkillsChanged?.Invoke();
        }

        private bool AddRuntimeSkillUnique(SkillDefinition skill)
        {
            if (skill == null || runtimeSkills.Contains(skill))
            {
                return false;
            }

            runtimeSkills.Add(skill);
            return true;
        }

        private void RefreshSkillRuntimeStates()
        {
            ammoStates.Clear();
            recastStates.Clear();
            sequenceStates.Clear();

            for (int i = 0; i < runtimeSkills.Count; i++)
            {
                EnsureAmmoState(runtimeSkills[i], true);
                EnsureRecastState(runtimeSkills[i]);
                EnsureSequenceState(runtimeSkills[i]);
            }
        }

        private void EnsureAmmoState(SkillDefinition skill, bool resetIfMissing)
        {
            if (skill == null || !skill.SupportsAmmo || skill.AmmoConfig == null)
            {
                return;
            }

            if (ammoStates.ContainsKey(skill))
            {
                return;
            }

            var initial = skill.AmmoConfig.InitialCharges;
            if (!resetIfMissing)
            {
                initial = skill.AmmoConfig.MaxCharges;
            }

            ammoStates[skill] = new AmmoRuntimeState(initial, 0f);
        }

        private void EnsureRecastState(SkillDefinition skill)
        {
            if (skill == null || !skill.SupportsRecast)
            {
                return;
            }

            if (!recastStates.ContainsKey(skill))
            {
                recastStates[skill] = default;
            }
        }

        private void EnsureSequenceState(SkillDefinition skill)
        {
            if (skill == null || !skill.SupportsSequence)
            {
                return;
            }

            if (!sequenceStates.ContainsKey(skill))
            {
                sequenceStates[skill] = default;
            }
        }

        /// <summary>
        /// 检查当前技能列表中是否已包含指定技能。
        /// </summary>
        public bool HasSkill(SkillDefinition skill)
        {
            return skill != null && runtimeSkills.Contains(skill);
        }

        public int GetCurrentAmmo(SkillDefinition skill)
        {
            if (skill == null || !skill.SupportsAmmo || skill.AmmoConfig == null)
            {
                return -1;
            }

            EnsureAmmoState(skill, true);
            if (ammoStates.TryGetValue(skill, out var state))
            {
                return state.CurrentCharges;
            }

            return skill.AmmoConfig.InitialCharges;
        }

        public bool HasActiveRecast(SkillDefinition skill)
        {
            return TryGetRecastState(skill, out _);
        }

        public int GetCurrentSequencePhase(SkillDefinition skill)
        {
            if (skill == null || !skill.SupportsSequence)
            {
                return 1;
            }

            return GetSequencePhaseForCast(skill, Time.time);
        }

        /// <summary>
        /// 获取技能连段窗口状态（用于 UI 展示强化窗口倒计时）。
        /// </summary>
        /// <returns>存在有效连段窗口时返回 true。</returns>
        public bool TryGetSequenceWindowState(
            SkillDefinition skill,
            out int phase,
            out float remainingTime,
            out float totalWindow)
        {
            phase = 1;
            remainingTime = 0f;
            totalWindow = 0f;

            if (skill == null || !skill.SupportsSequence || skill.SequenceConfig == null)
            {
                return false;
            }

            EnsureSequenceState(skill);
            if (!sequenceStates.TryGetValue(skill, out var state) || !state.Active)
            {
                return false;
            }

            var now = Time.time;
            if (state.ExpireTime <= 0f || now > state.ExpireTime)
            {
                sequenceStates[skill] = default;
                return false;
            }

            phase = Mathf.Clamp(state.CurrentPhase, 1, skill.SequenceConfig.MaxPhases);
            totalWindow = Mathf.Max(0f, skill.SequenceConfig.ResetWindow);
            remainingTime = Mathf.Max(0f, state.ExpireTime - now);
            return totalWindow > 0f && remainingTime > 0f;
        }

        /// <summary>
        /// 获取技能在运行时列表中的索引，不存在时返回 -1。
        /// </summary>
        public int IndexOfSkill(SkillDefinition skill)
        {
            if (skill == null)
            {
                return -1;
            }

            return runtimeSkills.IndexOf(skill);
        }

        /// <summary>
        /// 尝试向运行时技能列表新增一个技能。
        /// </summary>
        /// <param name="skill">要添加的技能</param>
        /// <param name="maxSkillCount">最大技能数限制（-1 表示不限）</param>
        /// <returns>成功添加返回 true</returns>
        public bool TryAddSkill(SkillDefinition skill, int maxSkillCount = -1)
        {
            if (skill == null)
            {
                return false;
            }

            if (maxSkillCount > 0 && runtimeSkills.Count >= maxSkillCount)
            {
                return false;
            }

            if (!AddRuntimeSkillUnique(skill))
            {
                return false;
            }

            RefreshSkillRuntimeStates();
            SkillsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 尝试替换指定槽位技能。
        /// </summary>
        /// <param name="index">要替换的技能索引</param>
        /// <param name="skill">新技能</param>
        /// <param name="lockBasicAttackSlot">是否禁止替换普攻槽</param>
        /// <returns>替换成功返回 true</returns>
        public bool TryReplaceSkill(int index, SkillDefinition skill, bool lockBasicAttackSlot = true)
        {
            if (skill == null)
            {
                return false;
            }

            if (index < 0 || index >= runtimeSkills.Count)
            {
                return false;
            }

            var current = runtimeSkills[index];
            if (current == skill)
            {
                return true;
            }

            if (lockBasicAttackSlot && IsBasicAttackSkill(current))
            {
                return false;
            }

            if (runtimeSkills.Contains(skill))
            {
                return false;
            }

            runtimeSkills[index] = skill;
            RefreshSkillRuntimeStates();
            SkillsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 尝试释放指定技能。
        /// </summary>
        /// <param name="skill">要释放的技能</param>
        /// <param name="explicitTarget">显式指定的目标（可选）</param>
        /// <returns>若成功开始施法则返回 true</returns>
        public bool TryCast(SkillDefinition skill, GameObject explicitTarget = null)
        {
            return TryCast(skill, explicitTarget, false, default, default, 0f);
        }

        /// <summary>
        /// 尝试释放指定技能。
        /// </summary>
        /// <param name="skill">要释放的技能</param>
        /// <param name="explicitTarget">显式指定的目标（可选）</param>
        /// <param name="hasAimPoint">是否有瞄准点</param>
        /// <param name="aimPoint">瞄准点</param>
        /// <param name="aimDirection">瞄准方向</param>
        /// <param name="chargeDurationSeconds">按住蓄力时长（秒）</param>
        /// <returns>若成功开始施法则返回 true</returns>
        public bool TryCast(
            SkillDefinition skill,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            float chargeDurationSeconds = 0f)
        {
            if (skill == null)
            {
                return FailCast(SkillCastFailReason.InvalidSkill);
            }

            // Ensure presentation listeners exist before any cast events are dispatched.
            SkillPresentationSystem.EnsureRuntimeInstance();

            var isRecastCast = TryGetRecastState(skill, out var activeRecast);

            if (TryGetTauntSource(out var taunter))
            {
                if (skill != BasicAttack)
                {
                    return FailCast(SkillCastFailReason.TauntRestricted);
                }

                if (taunter == null)
                {
                    return FailCast(SkillCastFailReason.TauntRestricted);
                }

                explicitTarget = taunter.gameObject;
                hasAimPoint = false;
                aimPoint = default;
                aimDirection = default;
            }

            GameObject resolvedRecastTarget = explicitTarget;
            if (isRecastCast
                && !ResolveRecastTarget(skill, activeRecast, explicitTarget, out resolvedRecastTarget))
            {
                return FailCast(SkillCastFailReason.RecastTargetInvalid);
            }

            if (isRecastCast)
            {
                explicitTarget = resolvedRecastTarget;
            }

            if (!ShouldIgnoreOptionalExplicitTarget(skill) && !IsExplicitTargetAlive(explicitTarget))
            {
                return FailCast(SkillCastFailReason.TargetDead);
            }

            var sequencePhase = GetSequencePhaseForCast(skill, Time.time);
            if (!CanCastInternal(skill, false, explicitTarget, hasAimPoint, aimPoint, aimDirection, sequencePhase, out var canCastFailReason))
            {
                // 施法/后摇/GCD期间允许进入输入缓冲
                if (IsLockedOut() && TryQueueCast(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection, chargeDurationSeconds))
                {
                    lastCastFailReason = SkillCastFailReason.Queued;
                    return false;
                }

                return FailCast(canCastFailReason);
            }

            var effectiveExplicitTarget = ResolveEvaluationExplicitTarget(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection, sequencePhase);

            if (skill != null && skill.Targeting != null && skill.Targeting.Origin == TargetingOrigin.TargetPoint && !hasAimPoint)
            {
                if (effectiveExplicitTarget != null)
                {
                    aimPoint = effectiveExplicitTarget.transform.position;
                    hasAimPoint = true;
                }
            }

            // 锁定目标技能：有显式目标时强制使用“指向目标”的朝向，避免沿鼠标方向发射。
            if (skill.Targeting != null && skill.Targeting.RequireExplicitTarget && effectiveExplicitTarget != null && unitRoot != null)
            {
                if (skill.Targeting.Origin == TargetingOrigin.TargetPoint)
                {
                    aimPoint = effectiveExplicitTarget.transform.position;
                    hasAimPoint = true;
                }

                var toExplicitTarget = effectiveExplicitTarget.transform.position - unitRoot.transform.position;
                toExplicitTarget.y = 0f;
                if (toExplicitTarget.sqrMagnitude > 0.0001f)
                {
                    aimDirection = toExplicitTarget.normalized;
                }
            }

            if (aimDirection.sqrMagnitude <= 0.0001f && effectiveExplicitTarget != null && unitRoot != null)
            {
                var dir = effectiveExplicitTarget.transform.position - unitRoot.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    aimDirection = dir.normalized;
                }
            }

            if (hasAimPoint && aimDirection.sqrMagnitude <= 0.0001f && unitRoot != null)
            {
                var dir = aimPoint - unitRoot.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    aimDirection = dir.normalized;
                }
            }

            // 显式目标统一按“当前帧”的形状/距离进行校验，避免旧快照导致超范围命中。
            if (effectiveExplicitTarget != null
                && ShouldEnforceExplicitTargetRange(skill)
                && !IsTargetInRange(skill, effectiveExplicitTarget, hasAimPoint, aimPoint, aimDirection))
            {
                return FailCast(SkillCastFailReason.OutOfRange);
            }

            // 收集目标
            var targets = SimpleListPool<CombatTarget>.Get();
            if (!CollectTargets(skill, effectiveExplicitTarget, hasAimPoint, aimPoint, aimDirection, targets))
            {
                SimpleListPool<CombatTarget>.Release(targets);
                return FailCast(SkillCastFailReason.NoValidTargets);
            }

            if (!FilterCastTargetsByAlivePolicy(skill, targets))
            {
                SimpleListPool<CombatTarget>.Release(targets);
                return FailCast(SkillCastFailReason.NoValidTargets);
            }

            var chargeDuration = Mathf.Max(0f, chargeDurationSeconds);
            var chargeRatio = skill.ResolveChargeRatio(chargeDuration);
            var chargeMultiplier = skill.ResolveChargeMultiplier(chargeDuration);
            var castId = GenerateCastId();
            var contextExplicitTarget = ResolveContextExplicitTarget(skill, effectiveExplicitTarget, targets);

            // 创建技能上下文
            var context = CreateContext(
                skill,
                hasAimPoint,
                aimPoint,
                aimDirection,
                contextExplicitTarget,
                chargeDuration,
                chargeRatio,
                chargeMultiplier,
                castId,
                -1,
                sequencePhase);
            if (skill.Targeting != null
                && skill.Targeting.RequireExplicitTarget
                && !HasExecutableConditionalCastStartStep(context, targets))
            {
                SimpleListPool<CombatTarget>.Release(targets);
                return FailCast(SkillCastFailReason.NoExecutableStep);
            }

            var primaryTarget = targets.Count > 0 ? targets[0] : default;
            var resourceCost = ResolveModifiedResourceCost(skill, context, primaryTarget);
            if (isRecastCast && skill.RecastConfig != null && !skill.RecastConfig.ConsumesResourceOnRecast)
            {
                resourceCost = 0f;
            }

            var cooldownDuration = Mathf.Max(0f, ModifierResolver.ApplySkillModifiers(skill.Cooldown, skill, context, primaryTarget, ModifierParameters.SkillCooldown));
            var castTime = Mathf.Max(0f, ModifierResolver.ApplySkillModifiers(skill.CastTime, skill, context, primaryTarget, ModifierParameters.SkillCastTime));
            var channelTime = Mathf.Max(0f, ModifierResolver.ApplySkillModifiers(skill.ChannelTime, skill, context, primaryTarget, ModifierParameters.SkillChannelTime));
            var postCastTime = Mathf.Max(0f, ModifierResolver.ApplySkillModifiers(skill.PostCastTime, skill, context, primaryTarget, ModifierParameters.SkillPostCastTime));
            var gcdDuration = Mathf.Max(0f, ModifierResolver.ApplySkillModifiers(skill.GcdDuration, skill, context, primaryTarget, ModifierParameters.SkillGcdDuration));
            var channelTickInterval = Mathf.Max(0f, ModifierResolver.ApplySkillModifiers(skill.ChannelTickInterval, skill, context, primaryTarget, ModifierParameters.SkillChannelTickInterval));
            var queueWindow = Mathf.Max(0f, ModifierResolver.ApplySkillModifiers(skill.QueueWindow, skill, context, primaryTarget, ModifierParameters.SkillQueueWindow));
            var isChannel = channelTime > 0f;
            var totalTime = castTime + channelTime;

            if (IsBasicAttackSkill(skill))
            {
                cooldownDuration = ApplyAttackSpeed(cooldownDuration);
            }
            else
            {
                cooldownDuration = ApplyAbilityHaste(cooldownDuration);
            }

            // 扣除资源
            if (!SpendResource(skill, resourceCost))
            {
                SimpleListPool<CombatTarget>.Release(targets);
                return FailCast(SkillCastFailReason.ResourceSpendFailed);
            }

            if (!ConsumeAmmo(skill))
            {
                // 极端情况下（并发消耗）确保状态回滚
                if (resourceCost > 0f)
                {
                    resource?.Restore(resourceCost);
                }

                SimpleListPool<CombatTarget>.Release(targets);
                return FailCast(SkillCastFailReason.AmmoDepleted);
            }

            var delayCooldownForRecast = skill.SupportsRecast
                && skill.RecastConfig != null
                && skill.RecastConfig.DelayCooldownUntilRecastEnds;
            var startCooldownNow = !delayCooldownForRecast;

            // 开始冷却
            if (startCooldownNow && cooldown != null && cooldownDuration > 0f)
            {
                cooldown.StartCooldown(skill, cooldownDuration);
            }

            var now = Time.time;
            UpdateRecastAfterSuccessfulCast(
                skill,
                isRecastCast,
                activeRecast,
                now,
                contextExplicitTarget,
                hasAimPoint,
                aimPoint,
                aimDirection,
                cooldownDuration);
            UpdateSequenceAfterSuccessfulCast(skill, sequencePhase, now);
            ResetSequencesMarkedOnOtherCast(skill);

            if (gcdDuration > 0f)
            {
                // 公共冷却可能被更长的技能覆盖
                gcdEndTime = Mathf.Max(gcdEndTime, now + gcdDuration);
            }

            // 调度技能步骤
            var targetHandle = GetTargetHandle(targets);
            ScheduleSteps(skill, SkillStepTrigger.OnCastStart, now, targetHandle, context);
            ScheduleSteps(skill, SkillStepTrigger.OnCastComplete, now + totalTime, targetHandle, context);
            // 引导期间的周期步骤
            ScheduleChannelTicks(skill, now, castTime, channelTime, channelTickInterval, targetHandle, context);

            // 如果没有任何步骤引用该目标列表，立即释放
            if (targetHandle.RefCount == 0)
            {
                ReleaseHandleUnused(targetHandle);
            }

            // 派发施法开始事件
            RaiseSkillCastStarted(context, castTime, channelTime, isChannel);
            buffController?.NotifySkillCast(context, primaryTarget);
            if (IsBasicAttackSkill(skill))
            {
                buffController?.NotifyAttack(context, primaryTarget);
            }

            currentQueueWindow = queueWindow;
            currentPostCastTime = postCastTime;

            // 如果有施法或引导时间，进入施法状态
            if (totalTime > 0f)
            {
                isCasting = true;
                castStartTime = now;
                castEndTime = now + totalTime;
                currentSkill = skill;
                currentCastTime = castTime;
                currentChannelTime = channelTime;
                currentIsChannel = isChannel;
                currentContext = context;
                currentHasAimPoint = hasAimPoint;
                currentAimPoint = aimPoint;
                currentAimDirection = aimDirection;
                currentChargeDuration = chargeDuration;
                currentChargeRatio = chargeRatio;
                currentChargeMultiplier = chargeMultiplier;
                currentSequencePhase = sequencePhase;
                // 后摇锁定开始于施法结束
                recoveryEndTime = castEndTime + postCastTime;
            }
            else
            {
                // 瞬发技能立即完成
                // 瞬发也会进入后摇锁定
                recoveryEndTime = now + postCastTime;
                RaiseSkillCastCompleted(context, castTime, channelTime, isChannel);
            }

            lastCastFailReason = SkillCastFailReason.None;
            return true;
        }

        /// <summary>
        /// 打断当前施法（不会触发 OnCastComplete 相关步骤）。
        /// </summary>
        public bool InterruptCast()
        {
            if (!isCasting || currentSkill == null)
            {
                return false;
            }

            var context = currentContext.Skill != null
                ? currentContext
                : CreateContext(
                    currentSkill,
                    currentHasAimPoint,
                    currentAimPoint,
                    currentAimDirection,
                    null,
                    currentChargeDuration,
                    currentChargeRatio,
                    currentChargeMultiplier,
                    0UL,
                    -1,
                    currentSequencePhase);
            ClearPendingSteps(currentSkill);
            RaiseSkillCastInterrupted(context, currentCastTime, currentChannelTime, currentIsChannel);
            recoveryEndTime = Time.time + Mathf.Max(0f, currentPostCastTime);
            ClearCastState();
            return true;
        }

        /// <summary>
        /// 主动取消当前施法。
        /// </summary>
        public bool CancelCast()
        {
            return InterruptCast();
        }

        /// <summary>
        /// 取消普攻前摇（用于移动打断普攻）。
        /// </summary>
        public bool CancelBasicAttackWindup()
        {
            if (!isCasting || currentSkill == null)
            {
                return false;
            }

            if (!IsBasicAttackSkill(currentSkill))
            {
                return false;
            }

            if (currentChannelTime > 0f)
            {
                return false;
            }

            var context = currentContext.Skill != null
                ? currentContext
                : CreateContext(
                    currentSkill,
                    currentHasAimPoint,
                    currentAimPoint,
                    currentAimDirection,
                    null,
                    currentChargeDuration,
                    currentChargeRatio,
                    currentChargeMultiplier,
                    0UL,
                    -1,
                    currentSequencePhase);

            ClearPendingSteps(currentSkill);
            RaiseSkillCastInterrupted(context, currentCastTime, currentChannelTime, currentIsChannel);
            recoveryEndTime = Mathf.Min(recoveryEndTime, Time.time);
            ClearCastState();
            return true;
        }

        /// <summary>
        /// 重置普攻冷却与后摇，用于普攻重置类效果。
        /// </summary>
        /// <returns>若成功重置则返回 true，无普攻技能时返回 false</returns>
        public bool ResetBasicAttack()
        {
            var basic = BasicAttack;
            if (basic == null)
            {
                return false;
            }

            cooldown?.ClearCooldown(basic);
            recoveryEndTime = Mathf.Min(recoveryEndTime, Time.time);
            return true;
        }

        /// <summary>
        /// 检查指定技能是否可以释放。
        /// </summary>
        /// <param name="skill">要检查的技能</param>
        /// <returns>若满足所有释放条件则返回 true</returns>
        public bool CanCast(SkillDefinition skill)
        {
            return CanCastInternal(skill, false, null, false, default, default, -1, out _);
        }

        /// <summary>
        /// 检查指定技能是否可以释放，并返回失败原因。
        /// </summary>
        public bool CanCast(SkillDefinition skill, out SkillCastFailReason failReason)
        {
            return CanCastInternal(skill, false, null, false, default, default, -1, out failReason);
        }

        /// <summary>
        /// 检查指定技能在当前目标/瞄准上下文下是否可释放。
        /// </summary>
        public bool CanCast(
            SkillDefinition skill,
            GameObject explicitTarget,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default)
        {
            return CanCastInternal(skill, false, explicitTarget, hasAimPoint, aimPoint, aimDirection, -1, out _);
        }

        /// <summary>
        /// 检查指定技能在当前目标/瞄准上下文下是否可释放，并返回失败原因。
        /// </summary>
        public bool CanCast(
            SkillDefinition skill,
            GameObject explicitTarget,
            out SkillCastFailReason failReason,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default)
        {
            return CanCastInternal(skill, false, explicitTarget, hasAimPoint, aimPoint, aimDirection, -1, out failReason);
        }

        private bool FailCast(SkillCastFailReason failReason)
        {
            lastCastFailReason = failReason;
            return false;
        }

        private bool TryHandleTaunt()
        {
            if (buffController == null || IsLockedOut())
            {
                return false;
            }

            if (!TryGetTauntSource(out var source))
            {
                return false;
            }

            var basicAttack = BasicAttack;
            if (basicAttack == null)
            {
                return true;
            }

            if (cooldown != null && !cooldown.IsReady(basicAttack))
            {
                return true;
            }

            if (!HasAmmo(basicAttack))
            {
                return true;
            }

            var resourceCost = ResolveModifiedResourceCost(basicAttack, source.gameObject, false, default, default);
            if (!HasResource(basicAttack, resourceCost))
            {
                return true;
            }

            TryCast(basicAttack, source.gameObject);
            return true;
        }

        private bool IsTauntBasicAttack(SkillDefinition skill)
        {
            if (skill != BasicAttack)
            {
                return false;
            }

            if (!TryGetTauntSource(out _))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 尝试获取当前单位被“嘲讽”（Taunt）强制指向的来源单位。
        /// 若返回 true，则通过 <paramref name="source"/> 输出嘲讽来源的 <see cref="UnitRoot"/>，
        /// 上层调用可据此限制只能对该来源进行普攻或自动响应。
        /// </summary>
        /// <remarks>
        /// 实现细节：调用 <c>buffController.TryGetForcedMovement(out control, out source)</c>
        /// 并检查返回的控制类型是否为 <c>ControlType.Taunt</c> 且 <c>source</c> 非空。
        /// 常被 <see cref="TryCast"/>、<see cref="CanCast"/> 与 <see cref="TryHandleTaunt"/> 等方法使用，
        /// 用于在被嘲讽状态下强制只能向嘲讽者进行普攻。
        /// </remarks>
        /// <param name="source">当返回 true 时指向嘲讽来源的单位根，否则为 null。</param>
        /// <returns>若存在有效的嘲讽来源则返回 true。</returns>
        private bool TryGetTauntSource(out UnitRoot source)
        {
            source = null;
            if (buffController == null)
            {
                return false;
            }

            if (!buffController.TryGetForcedMovement(out var control, out source))
            {
                return false;
            }

            return control == ControlType.Taunt && source != null;
        }

        /// <summary>
        /// 预检查技能目标是否在有效形状/距离内（不触发施法）。
        /// </summary>
        public bool IsTargetInRangePreview(
            SkillDefinition skill,
            GameObject target,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default)
        {
            if (skill != null && skill.Targeting != null && skill.Targeting.RequireExplicitTarget && target == null)
            {
                return false;
            }

            return IsTargetInRange(skill, target, hasAimPoint, aimPoint, aimDirection);
        }

        private bool IsTargetInRange(SkillDefinition skill, GameObject target, bool hasAimPoint, Vector3 aimPoint, Vector3 aimDirection)
        {
            if (skill == null || target == null || targetingSystem == null || unitRoot == null)
            {
                return true;
            }

            var targeting = skill.Targeting;
            if (targeting == null)
            {
                return true;
            }

            // 兼容纯效果技能：没有任何几何参数时不强制范围限制。
            if (targeting.Range <= 0f && targeting.Radius <= 0f)
            {
                return true;
            }

            if (!CombatTarget.TryCreate(target, out var combatTarget))
            {
                return false;
            }

            return targetingSystem.IsWithinTargetingShape(
                targeting,
                unitRoot,
                combatTarget,
                target,
                hasAimPoint,
                aimPoint,
                aimDirection);
        }

        private bool IsLockedOut()
        {
            // 施法/后摇/GCD 任一命中则锁定
            if (isCasting)
            {
                return true;
            }

            if (Time.time < recoveryEndTime)
            {
                return true;
            }

            return Time.time < gcdEndTime;
        }

        private bool CanCastIgnoringLockouts(
            SkillDefinition skill,
            GameObject explicitTarget = null,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default)
        {
            return CanCastInternal(skill, true, explicitTarget, hasAimPoint, aimPoint, aimDirection, -1, out _);
        }

        private bool CanCastInternal(
            SkillDefinition skill,
            bool ignoreLockouts,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            int sequencePhase = -1)
        {
            return CanCastInternal(
                skill,
                ignoreLockouts,
                explicitTarget,
                hasAimPoint,
                aimPoint,
                aimDirection,
                sequencePhase,
                out _);
        }

        private bool CanCastInternal(
            SkillDefinition skill,
            bool ignoreLockouts,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            int sequencePhase,
            out SkillCastFailReason failReason)
        {
            failReason = SkillCastFailReason.None;
            if (skill == null)
            {
                failReason = SkillCastFailReason.InvalidSkill;
                return false;
            }

            var isTauntBasic = IsTauntBasicAttack(skill);
            if (TryGetTauntSource(out _) && !isTauntBasic)
            {
                failReason = SkillCastFailReason.TauntRestricted;
                return false;
            }

            if (!ignoreLockouts && IsLockedOut())
            {
                failReason = SkillCastFailReason.LockedOut;
                return false;
            }

            if (buffController != null && buffController.HasControlFlag(ControlFlag.BlocksCasting) && !isTauntBasic)
            {
                failReason = SkillCastFailReason.CastingBlocked;
                return false;
            }

            if (skill == BasicAttack && buffController != null && buffController.HasControlFlag(ControlFlag.BlocksBasicAttack) && !isTauntBasic)
            {
                failReason = SkillCastFailReason.BasicAttackBlocked;
                return false;
            }

            if (health != null && !health.IsAlive)
            {
                failReason = SkillCastFailReason.CasterDead;
                return false;
            }

            var resolvedSequencePhase = sequencePhase > 0
                ? sequencePhase
                : GetSequencePhaseForCast(skill, Time.time);
            var evaluationTarget = ResolveEvaluationExplicitTarget(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection, resolvedSequencePhase);

            var isRecastCast = TryGetRecastState(skill, out var recastState);
            if (isRecastCast
                && !ResolveRecastTarget(skill, recastState, evaluationTarget, out _))
            {
                failReason = SkillCastFailReason.RecastTargetInvalid;
                return false;
            }

            if (!isRecastCast && cooldown != null && !cooldown.IsReady(skill))
            {
                failReason = SkillCastFailReason.Cooldown;
                return false;
            }

            if (!IsExplicitTargetAlive(evaluationTarget))
            {
                failReason = SkillCastFailReason.TargetDead;
                return false;
            }

            if (!HasAmmo(skill))
            {
                failReason = SkillCastFailReason.AmmoDepleted;
                return false;
            }

            if (!EvaluateCastConstraints(skill, evaluationTarget, hasAimPoint, aimPoint, aimDirection, resolvedSequencePhase, out failReason))
            {
                return false;
            }

            var cost = ResolveModifiedResourceCost(skill, evaluationTarget, hasAimPoint, aimPoint, aimDirection, resolvedSequencePhase);
            if (isRecastCast && skill.RecastConfig != null && !skill.RecastConfig.ConsumesResourceOnRecast)
            {
                cost = 0f;
            }

            if (!HasResource(skill, cost))
            {
                failReason = SkillCastFailReason.InsufficientResource;
                return false;
            }

            return true;
        }

        private bool EvaluateCastConstraints(
            SkillDefinition skill,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            int sequencePhase,
            out SkillCastFailReason failReason)
        {
            failReason = SkillCastFailReason.None;
            var constraints = skill != null ? skill.CastConstraints : null;
            if (constraints == null || constraints.Count == 0)
            {
                return true;
            }

            CombatTarget target = default;
            if (explicitTarget != null && !CombatTarget.TryCreate(explicitTarget, out target))
            {
                failReason = SkillCastFailReason.NoValidTargets;
                return false;
            }

            var context = CreateContext(
                skill,
                hasAimPoint,
                aimPoint,
                aimDirection,
                explicitTarget,
                0f,
                0f,
                1f,
                0UL,
                -1,
                sequencePhase);
            for (int i = 0; i < constraints.Count; i++)
            {
                var constraint = constraints[i];
                if (constraint == null || constraint.condition == null)
                {
                    continue;
                }

                if (ConditionEvaluator.Evaluate(constraint.condition, context, target))
                {
                    continue;
                }

                failReason = constraint.failReason != SkillCastFailReason.None
                    ? constraint.failReason
                    : SkillCastFailReason.CastConstraintFailed;
                return false;
            }

            return true;
        }

        private bool TryQueueCast(
            SkillDefinition skill,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            float chargeDurationSeconds)
        {
            if (skill == null || !CanCastIgnoringLockouts(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection))
            {
                return false;
            }

            if (!IsWithinQueueWindow())
            {
                return false;
            }

            if (hasQueuedCast && skill.QueuePolicy == SkillQueuePolicy.Ignore)
            {
                return false;
            }

            queuedCast = new QueuedCast(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection, chargeDurationSeconds);
            hasQueuedCast = true;
            return true;
        }

        private bool IsWithinQueueWindow()
        {
            if (currentQueueWindow <= 0f)
            {
                return false;
            }

            var endTime = GetQueueWindowEndTime();
            if (endTime <= 0f)
            {
                return false;
            }

            // 在技能结束前的窗口期允许缓存输入
            return Time.time >= endTime - currentQueueWindow;
        }

        private float GetQueueWindowEndTime()
        {
            if (isCasting)
            {
                // 以施法结束为窗口基准
                return castEndTime;
            }

            return Mathf.Max(recoveryEndTime, gcdEndTime);
        }

        private void TryConsumeQueuedCast()
        {
            if (!hasQueuedCast)
            {
                return;
            }

            if (IsLockedOut())
            {
                return;
            }

            // 锁定结束后立即执行队列
            var request = queuedCast;
            hasQueuedCast = false;
            queuedCast = default;
            if (request.Skill == null)
            {
                return;
            }

            TryCast(
                request.Skill,
                request.ExplicitTarget,
                request.HasAimPoint,
                request.AimPoint,
                request.AimDirection,
                request.ChargeDurationSeconds);
        }

        /// <summary>
        /// 内部方法：当技能直接命中目标时触发。
        /// </summary>
        internal void NotifyHit(SkillRuntimeContext context, CombatTarget target)
        {
            UpdateSequenceAfterSuccessfulHit(context, Time.time);
            ScheduleTriggeredSteps(context, SkillStepTrigger.OnHit, target);
        }

        /// <summary>
        /// 内部方法：当投射物命中目标时触发。
        /// </summary>
        internal void NotifyProjectileHit(SkillRuntimeContext context, CombatTarget target)
        {
            ScheduleTriggeredSteps(context, SkillStepTrigger.OnProjectileHit, target);
        }

        private void HandleUnitDied(HealthComponent source)
        {
            InterruptCast();
        }

        /// <summary>
        /// 收集技能目标。
        /// </summary>
        /// <returns>若成功收集到至少一个目标则返回 true</returns>
        private bool CollectTargets(
            SkillDefinition skill,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            List<CombatTarget> targets)
        {
            if (skill == null)
            {
                return false;
            }

            // 无目标系统或无目标定义时，默认选择自身
            if (targetingSystem == null || skill.Targeting == null)
            {
                if (unitRoot != null && CombatTarget.TryCreate(unitRoot.gameObject, out var selfTarget))
                {
                    targets.Add(selfTarget);
                    return true;
                }

                return false;
            }

            if (skill.Targeting.RequireExplicitTarget && explicitTarget == null)
            {
                return false;
            }

            // 使用目标系统收集目标
            targetingSystem.CollectTargets(skill.Targeting, unitRoot, explicitTarget, targets, hasAimPoint, aimPoint, aimDirection);
            if (targets.Count > 0)
            {
                return true;
            }

            if (skill.Targeting.RequireExplicitTarget)
            {
                return false;
            }

            return skill.Targeting.AllowEmpty;
        }

        private bool FilterCastTargetsByAlivePolicy(SkillDefinition skill, List<CombatTarget> targets)
        {
            if (skill == null || targets == null)
            {
                return false;
            }

            var targeting = skill.Targeting;
            if (targeting == null || targeting.HitValidation < HitValidationPolicy.AliveOnly)
            {
                return targets.Count > 0 || targeting != null && targeting.AllowEmpty;
            }

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var target = targets[i];
                if (target.Health != null && !target.Health.IsAlive)
                {
                    targets.RemoveAt(i);
                }
            }

            return targets.Count > 0 || targeting.AllowEmpty;
        }

        private bool IsExplicitTargetAlive(GameObject explicitTarget)
        {
            if (explicitTarget == null)
            {
                return true;
            }

            var targetHealth = explicitTarget.GetComponentInParent<HealthComponent>();
            return targetHealth == null || targetHealth.IsAlive;
        }

        private bool HasExecutableConditionalCastStartStep(SkillRuntimeContext context, List<CombatTarget> targets)
        {
            if (context.Skill == null)
            {
                return true;
            }

            var steps = context.Skill.Steps;
            if (steps == null || steps.Count == 0)
            {
                return true;
            }

            var hasConditionalCastStartStep = false;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null
                    || step.trigger != SkillStepTrigger.OnCastStart
                    || step.condition == null
                    || step.effects == null
                    || step.effects.Count == 0)
                {
                    continue;
                }

                hasConditionalCastStartStep = true;
                break;
            }

            if (!hasConditionalCastStartStep)
            {
                return true;
            }

            if (targets == null || targets.Count == 0)
            {
                if (context.Skill.Targeting == null || !context.Skill.Targeting.AllowEmpty)
                {
                    return false;
                }

                var emptyTarget = default(CombatTarget);
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    if (step == null
                        || step.trigger != SkillStepTrigger.OnCastStart
                        || step.effects == null
                        || step.effects.Count == 0)
                    {
                        continue;
                    }

                    if (step.condition == null || ConditionEvaluator.Evaluate(step.condition, context, emptyTarget))
                    {
                        return true;
                    }
                }

                return false;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null
                    || step.trigger != SkillStepTrigger.OnCastStart
                    || step.effects == null
                    || step.effects.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < targets.Count; j++)
                {
                    var target = targets[j];
                    if (!IsTargetValidForStep(context, SkillStepTrigger.OnCastStart, target))
                    {
                        continue;
                    }

                    if (step.condition != null && !ConditionEvaluator.Evaluate(step.condition, context, target))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 为单个目标调度触发型技能步骤（如 OnHit）。
        /// </summary>
        private void ScheduleTriggeredSteps(SkillRuntimeContext context, SkillStepTrigger trigger, CombatTarget target)
        {
            if (context.Skill == null)
            {
                return;
            }

            // 为单个目标创建临时列表
            var list = SimpleListPool<CombatTarget>.Get(1);
            list.Add(target);
            var handle = GetTargetHandle(list);
            ScheduleSteps(context.Skill, trigger, Time.time, handle, context);

            // 如果没有步骤引用该 Handle，立即释放
            if (handle.RefCount == 0)
            {
                ReleaseHandleUnused(handle);
            }
        }

        /// <summary>
        /// 根据技能定义调度指定触发时机的所有步骤。
        /// </summary>
        private void ScheduleSteps(SkillDefinition skill, SkillStepTrigger trigger, float baseTime, TargetListHandle targets, SkillRuntimeContext context)
        {
            var steps = skill.Steps;
            if (steps == null || steps.Count == 0)
            {
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                // 跳过不匹配的触发时机
                if (step == null || step.trigger != trigger)
                {
                    continue;
                }

                // 计算执行时间（基准时间 + 延迟）
                var executeAt = baseTime + Mathf.Max(0f, step.delay);
                pendingSteps.Add(new PendingStep(step, trigger, i, executeAt, targets, context));
                // 增加引用计数，防止过早释放
                targets.RefCount++;
            }
        }

        private void ScheduleChannelTicks(
            SkillDefinition skill,
            float castStart,
            float castTime,
            float channelTime,
            float tickInterval,
            TargetListHandle targets,
            SkillRuntimeContext context)
        {
            if (skill == null || channelTime <= 0f || tickInterval <= 0f)
            {
                return;
            }

            // 只有配置了 OnChannelTick 步骤才需要调度
            if (!HasSteps(skill, SkillStepTrigger.OnChannelTick))
            {
                return;
            }

            var startTime = castStart + castTime;
            var endTime = startTime + channelTime;
            var nextTime = startTime + tickInterval;

            if (nextTime > endTime)
            {
                return;
            }

            while (nextTime <= endTime)
            {
                // 每个 Tick 都对应一次步骤调度
                ScheduleSteps(skill, SkillStepTrigger.OnChannelTick, nextTime, targets, context);
                nextTime += tickInterval;
            }
        }

        private static bool HasSteps(SkillDefinition skill, SkillStepTrigger trigger)
        {
            if (skill == null)
            {
                return false;
            }

            var steps = skill.Steps;
            if (steps == null || steps.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step != null && step.trigger == trigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldReselectTargets(SkillDefinition skill, SkillStepTrigger trigger)
        {
            if (skill == null)
            {
                return false;
            }

            // 命中触发不再重新选目标
            if (trigger == SkillStepTrigger.OnHit || trigger == SkillStepTrigger.OnProjectileHit)
            {
                return false;
            }

            switch (skill.TargetSnapshotPolicy)
            {
                case TargetSnapshotPolicy.PerStep:
                    return true;
                case TargetSnapshotPolicy.AtCastComplete:
                    return trigger != SkillStepTrigger.OnCastStart;
                case TargetSnapshotPolicy.AtCastStart:
                default:
                    return false;
            }
        }

        private bool IsTargetValidForStep(SkillRuntimeContext context, SkillStepTrigger trigger, CombatTarget target)
        {
            if (!target.IsValid)
            {
                return false;
            }

            var targeting = context.Skill != null ? context.Skill.Targeting : null;
            if (targeting == null)
            {
                return true;
            }

            if (!HitResolutionSystem.CanSelectTarget(context.CasterUnit, targeting, target))
            {
                return false;
            }

            var policy = targeting.HitValidation;
            if (policy == HitValidationPolicy.None)
            {
                return true;
            }

            if (policy >= HitValidationPolicy.AliveOnly && target.Health != null && !target.Health.IsAlive)
            {
                return false;
            }

            // 命中触发已在物理/命中路径校验，无需重复
            if (trigger == SkillStepTrigger.OnHit || trigger == SkillStepTrigger.OnProjectileHit)
            {
                return true;
            }

            if (targetingSystem == null)
            {
                return true;
            }

            if (policy == HitValidationPolicy.InRange || policy == HitValidationPolicy.InRangeAndLoS)
            {
                // 形状范围校验
                if (!targetingSystem.IsWithinTargetingShape(
                        targeting,
                        context.CasterUnit,
                        target,
                        context.ExplicitTarget,
                        context.HasAimPoint,
                        context.AimPoint,
                        context.AimDirection))
                {
                    return false;
                }

                if (policy == HitValidationPolicy.InRangeAndLoS
                    && !targetingSystem.HasLineOfSight(targeting, context.CasterUnit, target))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 执行单个技能步骤，对所有目标应用效果。
        /// </summary>
        private void ExecuteStep(PendingStep pending)
        {
            var step = pending.Step;
            if (step == null)
            {
                return;
            }

            var context = pending.Context.WithStepIndex(pending.StepIndex);
            var targets = pending.Targets.Targets;
            var useSnapshot = true;

            if (ShouldReselectTargets(context.Skill, pending.Trigger))
            {
                var refreshed = SimpleListPool<CombatTarget>.Get();
                if (CollectTargets(context.Skill, context.ExplicitTarget, context.HasAimPoint, context.AimPoint, context.AimDirection, refreshed))
                {
                    targets = refreshed;
                    useSnapshot = false;
                }
                else
                {
                    SimpleListPool<CombatTarget>.Release(refreshed);
                    return;
                }
            }

            var primaryTarget = targets != null && targets.Count > 0 ? targets[0] : default;
            RaiseSkillStepExecuted(context, step, pending.Trigger, primaryTarget, pending.StepIndex);

            var effects = step.effects;
            if (effects == null || effects.Count == 0)
            {
                if (!useSnapshot)
                {
                    SimpleListPool<CombatTarget>.Release(targets);
                }

                return;
            }

            if (effectExecutor == null)
            {
                if (!useSnapshot)
                {
                    SimpleListPool<CombatTarget>.Release(targets);
                }

                return;
            }

            if (targets == null || targets.Count == 0)
            {
                if (!useSnapshot)
                {
                    SimpleListPool<CombatTarget>.Release(targets);
                }

                // 允许“无目标释放”的技能（例如指向性投射物/召唤等）在没有命中单位时仍执行一次。
                // 这里用 default(CombatTarget) 作为占位目标：需要单位目标的效果会自然 no-op（Health/Stats 等为空），
                // 而依赖 AimPoint/AimDirection 的效果（如 Projectile/Summon）仍可正常工作。
                if (context.Skill != null && context.Skill.Targeting != null && context.Skill.Targeting.AllowEmpty)
                {
                    var emptyTarget = default(CombatTarget);

                    // 检查步骤级别的条件（允许仅依赖 Caster 的条件通过）
                    if (step.condition == null || ConditionEvaluator.Evaluate(step.condition, context, emptyTarget))
                    {
                        for (int j = 0; j < effects.Count; j++)
                        {
                            effectExecutor.ExecuteEffect(effects[j], context, emptyTarget, pending.Trigger);
                        }
                    }
                }

                return;
            }

            if (step.executeOnce)
            {
                if (TryGetSingleExecutionTarget(context, pending.Trigger, targets, out var executionTarget))
                {
                    ExecuteEffectsForTarget(step, effects, context, pending.Trigger, executionTarget);
                }

                if (!useSnapshot)
                {
                    SimpleListPool<CombatTarget>.Release(targets);
                }

                return;
            }

            // 遍历所有目标
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!IsTargetValidForStep(context, pending.Trigger, target))
                {
                    continue;
                }

                // 检查步骤级别的条件
                if (step.condition != null && !ConditionEvaluator.Evaluate(step.condition, context, target))
                {
                    continue;
                }

                ExecuteEffectsForTarget(step, effects, context, pending.Trigger, target);
            }

            if (!useSnapshot)
            {
                SimpleListPool<CombatTarget>.Release(targets);
            }
        }

        private bool TryGetSingleExecutionTarget(
            SkillRuntimeContext context,
            SkillStepTrigger trigger,
            List<CombatTarget> targets,
            out CombatTarget executionTarget)
        {
            executionTarget = default;
            if (targets == null)
            {
                return false;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var candidate = targets[i];
                if (!IsTargetValidForStep(context, trigger, candidate))
                {
                    continue;
                }

                executionTarget = candidate;
                return true;
            }

            return false;
        }

        private void ExecuteEffectsForTarget(
            SkillStep step,
            IReadOnlyList<EffectDefinition> effects,
            SkillRuntimeContext context,
            SkillStepTrigger trigger,
            CombatTarget target)
        {
            if (step != null && step.condition != null && !ConditionEvaluator.Evaluate(step.condition, context, target))
            {
                return;
            }

            var spellShieldChargesBefore = GetSpellShieldCharges(target);
            for (int j = 0; j < effects.Count; j++)
            {
                effectExecutor.ExecuteEffect(effects[j], context, target, trigger);
                if (HasSpellShieldConsumed(target, spellShieldChargesBefore))
                {
                    // 护盾命中后终止该目标本次步骤的剩余效果，避免“先挡伤害后吃 Debuff”。
                    break;
                }
            }
        }

        private float ResolveModifiedResourceCost(
            SkillDefinition skill,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            int sequencePhase = 1)
        {
            if (skill == null)
            {
                return 0f;
            }

            var evaluationTarget = ResolveEvaluationExplicitTarget(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection, sequencePhase);
            var context = CreateContext(skill, hasAimPoint, aimPoint, aimDirection, evaluationTarget, 0f, 0f, 1f, 0UL, -1, sequencePhase);
            var primaryTarget = default(CombatTarget);
            if (evaluationTarget != null)
            {
                CombatTarget.TryCreate(evaluationTarget, out primaryTarget);
            }

            return ResolveModifiedResourceCost(skill, context, primaryTarget);
        }

        private static bool ShouldIgnoreOptionalExplicitTarget(SkillDefinition skill)
        {
            var targeting = skill != null ? skill.Targeting : null;
            return targeting != null && !targeting.RequireExplicitTarget && targeting.IgnoreOptionalExplicitTarget;
        }

        private static bool ShouldResolveSoftEvaluationExplicitTarget(SkillDefinition skill)
        {
            var targeting = skill != null ? skill.Targeting : null;
            if (targeting == null || targeting.RequireExplicitTarget)
            {
                return false;
            }

            switch (targeting.Mode)
            {
                case TargetingMode.Single:
                case TargetingMode.Chain:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ShouldEnforceExplicitTargetRange(SkillDefinition skill)
        {
            var targeting = skill != null ? skill.Targeting : null;
            if (targeting == null)
            {
                return false;
            }

            if (targeting.RequireExplicitTarget)
            {
                return true;
            }

            switch (targeting.Mode)
            {
                case TargetingMode.Single:
                case TargetingMode.Chain:
                    return true;
                default:
                    return false;
            }
        }

        public bool TryResolveAutoCastTarget(
            SkillDefinition skill,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            out GameObject explicitTarget)
        {
            var sequencePhase = GetSequencePhaseForCast(skill, Time.time);
            return TryResolveAutoCastTarget(skill, hasAimPoint, aimPoint, aimDirection, sequencePhase, out explicitTarget);
        }

        private GameObject ResolveEvaluationExplicitTarget(
            SkillDefinition skill,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            int sequencePhase = -1)
        {
            var candidate = ShouldIgnoreOptionalExplicitTarget(skill) ? null : explicitTarget;
            if (candidate != null)
            {
                return candidate;
            }

            if (skill == null || skill.Targeting == null || skill.Targeting.RequireExplicitTarget || targetingSystem == null)
            {
                return candidate;
            }

            if (!ShouldResolveSoftEvaluationExplicitTarget(skill))
            {
                return candidate;
            }

            return TryResolveAutoCastTarget(skill, hasAimPoint, aimPoint, aimDirection, sequencePhase, out var resolved)
                ? resolved
                : null;
        }

        private static GameObject ResolveContextExplicitTarget(
            SkillDefinition skill,
            GameObject explicitTarget,
            List<CombatTarget> targets)
        {
            if (!ShouldIgnoreOptionalExplicitTarget(skill))
            {
                return explicitTarget;
            }

            if (targets == null || targets.Count == 0)
            {
                return explicitTarget;
            }

            return targets[0].GameObject;
        }

        private bool TryResolveAutoCastTarget(
            SkillDefinition skill,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            int sequencePhase,
            out GameObject explicitTarget)
        {
            explicitTarget = null;
            if (skill == null || skill.Targeting == null || targetingSystem == null || unitRoot == null)
            {
                return false;
            }

            var resolvedSequencePhase = sequencePhase > 0
                ? sequencePhase
                : GetSequencePhaseForCast(skill, Time.time);
            var sort = skill.Targeting.Sort;
            var origin = ResolveAutoTargetOrigin(skill.Targeting, hasAimPoint, aimPoint);
            var candidates = SimpleListPool<CombatTarget>.Get();
            var best = default(CombatTarget);
            var hasBest = false;
            var bestMetric = 0f;
            var randomCount = 0;

            try
            {
                targetingSystem.CollectAllCandidates(skill.Targeting, unitRoot, candidates, hasAimPoint, aimPoint, aimDirection);
                for (int i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (!candidate.IsValid || candidate.GameObject == null)
                    {
                        continue;
                    }

                    if (!IsAutoCastTargetCandidate(skill, candidate, hasAimPoint, aimPoint, aimDirection, resolvedSequencePhase))
                    {
                        continue;
                    }

                    if (sort == TargetSort.Random)
                    {
                        randomCount++;
                        if (!hasBest || UnityEngine.Random.Range(0, randomCount) == 0)
                        {
                            best = candidate;
                            hasBest = true;
                        }

                        continue;
                    }

                    if (sort == TargetSort.None)
                    {
                        explicitTarget = candidate.GameObject;
                        return true;
                    }

                    var metric = GetAutoTargetMetric(sort, origin, candidate);
                    if (!hasBest || metric < bestMetric)
                    {
                        best = candidate;
                        bestMetric = metric;
                        hasBest = true;
                    }
                }

                if (!hasBest)
                {
                    return false;
                }

                explicitTarget = best.GameObject;
                return explicitTarget != null;
            }
            finally
            {
                SimpleListPool<CombatTarget>.Release(candidates);
            }
        }

        private bool IsAutoCastTargetCandidate(
            SkillDefinition skill,
            CombatTarget candidate,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            int sequencePhase)
        {
            if (!PassesAlivePolicy(skill, candidate))
            {
                return false;
            }

            return EvaluateCastConstraints(skill, candidate.GameObject, hasAimPoint, aimPoint, aimDirection, sequencePhase, out _);
        }

        private static bool PassesAlivePolicy(SkillDefinition skill, CombatTarget candidate)
        {
            if (!candidate.IsValid)
            {
                return false;
            }

            var targeting = skill != null ? skill.Targeting : null;
            if (targeting == null || targeting.HitValidation < HitValidationPolicy.AliveOnly)
            {
                return true;
            }

            return candidate.Health == null || candidate.Health.IsAlive;
        }

        private Vector3 ResolveAutoTargetOrigin(TargetingDefinition targeting, bool hasAimPoint, Vector3 aimPoint)
        {
            var origin = unitRoot != null ? unitRoot.transform.position : transform.position;
            if (targeting != null && targeting.Origin == TargetingOrigin.TargetPoint && hasAimPoint)
            {
                origin = aimPoint;
            }

            return origin;
        }

        private static float GetAutoTargetMetric(TargetSort sort, Vector3 origin, CombatTarget candidate)
        {
            switch (sort)
            {
                case TargetSort.Farthest:
                    return -GetAutoTargetDistanceSqr(origin, candidate);
                case TargetSort.LowestHealth:
                    return GetAutoTargetHealthValue(candidate);
                case TargetSort.HighestHealth:
                    return -GetAutoTargetHealthValue(candidate);
                case TargetSort.Random:
                case TargetSort.None:
                case TargetSort.Closest:
                default:
                    return GetAutoTargetDistanceSqr(origin, candidate);
            }
        }

        private static float GetAutoTargetDistanceSqr(Vector3 origin, CombatTarget candidate)
        {
            if (candidate.Transform == null)
            {
                return float.MaxValue;
            }

            var offset = candidate.Transform.position - origin;
            return offset.sqrMagnitude;
        }

        private static float GetAutoTargetHealthValue(CombatTarget candidate)
        {
            return candidate.Health != null ? candidate.Health.Current : float.MaxValue;
        }

        private float ResolveModifiedResourceCost(SkillDefinition skill, SkillRuntimeContext context, CombatTarget primaryTarget)
        {
            if (skill == null)
            {
                return 0f;
            }

            var modifiedCost = ModifierResolver.ApplySkillModifiers(
                skill.ResourceCost,
                skill,
                context,
                primaryTarget,
                ModifierParameters.SkillResourceCost);
            return Mathf.Max(0f, modifiedCost);
        }

        private bool HasAmmo(SkillDefinition skill)
        {
            if (skill == null || !skill.SupportsAmmo || skill.AmmoConfig == null)
            {
                return true;
            }

            EnsureAmmoState(skill, true);
            if (!ammoStates.TryGetValue(skill, out var state))
            {
                return true;
            }

            return state.CurrentCharges > 0;
        }

        private bool ConsumeAmmo(SkillDefinition skill)
        {
            if (skill == null || !skill.SupportsAmmo || skill.AmmoConfig == null)
            {
                return true;
            }

            EnsureAmmoState(skill, true);
            if (!ammoStates.TryGetValue(skill, out var state))
            {
                return false;
            }

            if (state.CurrentCharges <= 0)
            {
                return false;
            }

            state.CurrentCharges--;
            var rechargeTime = skill.AmmoConfig.RechargeTime;
            if (state.CurrentCharges < skill.AmmoConfig.MaxCharges && rechargeTime > 0f && state.NextRechargeTime <= 0f)
            {
                state.NextRechargeTime = Time.time + rechargeTime;
            }

            ammoStates[skill] = state;
            return true;
        }

        private void UpdateAmmoRecharge(float now)
        {
            if (ammoStates.Count == 0)
            {
                return;
            }

            var keys = SimpleListPool<SkillDefinition>.Get();
            foreach (var pair in ammoStates)
            {
                keys.Add(pair.Key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                var skill = keys[i];
                if (skill == null || !skill.SupportsAmmo || skill.AmmoConfig == null)
                {
                    continue;
                }

                if (!ammoStates.TryGetValue(skill, out var state))
                {
                    continue;
                }

                if (state.CurrentCharges >= skill.AmmoConfig.MaxCharges)
                {
                    state.NextRechargeTime = 0f;
                    ammoStates[skill] = state;
                    continue;
                }

                var rechargeTime = skill.AmmoConfig.RechargeTime;
                if (rechargeTime <= 0f)
                {
                    state.CurrentCharges = skill.AmmoConfig.MaxCharges;
                    state.NextRechargeTime = 0f;
                    ammoStates[skill] = state;
                    continue;
                }

                if (state.NextRechargeTime <= 0f)
                {
                    state.NextRechargeTime = now + rechargeTime;
                    ammoStates[skill] = state;
                    continue;
                }

                while (state.CurrentCharges < skill.AmmoConfig.MaxCharges && now >= state.NextRechargeTime)
                {
                    state.CurrentCharges++;
                    if (state.CurrentCharges >= skill.AmmoConfig.MaxCharges)
                    {
                        state.NextRechargeTime = 0f;
                        break;
                    }

                    state.NextRechargeTime += rechargeTime;
                }

                ammoStates[skill] = state;
            }

            SimpleListPool<SkillDefinition>.Release(keys);
        }

        private bool TryGetRecastState(SkillDefinition skill, out RecastRuntimeState state)
        {
            state = default;
            if (skill == null || !skill.SupportsRecast)
            {
                return false;
            }

            if (!recastStates.TryGetValue(skill, out var runtime) || !runtime.Active)
            {
                return false;
            }

            if (runtime.ExpireTime > 0f && Time.time > runtime.ExpireTime)
            {
                runtime = default;
                recastStates[skill] = runtime;
                return false;
            }

            if (runtime.RemainingRecasts <= 0)
            {
                runtime = default;
                recastStates[skill] = runtime;
                return false;
            }

            state = runtime;
            return true;
        }

        private bool ResolveRecastTarget(
            SkillDefinition skill,
            RecastRuntimeState state,
            GameObject requestTarget,
            out GameObject resolvedTarget)
        {
            resolvedTarget = requestTarget;
            if (skill == null || !skill.SupportsRecast || skill.RecastConfig == null)
            {
                return true;
            }

            switch (skill.RecastConfig.TargetPolicy)
            {
                case RecastTargetPolicy.RequireOriginal:
                    if (state.LockedTarget == null)
                    {
                        return false;
                    }

                    resolvedTarget = state.LockedTarget;
                    return IsExplicitTargetAlive(resolvedTarget);
                case RecastTargetPolicy.KeepOriginalIfPossible:
                    if (state.LockedTarget != null && IsExplicitTargetAlive(state.LockedTarget))
                    {
                        resolvedTarget = state.LockedTarget;
                    }

                    return true;
                case RecastTargetPolicy.AnyValid:
                default:
                    return true;
            }
        }

        private void UpdateRecastAfterSuccessfulCast(
            SkillDefinition skill,
            bool wasRecastCast,
            RecastRuntimeState activeState,
            float now,
            GameObject explicitTarget,
            bool hasAimPoint,
            Vector3 aimPoint,
            Vector3 aimDirection,
            float cooldownDuration)
        {
            if (skill == null || !skill.SupportsRecast || skill.RecastConfig == null)
            {
                return;
            }

            var config = skill.RecastConfig;
            if (!wasRecastCast)
            {
                var next = new RecastRuntimeState(
                    true,
                    config.MaxRecasts,
                    now + config.RecastWindow,
                    cooldownDuration,
                    explicitTarget,
                    hasAimPoint,
                    aimPoint,
                    aimDirection);
                recastStates[skill] = next;
                return;
            }

            var remaining = Mathf.Max(0, activeState.RemainingRecasts - 1);
            if (remaining > 0)
            {
                activeState.Active = true;
                activeState.RemainingRecasts = remaining;
                activeState.ExpireTime = now + config.RecastWindow;
                recastStates[skill] = activeState;
                return;
            }

            recastStates[skill] = default;
            var cooldownToApply = Mathf.Max(activeState.CooldownOnFinish, cooldownDuration);
            if (config.DelayCooldownUntilRecastEnds && cooldown != null && cooldownToApply > 0f)
            {
                cooldown.StartCooldown(skill, cooldownToApply);
            }
        }

        private void UpdateRecastExpiry(float now)
        {
            if (recastStates.Count == 0)
            {
                return;
            }

            var keys = SimpleListPool<SkillDefinition>.Get();
            foreach (var pair in recastStates)
            {
                keys.Add(pair.Key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                var skill = keys[i];
                if (!recastStates.TryGetValue(skill, out var state))
                {
                    continue;
                }

                if (!state.Active)
                {
                    continue;
                }

                if (state.ExpireTime > 0f && now > state.ExpireTime)
                {
                    if (skill != null
                        && skill.SupportsRecast
                        && skill.RecastConfig != null
                        && skill.RecastConfig.DelayCooldownUntilRecastEnds
                        && cooldown != null
                        && state.CooldownOnFinish > 0f)
                    {
                        cooldown.StartCooldown(skill, state.CooldownOnFinish);
                    }

                    recastStates[skill] = default;
                }
            }

            SimpleListPool<SkillDefinition>.Release(keys);
        }

        private int GetSequencePhaseForCast(SkillDefinition skill, float now)
        {
            if (skill == null || !skill.SupportsSequence || skill.SequenceConfig == null)
            {
                return 1;
            }

            EnsureSequenceState(skill);
            if (!sequenceStates.TryGetValue(skill, out var state))
            {
                return 1;
            }

            if (!state.Active)
            {
                return 1;
            }

            if (state.ExpireTime > 0f && now > state.ExpireTime)
            {
                sequenceStates[skill] = default;
                return 1;
            }

            return Mathf.Clamp(state.CurrentPhase, 1, skill.SequenceConfig.MaxPhases);
        }

        private void UpdateSequenceAfterSuccessfulCast(SkillDefinition skill, int castPhase, float now)
        {
            if (skill == null || !skill.SupportsSequence || skill.SequenceConfig == null)
            {
                return;
            }

            var config = skill.SequenceConfig;
            var maxPhases = config.MaxPhases;
            if (maxPhases <= 1 || config.ResetWindow <= 0f)
            {
                sequenceStates[skill] = default;
                return;
            }

            var safeCastPhase = Mathf.Clamp(castPhase, 1, maxPhases);

            // 命中推进模式：普通段不在施放成功时进阶，只在命中时进阶；终结段施放后再按溢出策略回退。
            if (config.AdvanceOnHit)
            {
                if (safeCastPhase < maxPhases)
                {
                    if (sequenceStates.TryGetValue(skill, out var holdState) && holdState.Active)
                    {
                        holdState.ExpireTime = now + config.ResetWindow;
                        holdState.LastAdvancedCastId = 0UL;
                        sequenceStates[skill] = holdState;
                    }
                    else
                    {
                        sequenceStates[skill] = default;
                    }

                    return;
                }
            }

            var nextState = new SequenceRuntimeState
            {
                Active = true,
                CurrentPhase = safeCastPhase,
                ExpireTime = now + config.ResetWindow,
                LastAdvancedCastId = 0UL
            };

            if (safeCastPhase < maxPhases)
            {
                nextState.CurrentPhase = safeCastPhase + 1;
                sequenceStates[skill] = nextState;
                return;
            }

            switch (config.OverflowPolicy)
            {
                case SkillSequenceOverflowPolicy.HoldAtMax:
                    nextState.CurrentPhase = maxPhases;
                    nextState.Active = true;
                    sequenceStates[skill] = nextState;
                    break;
                case SkillSequenceOverflowPolicy.ResetAfterMax:
                    sequenceStates[skill] = default;
                    break;
                case SkillSequenceOverflowPolicy.LoopToStart:
                default:
                    nextState.CurrentPhase = 1;
                    nextState.Active = true;
                    sequenceStates[skill] = nextState;
                    break;
            }
        }

        private void UpdateSequenceAfterSuccessfulHit(SkillRuntimeContext context, float now)
        {
            var skill = context.Skill;
            if (skill == null || !skill.SupportsSequence || skill.SequenceConfig == null)
            {
                return;
            }

            var config = skill.SequenceConfig;
            if (!config.AdvanceOnHit)
            {
                return;
            }

            var maxPhases = config.MaxPhases;
            if (maxPhases <= 1 || config.ResetWindow <= 0f)
            {
                return;
            }

            // 终结段命中不再累计下一轮层数，避免 Q3 命中后立刻变 Q2。
            var safeCastPhase = Mathf.Clamp(context.SequencePhase, 1, maxPhases);
            if (safeCastPhase >= maxPhases)
            {
                return;
            }

            EnsureSequenceState(skill);
            if (!sequenceStates.TryGetValue(skill, out var state) || !state.Active)
            {
                state = new SequenceRuntimeState
                {
                    Active = true,
                    CurrentPhase = safeCastPhase,
                    ExpireTime = now + config.ResetWindow,
                    LastAdvancedCastId = 0UL
                };
            }

            if (state.ExpireTime > 0f && now > state.ExpireTime)
            {
                state = new SequenceRuntimeState
                {
                    Active = true,
                    CurrentPhase = safeCastPhase,
                    ExpireTime = now + config.ResetWindow,
                    LastAdvancedCastId = 0UL
                };
            }

            if (context.CastId != 0UL && state.LastAdvancedCastId == context.CastId)
            {
                return;
            }

            state.CurrentPhase = Mathf.Clamp(state.CurrentPhase + 1, 1, maxPhases);
            state.ExpireTime = now + config.ResetWindow;
            state.LastAdvancedCastId = context.CastId;
            state.Active = true;
            sequenceStates[skill] = state;
        }

        private void ResetSequencesMarkedOnOtherCast(SkillDefinition castedSkill)
        {
            if (sequenceStates.Count == 0)
            {
                return;
            }

            var keys = SimpleListPool<SkillDefinition>.Get();
            foreach (var pair in sequenceStates)
            {
                keys.Add(pair.Key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                var skill = keys[i];
                if (skill == null || skill == castedSkill || !skill.SupportsSequence || skill.SequenceConfig == null)
                {
                    continue;
                }

                if (!skill.SequenceConfig.ResetOnOtherSkillCast)
                {
                    continue;
                }

                if (!sequenceStates.TryGetValue(skill, out var state) || !state.Active)
                {
                    continue;
                }

                sequenceStates[skill] = default;
            }

            SimpleListPool<SkillDefinition>.Release(keys);
        }

        private void UpdateSequenceExpiry(float now)
        {
            if (sequenceStates.Count == 0)
            {
                return;
            }

            var keys = SimpleListPool<SkillDefinition>.Get();
            foreach (var pair in sequenceStates)
            {
                keys.Add(pair.Key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                var skill = keys[i];
                if (skill == null || !sequenceStates.TryGetValue(skill, out var state) || !state.Active)
                {
                    continue;
                }

                if (state.ExpireTime > 0f && now > state.ExpireTime)
                {
                    sequenceStates[skill] = default;
                }
            }

            SimpleListPool<SkillDefinition>.Release(keys);
        }

        /// <summary>
        /// 检查当前资源是否足够释放技能。
        /// </summary>
        private bool HasResource(SkillDefinition skill, float cost)
        {
            // 无资源组件时，只有零消耗技能可以释放
            if (resource == null)
            {
                return cost <= 0f;
            }

            // 资源类型必须匹配
            if (resource.ResourceType != skill.ResourceType)
            {
                return false;
            }

            return resource.Current >= cost;
        }

        /// <summary>
        /// 扣除技能所需资源。
        /// </summary>
        /// <returns>若成功扣除则返回 true</returns>
        private bool SpendResource(SkillDefinition skill, float cost)
        {
            if (resource == null)
            {
                return cost <= 0f;
            }

            if (resource.ResourceType != skill.ResourceType)
            {
                return false;
            }

            return resource.Spend(cost);
        }

        private ulong GenerateCastId()
        {
            if (nextCastId == 0UL)
            {
                nextCastId = 1UL;
            }

            return nextCastId++;
        }

        /// <summary>
        /// 创建技能运行时上下文。
        /// </summary>
        private SkillRuntimeContext CreateContext(
            SkillDefinition skill,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default,
            GameObject explicitTarget = null,
            float chargeDuration = 0f,
            float chargeRatio = 0f,
            float chargeMultiplier = 1f,
            ulong castId = 0UL,
            int stepIndex = -1,
            int sequencePhase = 1)
        {
            return new SkillRuntimeContext(
                this,
                unitRoot,
                skill,
                eventHub,
                targetingSystem,
                effectExecutor,
                hasAimPoint,
                aimPoint,
                aimDirection,
                explicitTarget,
                chargeDuration,
                chargeRatio,
                chargeMultiplier,
                castId,
                stepIndex,
                sequencePhase);
        }

        /// <summary>
        /// 派发技能施法开始事件。
        /// </summary>
        private void RaiseSkillCastStarted(SkillRuntimeContext context, float castTime, float channelTime, bool isChannel)
        {
            var evt = new SkillCastEvent(context.CasterUnit, context.Skill, castTime, channelTime, isChannel);
            SkillCastStarted?.Invoke(evt);
            eventHub?.RaiseSkillCastStarted(evt);
        }

        /// <summary>
        /// 派发技能施法完成事件。
        /// </summary>
        private void RaiseSkillCastCompleted(SkillRuntimeContext context, float castTime, float channelTime, bool isChannel)
        {
            var evt = new SkillCastEvent(context.CasterUnit, context.Skill, castTime, channelTime, isChannel);
            SkillCastCompleted?.Invoke(evt);
            eventHub?.RaiseSkillCastCompleted(evt);
        }

        private void RaiseSkillCastInterrupted(SkillRuntimeContext context, float castTime, float channelTime, bool isChannel)
        {
            var evt = new SkillCastEvent(context.CasterUnit, context.Skill, castTime, channelTime, isChannel);
            SkillCastInterrupted?.Invoke(evt);
            eventHub?.RaiseSkillCastInterrupted(evt);
        }

        private void RaiseSkillStepExecuted(
            SkillRuntimeContext context,
            SkillStep step,
            SkillStepTrigger trigger,
            CombatTarget primaryTarget,
            int stepIndex)
        {
            var hub = context.EventHub != null ? context.EventHub : eventHub;
            if (hub == null)
            {
                return;
            }

            var evt = new SkillStepExecutedEvent(
                context.CasterUnit,
                context.Skill,
                step,
                trigger,
                primaryTarget,
                context.ExplicitTarget,
                context.HasAimPoint,
                context.AimPoint,
                context.AimDirection,
                context.CastId,
                stepIndex);
            hub.RaiseSkillStepExecuted(evt);
        }

        private void ClearPendingSteps(SkillDefinition skill)
        {
            if (skill == null || pendingSteps.Count == 0)
            {
                return;
            }

            for (int i = pendingSteps.Count - 1; i >= 0; i--)
            {
                var pending = pendingSteps[i];
                if (pending.Context.Caster != this || pending.Context.Skill != skill)
                {
                    continue;
                }

                if (pending.Trigger == SkillStepTrigger.OnCastStart
                    || pending.Trigger == SkillStepTrigger.OnCastComplete
                    || pending.Trigger == SkillStepTrigger.OnChannelTick)
                {
                    ReleaseHandle(pending.Targets);
                    pendingSteps.RemoveAt(i);
                }
            }
        }

        private void ClearCastState()
        {
            isCasting = false;
            castStartTime = 0f;
            castEndTime = 0f;
            currentSkill = null;
            currentCastTime = 0f;
            currentChannelTime = 0f;
            currentIsChannel = false;
            currentPostCastTime = 0f;
            currentContext = default;
            currentHasAimPoint = false;
            currentAimPoint = default;
            currentAimDirection = default;
            currentChargeDuration = 0f;
            currentChargeRatio = 0f;
            currentChargeMultiplier = 1f;
            currentSequencePhase = 1;
        }

        /// <summary>
        /// 检查指定技能是否为普攻技能。
        /// </summary>
        /// <param name="skill">要检查的技能</param>
        /// <returns>若为普攻技能则返回 true</returns>
        public bool IsBasicAttackSkill(SkillDefinition skill)
        {
            if (skill == null)
            {
                return false;
            }

            var basic = BasicAttack;
            return basic != null && basic == skill;
        }

        private float ApplyAbilityHaste(float cooldownDuration)
        {
            if (cooldownDuration <= 0f || stats == null)
            {
                return cooldownDuration;
            }

            var haste = Mathf.Max(0f, stats.GetValueById(CombatStatIds.AbilityHaste, 0f));
            if (haste <= 0f)
            {
                return cooldownDuration;
            }

            return cooldownDuration * 100f / (100f + haste);
        }

        /// <summary>
        /// 最小攻击冷却时间（秒），防止极端攻速导致冷却趋近于 0。
        /// </summary>
        private const float MinAttackCooldown = 0.1f;

        private float ApplyAttackSpeed(float cooldownDuration)
        {
            if (cooldownDuration <= 0f || stats == null)
            {
                return cooldownDuration;
            }

            var bonusAttackSpeed = Mathf.Max(0f, stats.GetValueById(CombatStatIds.AttackSpeed, 0f));
            if (bonusAttackSpeed <= 0f)
            {
                return cooldownDuration;
            }

            var result = cooldownDuration / (1f + bonusAttackSpeed);
            return Mathf.Max(result, MinAttackCooldown);
        }

        #region Target Handle Pool
        // 目标列表句柄池，用于复用 TargetListHandle 避免 GC
        private static readonly Stack<TargetListHandle> TargetHandlePool = new Stack<TargetListHandle>(16);

        /// <summary>
        /// 从池中获取目标列表句柄。
        /// </summary>
        private static TargetListHandle GetTargetHandle(List<CombatTarget> targets)
        {
            var handle = TargetHandlePool.Count > 0 ? TargetHandlePool.Pop() : new TargetListHandle();
            handle.Targets = targets;
            handle.RefCount = 0;
            return handle;
        }

        /// <summary>
        /// 减少句柄引用计数，归零时释放。
        /// </summary>
        private static void ReleaseHandle(TargetListHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            handle.RefCount--;
            if (handle.RefCount <= 0)
            {
                ReleaseHandleInternal(handle);
            }
        }

        /// <summary>
        /// 释放未被使用的句柄（引用计数为 0）。
        /// </summary>
        private static void ReleaseHandleUnused(TargetListHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            ReleaseHandleInternal(handle);
        }

        private static int GetSpellShieldCharges(CombatTarget target)
        {
            if (target.State == null || !target.State.HasFlag(CombatStateFlags.SpellShielded))
            {
                return 0;
            }

            return target.State.SpellShieldCharges;
        }

        private static bool HasSpellShieldConsumed(CombatTarget target, int chargesBefore)
        {
            if (chargesBefore <= 0 || target.State == null)
            {
                return false;
            }

            return target.State.SpellShieldCharges < chargesBefore;
        }

        private void ClearAllPendingSteps()
        {
            if (pendingSteps.Count <= 0)
            {
                return;
            }

            for (var i = pendingSteps.Count - 1; i >= 0; i--)
            {
                ReleaseHandle(pendingSteps[i].Targets);
            }

            pendingSteps.Clear();
        }

        /// <summary>
        /// 内部释放逻辑：归还目标列表和句柄到池中。
        /// </summary>
        private static void ReleaseHandleInternal(TargetListHandle handle)
        {
            SimpleListPool<CombatTarget>.Release(handle.Targets);
            handle.Targets = null;
            handle.RefCount = 0;
            TargetHandlePool.Push(handle);
        }
        #endregion

        #region Internal Types
        private struct AmmoRuntimeState
        {
            public int CurrentCharges;
            public float NextRechargeTime;

            public AmmoRuntimeState(int currentCharges, float nextRechargeTime)
            {
                CurrentCharges = Mathf.Max(0, currentCharges);
                NextRechargeTime = nextRechargeTime;
            }
        }

        private struct RecastRuntimeState
        {
            public bool Active;
            public int RemainingRecasts;
            public float ExpireTime;
            public float CooldownOnFinish;
            public GameObject LockedTarget;
            public bool HasAimPoint;
            public Vector3 AimPoint;
            public Vector3 AimDirection;

            public RecastRuntimeState(
                bool active,
                int remainingRecasts,
                float expireTime,
                float cooldownOnFinish,
                GameObject lockedTarget,
                bool hasAimPoint,
                Vector3 aimPoint,
                Vector3 aimDirection)
            {
                Active = active;
                RemainingRecasts = Mathf.Max(0, remainingRecasts);
                ExpireTime = expireTime;
                CooldownOnFinish = Mathf.Max(0f, cooldownOnFinish);
                LockedTarget = lockedTarget;
                HasAimPoint = hasAimPoint;
                AimPoint = aimPoint;
                AimDirection = aimDirection;
            }
        }

        private struct SequenceRuntimeState
        {
            public bool Active;
            public int CurrentPhase;
            public float ExpireTime;
            public ulong LastAdvancedCastId;
        }

        /// <summary>
        /// 排队的技能释放请求。
        /// </summary>
        private struct QueuedCast
        {
            public SkillDefinition Skill;
            public GameObject ExplicitTarget;
            public bool HasAimPoint;
            public Vector3 AimPoint;
            public Vector3 AimDirection;
            public float ChargeDurationSeconds;

            public QueuedCast(
                SkillDefinition skill,
                GameObject explicitTarget,
                bool hasAimPoint,
                Vector3 aimPoint,
                Vector3 aimDirection,
                float chargeDurationSeconds)
            {
                Skill = skill;
                ExplicitTarget = explicitTarget;
                HasAimPoint = hasAimPoint;
                AimPoint = aimPoint;
                AimDirection = aimDirection;
                ChargeDurationSeconds = Mathf.Max(0f, chargeDurationSeconds);
            }
        }

        /// <summary>
        /// 待执行的技能步骤数据。
        /// </summary>
        private struct PendingStep
        {
            /// <summary>技能步骤配置</summary>
            public SkillStep Step;
            /// <summary>触发时机</summary>
            public SkillStepTrigger Trigger;
            /// <summary>步骤索引（Skill.Steps 中的位置）</summary>
            public int StepIndex;
            /// <summary>执行时间戳</summary>
            public float ExecuteAt;
            /// <summary>目标列表句柄</summary>
            public TargetListHandle Targets;
            /// <summary>技能运行时上下文</summary>
            public SkillRuntimeContext Context;

            public PendingStep(
                SkillStep step,
                SkillStepTrigger trigger,
                int stepIndex,
                float executeAt,
                TargetListHandle targets,
                SkillRuntimeContext context)
            {
                Step = step;
                Trigger = trigger;
                StepIndex = stepIndex;
                ExecuteAt = executeAt;
                Targets = targets;
                Context = context;
            }
        }

        /// <summary>
        /// 目标列表句柄，用于引用计数管理目标列表的生命周期。
        /// </summary>
        /// <remarks>
        /// 多个 PendingStep 可能共享同一个目标列表，使用引用计数确保
        /// 在最后一个步骤执行完后才释放列表。
        /// </remarks>
        private sealed class TargetListHandle
        {
            /// <summary>目标列表</summary>
            public List<CombatTarget> Targets;
            /// <summary>引用计数</summary>
            public int RefCount;
        }
        #endregion
    }
}
