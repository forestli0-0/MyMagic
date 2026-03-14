using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Input;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 玩家技能指示器驱动。
    /// 处理玩家的技能瞄准输入，在按键按下时显示瞄准指示器，松开时释放技能。
    /// </summary>
    public class PlayerSkillIndicatorDriver : MonoBehaviour
    {
        #region 序列化字段

        [Header("References")]
        [Tooltip("技能使用组件引用")]
        [SerializeField] private SkillUserComponent skillUser;
        [Tooltip("技能指示器组件引用")]
        [SerializeField] private SkillIndicator indicator;
        [Tooltip("用于射线检测的摄像机")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("目标选择系统引用，用于获取当前目标")]
        [SerializeField] private TargetingSystem targetingSystem;
        [Tooltip("单位根组件引用")]
        [SerializeField] private UnitRoot unitRoot;
        [Tooltip("战斗事件中心")]
        [SerializeField] private CombatEventHub eventHub;

        [Header("Aiming")]
        [Tooltip("释放技能时是否旋转角色朝向瞄准方向")]
        [SerializeField] private bool rotateCasterToAim = true;
        [Tooltip("是否使用地面层级遮罩进行射线检测")]
        [SerializeField] private bool useGroundMask;
        [Tooltip("地面层级遮罩，用于射线检测")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("射线检测最大距离")]
        [SerializeField] private float raycastDistance = 200f;

        [Header("Input")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private bool autoFindInputReader = true;
        [Tooltip("是否启用 7~12 槽位的键盘补充热键（用于扩展技能栏测试）")]
        [SerializeField] private bool enableOverflowSkillHotkeys = true;
        [Tooltip("补充热键，依次对应第 7、8、9... 个非普攻技能槽")]
        [SerializeField] private Key[] overflowSkillHotkeys =
        {
            Key.Digit7,
            Key.Digit8,
            Key.Digit9,
            Key.Digit0,
            Key.Minus,
            Key.Equals
        };

        [Header("HUD")]
        [Tooltip("技能栏 UI（用于显示蓄力进度）")]
        [SerializeField] private SkillBarUI skillBarUI;
        [SerializeField] private bool autoFindSkillBarUI = true;

        #endregion

        #region 私有字段

        /// <summary>当前正在瞄准的技能</summary>
        private SkillDefinition activeSkill;
        /// <summary>当前激活的技能槽位</summary>
        private int activeSlot = -1;
        /// <summary>当前技能按下时间（用于蓄力）</summary>
        private float activeSkillPressedAt;
        /// <summary>上一帧的瞄准方向</summary>
        private Vector3 lastAimDirection = Vector3.forward;
        /// <summary>上一帧的瞄准点</summary>
        private Vector3 lastAimPoint;
        /// <summary>是否存在有效瞄准点</summary>
        private bool hasAimPoint;

        /// <summary>目标列表缓存（避免GC）</summary>
        private readonly List<CombatTarget> cachedTargets = new List<CombatTarget>(8);
        /// <summary>非普攻技能缓存（用于数字键映射）</summary>
        private readonly List<SkillDefinition> nonBasicSkillBuffer = new List<SkillDefinition>(8);
        /// <summary>鼠标命中检测缓存（避免GC）</summary>
        private readonly RaycastHit[] hoverHitBuffer = new RaycastHit[32];

        /// <summary>当前高亮的目标</summary>
        private Transform currentTarget;
        private const int PrimarySkillHotkeyCount = 6;

        #endregion

        #region Unity 生命周期

        /// <summary>
        /// 编辑器重置时自动获取组件引用。
        /// </summary>
        private void Reset()
        {
            skillUser = GetComponent<SkillUserComponent>();
            indicator = GetComponentInChildren<SkillIndicator>();
            targetingSystem = FindFirstObjectByType<TargetingSystem>();
            unitRoot = GetComponent<UnitRoot>();
        }

        /// <summary>
        /// 初始化：获取摄像机引用并设置指示器锚点。
        /// </summary>
        private void Awake()
        {
            // 如果没有手动指定摄像机，则使用主摄像机
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (eventHub == null && unitRoot != null)
            {
                eventHub = unitRoot.EventHub;
            }

            // 设置指示器的锚点为当前物体
            if (indicator != null)
            {
                indicator.SetAnchor(transform);
            }

            ResolveInputReader();
            ResolveTargetingSystem();
            ResolveSkillBarUI();
        }

        /// <summary>
        /// 禁用时隐藏指示器并重置状态。
        /// </summary>
        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.SkillStarted -= HandleSkillStarted;
                inputReader.SkillCanceled -= HandleSkillCanceled;
                inputReader.CancelPerformed -= HandleCancelPerformed;
            }

            if (indicator != null)
            {
                indicator.Hide();
            }

            activeSkill = null;
            activeSlot = -1;
            activeSkillPressedAt = 0f;
            hasAimPoint = false;
            ClearSkillChargeVisual();
        }

        private void OnEnable()
        {
            if (eventHub == null && unitRoot != null)
            {
                eventHub = unitRoot.EventHub;
            }

            ResolveTargetingSystem();
            ResolveInputReader();
            ResolveSkillBarUI();
            if (inputReader != null)
            {
                inputReader.SkillStarted += HandleSkillStarted;
                inputReader.SkillCanceled += HandleSkillCanceled;
                inputReader.CancelPerformed += HandleCancelPerformed;
            }
        }

        /// <summary>
        /// 每帧更新：处理技能瞄准输入。
        /// </summary>
        private void Update()
        {
            if (!UIRoot.IsGameplayInputAllowed())
            {
                return;
            }

            PollOverflowSkillHotkeys();

            // 必要组件检查
            if (skillUser == null || indicator == null)
            {
                return;
            }

            if (activeSkill == null)
            {
                return;
            }

            // 持续更新瞄准方向
            UpdateAimDirection();

            // 更新目标高亮
            UpdateTargetHighlight();

            // 更新技能槽蓄力进度
            UpdateChargeVisual();
        }

        #endregion

        #region 私有方法

        private void ResolveInputReader()
        {
            if (!autoFindInputReader || inputReader != null)
            {
                return;
            }

            inputReader = FindFirstObjectByType<InputReader>();
        }

        private void ResolveTargetingSystem()
        {
            if (targetingSystem != null)
            {
                return;
            }

            targetingSystem = FindFirstObjectByType<TargetingSystem>();
        }

        private void ResolveSkillBarUI()
        {
            if (skillBarUI != null || !autoFindSkillBarUI)
            {
                return;
            }

            if (UIRoot.Instance != null && UIRoot.Instance.HudCanvas != null)
            {
                skillBarUI = UIRoot.Instance.HudCanvas.GetComponentInChildren<SkillBarUI>(true);
            }

            if (skillBarUI == null)
            {
                skillBarUI = FindFirstObjectByType<SkillBarUI>(FindObjectsInactive.Include);
            }
        }

        private void ClearSkillChargeVisual()
        {
            if (skillBarUI == null)
            {
                ResolveSkillBarUI();
            }

            if (skillBarUI != null)
            {
                skillBarUI.ClearSkillCharge();
            }
        }

        private void PollOverflowSkillHotkeys()
        {
            if (!enableOverflowSkillHotkeys || overflowSkillHotkeys == null || overflowSkillHotkeys.Length == 0)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            for (var i = 0; i < overflowSkillHotkeys.Length; i++)
            {
                var key = overflowSkillHotkeys[i];
                if (key == Key.None)
                {
                    continue;
                }

                var keyControl = keyboard[key];
                if (keyControl == null)
                {
                    continue;
                }

                var slotIndex = PrimarySkillHotkeyCount + i;
                if (keyControl.wasPressedThisFrame)
                {
                    HandleSkillStarted(slotIndex);
                }

                if (keyControl.wasReleasedThisFrame)
                {
                    HandleSkillCanceled(slotIndex);
                }
            }
        }

        private void HandleSkillStarted(int slotIndex)
        {
            if (!UIRoot.IsGameplayInputAllowed())
            {
                return;
            }

            if (activeSkill != null)
            {
                return;
            }

            var skill = GetSkillForSlot(slotIndex);
            if (skill == null)
            {
                return;
            }

            BeginAim(skill, slotIndex);
        }

        private void HandleSkillCanceled(int slotIndex)
        {
            if (activeSkill == null || slotIndex != activeSlot)
            {
                return;
            }

            CommitCast();
        }

        private void HandleCancelPerformed()
        {
            if (activeSkill == null)
            {
                return;
            }

            CancelAim();
        }

        private void UpdateChargeVisual()
        {
            if (skillBarUI == null)
            {
                ResolveSkillBarUI();
            }

            if (skillBarUI == null)
            {
                return;
            }

            if (activeSkill == null || !activeSkill.SupportsCharge)
            {
                skillBarUI.ClearSkillCharge();
                return;
            }

            var elapsed = Mathf.Max(0f, Time.time - activeSkillPressedAt);
            var ratio = activeSkill.ResolveChargeRatio(elapsed);
            skillBarUI.NotifySkillCharge(activeSkill, ratio, true);
        }

        /// <summary>
        /// 根据槽位索引获取对应的技能定义。
        /// </summary>
        /// <param name="index">技能槽位索引（0-5）</param>
        /// <returns>对应的技能定义，如果不存在则返回null</returns>
        private SkillDefinition GetSkillForSlot(int index)
        {
            if (skillUser == null || index < 0)
            {
                return null;
            }

            var skills = skillUser.Skills;
            if (skills == null || skills.Count == 0)
            {
                return null;
            }

            nonBasicSkillBuffer.Clear();
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null || skillUser.IsBasicAttackSkill(skill))
                {
                    continue;
                }

                nonBasicSkillBuffer.Add(skill);
            }

            if (index >= nonBasicSkillBuffer.Count)
            {
                return null;
            }

            return nonBasicSkillBuffer[index];
        }

        /// <summary>
        /// 开始瞄准指定技能。
        /// 显示技能指示器并记录当前激活的技能和按键。
        /// </summary>
        /// <param name="skill">要瞄准的技能</param>
        /// <param name="slotIndex">触发该技能的槽位索引</param>
        private void BeginAim(SkillDefinition skill, int slotIndex)
        {
            // 检查有效性：技能存在、有技能组件、且当前不在施法中
            if (skill == null || skillUser == null || skillUser.IsCasting)
            {
                return;
            }

            activeSkill = skill;
            activeSlot = slotIndex;
            activeSkillPressedAt = Time.time;
            ClearSkillChargeVisual();
            indicator.Show(skill);
            UpdateAimDirection();
            UpdateTargetHighlight();
        }

        /// <summary>
        /// 更新瞄准方向。
        /// 根据鼠标位置计算瞄准点，并更新指示器显示。
        /// </summary>
        private void UpdateAimDirection()
        {
            // 尝试获取鼠标在世界空间中的瞄准点
            if (!TryGetAimPoint(out var point))
            {
                hasAimPoint = false;
                if (indicator != null)
                {
                    indicator.ClearAimPoint();
                }
                return;
            }

            hasAimPoint = true;
            lastAimPoint = point;
            indicator.SetAimPoint(point);

            // 计算从角色位置到瞄准点的方向（忽略Y轴）
            var origin = transform.position;
            var direction = point - origin;
            direction.y = 0f;

            // 避免零向量导致的问题
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            // 更新瞄准方向并同步到指示器
            lastAimDirection = direction.normalized;
            indicator.SetAimDirection(lastAimDirection);
        }

        /// <summary>
        /// 更新目标高亮显示。
        /// 根据当前技能的目标选择配置查询目标，并高亮显示距离鼠标最近的目标。
        /// </summary>
        private void UpdateTargetHighlight()
        {
            // 没有必要的组件则跳过
            if (indicator == null || activeSkill == null)
            {
                ClearTargetHighlight();
                return;
            }

            // 没有目标选择系统或单位根则跳过
            if (targetingSystem == null || unitRoot == null)
            {
                ClearTargetHighlight();
                return;
            }

            var targeting = activeSkill.Targeting;
            // 自身技能不需要高亮目标
            if (targeting == null || targeting.Mode == TargetingMode.Self)
            {
                ClearTargetHighlight();
                return;
            }

            // 查询所有候选目标（不限制数量）
            targetingSystem.CollectAllCandidates(targeting, unitRoot, cachedTargets, hasAimPoint, lastAimPoint, lastAimDirection);

            if (cachedTargets.Count == 0)
            {
                ClearTargetHighlight();
                return;
            }

            if (targeting.IgnoreOptionalExplicitTarget)
            {
                if (skillUser != null
                    && skillUser.TryResolveAutoCastTarget(activeSkill, hasAimPoint, lastAimPoint, lastAimDirection, out var autoTarget))
                {
                    SetHighlightIfChanged(autoTarget != null ? autoTarget.transform : null);
                }
                else
                {
                    ClearTargetHighlight();
                }

                return;
            }

            // 如果只有一个目标，直接使用
            if (cachedTargets.Count == 1)
            {
                SetHighlightIfChanged(cachedTargets[0].Transform);
                return;
            }

            // 显式目标技能优先按配置排序自动选择（通常为最近目标），降低对鼠标位置的依赖。
            if (targeting.RequireExplicitTarget)
            {
                var explicitTarget = SelectMousePreferredTarget(cachedTargets, targeting.Sort);
                SetHighlightIfChanged(explicitTarget);
                return;
            }

            // 多个目标时，选择距离鼠标最近的
            var bestTarget = SelectClosestToMouse(cachedTargets);
            SetHighlightIfChanged(bestTarget);
        }

        /// <summary>
        /// 从候选目标中选择距离鼠标位置最近的目标。
        /// </summary>
        /// <param name="candidates">候选目标列表</param>
        /// <returns>距离鼠标最近的目标Transform</returns>
        private Transform SelectClosestToMouse(List<CombatTarget> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            // 获取鼠标在世界空间的位置
            if (!hasAimPoint)
            {
                // 如果无法获取鼠标位置，返回第一个目标
                return candidates[0].Transform;
            }

            var mouseWorldPos = lastAimPoint;

            Transform closest = null;
            var minDistSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.IsValid)
                {
                    continue;
                }

                var pos = candidate.Transform.position;
                // 在水平面上计算距离（忽略Y轴）
                var dx = pos.x - mouseWorldPos.x;
                var dz = pos.z - mouseWorldPos.z;
                var distSqr = dx * dx + dz * dz;

                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                    closest = candidate.Transform;
                }
            }

            return closest ?? candidates[0].Transform;
        }

        private Transform SelectAutoTargetBySort(List<CombatTarget> candidates, TargetSort sort)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var origin = transform.position;
            CombatTarget best = default;
            var hasBest = false;
            var bestDistance = 0f;
            var bestHealth = 0f;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.IsValid || candidate.Transform == null)
                {
                    continue;
                }

                var distance = GetHorizontalDistanceSqr(origin, candidate.Transform.position);
                var healthValue = candidate.Health != null ? candidate.Health.Current : float.MaxValue;

                if (!hasBest)
                {
                    best = candidate;
                    bestDistance = distance;
                    bestHealth = healthValue;
                    hasBest = true;
                    continue;
                }

                if (IsBetterCandidate(sort, distance, healthValue, bestDistance, bestHealth))
                {
                    best = candidate;
                    bestDistance = distance;
                    bestHealth = healthValue;
                }
            }

            return hasBest ? best.Transform : candidates[0].Transform;
        }

        private Transform SelectMousePreferredTarget(List<CombatTarget> candidates, TargetSort sort)
        {
            var hovered = SelectHoveredCandidate(candidates);
            if (hovered != null)
            {
                return hovered;
            }

            return SelectAutoTargetBySort(candidates, sort);
        }

        private Transform SelectHoveredCandidate(List<CombatTarget> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (viewCamera == null || inputReader == null)
            {
                return null;
            }

            var ray = viewCamera.ScreenPointToRay(inputReader.AimPoint);
            var hitCount = Physics.RaycastNonAlloc(ray, hoverHitBuffer, raycastDistance, ~0, QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
            {
                return null;
            }

            Transform best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = hoverHitBuffer[i];
                if (hit.collider == null)
                {
                    continue;
                }

                var candidate = ResolveCandidateFromHit(hit.collider.transform, candidates);
                if (candidate == null)
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static Transform ResolveCandidateFromHit(Transform hitTransform, List<CombatTarget> candidates)
        {
            if (hitTransform == null || candidates == null)
            {
                return null;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.IsValid || candidate.Transform == null)
                {
                    continue;
                }

                var candidateTransform = candidate.Transform;
                if (hitTransform == candidateTransform ||
                    hitTransform.IsChildOf(candidateTransform) ||
                    candidateTransform.IsChildOf(hitTransform))
                {
                    return candidateTransform;
                }
            }

            return null;
        }

        private static float GetHorizontalDistanceSqr(Vector3 from, Vector3 to)
        {
            var dx = to.x - from.x;
            var dz = to.z - from.z;
            return dx * dx + dz * dz;
        }

        private static bool IsBetterCandidate(
            TargetSort sort,
            float candidateDistance,
            float candidateHealth,
            float bestDistance,
            float bestHealth)
        {
            switch (sort)
            {
                case TargetSort.Farthest:
                    return candidateDistance > bestDistance;
                case TargetSort.LowestHealth:
                    return candidateHealth < bestHealth;
                case TargetSort.HighestHealth:
                    return candidateHealth > bestHealth;
                case TargetSort.Random:
                    return Random.value > 0.5f;
                case TargetSort.None:
                case TargetSort.Closest:
                default:
                    return candidateDistance < bestDistance;
            }
        }

        /// <summary>
        /// 如果目标发生变化则更新高亮。
        /// </summary>
        /// <param name="target">新目标</param>
        private void SetHighlightIfChanged(Transform target)
        {
            if (target != currentTarget)
            {
                currentTarget = target;
                indicator.SetHighlightTarget(currentTarget);
                NotifyTargetChanged(currentTarget);
            }
        }

        /// <summary>
        /// 清除目标高亮。
        /// </summary>
        private void ClearTargetHighlight()
        {
            if (currentTarget == null)
            {
                return;
            }

            currentTarget = null;
            if (indicator != null)
            {
                indicator.SetHighlightTarget(null);
            }

            // 复用 NotifyTargetChanged 通知目标清除事件
            NotifyTargetChanged(null);
        }

        /// <summary>
        /// 尝试获取鼠标在世界空间中的瞄准点。
        /// 使用射线检测地面或水平面来确定瞄准位置。
        /// </summary>
        /// <param name="point">输出的瞄准点世界坐标</param>
        /// <returns>是否成功获取瞄准点</returns>
        private bool TryGetAimPoint(out Vector3 point)
        {
            // 确保有摄像机引用
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            // 没有摄像机时使用上一帧的方向
            if (viewCamera == null)
            {
                point = transform.position + lastAimDirection;
                return false;
            }

            if (inputReader == null)
            {
                point = transform.position + lastAimDirection;
                return false;
            }

            // 从摄像机发射一条经过指针位置的射线
            var ray = viewCamera.ScreenPointToRay(inputReader.AimPoint);

            // 如果启用了地面遮罩检测
            if (useGroundMask)
            {
                if (Physics.Raycast(ray, out var hit, raycastDistance, groundMask, QueryTriggerInteraction.Ignore))
                {
                    point = hit.point;
                    return true;
                }
            }

            // 使用水平面进行射线相交计算（以角色高度为准）
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (plane.Raycast(ray, out var distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            // 都失败时返回默认方向
            point = transform.position + lastAimDirection;
            return false;
        }

        /// <summary>
        /// 确认释放技能。
        /// 根据当前瞄准方向旋转角色（如果启用），然后尝试施放技能。
        /// 将鼠标选中的目标作为显式目标传递，确保与指示器显示一致。
        /// </summary>
        private void CommitCast()
        {
            var skill = activeSkill;
            var direction = lastAimDirection;
            var aimPoint = lastAimPoint;
            var hadAimPoint = hasAimPoint;
            // 只有蓄力技能才记录蓄力时长，避免“按住瞄准”被误认为所有技能都在蓄力。
            var chargeDuration = Mathf.Max(0f, Time.time - activeSkillPressedAt);
            if (skill != null && !skill.SupportsCharge)
            {
                chargeDuration = 0f;
            }
            // 保存当前预览到的目标；自动锁定类技能只拿它做预览/朝向，不把它当作输入硬选中。
            var previewTarget = currentTarget != null ? currentTarget.gameObject : null;
            var selectedTarget = previewTarget;
            if (skill != null
                && skill.Targeting != null
                && skill.Targeting.IgnoreOptionalExplicitTarget
                && !skill.Targeting.RequireExplicitTarget)
            {
                selectedTarget = null;
            }

            // 先取消瞄准状态
            CancelAim();

            if (skill == null || skillUser == null)
            {
                return;
            }

            // 如果启用了施法转向：显式目标技能优先面向目标，其次面向瞄准方向。
            if (rotateCasterToAim)
            {
                var facingDirection = direction;
                GameObject facingTarget = null;
                if (skill != null && skill.Targeting != null)
                {
                    if (skill.Targeting.RequireExplicitTarget)
                    {
                        facingTarget = selectedTarget;
                    }
                    else if (skill.Targeting.IgnoreOptionalExplicitTarget)
                    {
                        facingTarget = previewTarget;
                    }
                }

                if (facingTarget != null)
                {
                    facingDirection = facingTarget.transform.position - transform.position;
                    facingDirection.y = 0f;
                }

                if (facingDirection.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(facingDirection.normalized);
                }
            }

            // 尝试施放技能，传递选中的目标作为显式目标
            skillUser.TryCast(skill, selectedTarget, hadAimPoint, aimPoint, direction, chargeDuration);
        }

        /// <summary>
        /// 取消瞄准。
        /// 隐藏指示器并重置瞄准状态。
        /// </summary>
        private void CancelAim()
        {
            activeSkill = null;
            activeSlot = -1;
            activeSkillPressedAt = 0f;
            hasAimPoint = false;
            indicator.Hide();
            indicator.ClearAimPoint();
            ClearTargetHighlight();
            ClearSkillChargeVisual();
        }

        private void NotifyTargetChanged(Transform target)
        {
            if (eventHub == null)
            {
                return;
            }

            if (target == null)
            {
                eventHub.RaiseTargetCleared();
                return;
            }

            var health = target.GetComponentInParent<HealthComponent>();
            if (health != null)
            {
                eventHub.RaiseTargetChanged(health);
            }
        }

        #endregion
    }
}
