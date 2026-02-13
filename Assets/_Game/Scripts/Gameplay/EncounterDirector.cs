using System;
using System.Collections;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 遭遇运行时控制器：按 EncounterDefinition 刷怪并跟踪清场状态。
    /// </summary>
    public class EncounterDirector : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private EncounterDefinition encounter;
        [SerializeField] private Transform spawnCenter;
        [SerializeField] private Transform spawnParent;
        [SerializeField] private int enemyTeamId = 2;
        [SerializeField] private bool spawnOnEnable = true;
        [SerializeField] private bool clearPreviousSpawns = true;
        [SerializeField] private bool autoRespawnWhenCleared;
        [SerializeField] private float respawnDelay = 2f;
        [SerializeField] private bool verboseLogs;

        private readonly List<GameObject> spawnedUnits = new List<GameObject>(32);
        private readonly HashSet<HealthComponent> aliveUnits = new HashSet<HealthComponent>();
        private System.Random random;
        private bool respawnPending;

        public event Action<EncounterDirector> EncounterCleared;

        public EncounterDefinition Encounter => encounter;
        public int SpawnedCount => spawnedUnits.Count;
        public int AliveCount => aliveUnits.Count;

        private void OnEnable()
        {
            if (spawnOnEnable)
            {
                SpawnEncounter();
            }
        }

        private void OnDisable()
        {
            UnsubscribeAliveUnits();
            respawnPending = false;
        }

        public void SetEncounter(EncounterDefinition definition)
        {
            encounter = definition;
        }

        public void SpawnEncounter()
        {
            if (encounter == null)
            {
                if (verboseLogs)
                {
                    Debug.LogWarning("[EncounterDirector] Encounter is null.", this);
                }

                return;
            }

            if (clearPreviousSpawns)
            {
                ClearSpawnedUnits(true);
            }

            EnsureRandom();

            var waves = encounter.Waves;
            for (int i = 0; i < waves.Count; i++)
            {
                SpawnWave(waves[i]);
            }

            CheckEncounterCleared();
        }

        public void ClearSpawnedUnits(bool destroyInstances)
        {
            UnsubscribeAliveUnits();

            for (int i = spawnedUnits.Count - 1; i >= 0; i--)
            {
                var instance = spawnedUnits[i];
                if (instance == null)
                {
                    continue;
                }

                if (!destroyInstances)
                {
                    instance.SetActive(false);
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
                    DestroyImmediate(instance);
                }
            }

            spawnedUnits.Clear();
            aliveUnits.Clear();
        }

        private void SpawnWave(EncounterWaveDefinition wave)
        {
            if (wave == null)
            {
                return;
            }

            var spawnCount = ResolveSpawnCount(wave);
            if (spawnCount <= 0)
            {
                return;
            }

            var guaranteedEliteLeft = Mathf.Min(spawnCount, wave.GuaranteedEliteCount);
            for (int i = 0; i < spawnCount; i++)
            {
                var instance = SpawnSingleUnit(wave);
                if (instance == null)
                {
                    continue;
                }

                var isElite = guaranteedEliteLeft > 0 || RollChance(wave.EliteChance);
                if (isElite)
                {
                    guaranteedEliteLeft = Mathf.Max(0, guaranteedEliteLeft - 1);
                    var affix = SelectEliteAffix(wave);
                    EnemyAffixRuntime.Apply(instance, affix);
                }

                TrackSpawnedUnit(instance);
            }
        }

        private int ResolveSpawnCount(EncounterWaveDefinition wave)
        {
            var min = Mathf.Max(0, wave.MinCount);
            var max = Mathf.Max(min, wave.MaxCount);
            if (max == min)
            {
                return max;
            }

            return random.Next(min, max + 1);
        }

        private GameObject SpawnSingleUnit(EncounterWaveDefinition wave)
        {
            var prefab = wave.PrefabOverride != null
                ? wave.PrefabOverride
                : wave.Unit != null
                    ? wave.Unit.Prefab
                    : null;

            if (prefab == null)
            {
                if (verboseLogs)
                {
                    Debug.LogWarning($"[EncounterDirector] Wave '{wave.WaveId}' has no prefab.", this);
                }

                return null;
            }

            var center = spawnCenter != null ? spawnCenter.position : transform.position;
            var radius = encounter != null ? encounter.SpawnRadius : 8f;
            var position = center + SampleOffset(radius);
            var parent = spawnParent != null ? spawnParent : transform;

            var instance = Instantiate(prefab, position, Quaternion.identity, parent);
            instance.name = $"{prefab.name}_{wave.WaveId}_{spawnedUnits.Count + 1}";

            InitializeSpawnedUnit(instance, wave.Unit);
            ApplyTeam(instance);
            return instance;
        }

        private void InitializeSpawnedUnit(GameObject instance, UnitDefinition unitDefinition)
        {
            if (instance == null || unitDefinition == null)
            {
                return;
            }

            var unitRoot = instance.GetComponent<UnitRoot>();
            if (unitRoot != null)
            {
                unitRoot.Initialize(unitDefinition);
            }
            else
            {
                var stats = instance.GetComponent<StatsComponent>();
                if (stats != null)
                {
                    stats.Initialize(unitDefinition);
                }

                var tags = instance.GetComponent<UnitTagsComponent>();
                if (tags != null)
                {
                    tags.Initialize(unitDefinition);
                }
            }

            var skillUser = instance.GetComponent<SkillUserComponent>();
            if (skillUser != null)
            {
                skillUser.Initialize(unitDefinition);
            }

            var health = instance.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.Initialize();
            }

            var resource = instance.GetComponent<ResourceComponent>();
            if (resource != null)
            {
                resource.Initialize();
            }
        }

        private void ApplyTeam(GameObject instance)
        {
            var team = instance.GetComponent<TeamComponent>();
            if (team != null)
            {
                team.SetTeamId(enemyTeamId);
            }
        }

        private void TrackSpawnedUnit(GameObject instance)
        {
            spawnedUnits.Add(instance);

            var health = instance != null ? instance.GetComponent<HealthComponent>() : null;
            if (health == null || !health.IsAlive)
            {
                return;
            }

            if (!aliveUnits.Add(health))
            {
                return;
            }

            health.Died += HandleSpawnedUnitDied;
        }

        private void HandleSpawnedUnitDied(HealthComponent source)
        {
            if (source == null)
            {
                return;
            }

            source.Died -= HandleSpawnedUnitDied;
            if (!aliveUnits.Remove(source))
            {
                return;
            }

            CheckEncounterCleared();
        }

        private void CheckEncounterCleared()
        {
            if (aliveUnits.Count > 0)
            {
                return;
            }

            EncounterCleared?.Invoke(this);
            if (autoRespawnWhenCleared && isActiveAndEnabled && !respawnPending)
            {
                StartCoroutine(RespawnAfterDelay());
            }
        }

        private IEnumerator RespawnAfterDelay()
        {
            respawnPending = true;
            var delay = Mathf.Max(0f, respawnDelay);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            respawnPending = false;
            SpawnEncounter();
        }

        private EnemyAffixDefinition SelectEliteAffix(EncounterWaveDefinition wave)
        {
            var waveAffixes = wave.EliteAffixes;
            if (waveAffixes != null && waveAffixes.Count > 0)
            {
                return PickRandomAffix(waveAffixes);
            }

            if (encounter != null && encounter.GlobalEliteAffixes.Count > 0)
            {
                return PickRandomAffix(encounter.GlobalEliteAffixes);
            }

            return null;
        }

        private EnemyAffixDefinition PickRandomAffix(IReadOnlyList<EnemyAffixDefinition> affixes)
        {
            if (affixes == null || affixes.Count == 0)
            {
                return null;
            }

            var index = random.Next(0, affixes.Count);
            return affixes[index];
        }

        private Vector3 SampleOffset(float radius)
        {
            var angle = (float)(random.NextDouble() * Math.PI * 2d);
            var distance = Mathf.Sqrt((float)random.NextDouble()) * Mathf.Max(0.1f, radius);
            return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        }

        private bool RollChance(float chance)
        {
            return random.NextDouble() <= Mathf.Clamp01(chance);
        }

        private void EnsureRandom()
        {
            var seed = encounter != null ? encounter.RandomSeed : 0;
            if (seed == 0)
            {
                seed = unchecked(Environment.TickCount + GetInstanceID());
            }

            random = new System.Random(seed);
        }

        private void UnsubscribeAliveUnits()
        {
            foreach (var health in aliveUnits)
            {
                if (health != null)
                {
                    health.Died -= HandleSpawnedUnitDied;
                }
            }
        }
    }
}
