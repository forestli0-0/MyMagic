using System.Collections.Generic;
using System.IO;
using CombatSystem.AI;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.EditorTools
{
    /// <summary>
    /// Day6 一键搭建工具：敌人遭遇、精英词缀与 Boss 技能节奏。
    /// </summary>
    public static class Day6SetupUtility
    {
        private const string EncounterFolder = "Assets/_Game/ScriptableObjects/Encounters";
        private const string EnemyAffixFolder = "Assets/_Game/ScriptableObjects/EnemyAffixes";

        private const string EncounterFieldPath = "Assets/_Game/ScriptableObjects/Encounters/Encounter_Field_Act1.asset";
        private const string EncounterBossPath = "Assets/_Game/ScriptableObjects/Encounters/Encounter_Boss_Act1.asset";
        private const string AffixBerserkerPath = "Assets/_Game/ScriptableObjects/EnemyAffixes/EnemyAffix_Berserker.asset";
        private const string AffixJuggernautPath = "Assets/_Game/ScriptableObjects/EnemyAffixes/EnemyAffix_Juggernaut.asset";

        private const string UnitEnemyPath = "Assets/_Game/ScriptableObjects/Units/Unit_Enemy.asset";
        private const string UnitEnemyBossPath = "Assets/_Game/ScriptableObjects/Units/Unit_Enemy_high_hp.asset";
        private const string DatabasePath = "Assets/_Game/ScriptableObjects/Database/GameDatabase.asset";

        private const string EnemyPrefabPath = "Assets/_Game/Prefabs/Enemy_Default.prefab";
        private const string BossPrefabPath = "Assets/_Game/Prefabs/Enemy_Boss.prefab";

        [MenuItem("Combat/Day6/Setup Encounter Assets")]
        public static void SetupEncounterAssets()
        {
            EnsureFolder("Assets/_Game/ScriptableObjects", "Encounters");
            EnsureFolder("Assets/_Game/ScriptableObjects", "EnemyAffixes");

            var stats = CollectStatReferences();
            var berserker = CreateOrUpdateAffix(
                AffixBerserkerPath,
                "EnemyAffix_Berserker",
                "狂暴",
                new[]
                {
                    new StatModifierSeed(stats.AttackPower, 0f, 1.4f),
                    new StatModifierSeed(stats.MoveSpeed, 0f, 1.2f)
                },
                new Color(1f, 0.45f, 0.25f, 1f),
                1.08f);

            var juggernaut = CreateOrUpdateAffix(
                AffixJuggernautPath,
                "EnemyAffix_Juggernaut",
                "钢铁",
                new[]
                {
                    new StatModifierSeed(stats.MaxHealth, 0f, 1.9f),
                    new StatModifierSeed(stats.Armor, 12f, 1f)
                },
                new Color(0.45f, 0.85f, 1f, 1f),
                1.14f);

            var unitEnemy = AssetDatabase.LoadAssetAtPath<UnitDefinition>(UnitEnemyPath);
            var unitBoss = AssetDatabase.LoadAssetAtPath<UnitDefinition>(UnitEnemyBossPath);
            CreateOrUpdateEncounter(
                EncounterFieldPath,
                "Encounter_Field_Act1",
                "野外遭遇",
                10f,
                101,
                new[] { berserker, juggernaut },
                new[]
                {
                    new WaveSeed("field_wave_1", unitEnemy, 4, 6, 1, 0.25f, null),
                    new WaveSeed("field_wave_2", unitEnemy, 3, 5, 0, 0.3f, null)
                });

            CreateOrUpdateEncounter(
                EncounterBossPath,
                "Encounter_Boss_Act1",
                "守关首领",
                6f,
                202,
                new[] { juggernaut },
                new[]
                {
                    new WaveSeed("boss_wave", unitBoss, 1, 1, 1, 1f, new[] { juggernaut }),
                    new WaveSeed("boss_adds", unitEnemy, 2, 3, 0, 0.2f, new[] { berserker })
                });

            UpdateGameDatabase(berserker, juggernaut);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Day6] Encounter assets setup complete.");
        }

        [MenuItem("Combat/Day6/Setup Encounter Runtime (Current Scene)")]
        public static void SetupEncounterRuntimeForCurrentScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Day6] No active scene.");
                return;
            }

            SetupEncounterAssets();
            EnsureEnemyPrefabsAndBindUnits();

            var encounter = ResolveEncounterForScene(scene.name);
            if (encounter == null)
            {
                Debug.LogWarning($"[Day6] Encounter asset not found for scene '{scene.name}'.");
                return;
            }

            var director = Object.FindFirstObjectByType<EncounterDirector>();
            if (director == null)
            {
                var go = new GameObject("EncounterDirector");
                director = go.AddComponent<EncounterDirector>();
            }

            var spawnParent = GameObject.Find("Encounter_RuntimeUnits");
            if (spawnParent == null)
            {
                spawnParent = new GameObject("Encounter_RuntimeUnits");
            }

            var spawnCenter = GameObject.Find("EncounterSpawnCenter");
            if (spawnCenter == null)
            {
                spawnCenter = GameObject.Find("Sample_Enemies");
            }

            var serialized = new SerializedObject(director);
            serialized.FindProperty("encounter").objectReferenceValue = encounter;
            serialized.FindProperty("spawnCenter").objectReferenceValue = spawnCenter != null ? spawnCenter.transform : director.transform;
            serialized.FindProperty("spawnParent").objectReferenceValue = spawnParent.transform;
            serialized.FindProperty("enemyTeamId").intValue = 2;
            serialized.FindProperty("spawnOnEnable").boolValue = true;
            serialized.FindProperty("clearPreviousSpawns").boolValue = true;
            serialized.FindProperty("autoRespawnWhenCleared").boolValue = !IsBossScene(scene.name);
            serialized.FindProperty("respawnDelay").floatValue = IsBossScene(scene.name) ? 0f : 3f;
            serialized.FindProperty("verboseLogs").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);

            var legacyRoot = GameObject.Find("Sample_Enemies");
            if (legacyRoot != null)
            {
                legacyRoot.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeObject = director.gameObject;
            Debug.Log($"[Day6] Encounter runtime setup complete for scene '{scene.name}'.");
        }

        [MenuItem("Combat/Day6/Setup Encounter Runtime (Field + Boss)")]
        public static void SetupEncounterRuntimeForPlayableScenes()
        {
            SetupEncounterAssets();
            EnsureEnemyPrefabsAndBindUnits();

            SetupSceneEncounter("Assets/Scenes/Field.unity");
            SetupSceneEncounter("Assets/Scenes/Boss.unity");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Day6] Encounter runtime setup complete for Field + Boss.");
        }

        private static void EnsureEnemyPrefabsAndBindUnits()
        {
            EnsureFolder("Assets/_Game", "Prefabs");

            var baseEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (baseEnemyPrefab == null)
            {
                var source = GameObject.Find("Sample_Enemy_1");
                if (source == null)
                {
                    source = FindAnyEnemyCandidate();
                }

                if (source == null)
                {
                    Debug.LogWarning("[Day6] Cannot build enemy prefab. No sample enemy found in current scene.");
                    return;
                }

                baseEnemyPrefab = PrefabUtility.SaveAsPrefabAsset(source, EnemyPrefabPath);
            }

            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (bossPrefab == null)
            {
                AssetDatabase.CopyAsset(EnemyPrefabPath, BossPrefabPath);
                bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            }

            ConfigureBossPrefab(bossPrefab);
            BindUnitPrefab(UnitEnemyPath, baseEnemyPrefab);
            BindUnitPrefab(UnitEnemyBossPath, bossPrefab != null ? bossPrefab : baseEnemyPrefab);
        }

        private static void ConfigureBossPrefab(GameObject bossPrefab)
        {
            if (bossPrefab == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (root == null)
            {
                return;
            }

            var scheduler = root.GetComponent<BossSkillScheduler>();
            if (scheduler == null)
            {
                scheduler = root.AddComponent<BossSkillScheduler>();
            }

            var skillUser = root.GetComponent<SkillUserComponent>();
            var shockwave = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Game/ScriptableObjects/Skills/Skill_Shockwave.asset");
            var cleave = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Game/ScriptableObjects/Skills/Skill_Cleave.asset");
            var arcaneBolt = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Game/ScriptableObjects/Skills/Skill_ArcaneBolt.asset");

            var serialized = new SerializedObject(scheduler);
            serialized.FindProperty("skillUser").objectReferenceValue = skillUser;
            serialized.FindProperty("targetTag").stringValue = "Player";
            serialized.FindProperty("autoStart").boolValue = true;
            serialized.FindProperty("initialDelay").floatValue = 0.8f;
            serialized.FindProperty("retryInterval").floatValue = 0.15f;
            serialized.FindProperty("showGroundTelegraph").boolValue = true;
            serialized.FindProperty("fallbackTelegraphRadius").floatValue = 2.5f;

            var cycle = serialized.FindProperty("skillCycle");
            cycle.ClearArray();
            ConfigureBossCycleEntry(cycle, 0, shockwave, 1f, 1f);
            ConfigureBossCycleEntry(cycle, 1, arcaneBolt, 0.65f, 0.6f);
            ConfigureBossCycleEntry(cycle, 2, cleave, 0.45f, 0.9f);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void ConfigureBossCycleEntry(SerializedProperty cycle, int index, SkillDefinition skill, float telegraphDuration, float delayAfterCast)
        {
            if (cycle == null)
            {
                return;
            }

            cycle.arraySize = Mathf.Max(cycle.arraySize, index + 1);
            var entry = cycle.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("skill").objectReferenceValue = skill;
            entry.FindPropertyRelative("telegraphDuration").floatValue = telegraphDuration;
            entry.FindPropertyRelative("castRetryWindow").floatValue = 1.5f;
            entry.FindPropertyRelative("delayAfterCast").floatValue = delayAfterCast;
            entry.FindPropertyRelative("delayOnFail").floatValue = 0.4f;
            entry.FindPropertyRelative("requireTarget").boolValue = true;
        }

        private static void BindUnitPrefab(string unitPath, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(unitPath);
            if (unit == null)
            {
                return;
            }

            var so = new SerializedObject(unit);
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unit);
        }

        private static GameObject FindAnyEnemyCandidate()
        {
            var ai = Object.FindFirstObjectByType<CombatAIController>();
            if (ai != null)
            {
                return ai.gameObject;
            }

            var health = Object.FindFirstObjectByType<HealthComponent>();
            if (health != null && !health.CompareTag("Player"))
            {
                return health.gameObject;
            }

            return null;
        }

        private static void SetupSceneEncounter(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Day6] Scene not found: {scenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            SetupEncounterRuntimeForCurrentScene();
            EditorSceneManager.SaveScene(scene);
        }

        private static EncounterDefinition ResolveEncounterForScene(string sceneName)
        {
            var path = IsBossScene(sceneName) ? EncounterBossPath : EncounterFieldPath;
            return AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
        }

        private static bool IsBossScene(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName) && sceneName.ToLowerInvariant().Contains("boss");
        }

        private static EnemyAffixDefinition CreateOrUpdateAffix(
            string path,
            string id,
            string displayName,
            IReadOnlyList<StatModifierSeed> modifiers,
            Color tint,
            float scaleMultiplier)
        {
            var affix = AssetDatabase.LoadAssetAtPath<EnemyAffixDefinition>(path);
            if (affix == null)
            {
                affix = ScriptableObject.CreateInstance<EnemyAffixDefinition>();
                AssetDatabase.CreateAsset(affix, path);
            }

            var serialized = new SerializedObject(affix);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("tintColor").colorValue = tint;
            serialized.FindProperty("scaleMultiplier").floatValue = scaleMultiplier;

            var statProp = serialized.FindProperty("statModifiers");
            statProp.ClearArray();
            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    statProp.arraySize++;
                    var item = statProp.GetArrayElementAtIndex(statProp.arraySize - 1);
                    item.FindPropertyRelative("stat").objectReferenceValue = modifiers[i].Stat;
                    item.FindPropertyRelative("flatBonus").floatValue = modifiers[i].FlatBonus;
                    item.FindPropertyRelative("multiplier").floatValue = modifiers[i].Multiplier;
                }
            }

            serialized.FindProperty("bonusSkills").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(affix);
            return affix;
        }

        private static EncounterDefinition CreateOrUpdateEncounter(
            string path,
            string id,
            string displayName,
            float spawnRadius,
            int randomSeed,
            IReadOnlyList<EnemyAffixDefinition> globalAffixes,
            IReadOnlyList<WaveSeed> waves)
        {
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (encounter == null)
            {
                encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(encounter, path);
            }

            var serialized = new SerializedObject(encounter);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("spawnRadius").floatValue = spawnRadius;
            serialized.FindProperty("randomSeed").intValue = randomSeed;

            var global = serialized.FindProperty("globalEliteAffixes");
            global.ClearArray();
            if (globalAffixes != null)
            {
                for (int i = 0; i < globalAffixes.Count; i++)
                {
                    global.arraySize++;
                    global.GetArrayElementAtIndex(global.arraySize - 1).objectReferenceValue = globalAffixes[i];
                }
            }

            var waveArray = serialized.FindProperty("waves");
            waveArray.ClearArray();
            if (waves != null)
            {
                for (int i = 0; i < waves.Count; i++)
                {
                    waveArray.arraySize++;
                    var waveProp = waveArray.GetArrayElementAtIndex(waveArray.arraySize - 1);
                    waveProp.FindPropertyRelative("waveId").stringValue = waves[i].WaveId;
                    waveProp.FindPropertyRelative("unit").objectReferenceValue = waves[i].Unit;
                    waveProp.FindPropertyRelative("prefabOverride").objectReferenceValue = null;
                    waveProp.FindPropertyRelative("minCount").intValue = Mathf.Max(0, waves[i].MinCount);
                    waveProp.FindPropertyRelative("maxCount").intValue = Mathf.Max(waves[i].MinCount, waves[i].MaxCount);
                    waveProp.FindPropertyRelative("guaranteedEliteCount").intValue = Mathf.Max(0, waves[i].GuaranteedEliteCount);
                    waveProp.FindPropertyRelative("eliteChance").floatValue = Mathf.Clamp01(waves[i].EliteChance);

                    var affixArray = waveProp.FindPropertyRelative("eliteAffixes");
                    affixArray.ClearArray();
                    if (waves[i].EliteAffixes != null)
                    {
                        for (int j = 0; j < waves[i].EliteAffixes.Count; j++)
                        {
                            affixArray.arraySize++;
                            affixArray.GetArrayElementAtIndex(affixArray.arraySize - 1).objectReferenceValue = waves[i].EliteAffixes[j];
                        }
                    }
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(encounter);
            return encounter;
        }

        private static void UpdateGameDatabase(EnemyAffixDefinition affixA, EnemyAffixDefinition affixB)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogWarning("[Day6] GameDatabase not found.");
                return;
            }

            var fieldEncounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(EncounterFieldPath);
            var bossEncounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(EncounterBossPath);

            var serialized = new SerializedObject(database);
            AddToList(serialized.FindProperty("encounters"), fieldEncounter);
            AddToList(serialized.FindProperty("encounters"), bossEncounter);
            AddToList(serialized.FindProperty("enemyAffixes"), affixA);
            AddToList(serialized.FindProperty("enemyAffixes"), affixB);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            database.BuildIndexes();
            EditorUtility.SetDirty(database);
        }

        private static StatReferences CollectStatReferences()
        {
            return new StatReferences
            {
                MaxHealth = FindStatById("Stat_MaxHealth"),
                Armor = FindStatById(CombatStatIds.Armor),
                MoveSpeed = FindStatById(CombatStatIds.MoveSpeed),
                AttackPower = FindStatById("Stat_AttackPower")
            };
        }

        private static StatDefinition FindStatById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database != null)
            {
                database.BuildIndexes();
                var stat = database.GetStat(id);
                if (stat != null)
                {
                    return stat;
                }
            }

            var guids = AssetDatabase.FindAssets("t:StatDefinition", new[] { "Assets/_Game/ScriptableObjects/Stats" });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var stat = AssetDatabase.LoadAssetAtPath<StatDefinition>(path);
                if (stat != null && string.Equals(stat.Id, id, System.StringComparison.Ordinal))
                {
                    return stat;
                }
            }

            return null;
        }

        private static void AddToList(SerializedProperty list, Object value)
        {
            if (list == null || value == null)
            {
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                {
                    return;
                }
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private struct StatReferences
        {
            public StatDefinition MaxHealth;
            public StatDefinition Armor;
            public StatDefinition MoveSpeed;
            public StatDefinition AttackPower;
        }

        private readonly struct StatModifierSeed
        {
            public readonly StatDefinition Stat;
            public readonly float FlatBonus;
            public readonly float Multiplier;

            public StatModifierSeed(StatDefinition stat, float flatBonus, float multiplier)
            {
                Stat = stat;
                FlatBonus = flatBonus;
                Multiplier = multiplier;
            }
        }

        private readonly struct WaveSeed
        {
            public readonly string WaveId;
            public readonly UnitDefinition Unit;
            public readonly int MinCount;
            public readonly int MaxCount;
            public readonly int GuaranteedEliteCount;
            public readonly float EliteChance;
            public readonly IReadOnlyList<EnemyAffixDefinition> EliteAffixes;

            public WaveSeed(
                string waveId,
                UnitDefinition unit,
                int minCount,
                int maxCount,
                int guaranteedEliteCount,
                float eliteChance,
                IReadOnlyList<EnemyAffixDefinition> eliteAffixes)
            {
                WaveId = waveId;
                Unit = unit;
                MinCount = minCount;
                MaxCount = maxCount;
                GuaranteedEliteCount = guaranteedEliteCount;
                EliteChance = eliteChance;
                EliteAffixes = eliteAffixes;
            }
        }
    }
}
