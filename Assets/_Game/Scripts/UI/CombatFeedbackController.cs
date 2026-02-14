using CombatSystem.Core;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// 战斗反馈控制器：处理命中音效与相机轻抖。
    /// </summary>
    public class CombatFeedbackController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatEventHub eventHub;
        [SerializeField] private UnitRoot playerUnit;
        [SerializeField] private CameraShakeController cameraShake;
        [SerializeField] private AudioSource audioSource;

        [Header("Audio")]
        [SerializeField] private AudioClip[] enemyHitClips;
        [SerializeField] private AudioClip[] playerHitClips;
        [SerializeField] [Range(0f, 1f)] private float audioVolume = 0.75f;
        [SerializeField] [Range(0.7f, 1.2f)] private float minPitch = 0.95f;
        [SerializeField] [Range(0.8f, 1.3f)] private float maxPitch = 1.05f;
        [SerializeField] private bool useProceduralFallback = true;

        [Header("Camera Shake")]
        [SerializeField] private bool enforceMinimumShakeTuning = true;
        [SerializeField] private bool onlyWhenPlayerInvolved = true;
        [SerializeField] [Range(0f, 1f)] private float shakeOnEnemyHit = 0.1f;
        [SerializeField] [Range(0f, 1f)] private float shakeOnPlayerHit = 0.16f;
        [SerializeField] [Range(0f, 1f)] private float criticalBonus = 0.05f;
        [SerializeField] private float minEnemyDamageForShake = 1f;

        private HealthComponent playerHealth;
        private bool subscribed;
        private AudioClip fallbackEnemyHitClip;
        private AudioClip fallbackPlayerHitClip;

        private void Awake()
        {
            ApplyMinimumShakeTuningIfNeeded();
            EnsureReferences();
            EnsureAudioSource();
            CreateFallbackClipsIfNeeded();
        }

        private void OnEnable()
        {
            EnsureReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void EnsureReferences()
        {
            if (!PlayerUnitLocator.IsPlayerUnit(playerUnit))
            {
                playerUnit = null;
                playerHealth = null;
            }

            if (playerUnit == null)
            {
                playerUnit = ResolvePlayerUnit();
            }

            if (playerUnit != null)
            {
                if (eventHub == null)
                {
                    eventHub = playerUnit.EventHub;
                }

                if (playerHealth == null)
                {
                    playerHealth = playerUnit.GetComponent<HealthComponent>();
                }
            }

            if (eventHub == null)
            {
                var anyUnit = FindFirstObjectByType<UnitRoot>(FindObjectsInactive.Include);
                if (anyUnit != null)
                {
                    eventHub = anyUnit.EventHub;
                }
            }

            if (cameraShake == null && Camera.main != null)
            {
                cameraShake = Camera.main.GetComponent<CameraShakeController>();
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        private void Subscribe()
        {
            if (subscribed || eventHub == null)
            {
                return;
            }

            eventHub.DamageApplied += HandleDamageApplied;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || eventHub == null)
            {
                return;
            }

            eventHub.DamageApplied -= HandleDamageApplied;
            subscribed = false;
        }

        private void HandleDamageApplied(DamageAppliedEvent evt)
        {
            if (evt.TotalImpact <= 0f)
            {
                return;
            }

            if (!IsPlayerUnit(playerUnit) || playerHealth == null)
            {
                EnsureReferences();
            }

            var targetIsPlayer = IsPlayerHealth(evt.Target);
            var attackerIsPlayer = IsPlayerAttacker(evt.Attacker);
            var hasResolvedPlayer = playerHealth != null && IsPlayerUnit(playerUnit);

            if (onlyWhenPlayerInvolved && !targetIsPlayer && !attackerIsPlayer)
            {
                // 如果运行时还未能识别玩家单位，则降级为通用反馈，避免“完全没有音效/抖动”。
                if (hasResolvedPlayer)
                {
                    return;
                }
            }

            PlayHitAudio(targetIsPlayer);
            ApplyShake(evt, targetIsPlayer, attackerIsPlayer, hasResolvedPlayer);
        }

        private void PlayHitAudio(bool targetIsPlayer)
        {
            if (audioSource == null)
            {
                return;
            }

            var clip = SelectHitClip(targetIsPlayer);
            if (clip == null)
            {
                return;
            }

            var safeMinPitch = Mathf.Min(minPitch, maxPitch);
            var safeMaxPitch = Mathf.Max(minPitch, maxPitch);
            audioSource.pitch = Random.Range(safeMinPitch, safeMaxPitch);
            audioSource.PlayOneShot(clip, audioVolume);
        }

        private void ApplyShake(DamageAppliedEvent evt, bool targetIsPlayer, bool attackerIsPlayer, bool hasResolvedPlayer)
        {
            if (cameraShake == null)
            {
                return;
            }

            var shakeAmount = 0f;
            if (targetIsPlayer)
            {
                shakeAmount = shakeOnPlayerHit;
            }
            else if (attackerIsPlayer && evt.AppliedDamage >= minEnemyDamageForShake)
            {
                shakeAmount = shakeOnEnemyHit;
            }
            else if (!hasResolvedPlayer && evt.AppliedDamage >= minEnemyDamageForShake)
            {
                shakeAmount = shakeOnEnemyHit;
            }

            if (shakeAmount <= 0f)
            {
                return;
            }

            if (evt.IsCritical)
            {
                shakeAmount += criticalBonus;
            }

            cameraShake.Shake(shakeAmount);
        }

        private AudioClip SelectHitClip(bool targetIsPlayer)
        {
            var collection = targetIsPlayer ? playerHitClips : enemyHitClips;
            if (collection != null && collection.Length > 0)
            {
                var index = Random.Range(0, collection.Length);
                return collection[index];
            }

            if (!useProceduralFallback)
            {
                return null;
            }

            return targetIsPlayer ? fallbackPlayerHitClip : fallbackEnemyHitClip;
        }

        private void CreateFallbackClipsIfNeeded()
        {
            if (!useProceduralFallback)
            {
                return;
            }

            if (fallbackEnemyHitClip == null)
            {
                fallbackEnemyHitClip = CreateProceduralImpactClip("Impact_Enemy", 0.055f, 0.75f, 260f, 92717u);
            }

            if (fallbackPlayerHitClip == null)
            {
                fallbackPlayerHitClip = CreateProceduralImpactClip("Impact_Player", 0.075f, 1f, 120f, 193939u);
            }
        }

        private static AudioClip CreateProceduralImpactClip(string clipName, float duration, float amplitude, float toneFrequency, uint seed)
        {
            var sampleRate = 22050;
            var samples = Mathf.Max(256, Mathf.RoundToInt(sampleRate * duration));
            var data = new float[samples];
            var random = seed != 0u ? seed : 1u;

            for (int i = 0; i < samples; i++)
            {
                var time = i / (float)sampleRate;
                var envelope = Mathf.Exp(-36f * time);
                random ^= random << 13;
                random ^= random >> 17;
                random ^= random << 5;
                var noise = ((random & 1023u) / 511.5f) - 1f;
                var tone = Mathf.Sin(2f * Mathf.PI * toneFrequency * time);
                data[i] = (noise * 0.72f + tone * 0.28f) * envelope * amplitude;
            }

            var clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private UnitRoot ResolvePlayerUnit()
        {
            return PlayerUnitLocator.FindPlayerUnit();
        }

        private bool IsPlayerAttacker(UnitRoot attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            if (PlayerUnitLocator.IsPlayerUnit(playerUnit) && attacker == playerUnit)
            {
                return true;
            }

            return PlayerUnitLocator.IsPlayerUnit(attacker);
        }

        private bool IsPlayerHealth(HealthComponent health)
        {
            if (health == null)
            {
                return false;
            }

            if (playerHealth != null && health == playerHealth)
            {
                return true;
            }

            var unit = health.GetComponent<UnitRoot>();
            return PlayerUnitLocator.IsPlayerUnit(unit);
        }

        private bool IsPlayerUnit(UnitRoot unit)
        {
            return PlayerUnitLocator.IsPlayerUnit(unit);
        }

        private void ApplyMinimumShakeTuningIfNeeded()
        {
            if (!enforceMinimumShakeTuning)
            {
                return;
            }

            // 旧场景参数过小会导致几乎看不到抖动。
            shakeOnEnemyHit = Mathf.Max(shakeOnEnemyHit, 0.08f);
            shakeOnPlayerHit = Mathf.Max(shakeOnPlayerHit, 0.14f);
            criticalBonus = Mathf.Max(criticalBonus, 0.05f);
        }
    }
}
