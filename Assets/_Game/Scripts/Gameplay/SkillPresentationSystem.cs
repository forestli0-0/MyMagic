using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 技能表现系统：订阅机制事件并执行动画/VFX/SFX。
    /// </summary>
    public class SkillPresentationSystem : MonoBehaviour
    {
        private const string RuntimeObjectName = "SkillPresentationSystem_Runtime";

        [Header("References")]
        [SerializeField] private CombatEventHub eventHub;
        [SerializeField] private FxPool fxPool;
        [SerializeField] private AudioEmitterPool audioEmitterPool;

        [Header("Runtime")]
        [SerializeField] private bool enableStepEvents = true;
        [SerializeField] private bool enableEffectEvents = true;
        [SerializeField] private bool enableProjectileEvents = true;
        [SerializeField] private bool debugLogs;

        private readonly Dictionary<CueCacheKey, List<SkillPresentationCue>> cueCache = new Dictionary<CueCacheKey, List<SkillPresentationCue>>(128);
        private CombatEventHub subscribedEventHub;
        private static SkillPresentationSystem runtimeInstance;

        public static SkillPresentationSystem EnsureRuntimeInstance()
        {
            if (!Application.isPlaying)
            {
                return FindFirstObjectByType<SkillPresentationSystem>(FindObjectsInactive.Include);
            }

            if (runtimeInstance != null)
            {
                return runtimeInstance;
            }

            runtimeInstance = FindFirstObjectByType<SkillPresentationSystem>(FindObjectsInactive.Include);
            if (runtimeInstance != null)
            {
                return runtimeInstance;
            }

            var go = new GameObject(RuntimeObjectName);
            DontDestroyOnLoad(go);
            runtimeInstance = go.AddComponent<SkillPresentationSystem>();
            return runtimeInstance;
        }

        private void Awake()
        {
            runtimeInstance = this;
            ResolveDependencies();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            RebindEventHub();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            Unsubscribe();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            cueCache.Clear();
        }

        private void OnDestroy()
        {
            if (runtimeInstance == this)
            {
                runtimeInstance = null;
            }
        }

        private void LateUpdate()
        {
            if (eventHub == null || subscribedEventHub == null)
            {
                ResolveDependencies();
                RebindEventHub();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveDependencies();
            RebindEventHub();
        }

        private void RebindEventHub()
        {
            if (subscribedEventHub == eventHub)
            {
                return;
            }

            Unsubscribe();
            Subscribe();
            cueCache.Clear();
        }

        private void Subscribe()
        {
            if (eventHub == null)
            {
                return;
            }

            subscribedEventHub = eventHub;
            subscribedEventHub.SkillStepExecuted -= HandleSkillStepExecuted;
            subscribedEventHub.SkillStepExecuted += HandleSkillStepExecuted;

            subscribedEventHub.SkillEffectExecuted -= HandleSkillEffectExecuted;
            subscribedEventHub.SkillEffectExecuted += HandleSkillEffectExecuted;

            subscribedEventHub.ProjectileLifecycle -= HandleProjectileLifecycle;
            subscribedEventHub.ProjectileLifecycle += HandleProjectileLifecycle;
        }

        private void Unsubscribe()
        {
            if (subscribedEventHub == null)
            {
                return;
            }

            subscribedEventHub.SkillStepExecuted -= HandleSkillStepExecuted;
            subscribedEventHub.SkillEffectExecuted -= HandleSkillEffectExecuted;
            subscribedEventHub.ProjectileLifecycle -= HandleProjectileLifecycle;
            subscribedEventHub = null;
        }

        private void ResolveDependencies()
        {
            if (eventHub == null)
            {
                var units = FindObjectsByType<UnitRoot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < units.Length; i++)
                {
                    if (units[i] != null && units[i].EventHub != null)
                    {
                        eventHub = units[i].EventHub;
                        break;
                    }
                }
            }

            if (fxPool == null)
            {
                fxPool = FindFirstObjectByType<FxPool>();
                if (fxPool == null)
                {
                    var go = new GameObject("FxPool");
                    go.transform.SetParent(transform, false);
                    fxPool = go.AddComponent<FxPool>();
                }
            }

            if (audioEmitterPool == null)
            {
                audioEmitterPool = FindFirstObjectByType<AudioEmitterPool>();
                if (audioEmitterPool == null)
                {
                    var go = new GameObject("AudioEmitterPool");
                    go.transform.SetParent(transform, false);
                    audioEmitterPool = go.AddComponent<AudioEmitterPool>();
                }
            }
        }

        private void HandleSkillStepExecuted(SkillStepExecutedEvent evt)
        {
            if (!enableStepEvents)
            {
                return;
            }

            var cues = ResolveCues(evt.Skill, evt.Step, evt.StepIndex, PresentationEventType.StepExecuted);
            if (cues != null && cues.Count > 0)
            {
                for (int i = 0; i < cues.Count; i++)
                {
                    ExecuteCue(cues[i], evt);
                }
            }
        }

        private void HandleSkillEffectExecuted(SkillEffectExecutedEvent evt)
        {
            if (!enableEffectEvents)
            {
                return;
            }

            var step = ResolveStep(evt.Skill, evt.StepIndex);
            var cueEvent = evt.Phase == SkillEffectExecutionPhase.BeforeApply
                ? PresentationEventType.EffectBeforeApply
                : PresentationEventType.EffectAfterApply;
            var cues = ResolveCues(evt.Skill, step, evt.StepIndex, cueEvent);
            if (cues == null || cues.Count == 0)
            {
                return;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (cue == null)
                {
                    continue;
                }

                if (cue.filterByEffectType && evt.Effect != null && cue.effectTypeFilter != evt.Effect.EffectType)
                {
                    continue;
                }

                if (cue.effectFilter != null && cue.effectFilter != evt.Effect)
                {
                    continue;
                }

                ExecuteCue(cue, evt);
            }
        }

        private void HandleProjectileLifecycle(ProjectileLifecycleEvent evt)
        {
            if (!enableProjectileEvents)
            {
                return;
            }

            var cueEvent = MapProjectileEvent(evt.LifecycleType);
            var step = ResolveStep(evt.Skill, evt.StepIndex);
            var cues = ResolveCues(evt.Skill, step, evt.StepIndex, cueEvent);
            if (cues == null || cues.Count == 0)
            {
                return;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                ExecuteCue(cues[i], evt);
            }
        }

        private void ExecuteCue(SkillPresentationCue cue, SkillStepExecutedEvent evt)
        {
            if (cue == null || !cue.HasPayload)
            {
                return;
            }

            ResolveAnchor(cue, evt, out var anchor, out var worldPosition, out var worldRotation);
            PlayCue(cue, evt.Caster, anchor, worldPosition, worldRotation);
        }

        private void ExecuteCue(SkillPresentationCue cue, SkillEffectExecutedEvent evt)
        {
            if (cue == null || !cue.HasPayload)
            {
                return;
            }

            // 空目标效果事件仍可能存在，但依赖目标锚点的命中特效不应回退到错误坐标。
            if (RequiresValidTargetAnchor(cue.anchorType) && evt.Target.Transform == null)
            {
                return;
            }

            ResolveAnchor(cue, evt, out var anchor, out var worldPosition, out var worldRotation);
            PlayCue(cue, evt.Caster, anchor, worldPosition, worldRotation);
        }

        private void ExecuteCue(SkillPresentationCue cue, ProjectileLifecycleEvent evt)
        {
            if (cue == null || !cue.HasPayload)
            {
                return;
            }

            ResolveAnchor(cue, evt, out var anchor, out var worldPosition, out var worldRotation);
            PlayCue(cue, evt.Caster, anchor, worldPosition, worldRotation);
        }

        private void PlayCue(
            SkillPresentationCue cue,
            UnitRoot caster,
            Transform anchor,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            if (caster != null && !string.IsNullOrWhiteSpace(cue.animationTrigger))
            {
                var animator = caster.GetComponentInChildren<Animator>();
                animator?.SetTrigger(cue.animationTrigger);
            }

            if (cue.vfxPrefab != null && fxPool != null)
            {
                var parent = cue.followAnchor ? anchor : null;
                var instance = fxPool.Play(cue.vfxPrefab, worldPosition, worldRotation, parent, cue.maxLifetime);
                if (instance != null
                    && cue.spawnSpace == PresentationSpawnSpace.LocalToAnchor
                    && anchor != null)
                {
                    instance.transform.localPosition = cue.positionOffset;
                    instance.transform.localRotation = Quaternion.Euler(cue.rotationOffset);
                }
            }

            if (cue.sfx != null && audioEmitterPool != null)
            {
                var parent = cue.followAnchor ? anchor : null;
                audioEmitterPool.Play(
                    cue.sfx,
                    worldPosition,
                    cue.audioBus,
                    cue.audioVolume,
                    cue.audioPitch,
                    cue.audioSpatialBlend,
                    parent);
            }

            if (debugLogs)
            {
                Debug.Log($"[SkillPresentation] Played cue '{cue.cueId}'", this);
            }
        }

        private List<SkillPresentationCue> ResolveCues(
            SkillDefinition skill,
            SkillStep step,
            int stepIndex,
            PresentationEventType eventType)
        {
            if (skill == null || step == null || stepIndex < 0)
            {
                return null;
            }

            var key = new CueCacheKey(skill.GetInstanceID(), stepIndex, eventType);
            if (cueCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var source = step.presentationCues;
            if (source == null || source.Count == 0)
            {
                cueCache[key] = null;
                return null;
            }

            List<SkillPresentationCue> result = null;
            for (int i = 0; i < source.Count; i++)
            {
                var cue = source[i];
                if (cue == null || cue.eventType != eventType)
                {
                    continue;
                }

                if (result == null)
                {
                    result = new List<SkillPresentationCue>(4);
                }

                result.Add(cue);
            }

            cueCache[key] = result;
            return result;
        }

        private static SkillStep ResolveStep(SkillDefinition skill, int stepIndex)
        {
            if (skill == null || stepIndex < 0)
            {
                return null;
            }

            var steps = skill.Steps;
            if (steps == null || stepIndex >= steps.Count)
            {
                return null;
            }

            return steps[stepIndex];
        }

        private static PresentationEventType MapProjectileEvent(ProjectileLifecycleType lifecycleType)
        {
            switch (lifecycleType)
            {
                case ProjectileLifecycleType.Spawn:
                    return PresentationEventType.ProjectileSpawn;
                case ProjectileLifecycleType.Hit:
                    return PresentationEventType.ProjectileHit;
                case ProjectileLifecycleType.Return:
                    return PresentationEventType.ProjectileReturn;
                case ProjectileLifecycleType.Split:
                default:
                    return PresentationEventType.ProjectileSplit;
            }
        }

        private static Transform ResolveAnchorTransform(Transform anchorRoot, string anchorChildPath)
        {
            if (anchorRoot == null || string.IsNullOrWhiteSpace(anchorChildPath))
            {
                return anchorRoot;
            }

            var child = anchorRoot.Find(anchorChildPath);
            return child != null ? child : anchorRoot;
        }

        private static bool RequiresValidTargetAnchor(PresentationAnchorType anchorType)
        {
            return anchorType == PresentationAnchorType.PrimaryTarget
                || anchorType == PresentationAnchorType.ExplicitTarget;
        }

        private static void ResolveAnchor(
            SkillPresentationCue cue,
            SkillStepExecutedEvent evt,
            out Transform anchor,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            anchor = null;
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;

            switch (cue.anchorType)
            {
                case PresentationAnchorType.PrimaryTarget:
                    anchor = evt.PrimaryTarget.Transform;
                    break;
                case PresentationAnchorType.ExplicitTarget:
                    anchor = evt.ExplicitTarget != null ? evt.ExplicitTarget.transform : null;
                    break;
                case PresentationAnchorType.AimPoint:
                    worldPosition = evt.HasAimPoint ? evt.AimPoint : evt.Caster != null ? evt.Caster.transform.position : Vector3.zero;
                    worldRotation = evt.AimDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(evt.AimDirection) : Quaternion.identity;
                    break;
                case PresentationAnchorType.World:
                    worldPosition = cue.worldPosition;
                    worldRotation = Quaternion.identity;
                    break;
                case PresentationAnchorType.Caster:
                case PresentationAnchorType.Projectile:
                default:
                    anchor = evt.Caster != null ? evt.Caster.transform : null;
                    break;
            }

            ResolveAnchorResult(cue, anchor, ref worldPosition, ref worldRotation);
        }

        private static void ResolveAnchor(
            SkillPresentationCue cue,
            SkillEffectExecutedEvent evt,
            out Transform anchor,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            anchor = null;
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;

            switch (cue.anchorType)
            {
                case PresentationAnchorType.PrimaryTarget:
                case PresentationAnchorType.ExplicitTarget:
                    anchor = evt.Target.Transform;
                    break;
                case PresentationAnchorType.World:
                    worldPosition = cue.worldPosition;
                    worldRotation = Quaternion.identity;
                    break;
                case PresentationAnchorType.AimPoint:
                    worldPosition = evt.Target.Transform != null
                        ? evt.Target.Transform.position
                        : evt.Caster != null ? evt.Caster.transform.position : Vector3.zero;
                    break;
                case PresentationAnchorType.Projectile:
                case PresentationAnchorType.Caster:
                default:
                    anchor = evt.Caster != null ? evt.Caster.transform : null;
                    break;
            }

            ResolveAnchorResult(cue, anchor, ref worldPosition, ref worldRotation);
        }

        private static void ResolveAnchor(
            SkillPresentationCue cue,
            ProjectileLifecycleEvent evt,
            out Transform anchor,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            anchor = null;
            worldPosition = evt.Position;
            worldRotation = evt.Direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(evt.Direction) : Quaternion.identity;

            switch (cue.anchorType)
            {
                case PresentationAnchorType.Projectile:
                    anchor = evt.ProjectileObject != null ? evt.ProjectileObject.transform : null;
                    break;
                case PresentationAnchorType.PrimaryTarget:
                case PresentationAnchorType.ExplicitTarget:
                    anchor = evt.Target.Transform;
                    break;
                case PresentationAnchorType.World:
                    worldPosition = cue.worldPosition;
                    worldRotation = Quaternion.identity;
                    break;
                case PresentationAnchorType.AimPoint:
                    worldPosition = evt.Position;
                    break;
                case PresentationAnchorType.Caster:
                default:
                    anchor = evt.Caster != null ? evt.Caster.transform : null;
                    break;
            }

            ResolveAnchorResult(cue, anchor, ref worldPosition, ref worldRotation);
        }

        private static void ResolveAnchorResult(
            SkillPresentationCue cue,
            Transform anchor,
            ref Vector3 worldPosition,
            ref Quaternion worldRotation)
        {
            anchor = ResolveAnchorTransform(anchor, cue.anchorChildPath);

            if (anchor != null)
            {
                if (cue.spawnSpace == PresentationSpawnSpace.LocalToAnchor)
                {
                    worldPosition = anchor.TransformPoint(cue.positionOffset);
                }
                else
                {
                    worldPosition = anchor.position + cue.positionOffset;
                }

                worldRotation = anchor.rotation * Quaternion.Euler(cue.rotationOffset);
                return;
            }

            worldPosition += cue.positionOffset;
            worldRotation *= Quaternion.Euler(cue.rotationOffset);
        }

        private readonly struct CueCacheKey : IEquatable<CueCacheKey>
        {
            public readonly int SkillInstanceId;
            public readonly int StepIndex;
            public readonly PresentationEventType EventType;

            public CueCacheKey(int skillInstanceId, int stepIndex, PresentationEventType eventType)
            {
                SkillInstanceId = skillInstanceId;
                StepIndex = stepIndex;
                EventType = eventType;
            }

            public bool Equals(CueCacheKey other)
            {
                return SkillInstanceId == other.SkillInstanceId
                       && StepIndex == other.StepIndex
                       && EventType == other.EventType;
            }

            public override bool Equals(object obj)
            {
                return obj is CueCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = SkillInstanceId;
                    hash = (hash * 397) ^ StepIndex;
                    hash = (hash * 397) ^ (int)EventType;
                    return hash;
                }
            }
        }
    }
}
