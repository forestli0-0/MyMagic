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
        }

        /// <summary>
        /// 每帧更新：处理延迟执行的技能步骤和施法状态。
        /// </summary>
        private void Update()
        {
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
                        : CreateContext(currentSkill, currentHasAimPoint, currentAimPoint, currentAimDirection);
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

        /// <summary>
        /// 检查当前技能列表中是否已包含指定技能。
        /// </summary>
        public bool HasSkill(SkillDefinition skill)
        {
            return skill != null && runtimeSkills.Contains(skill);
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
            return TryCast(skill, explicitTarget, false, default, default);
        }

        /// <summary>
        /// 尝试释放指定技能。
        /// </summary>
        /// <param name="skill">要释放的技能</param>
        /// <param name="explicitTarget">显式指定的目标（可选）</param>
        /// <param name="hasAimPoint">是否有瞄准点</param>
        /// <param name="aimPoint">瞄准点</param>
        /// <param name="aimDirection">瞄准方向</param>
        /// <returns>若成功开始施法则返回 true</returns>
        public bool TryCast(SkillDefinition skill, GameObject explicitTarget, bool hasAimPoint, Vector3 aimPoint, Vector3 aimDirection)
        {
            if (skill == null)
            {
                return false;
            }

            if (TryGetTauntSource(out var taunter))
            {
                if (skill != BasicAttack)
                {
                    return false;
                }

                if (taunter == null)
                {
                    return false;
                }

                explicitTarget = taunter.gameObject;
                hasAimPoint = false;
                aimPoint = default;
                aimDirection = default;

                if (!IsTargetInRange(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection))
                {
                    return false;
                }
            }

            if (!CanCast(skill))
            {
                // 施法/后摇/GCD期间允许进入输入缓冲
                if (IsLockedOut() && TryQueueCast(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection))
                {
                    return false;
                }

                return false;
            }

            if (skill != null && skill.Targeting != null && skill.Targeting.Origin == TargetingOrigin.TargetPoint && !hasAimPoint)
            {
                if (explicitTarget != null)
                {
                    aimPoint = explicitTarget.transform.position;
                    hasAimPoint = true;
                }
            }

            if (aimDirection.sqrMagnitude <= 0.0001f && explicitTarget != null && unitRoot != null)
            {
                var dir = explicitTarget.transform.position - unitRoot.transform.position;
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

            // 收集目标
            var targets = SimpleListPool<CombatTarget>.Get();
            if (!CollectTargets(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection, targets))
            {
                SimpleListPool<CombatTarget>.Release(targets);
                return false;
            }

            // 创建技能上下文
            var context = CreateContext(skill, hasAimPoint, aimPoint, aimDirection, explicitTarget);
            var primaryTarget = targets.Count > 0 ? targets[0] : default;
            var resourceCost = Mathf.Max(0f, ModifierResolver.ApplySkillModifiers(skill.ResourceCost, skill, context, primaryTarget, ModifierParameters.SkillResourceCost));
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
                return false;
            }

            // 开始冷却
            if (cooldown != null && cooldownDuration > 0f)
            {
                cooldown.StartCooldown(skill, cooldownDuration);
            }

            var now = Time.time;

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
                : CreateContext(currentSkill, currentHasAimPoint, currentAimPoint, currentAimDirection);
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
                : CreateContext(currentSkill, currentHasAimPoint, currentAimPoint, currentAimDirection);

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
            // 技能不能为空
            if (skill == null)
            {
                return false;
            }

            var isTauntBasic = IsTauntBasicAttack(skill);
            if (TryGetTauntSource(out _) && !isTauntBasic)
            {
                return false;
            }

            // 施法/后摇/GCD 期间不可释放
            if (IsLockedOut())
            {
                return false;
            }

            // 控制状态限制（眩晕/沉默等）
            if (buffController != null && buffController.HasControlFlag(ControlFlag.BlocksCasting))
            {
                if (!isTauntBasic)
                {
                    return false;
                }
            }

            if (skill == BasicAttack && buffController != null && buffController.HasControlFlag(ControlFlag.BlocksBasicAttack))
            {
                if (!isTauntBasic)
                {
                    return false;
                }
            }

            // 必须存活
            if (health != null && !health.IsAlive)
            {
                return false;
            }

            // 检查冷却
            if (cooldown != null && !cooldown.IsReady(skill))
            {
                return false;
            }

            // 检查资源
            return HasResource(skill, skill.ResourceCost);
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

            if (!HasResource(basicAttack, basicAttack.ResourceCost))
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

        private bool IsTargetInRange(SkillDefinition skill, GameObject target, bool hasAimPoint, Vector3 aimPoint, Vector3 aimDirection)
        {
            if (skill == null || target == null || targetingSystem == null || unitRoot == null)
            {
                return true;
            }

            var targeting = skill.Targeting;
            if (targeting == null || targeting.Range <= 0f)
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

        private bool CanCastIgnoringLockouts(SkillDefinition skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (health != null && !health.IsAlive)
            {
                return false;
            }

            if (cooldown != null && !cooldown.IsReady(skill))
            {
                return false;
            }

            return HasResource(skill, skill.ResourceCost);
        }

        private bool TryQueueCast(SkillDefinition skill, GameObject explicitTarget, bool hasAimPoint, Vector3 aimPoint, Vector3 aimDirection)
        {
            if (skill == null || !CanCastIgnoringLockouts(skill))
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

            queuedCast = new QueuedCast(skill, explicitTarget, hasAimPoint, aimPoint, aimDirection);
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

            TryCast(request.Skill, request.ExplicitTarget, request.HasAimPoint, request.AimPoint, request.AimDirection);
        }

        /// <summary>
        /// 内部方法：当技能直接命中目标时触发。
        /// </summary>
        internal void NotifyHit(SkillRuntimeContext context, CombatTarget target)
        {
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

            // 使用目标系统收集目标
            targetingSystem.CollectTargets(skill.Targeting, unitRoot, explicitTarget, targets, hasAimPoint, aimPoint, aimDirection);
            if (targets.Count > 0)
            {
                return true;
            }

            return skill.Targeting.AllowEmpty;
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
                pendingSteps.Add(new PendingStep(step, trigger, executeAt, targets, context));
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
            if (effectExecutor == null)
            {
                return;
            }

            var step = pending.Step;
            if (step == null)
            {
                return;
            }

            var effects = step.effects;
            if (effects == null || effects.Count == 0)
            {
                return;
            }

            var context = pending.Context;
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

            if (targets == null || targets.Count == 0)
            {
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

                // 对该目标执行所有效果
                for (int j = 0; j < effects.Count; j++)
                {
                    effectExecutor.ExecuteEffect(effects[j], context, target, pending.Trigger);
                }
            }

            if (!useSnapshot)
            {
                SimpleListPool<CombatTarget>.Release(targets);
            }
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

        /// <summary>
        /// 创建技能运行时上下文。
        /// </summary>
        private SkillRuntimeContext CreateContext(
            SkillDefinition skill,
            bool hasAimPoint = false,
            Vector3 aimPoint = default,
            Vector3 aimDirection = default,
            GameObject explicitTarget = null)
        {
            return new SkillRuntimeContext(this, unitRoot, skill, eventHub, targetingSystem, effectExecutor, hasAimPoint, aimPoint, aimDirection, explicitTarget);
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

            public QueuedCast(SkillDefinition skill, GameObject explicitTarget, bool hasAimPoint, Vector3 aimPoint, Vector3 aimDirection)
            {
                Skill = skill;
                ExplicitTarget = explicitTarget;
                HasAimPoint = hasAimPoint;
                AimPoint = aimPoint;
                AimDirection = aimDirection;
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
            /// <summary>执行时间戳</summary>
            public float ExecuteAt;
            /// <summary>目标列表句柄</summary>
            public TargetListHandle Targets;
            /// <summary>技能运行时上下文</summary>
            public SkillRuntimeContext Context;

            public PendingStep(SkillStep step, SkillStepTrigger trigger, float executeAt, TargetListHandle targets, SkillRuntimeContext context)
            {
                Step = step;
                Trigger = trigger;
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
