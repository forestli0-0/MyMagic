using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 单位视觉表现驱动：
    /// - 根据 UnitVisualProfile 挂载模型到 VisualRoot
    /// - 将移动/施法/受击/死亡状态同步到 Animator 参数
    /// - 在模型启用时隐藏根节点胶囊渲染器（可选）
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitRoot))]
    public class UnitVisualPresenter : MonoBehaviour
    {
        private const string DefaultVisualRootName = "VisualRoot";

        [Header("References")]
        [SerializeField] private UnitRoot unitRoot;
        [SerializeField] private MovementComponent movement;
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private HealthComponent health;
        [SerializeField] private HitFlashReceiver hitFlashReceiver;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;

        [Header("Profile")]
        [SerializeField] private UnitVisualProfile profileOverride;
        [SerializeField] private bool useDefinitionProfile = true;
        [SerializeField] private bool autoCreateVisualRoot = true;
        [SerializeField] private bool autoFindAnimatorOnModel = true;
        [SerializeField] private bool refreshHitFlashTargetsAfterModelSwap = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog;

        [Header("Model Anchor")]
        [Tooltip("当不使用 Root Motion 时锁定模型局部位置，防止动画根节点位移导致模型与逻辑体错位。")]
        [SerializeField] private bool lockModelLocalPositionWhenNoRootMotion = true;
        [Tooltip("模型局部位置漂移容差（米），超出后判定为异常漂移。")]
        [SerializeField, Min(0f)] private float modelAnchorTolerance = 0.01f;
        [Tooltip("输出模型漂移诊断日志（仅用于排查）。")]
        [SerializeField] private bool logModelAnchorDrift;
        
        [Header("Hit Reaction")]
        [Tooltip("是否启用受击动画。关闭后仅保留掉血反馈（血条/飘字/受击闪白）。")]
        [SerializeField] private bool enableHitReaction = true;
        [Tooltip("玩家单位默认不播放受击动画，避免走A/施法时频繁被打断。")]
        [SerializeField] private bool suppressHitReactionForPlayer = true;
        [Tooltip("施法中不触发受击动画，保持施法动作稳定。")]
        [SerializeField] private bool suppressHitReactionWhileCasting = true;
        [Tooltip("移动中不触发受击动画，减少\"倒地/僵住\"观感。")]
        [SerializeField] private bool suppressHitReactionWhileMoving = true;
        [Tooltip("移动状态受击抑制阈值（米/秒）。")]
        [SerializeField, Min(0f)] private float movingSuppressSpeed = 0.2f;
        [Tooltip("受击动画最小触发间隔（秒），用于避免连续受击导致动画锁死。")]
        [SerializeField, Min(0f)] private float hitReactionCooldown = 1f;
        [Tooltip("受击状态最大保持时长（秒），超过后强制回到 Locomotion。")]
        [SerializeField, Min(0.02f)] private float hitMaxHoldSeconds = 0.08f;

        private readonly List<RendererState> cachedRootRenderers = new List<RendererState>(8);
        private readonly HashSet<Renderer> rootRendererSet = new HashSet<Renderer>();

        private UnitVisualProfile activeProfile;
        private GameObject spawnedModel;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private float smoothedSpeed;
        private bool isDead;
        private float nextHitReactionTime;
        private float forceExitHitAt = -1f;
        private string currentCastSkillName = string.Empty;
        private bool hasLoggedDriftForCurrentCast;

        // Animator parameter cache
        private int moveSpeedHash;
        private int movingHash;
        private int attackHash;
        private int castHash;
        private int hitHash;
        private int dieHash;
        private int deadHash;
        private int castingHash;
        private static readonly int HitStateHash = Animator.StringToHash("Hit");
        private static readonly int DieStateHash = Animator.StringToHash("Die");

        private bool hasMoveSpeedFloat;
        private bool hasMovingBool;
        private bool hasAttackTrigger;
        private bool hasCastTrigger;
        private bool hasHitTrigger;
        private bool hasDieTrigger;
        private bool hasDeadBool;
        private bool hasCastingBool;

        private void Reset()
        {
            unitRoot = GetComponent<UnitRoot>();
            movement = GetComponent<MovementComponent>();
            skillUser = GetComponent<SkillUserComponent>();
            health = GetComponent<HealthComponent>();
            hitFlashReceiver = GetComponent<HitFlashReceiver>();
            visualRoot = transform.Find(DefaultVisualRootName);
            animator = GetComponentInChildren<Animator>(true);
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureVisualRoot();
            RebuildVisual(force: true);
            CaptureMotionBaseline();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureVisualRoot();
            RebuildVisual(force: false);
            SubscribeEvents();
            CaptureMotionBaseline();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void OnDestroy()
        {
            RestoreRootRenderers();
            DestroySpawnedModel();
        }

        private void Update()
        {
            EnsureVisualChainReady();
            UpdateAnimatorMovementState();
        }

        private void LateUpdate()
        {
            EnforceModelAnchor();
        }

        /// <summary>
        /// 手动切换视觉配置（运行时热切换支持）。
        /// </summary>
        public void SetProfileOverride(UnitVisualProfile profile, bool rebuild = true)
        {
            profileOverride = profile;
            if (rebuild)
            {
                RebuildVisual(force: true);
            }
        }

        [ContextMenu("Rebuild Visual")]
        public void RebuildVisualNow()
        {
            RebuildVisual(force: true);
        }

        [ContextMenu("Dump Visual Diagnostics")]
        public void DumpVisualDiagnostics()
        {
            var modelWorldPos = spawnedModel != null ? spawnedModel.transform.position : Vector3.zero;
            var modelLocalPos = spawnedModel != null ? spawnedModel.transform.localPosition : Vector3.zero;
            var expectedLocalPos = activeProfile != null ? activeProfile.LocalPosition : Vector3.zero;
            var state = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
            var resolvedProfile = ResolveProfile();
            var definition = unitRoot != null ? unitRoot.Definition : null;

            Debug.Log(
                $"[UnitVisualPresenter] Diagnostics '{name}': unitDef={(definition != null ? definition.name : "None")}, resolvedProfile={(resolvedProfile != null ? resolvedProfile.name : "None")}, activeProfile={(activeProfile != null ? activeProfile.name : "None")}, modelPrefab={(resolvedProfile != null && resolvedProfile.ModelPrefab != null ? resolvedProfile.ModelPrefab.name : "None")}, spawnedModel={(spawnedModel != null ? spawnedModel.name : "None")}, animator={(animator != null ? animator.name : "None")}, root={transform.position}, modelWorld={modelWorldPos}, modelLocal={modelLocalPos}, expectedLocal={expectedLocalPos}, rootMotion={(animator != null && animator.applyRootMotion)}, stateHash={(animator != null ? state.shortNameHash : 0)}, stateTime={(animator != null ? state.normalizedTime : 0f):F2}",
                this);
        }

        private void RebuildVisual(bool force)
        {
            var resolvedProfile = ResolveProfile();
            if (!force && resolvedProfile == activeProfile && (resolvedProfile == null || spawnedModel != null))
            {
                return;
            }

            activeProfile = resolvedProfile;
            isDead = false;
            nextHitReactionTime = 0f;
            forceExitHitAt = -1f;
            DestroySpawnedModel();
            RestoreRootRenderers();

            if (activeProfile != null && activeProfile.ModelPrefab != null)
            {
                spawnedModel = Instantiate(activeProfile.ModelPrefab, EnsureVisualRoot());
                spawnedModel.name = activeProfile.ModelPrefab.name;
                var modelTransform = spawnedModel.transform;
                modelTransform.localPosition = activeProfile.LocalPosition;
                modelTransform.localRotation = Quaternion.Euler(activeProfile.LocalEulerAngles);
                modelTransform.localScale = SanitizeScale(activeProfile.LocalScale);
            }

            ResolveAnimator();
            EnsureAnimatorComponent();
            EnsureAnimationEventProxy();
            ApplyAnimatorOverridesFromProfile();
            CacheAnimatorParameters();

            if (activeProfile != null &&
                activeProfile.ModelPrefab != null &&
                activeProfile.HideRootRenderersWhenModelActive)
            {
                CacheAndHideRootRenderers();
            }

            if (refreshHitFlashTargetsAfterModelSwap && hitFlashReceiver != null)
            {
                hitFlashReceiver.RefreshRendererCache();
            }

            if (enableDebugLog)
            {
                Debug.Log(
                    $"[UnitVisualPresenter] Rebuild {name}: profile={(activeProfile != null ? activeProfile.name : "None")} model={(spawnedModel != null ? spawnedModel.name : "None")} animator={(animator != null ? animator.name : "None")}",
                    this);
            }
        }

        private UnitVisualProfile ResolveProfile()
        {
            if (profileOverride != null)
            {
                return profileOverride;
            }

            if (!useDefinitionProfile || unitRoot == null || unitRoot.Definition == null)
            {
                return null;
            }

            return unitRoot.Definition.VisualProfile;
        }

        private void ResolveReferences()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (movement == null)
            {
                movement = GetComponent<MovementComponent>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (hitFlashReceiver == null)
            {
                hitFlashReceiver = GetComponent<HitFlashReceiver>();
            }
        }

        private Transform EnsureVisualRoot()
        {
            if (visualRoot != null)
            {
                return visualRoot;
            }

            var existing = transform.Find(DefaultVisualRootName);
            if (existing != null)
            {
                visualRoot = existing;
                return visualRoot;
            }

            if (!autoCreateVisualRoot)
            {
                return transform;
            }

            var go = new GameObject(DefaultVisualRootName);
            visualRoot = go.transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
            return visualRoot;
        }

        private void ResolveAnimator()
        {
            animator = null;

            if (spawnedModel != null && autoFindAnimatorOnModel)
            {
                animator = spawnedModel.GetComponentInChildren<Animator>(true);
            }

            if (animator == null && visualRoot != null && autoFindAnimatorOnModel)
            {
                animator = visualRoot.GetComponentInChildren<Animator>(true);
            }

            if (animator == null && autoFindAnimatorOnModel)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        private void EnsureVisualChainReady()
        {
            var resolvedProfile = ResolveProfile();
            if (resolvedProfile != activeProfile)
            {
                RebuildVisual(force: true);
                return;
            }

            if (resolvedProfile == null || resolvedProfile.ModelPrefab == null)
            {
                return;
            }

            if (spawnedModel == null)
            {
                RebuildVisual(force: true);
                return;
            }

            if (animator != null)
            {
                return;
            }

            ResolveAnimator();
            EnsureAnimatorComponent();
            ApplyAnimatorOverridesFromProfile();
            CacheAnimatorParameters();

            if (refreshHitFlashTargetsAfterModelSwap && hitFlashReceiver != null)
            {
                hitFlashReceiver.RefreshRendererCache();
            }
        }

        private void EnsureAnimatorComponent()
        {
            if (animator != null || spawnedModel == null || activeProfile == null)
            {
                return;
            }

            if (!autoFindAnimatorOnModel)
            {
                return;
            }

            if (activeProfile.AnimatorController == null && activeProfile.AvatarOverride == null)
            {
                return;
            }

            animator = spawnedModel.GetComponent<Animator>();
            if (animator == null)
            {
                animator = spawnedModel.AddComponent<Animator>();
                if (enableDebugLog)
                {
                    Debug.Log($"[UnitVisualPresenter] Added Animator on spawned model '{spawnedModel.name}'.", this);
                }
            }
        }

        private void ApplyAnimatorOverridesFromProfile()
        {
            if (animator == null || activeProfile == null)
            {
                return;
            }

            if (activeProfile.AnimatorController != null)
            {
                animator.runtimeAnimatorController = activeProfile.AnimatorController;
            }

            if (activeProfile.AvatarOverride != null)
            {
                animator.avatar = activeProfile.AvatarOverride;
            }

            animator.applyRootMotion = activeProfile.ApplyRootMotion;
            animator.Rebind();
            animator.Update(0f);
        }

        private void EnforceModelAnchor()
        {
            if (spawnedModel == null || activeProfile == null || animator == null)
            {
                return;
            }

            if (activeProfile.ApplyRootMotion)
            {
                return;
            }

            var modelTransform = spawnedModel.transform;
            var expectedLocalPosition = activeProfile.LocalPosition;
            var delta = modelTransform.localPosition - expectedLocalPosition;
            var tolerance = Mathf.Max(0f, modelAnchorTolerance);
            if (delta.sqrMagnitude <= tolerance * tolerance)
            {
                return;
            }

            if (logModelAnchorDrift && (!hasLoggedDriftForCurrentCast || skillUser == null || !skillUser.IsCasting))
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                Debug.LogWarning(
                    $"[UnitVisualPresenter] Model drift on '{name}': cast='{currentCastSkillName}', stateHash={state.shortNameHash}, normalizedTime={state.normalizedTime:F2}, localDelta={delta}, rootMotion={animator.applyRootMotion}",
                    this);
                hasLoggedDriftForCurrentCast = true;
            }

            if (lockModelLocalPositionWhenNoRootMotion)
            {
                modelTransform.localPosition = expectedLocalPosition;
            }
        }

        private void SubscribeEvents()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
                health.Died += HandleDied;
                health.HealthChanged -= HandleHealthChanged;
                health.HealthChanged += HandleHealthChanged;
            }

            if (skillUser != null)
            {
                skillUser.SkillCastStarted -= HandleSkillCastStarted;
                skillUser.SkillCastStarted += HandleSkillCastStarted;
                skillUser.SkillCastCompleted -= HandleSkillCastCompleted;
                skillUser.SkillCastCompleted += HandleSkillCastCompleted;
                skillUser.SkillCastInterrupted -= HandleSkillCastInterrupted;
                skillUser.SkillCastInterrupted += HandleSkillCastInterrupted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
                health.HealthChanged -= HandleHealthChanged;
            }

            if (skillUser != null)
            {
                skillUser.SkillCastStarted -= HandleSkillCastStarted;
                skillUser.SkillCastCompleted -= HandleSkillCastCompleted;
                skillUser.SkillCastInterrupted -= HandleSkillCastInterrupted;
            }
        }

        private void HandleSkillCastStarted(SkillCastEvent evt)
        {
            if (unitRoot == null || evt.Caster != unitRoot)
            {
                return;
            }

            currentCastSkillName = evt.Skill != null
                ? (string.IsNullOrWhiteSpace(evt.Skill.DisplayName) ? evt.Skill.name : evt.Skill.DisplayName)
                : "<null>";
            hasLoggedDriftForCurrentCast = false;

            if (hasCastingBool)
            {
                animator.SetBool(castingHash, true);
            }

            var isBasicAttack = skillUser != null && skillUser.IsBasicAttackSkill(evt.Skill);
            if (isBasicAttack && activeProfile != null && activeProfile.UseAttackTriggerForBasicAttack)
            {
                TrySetTrigger(attackHash, hasAttackTrigger);
            }
            else
            {
                TrySetTrigger(castHash, hasCastTrigger);
            }
        }

        private void HandleSkillCastCompleted(SkillCastEvent evt)
        {
            if (unitRoot == null || evt.Caster != unitRoot)
            {
                return;
            }

            if (hasCastingBool)
            {
                animator.SetBool(castingHash, false);
            }

            currentCastSkillName = string.Empty;
            hasLoggedDriftForCurrentCast = false;
        }

        private void HandleSkillCastInterrupted(SkillCastEvent evt)
        {
            if (unitRoot == null || evt.Caster != unitRoot)
            {
                return;
            }

            if (hasCastingBool)
            {
                animator.SetBool(castingHash, false);
            }

            currentCastSkillName = string.Empty;
            hasLoggedDriftForCurrentCast = false;
        }

        private void HandleHealthChanged(HealthChangedEvent evt)
        {
            if (isDead)
            {
                if (evt.IsAlive && evt.NewValue > 0f)
                {
                    ResetFromDeadState();
                }

                return;
            }

            if (evt.Delta < 0f && evt.IsAlive)
            {
                if (!enableHitReaction || ShouldSuppressHitReaction())
                {
                    return;
                }

                if (hitReactionCooldown > 0f && Time.time < nextHitReactionTime)
                {
                    return;
                }

                if (IsInHitOrDeadState())
                {
                    return;
                }

                nextHitReactionTime = Time.time + hitReactionCooldown;
                forceExitHitAt = Time.time + Mathf.Max(0.05f, hitMaxHoldSeconds);
                TrySetTrigger(hitHash, hasHitTrigger);
            }
        }

        private void HandleDied(HealthComponent source)
        {
            if (source != health || isDead)
            {
                return;
            }

            isDead = true;
            forceExitHitAt = -1f;
            if (hasDeadBool)
            {
                animator.SetBool(deadHash, true);
            }

            if (hasCastingBool)
            {
                animator.SetBool(castingHash, false);
            }

            TrySetTrigger(dieHash, hasDieTrigger);
        }

        private void UpdateAnimatorMovementState()
        {
            if (animator == null)
            {
                return;
            }

            var deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            var current = transform.position;
            if (!hasLastPosition)
            {
                lastPosition = current;
                hasLastPosition = true;
                return;
            }

            var planarDelta = current - lastPosition;
            planarDelta.y = 0f;
            var rawSpeed = planarDelta.magnitude / deltaTime;
            lastPosition = current;

            var smoothing = activeProfile != null ? Mathf.Max(1f, activeProfile.MoveSpeedSmoothing) : 12f;
            var lerpT = 1f - Mathf.Exp(-smoothing * deltaTime);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, lerpT);
            if (!isDead && hasMoveSpeedFloat)
            {
                animator.SetFloat(moveSpeedHash, smoothedSpeed);
            }

            if (hasMovingBool)
            {
                var threshold = activeProfile != null ? Mathf.Max(0.01f, activeProfile.MovingThreshold) : 0.08f;
                animator.SetBool(movingHash, !isDead && smoothedSpeed >= threshold);
            }

            TryForceExitHitState();
        }

        private void CaptureMotionBaseline()
        {
            lastPosition = transform.position;
            hasLastPosition = true;
            smoothedSpeed = 0f;

            if (animator != null && hasMoveSpeedFloat)
            {
                animator.SetFloat(moveSpeedHash, 0f);
            }

            if (animator != null && hasMovingBool)
            {
                animator.SetBool(movingHash, false);
            }
        }

        private void CacheAnimatorParameters()
        {
            hasMoveSpeedFloat = TryCacheParameter(activeProfile != null ? activeProfile.MoveSpeedFloat : string.Empty, AnimatorControllerParameterType.Float, out moveSpeedHash);
            hasMovingBool = TryCacheParameter(activeProfile != null ? activeProfile.MovingBool : string.Empty, AnimatorControllerParameterType.Bool, out movingHash);
            hasAttackTrigger = TryCacheParameter(activeProfile != null ? activeProfile.AttackTrigger : string.Empty, AnimatorControllerParameterType.Trigger, out attackHash);
            hasCastTrigger = TryCacheParameter(activeProfile != null ? activeProfile.CastTrigger : string.Empty, AnimatorControllerParameterType.Trigger, out castHash);
            hasHitTrigger = TryCacheParameter(activeProfile != null ? activeProfile.HitTrigger : string.Empty, AnimatorControllerParameterType.Trigger, out hitHash);
            hasDieTrigger = TryCacheParameter(activeProfile != null ? activeProfile.DieTrigger : string.Empty, AnimatorControllerParameterType.Trigger, out dieHash);
            hasDeadBool = TryCacheParameter(activeProfile != null ? activeProfile.DeadBool : string.Empty, AnimatorControllerParameterType.Bool, out deadHash);
            hasCastingBool = TryCacheParameter(activeProfile != null ? activeProfile.CastingBool : string.Empty, AnimatorControllerParameterType.Bool, out castingHash);
        }

        private bool TryCacheParameter(string parameterName, AnimatorControllerParameterType type, out int hash)
        {
            hash = 0;
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            var parameters = animator.parameters;
            if (parameters == null || parameters.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.type != type || !string.Equals(parameter.name, parameterName))
                {
                    continue;
                }

                hash = parameter.nameHash;
                return true;
            }

            return false;
        }

        private void TrySetTrigger(int hash, bool available)
        {
            if (!available || animator == null || hash == 0)
            {
                return;
            }

            animator.ResetTrigger(hash);
            animator.SetTrigger(hash);
        }

        private bool ShouldSuppressHitReaction()
        {
            if (suppressHitReactionForPlayer && PlayerUnitLocator.IsPlayerUnit(unitRoot))
            {
                return true;
            }

            if (suppressHitReactionWhileCasting && skillUser != null && skillUser.IsCasting)
            {
                return true;
            }

            if (suppressHitReactionWhileMoving && smoothedSpeed >= Mathf.Max(0f, movingSuppressSpeed))
            {
                return true;
            }

            return false;
        }

        private bool IsInHitOrDeadState()
        {
            if (animator == null)
            {
                return false;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == HitStateHash || state.shortNameHash == DieStateHash;
        }

        private void TryForceExitHitState()
        {
            if (animator == null || isDead || forceExitHitAt < 0f)
            {
                return;
            }

            if (Time.time < forceExitHitAt)
            {
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash == HitStateHash)
            {
                animator.CrossFadeInFixedTime("Locomotion", 0.06f, 0, 0f);
            }

            forceExitHitAt = -1f;
        }

        /// <summary>
        /// 外部复活流程可显式调用，重置死亡相关视觉状态。
        /// </summary>
        public void ForceReviveVisualState()
        {
            ResetFromDeadState();
        }

        private void ResetFromDeadState()
        {
            isDead = false;
            forceExitHitAt = -1f;

            if (animator == null)
            {
                CaptureMotionBaseline();
                return;
            }

            if (hasDeadBool)
            {
                animator.SetBool(deadHash, false);
            }

            if (hasCastingBool)
            {
                animator.SetBool(castingHash, false);
            }

            if (hasMoveSpeedFloat)
            {
                animator.SetFloat(moveSpeedHash, 0f);
            }

            if (hasMovingBool)
            {
                animator.SetBool(movingHash, false);
            }

            animator.CrossFadeInFixedTime("Locomotion", 0.08f, 0, 0f);
            CaptureMotionBaseline();
        }

        private void EnsureAnimationEventProxy()
        {
            if (animator == null)
            {
                return;
            }

            var animatorGo = animator.gameObject;
            if (animatorGo.GetComponent<UnitAnimationEventProxy>() == null)
            {
                animatorGo.AddComponent<UnitAnimationEventProxy>();
            }
        }

        private void CacheAndHideRootRenderers()
        {
            cachedRootRenderers.Clear();
            rootRendererSet.Clear();

            var allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                var renderer = allRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (visualRoot != null && renderer.transform.IsChildOf(visualRoot))
                {
                    continue;
                }

                if (!rootRendererSet.Add(renderer))
                {
                    continue;
                }

                cachedRootRenderers.Add(new RendererState(renderer, renderer.enabled));
                renderer.enabled = false;
            }
        }

        private void RestoreRootRenderers()
        {
            for (int i = 0; i < cachedRootRenderers.Count; i++)
            {
                var state = cachedRootRenderers[i];
                if (state.Renderer == null)
                {
                    continue;
                }

                state.Renderer.enabled = state.WasEnabled;
            }

            cachedRootRenderers.Clear();
            rootRendererSet.Clear();
        }

        private void DestroySpawnedModel()
        {
            if (spawnedModel == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(spawnedModel);
            }
            else
            {
                DestroyImmediate(spawnedModel);
            }

            spawnedModel = null;
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        }

        private readonly struct RendererState
        {
            public readonly Renderer Renderer;
            public readonly bool WasEnabled;

            public RendererState(Renderer renderer, bool wasEnabled)
            {
                Renderer = renderer;
                WasEnabled = wasEnabled;
            }
        }
    }

    /// <summary>
    /// 兼容第三方动画片段中的 AnimationEvent。
    /// 这些事件在当前项目暂不驱动逻辑，仅用于避免缺少接收器导致的控制台警告刷屏。
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitAnimationEventProxy : MonoBehaviour
    {
        public void FootL()
        {
        }

        public void FootR()
        {
        }

        public void Hit()
        {
        }

        public void Land()
        {
        }

        public void WeaponSwitch()
        {
        }
    }
}
