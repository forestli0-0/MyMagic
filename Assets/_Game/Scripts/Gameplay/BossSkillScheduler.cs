using System;
using System.Collections;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// Boss 技能节奏控制器：按序列进行 telegraph + 施法。
    /// </summary>
    public class BossSkillScheduler : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private Transform explicitTarget;
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private Transform telegraphRoot;
        [SerializeField] private Animator animationLockAnimator;

        [Header("节奏")]
        [SerializeField] private List<BossSkillCycleEntry> skillCycle = new List<BossSkillCycleEntry>();
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float initialDelay = 1f;
        [SerializeField] private float retryInterval = 0.15f;
        [Tooltip("Boss 施法最小停移时长（秒），用于避免动画仍在播放时出现滑步。")]
        [SerializeField] private float minimumMovementLockDuration = 0.35f;
        [Tooltip("施法动作收尾的额外停移缓冲（秒），用于消除末尾一小段滑步。")]
        [SerializeField] private float castEndMovementLockBuffer = 0.5f;
        [Tooltip("是否按 Animator Cast 状态退出来解锁移动（推荐开启，减少硬编码时长依赖）。")]
        [SerializeField] private bool lockMovementByAnimatorCastState = true;
        [Tooltip("是否监听动画事件/状态机行为的 Cast 开始/结束信号来驱动停移。")]
        [SerializeField] private bool lockMovementByAnimationSignals = true;
        [Tooltip("被视为施法状态的 Animator 状态名（shortName）。")]
        [SerializeField] private string[] castStateNames = { "Cast" };
        [Tooltip("被视为施法状态的 Animator Tag。可选。")]
        [SerializeField] private string[] castStateTags = { "Cast", "Casting" };
        [Tooltip("等待进入/退出施法动画或事件信号的最大时长（秒），超时后回退到时长锁，防止卡死。")]
        [SerializeField] private float animatorCastStateLockTimeout = 2.5f;

        [Header("Telegraph")]
        [SerializeField] private bool showGroundTelegraph = true;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.25f, 0.2f, 0.4f);
        [SerializeField] private float fallbackTelegraphRadius = 2f;
        [SerializeField] private float telegraphHeight = 0.02f;
        [SerializeField] private bool logCastCancellation;

        private Coroutine loopRoutine;
        private float movementLockUntilTime;
        private int[] castStateNameHashes = Array.Empty<int>();
        private bool animatorCastLockActive;
        private bool animatorCastStateObserved;
        private float animatorCastLockDeadline;
        private UnitAnimationEventProxy animationEventProxy;

        /// <summary>
        /// 是否处于 Boss 技能流程导致的停移窗口。
        /// </summary>
        public bool IsMovementLocked => Time.time < movementLockUntilTime || animatorCastLockActive;

        public event Action<BossTelegraphEvent> TelegraphStarted;

        private void Reset()
        {
            skillUser = GetComponent<SkillUserComponent>();
            animationLockAnimator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }
            
            if (animationLockAnimator == null)
            {
                animationLockAnimator = GetComponentInChildren<Animator>();
            }

            CacheCastStateNameHashes();
            TryBindAnimationEventProxy();

            if (autoStart)
            {
                StartLoop();
            }
        }

        private void OnDisable()
        {
            StopLoop();
            movementLockUntilTime = 0f;
            animatorCastLockActive = false;
            animatorCastStateObserved = false;
            animatorCastLockDeadline = 0f;
            UnbindAnimationEventProxy();
        }

        public void StartLoop()
        {
            if (loopRoutine != null)
            {
                return;
            }

            loopRoutine = StartCoroutine(RunLoop());
        }

        public void StopLoop()
        {
            if (loopRoutine == null)
            {
                return;
            }

            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        private IEnumerator RunLoop()
        {
            var delay = Mathf.Max(0f, initialDelay);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            var index = 0;
            while (isActiveAndEnabled)
            {
                if (skillCycle.Count == 0 || skillUser == null)
                {
                    yield return null;
                    continue;
                }

                if (index >= skillCycle.Count)
                {
                    index = 0;
                }

                var entry = skillCycle[index];
                index++;

                if (entry == null || entry.Skill == null)
                {
                    yield return null;
                    continue;
                }

                var castSnapshot = default(BossCastSnapshot);
                var hasSnapshot = false;
                var castRetryDeadline = Time.time + Mathf.Max(0.05f, entry.CastRetryWindow);
                while (Time.time <= castRetryDeadline)
                {
                    if (TryAcquireCastSnapshot(entry, out castSnapshot))
                    {
                        hasSnapshot = true;
                        break;
                    }

                    yield return new WaitForSeconds(Mathf.Max(0.05f, retryInterval));
                }

                if (!hasSnapshot)
                {
                    yield return new WaitForSeconds(Mathf.Max(0.05f, entry.DelayOnFail));
                    continue;
                }

                var telegraphDuration = Mathf.Max(0f, entry.TelegraphDuration);
                if (telegraphDuration > 0f)
                {
                    RaiseTelegraph(entry, castSnapshot.TelegraphCenter, telegraphDuration);
                    if (showGroundTelegraph)
                    {
                        SpawnTelegraphMarker(entry, castSnapshot.TelegraphCenter, telegraphDuration);
                    }

                    yield return new WaitForSeconds(telegraphDuration);
                }

                var casted = TryCastSnapshot(entry, castSnapshot);
                if (casted)
                {
                    ExtendMovementLock(ResolveMovementLockDuration(entry));
                    BeginAnimatorCastLock();
                }
                else if (logCastCancellation)
                {
                    Debug.LogFormat(
                        this,
                        "[BossSkillScheduler] Telegraph finished but cast failed. Skill={0}, requireTarget={1}",
                        entry.Skill != null ? entry.Skill.DisplayName : "<null>",
                        entry.RequireTarget);
                }

                var postDelay = casted ? entry.DelayAfterCast : entry.DelayOnFail;
                if (postDelay > 0f)
                {
                    yield return new WaitForSeconds(postDelay);
                }
                else
                {
                    yield return null;
                }
            }
        }

        private bool TryAcquireCastSnapshot(BossSkillCycleEntry entry, out BossCastSnapshot snapshot)
        {
            var target = ResolveTarget();
            return TryBuildCastSnapshot(entry, target, out snapshot);
        }

        private bool TryBuildCastSnapshot(BossSkillCycleEntry entry, Transform target, out BossCastSnapshot snapshot)
        {
            snapshot = default;
            if (entry == null || entry.Skill == null || skillUser == null)
            {
                return false;
            }

            var skill = entry.Skill;
            var targeting = skill.Targeting;
            var resolvedTarget = target;
            if (!entry.RequireTarget && explicitTarget != null)
            {
                resolvedTarget = explicitTarget;
            }

            if (entry.RequireTarget && resolvedTarget == null)
            {
                return false;
            }

            if (!skillUser.CanCast(skill))
            {
                return false;
            }

            var useExplicitTarget = entry.RequireTarget;
            if (targeting != null && targeting.Origin == TargetingOrigin.Caster && targeting.Mode != TargetingMode.Single)
            {
                useExplicitTarget = false;
            }

            var explicitTargetObject = useExplicitTarget && resolvedTarget != null ? resolvedTarget.gameObject : null;
            var hasAimPoint = false;
            var aimPoint = resolvedTarget != null ? resolvedTarget.position : transform.position;
            var aimDirection = Vector3.zero;
            if (resolvedTarget != null)
            {
                aimDirection = resolvedTarget.position - transform.position;
                aimDirection.y = 0f;
                if (aimDirection.sqrMagnitude > 0.0001f)
                {
                    aimDirection.Normalize();
                }
            }

            if (targeting != null && targeting.Origin == TargetingOrigin.TargetPoint)
            {
                hasAimPoint = resolvedTarget != null || explicitTarget != null;
                if (!hasAimPoint)
                {
                    return false;
                }
            }

            if (useExplicitTarget && explicitTargetObject == null)
            {
                return false;
            }

            if (!useExplicitTarget && entry.RequireTarget && resolvedTarget != null && targeting != null && targeting.Origin == TargetingOrigin.Caster)
            {
                if (!IsWithinCasterSkillReach(targeting, resolvedTarget.position))
                {
                    return false;
                }
            }

            if (useExplicitTarget && !skillUser.IsTargetInRangePreview(skill, explicitTargetObject, hasAimPoint, aimPoint, aimDirection))
            {
                return false;
            }

            snapshot = new BossCastSnapshot(
                explicitTargetObject,
                hasAimPoint,
                aimPoint,
                aimDirection,
                ResolveTelegraphCenter(skill, resolvedTarget));
            return true;
        }

        private bool TryCastSnapshot(BossSkillCycleEntry entry, BossCastSnapshot snapshot)
        {
            if (entry == null || entry.Skill == null || skillUser == null)
            {
                return false;
            }

            return skillUser.TryCast(
                entry.Skill,
                snapshot.ExplicitTarget,
                snapshot.HasAimPoint,
                snapshot.AimPoint,
                snapshot.AimDirection);
        }

        private Transform ResolveTarget()
        {
            if (explicitTarget != null)
            {
                return explicitTarget;
            }

            if (!string.IsNullOrWhiteSpace(targetTag))
            {
                var tagged = PlayerUnitLocator.FindGameObjectWithTagSafe(targetTag);
                if (tagged != null)
                {
                    return tagged.transform;
                }
            }

            return null;
        }

        private Vector3 ResolveTelegraphCenter(SkillDefinition skill, Transform target)
        {
            if (skill != null && skill.Targeting != null && skill.Targeting.Origin == TargetingOrigin.Caster)
            {
                return transform.position;
            }

            return target != null ? target.position : transform.position;
        }

        private bool IsWithinCasterSkillReach(TargetingDefinition targeting, Vector3 targetPosition)
        {
            if (targeting == null)
            {
                return true;
            }

            var reach = 0f;
            switch (targeting.Mode)
            {
                case TargetingMode.Sphere:
                case TargetingMode.Random:
                case TargetingMode.Chain:
                    reach = Mathf.Max(targeting.Radius, targeting.Range);
                    break;
                default:
                    reach = targeting.Range;
                    break;
            }

            if (reach <= 0f)
            {
                return true;
            }

            var origin = transform.position;
            origin.y = 0f;
            targetPosition.y = 0f;
            var maxDistance = reach + 0.1f;
            return (targetPosition - origin).sqrMagnitude <= maxDistance * maxDistance;
        }

        private void RaiseTelegraph(BossSkillCycleEntry entry, Vector3 center, float duration)
        {
            TelegraphStarted?.Invoke(new BossTelegraphEvent(entry.Skill, center, duration));
        }

        private void SpawnTelegraphMarker(BossSkillCycleEntry entry, Vector3 center, float duration)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "BossTelegraph";
            marker.transform.SetParent(telegraphRoot != null ? telegraphRoot : null, true);
            marker.transform.position = new Vector3(center.x, center.y + telegraphHeight, center.z);

            var radius = ResolveTelegraphRadius(entry.Skill);
            marker.transform.localScale = new Vector3(radius * 2f, telegraphHeight, radius * 2f);

            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", telegraphColor);
                block.SetColor("_BaseColor", telegraphColor);
                renderer.SetPropertyBlock(block);
            }

            Destroy(marker, Mathf.Max(0.1f, duration));
        }

        private readonly struct BossCastSnapshot
        {
            public readonly GameObject ExplicitTarget;
            public readonly bool HasAimPoint;
            public readonly Vector3 AimPoint;
            public readonly Vector3 AimDirection;
            public readonly Vector3 TelegraphCenter;

            public BossCastSnapshot(GameObject explicitTarget, bool hasAimPoint, Vector3 aimPoint, Vector3 aimDirection, Vector3 telegraphCenter)
            {
                ExplicitTarget = explicitTarget;
                HasAimPoint = hasAimPoint;
                AimPoint = aimPoint;
                AimDirection = aimDirection;
                TelegraphCenter = telegraphCenter;
            }
        }

        private float ResolveMovementLockDuration(BossSkillCycleEntry entry)
        {
            var lockDuration = Mathf.Max(0f, minimumMovementLockDuration);
            if (entry != null)
            {
                lockDuration = Mathf.Max(lockDuration, entry.DelayAfterCast);
            }

            if (entry != null && entry.Skill != null)
            {
                lockDuration = Mathf.Max(
                    lockDuration,
                    Mathf.Max(0f, entry.Skill.CastTime)
                    + Mathf.Max(0f, entry.Skill.ChannelTime)
                    + Mathf.Max(0f, entry.Skill.PostCastTime));
            }

            return lockDuration + Mathf.Max(0f, castEndMovementLockBuffer);
        }

        private void ExtendMovementLock(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            var lockUntil = Time.time + duration;
            if (lockUntil > movementLockUntilTime)
            {
                movementLockUntilTime = lockUntil;
            }
        }

        private void Update()
        {
            UpdateAnimatorCastLock();
        }

        private void BeginAnimatorCastLock()
        {
            TryBindAnimationEventProxy();

            var canUseStatePolling = lockMovementByAnimatorCastState && animationLockAnimator != null;
            var canUseAnimationSignals = lockMovementByAnimationSignals && animationEventProxy != null;
            if (!canUseStatePolling && !canUseAnimationSignals)
            {
                return;
            }

            animatorCastLockActive = true;
            animatorCastStateObserved = false;
            animatorCastLockDeadline = Time.time + Mathf.Max(0.3f, animatorCastStateLockTimeout);

            if (canUseStatePolling && IsCastAnimatorState(animationLockAnimator.GetCurrentAnimatorStateInfo(0)))
            {
                animatorCastStateObserved = true;
            }
        }

        private void UpdateAnimatorCastLock()
        {
            if (!animatorCastLockActive)
            {
                return;
            }

            if (Time.time >= animatorCastLockDeadline)
            {
                ReleaseAnimatorCastLock(false);
                return;
            }

            if (!lockMovementByAnimatorCastState)
            {
                return;
            }

            if (animationLockAnimator == null)
            {
                animationLockAnimator = GetComponentInChildren<Animator>();
                TryBindAnimationEventProxy();
                if (animationLockAnimator == null)
                {
                    return;
                }
            }

            var state = animationLockAnimator.GetCurrentAnimatorStateInfo(0);
            var inCastState = IsCastAnimatorState(state);
            if (!animatorCastStateObserved)
            {
                if (inCastState)
                {
                    animatorCastStateObserved = true;
                }

                return;
            }

            if (inCastState)
            {
                return;
            }

            ReleaseAnimatorCastLock(true);
        }

        private void HandleAnimationCastStateEntered()
        {
            if (!animatorCastLockActive)
            {
                return;
            }

            animatorCastStateObserved = true;
        }

        private void HandleAnimationCastStateExited()
        {
            if (!animatorCastLockActive)
            {
                return;
            }

            animatorCastStateObserved = true;
            ReleaseAnimatorCastLock(true);
        }

        private void ReleaseAnimatorCastLock(bool applyEndBuffer)
        {
            if (!animatorCastLockActive)
            {
                return;
            }

            animatorCastLockActive = false;
            animatorCastStateObserved = false;
            animatorCastLockDeadline = 0f;

            if (applyEndBuffer)
            {
                ExtendMovementLock(castEndMovementLockBuffer);
            }
        }

        private void TryBindAnimationEventProxy()
        {
            if (!lockMovementByAnimationSignals)
            {
                UnbindAnimationEventProxy();
                return;
            }

            if (animationLockAnimator == null)
            {
                animationLockAnimator = GetComponentInChildren<Animator>();
            }

            if (animationLockAnimator == null)
            {
                UnbindAnimationEventProxy();
                return;
            }

            var resolvedProxy = animationLockAnimator.GetComponent<UnitAnimationEventProxy>();
            if (resolvedProxy == null)
            {
                resolvedProxy = animationLockAnimator.GetComponentInChildren<UnitAnimationEventProxy>(true);
            }

            if (resolvedProxy == null)
            {
                resolvedProxy = animationLockAnimator.GetComponentInParent<UnitAnimationEventProxy>();
            }

            if (resolvedProxy == null)
            {
                resolvedProxy = animationLockAnimator.gameObject.AddComponent<UnitAnimationEventProxy>();
            }

            if (ReferenceEquals(resolvedProxy, animationEventProxy))
            {
                return;
            }

            UnbindAnimationEventProxy();
            animationEventProxy = resolvedProxy;
            animationEventProxy.CastStateEntered += HandleAnimationCastStateEntered;
            animationEventProxy.CastStateExited += HandleAnimationCastStateExited;
        }

        private void UnbindAnimationEventProxy()
        {
            if (animationEventProxy == null)
            {
                return;
            }

            animationEventProxy.CastStateEntered -= HandleAnimationCastStateEntered;
            animationEventProxy.CastStateExited -= HandleAnimationCastStateExited;
            animationEventProxy = null;
        }

        private bool IsCastAnimatorState(AnimatorStateInfo state)
        {
            for (int i = 0; i < castStateNameHashes.Length; i++)
            {
                if (state.shortNameHash == castStateNameHashes[i])
                {
                    return true;
                }
            }

            if (castStateTags == null)
            {
                return false;
            }

            for (int i = 0; i < castStateTags.Length; i++)
            {
                var tag = castStateTags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                if (state.IsTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheCastStateNameHashes()
        {
            if (castStateNames == null || castStateNames.Length == 0)
            {
                castStateNameHashes = Array.Empty<int>();
                return;
            }

            var hashes = new List<int>(castStateNames.Length);
            for (int i = 0; i < castStateNames.Length; i++)
            {
                var stateName = castStateNames[i];
                if (string.IsNullOrWhiteSpace(stateName))
                {
                    continue;
                }

                hashes.Add(Animator.StringToHash(stateName.Trim()));
            }

            castStateNameHashes = hashes.Count > 0 ? hashes.ToArray() : Array.Empty<int>();
        }

        private float ResolveTelegraphRadius(SkillDefinition skill)
        {
            if (skill == null || skill.Targeting == null)
            {
                return Mathf.Max(0.5f, fallbackTelegraphRadius);
            }

            if (skill.Targeting.Radius > 0f)
            {
                return Mathf.Max(0.5f, skill.Targeting.Radius);
            }

            if (skill.Targeting.Range > 0f)
            {
                return Mathf.Max(0.5f, skill.Targeting.Range * 0.5f);
            }

            return Mathf.Max(0.5f, fallbackTelegraphRadius);
        }
    }

    [Serializable]
    public class BossSkillCycleEntry
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private float telegraphDuration = 0.8f;
        [SerializeField] private float castRetryWindow = 1.5f;
        [SerializeField] private float delayAfterCast = 1f;
        [SerializeField] private float delayOnFail = 0.4f;
        [SerializeField] private bool requireTarget = true;

        public SkillDefinition Skill => skill;
        public float TelegraphDuration => telegraphDuration;
        public float CastRetryWindow => castRetryWindow;
        public float DelayAfterCast => delayAfterCast;
        public float DelayOnFail => delayOnFail;
        public bool RequireTarget => requireTarget;
    }

    public readonly struct BossTelegraphEvent
    {
        public readonly SkillDefinition Skill;
        public readonly Vector3 Position;
        public readonly float Duration;

        public BossTelegraphEvent(SkillDefinition skill, Vector3 position, float duration)
        {
            Skill = skill;
            Position = position;
            Duration = duration;
        }
    }
}
