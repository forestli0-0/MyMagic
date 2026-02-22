using System.Collections.Generic;
using System.IO;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CombatSystem.EditorTools
{
    /// <summary>
    /// Act1 章节布局整理工具：
    /// Town(中转) -> Field(多遭遇区) -> Boss(单 Boss 区)。
    /// </summary>
    public static class Act1ChapterSetupUtility
    {
        private const string TownScenePath = "Assets/Scenes/Town.unity";
        private const string FieldScenePath = "Assets/Scenes/Field.unity";
        private const string BossScenePath = "Assets/Scenes/Boss.unity";

        private const string FieldEncounterPath = "Assets/_Game/ScriptableObjects/Encounters/Encounter_Field_Act1.asset";
        private const string BossEncounterPath = "Assets/_Game/ScriptableObjects/Encounters/Encounter_Boss_Act1.asset";

        [MenuItem("Combat/Chapter/Setup Act1 Layout (Town + Field + Boss)")]
        public static void SetupAct1Layout()
        {
            SetupScene(TownScenePath, SetupTownScene);
            SetupScene(FieldScenePath, SetupFieldScene);
            SetupScene(BossScenePath, SetupBossScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Act1] Chapter layout setup complete.");
        }

        [MenuItem("Combat/Chapter/Setup Current Scene As Chapter Layout")]
        public static void SetupCurrentSceneLayout()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Act1] No active scene.");
                return;
            }

            SceneManager.SetActiveScene(scene);

            switch (scene.name)
            {
                case "Town":
                    SetupTownScene(scene);
                    break;
                case "Field":
                    SetupFieldScene(scene);
                    break;
                case "Boss":
                    SetupBossScene(scene);
                    break;
                default:
                    Debug.LogWarning($"[Act1] Scene '{scene.name}' is not in Act1 setup list (Town/Field/Boss).");
                    return;
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Act1] Applied chapter layout to '{scene.name}'.");
        }

        private static void SetupScene(string scenePath, System.Action<Scene> setupAction)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Act1] Scene not found: {scenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            setupAction(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetupTownScene(Scene scene)
        {
            DisableLegacySampleEnemies();
            ClearLegacyEncounterLayout();

            ConfigureLevelFlowDefaults("Level_Town", "Start");
            RebuildSpawnPoints(new Vector3(0f, 0f, -16f), new Vector3(2f, 0f, -16f), Quaternion.Euler(0f, 0f, 0f));

            RebuildPortals(new[]
            {
                new PortalSeed("Portal_To_Field", "Level_Field", "Start", new Vector3(0f, 0f, 16f), new Vector3(3f, 2f, 3f))
            });

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void SetupFieldScene(Scene scene)
        {
            var fieldEncounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(FieldEncounterPath);
            if (fieldEncounter == null)
            {
                Debug.LogWarning($"[Act1] Missing encounter asset: {FieldEncounterPath}");
            }

            DisableLegacySampleEnemies();
            ClearLegacyEncounterLayout();

            ConfigureLevelFlowDefaults("Level_Field", "Start");
            RebuildSpawnPoints(new Vector3(-32f, 0f, 0f), new Vector3(-34f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

            var portals = RebuildPortals(new[]
            {
                new PortalSeed("Portal_To_Town", "Level_Town", "Return", new Vector3(-38f, 0f, 0f), new Vector3(3f, 2f, 3f)),
                new PortalSeed("Portal_To_Boss", "Level_Boss", "Start", new Vector3(38f, 0f, 0f), new Vector3(3f, 2f, 3f))
            });

            var directors = BuildEncounterZones(
                "Chapter_Encounters",
                fieldEncounter,
                new[]
                {
                    new EncounterZoneSeed("Zone_Field_A", new Vector3(-14f, 0f, 8f), 9f),
                    new EncounterZoneSeed("Zone_Field_B", new Vector3(4f, 0f, -10f), 9f),
                    new EncounterZoneSeed("Zone_Field_C", new Vector3(22f, 0f, 8f), 9f)
                });

            if (portals.TryGetValue("Portal_To_Boss", out var bossPortal))
            {
                BuildProgressGate("Chapter_FieldGate", directors, bossPortal);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void SetupBossScene(Scene scene)
        {
            var bossEncounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(BossEncounterPath);
            if (bossEncounter == null)
            {
                Debug.LogWarning($"[Act1] Missing encounter asset: {BossEncounterPath}");
            }

            DisableLegacySampleEnemies();
            ClearLegacyEncounterLayout();

            ConfigureLevelFlowDefaults("Level_Boss", "Start");
            RebuildSpawnPoints(new Vector3(-20f, 0f, 0f), new Vector3(-22f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

            var portals = RebuildPortals(new[]
            {
                new PortalSeed("Portal_To_Town", "Level_Town", "Return", new Vector3(-28f, 0f, 0f), new Vector3(3f, 2f, 3f))
            });

            var directors = BuildEncounterZones(
                "Chapter_Encounters",
                bossEncounter,
                new[]
                {
                    new EncounterZoneSeed("Zone_Boss_Main", new Vector3(8f, 0f, 0f), 12f)
                });

            if (portals.TryGetValue("Portal_To_Town", out var townPortal))
            {
                BuildProgressGate("Chapter_BossGate", directors, townPortal);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureLevelFlowDefaults(string startLevelId, string startSpawnId)
        {
            var flow = Object.FindFirstObjectByType<LevelFlowController>();
            if (flow == null)
            {
                return;
            }

            var serialized = new SerializedObject(flow);
            serialized.FindProperty("startLevelId").stringValue = startLevelId;
            serialized.FindProperty("startSpawnId").stringValue = startSpawnId;
            serialized.FindProperty("persistAcrossScenes").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);
        }

        private static void DisableLegacySampleEnemies()
        {
            var legacy = GameObject.Find("Sample_Enemies");
            if (legacy != null)
            {
                legacy.SetActive(false);
            }
        }

        private static void ClearLegacyEncounterLayout()
        {
            DestroyRootIfExists("Chapter_Encounters");
            DestroyRootIfExists("Chapter_FieldGate");
            DestroyRootIfExists("Chapter_BossGate");
            DestroyRootIfExists("Encounter_RuntimeUnits");
            DestroyRootIfExists("EncounterSpawnCenter");

            var legacyDirectors = Object.FindObjectsByType<EncounterDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < legacyDirectors.Length; i++)
            {
                var director = legacyDirectors[i];
                if (director == null)
                {
                    continue;
                }

                Object.DestroyImmediate(director.gameObject);
            }

            var legacyZones = Object.FindObjectsByType<EncounterZoneTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < legacyZones.Length; i++)
            {
                if (legacyZones[i] != null)
                {
                    Object.DestroyImmediate(legacyZones[i]);
                }
            }

            var legacyGates = Object.FindObjectsByType<EncounterProgressGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < legacyGates.Length; i++)
            {
                if (legacyGates[i] != null)
                {
                    Object.DestroyImmediate(legacyGates[i]);
                }
            }
        }

        private static void RebuildSpawnPoints(Vector3 startPosition, Vector3 returnPosition, Quaternion rotation)
        {
            var root = GetOrCreateRoot("LevelSpawns");
            ClearChildren(root.transform);

            CreateSpawnPoint(root.transform, "Spawn_Start", "Start", true, false, startPosition, rotation);
            CreateSpawnPoint(root.transform, "Spawn_Return", "Return", false, true, returnPosition, rotation);
        }

        private static Dictionary<string, LevelPortal> RebuildPortals(IReadOnlyList<PortalSeed> seeds)
        {
            var result = new Dictionary<string, LevelPortal>(8);
            var root = GetOrCreateRoot("LevelPortals");
            ClearChildren(root.transform);

            for (var i = 0; i < seeds.Count; i++)
            {
                var seed = seeds[i];
                var portalObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                portalObject.name = seed.Name;
                portalObject.transform.SetParent(root.transform, false);
                portalObject.transform.position = seed.Position;
                portalObject.transform.localScale = seed.Scale;

                var collider = portalObject.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.isTrigger = true;
                }

                var body = portalObject.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = portalObject.AddComponent<Rigidbody>();
                }

                body.isKinematic = true;
                body.useGravity = false;

                var portal = portalObject.GetComponent<LevelPortal>();
                if (portal == null)
                {
                    portal = portalObject.AddComponent<LevelPortal>();
                }

                var serialized = new SerializedObject(portal);
                serialized.FindProperty("targetLevelId").stringValue = seed.TargetLevelId;
                serialized.FindProperty("targetSpawnId").stringValue = seed.TargetSpawnId;
                serialized.FindProperty("requirePlayerTag").boolValue = true;
                serialized.FindProperty("playerTag").stringValue = "Player";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                result[seed.Name] = portal;
            }

            return result;
        }

        private static List<EncounterDirector> BuildEncounterZones(string rootName, EncounterDefinition encounter, IReadOnlyList<EncounterZoneSeed> zones)
        {
            var directors = new List<EncounterDirector>(zones.Count);
            var root = GetOrCreateRoot(rootName);
            ClearChildren(root.transform);

            for (var i = 0; i < zones.Count; i++)
            {
                var zoneSeed = zones[i];
                var zone = new GameObject(zoneSeed.Name);
                zone.transform.SetParent(root.transform, false);
                zone.transform.position = zoneSeed.Center;

                var triggerCollider = zone.AddComponent<SphereCollider>();
                triggerCollider.isTrigger = true;
                triggerCollider.radius = Mathf.Max(1f, zoneSeed.Radius);

                var trigger = zone.AddComponent<EncounterZoneTrigger>();

                var spawnCenter = new GameObject("SpawnCenter");
                spawnCenter.transform.SetParent(zone.transform, false);
                spawnCenter.transform.localPosition = Vector3.zero;

                var runtimeUnits = new GameObject("RuntimeUnits");
                runtimeUnits.transform.SetParent(zone.transform, false);
                runtimeUnits.transform.localPosition = Vector3.zero;

                var directorObject = new GameObject("EncounterDirector");
                directorObject.transform.SetParent(zone.transform, false);
                directorObject.transform.localPosition = Vector3.zero;

                var director = directorObject.AddComponent<EncounterDirector>();
                ConfigureEncounterDirector(director, encounter, spawnCenter.transform, runtimeUnits.transform);

                var triggerSerialized = new SerializedObject(trigger);
                triggerSerialized.FindProperty("encounterDirector").objectReferenceValue = director;
                triggerSerialized.FindProperty("triggerOnce").boolValue = true;
                triggerSerialized.FindProperty("requirePlayerTag").boolValue = true;
                triggerSerialized.FindProperty("playerTag").stringValue = "Player";
                triggerSerialized.FindProperty("disableColliderAfterTrigger").boolValue = true;
                triggerSerialized.FindProperty("verboseLogs").boolValue = false;
                triggerSerialized.ApplyModifiedPropertiesWithoutUndo();

                directors.Add(director);
            }

            return directors;
        }

        private static void ConfigureEncounterDirector(EncounterDirector director, EncounterDefinition encounter, Transform spawnCenter, Transform spawnParent)
        {
            var serialized = new SerializedObject(director);
            serialized.FindProperty("encounter").objectReferenceValue = encounter;
            serialized.FindProperty("spawnCenter").objectReferenceValue = spawnCenter;
            serialized.FindProperty("spawnParent").objectReferenceValue = spawnParent;
            serialized.FindProperty("enemyTeamId").intValue = 2;
            serialized.FindProperty("spawnOnEnable").boolValue = false;
            serialized.FindProperty("clearPreviousSpawns").boolValue = true;
            serialized.FindProperty("autoRespawnWhenCleared").boolValue = false;
            serialized.FindProperty("respawnDelay").floatValue = 0f;
            serialized.FindProperty("verboseLogs").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildProgressGate(string gateName, IReadOnlyList<EncounterDirector> directors, LevelPortal targetPortal)
        {
            if (targetPortal == null)
            {
                return;
            }

            var gateRoot = GetOrCreateRoot(gateName);
            gateRoot.transform.position = targetPortal.transform.position;

            var gate = gateRoot.GetComponent<EncounterProgressGate>();
            if (gate == null)
            {
                gate = gateRoot.AddComponent<EncounterProgressGate>();
            }

            var serialized = new SerializedObject(gate);
            serialized.FindProperty("gatedPortal").objectReferenceValue = targetPortal;
            serialized.FindProperty("gatedPortalCollider").objectReferenceValue = targetPortal.GetComponent<Collider>();
            serialized.FindProperty("lockPortalOnEnable").boolValue = true;
            serialized.FindProperty("autoFindPortalFromChildren").boolValue = false;
            serialized.FindProperty("verboseLogs").boolValue = false;

            var list = serialized.FindProperty("encounterDirectors");
            list.ClearArray();
            for (var i = 0; i < directors.Count; i++)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = directors[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing;
            }

            return new GameObject(name);
        }

        private static void DestroyRootIfExists(string name)
        {
            var root = GameObject.Find(name);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.GetChild(i).gameObject);
            }
        }

        private static void CreateSpawnPoint(
            Transform parent,
            string objectName,
            string spawnId,
            bool isDefault,
            bool isReturn,
            Vector3 position,
            Quaternion rotation)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;

            var spawn = go.AddComponent<LevelSpawnPoint>();
            var serialized = new SerializedObject(spawn);
            serialized.FindProperty("spawnId").stringValue = spawnId;
            serialized.FindProperty("isDefault").boolValue = isDefault;
            serialized.FindProperty("isReturn").boolValue = isReturn;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private readonly struct PortalSeed
        {
            public readonly string Name;
            public readonly string TargetLevelId;
            public readonly string TargetSpawnId;
            public readonly Vector3 Position;
            public readonly Vector3 Scale;

            public PortalSeed(string name, string targetLevelId, string targetSpawnId, Vector3 position, Vector3 scale)
            {
                Name = name;
                TargetLevelId = targetLevelId;
                TargetSpawnId = targetSpawnId;
                Position = position;
                Scale = scale;
            }
        }

        private readonly struct EncounterZoneSeed
        {
            public readonly string Name;
            public readonly Vector3 Center;
            public readonly float Radius;

            public EncounterZoneSeed(string name, Vector3 center, float radius)
            {
                Name = name;
                Center = center;
                Radius = radius;
            }
        }
    }
}
