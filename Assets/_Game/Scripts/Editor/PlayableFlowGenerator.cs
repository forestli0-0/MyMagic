using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using CombatSystem.Persistence;
using CombatSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.Editor
{
    public static class PlayableFlowGenerator
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MainMenuSceneName = "MainMenu";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ScenesFolder = "Assets/Scenes";
        private const string LevelsFolder = "Assets/_Game/ScriptableObjects/Levels";
        private const string DatabasePath = "Assets/_Game/ScriptableObjects/Database/GameDatabase.asset";

        private const string LevelTownId = "Level_Town";
        private const string LevelFieldId = "Level_Field";
        private const string LevelBossId = "Level_Boss";

        [MenuItem("Combat/Generate Playable Flow")]
        public static void GeneratePlayableFlow()
        {
            EnsureSampleScene();
            EnsureFolders();

            var levels = CreateOrUpdateLevels();
            UpdateDatabase(levels);

            EnsureMainMenuScene(levels[LevelTownId]);

            CreateLevelScene("Town", levels[LevelTownId], new[]
            {
                new PortalSeed("Portal_To_Field", LevelFieldId, "Start", new Vector3(6f, 0f, 0f))
            });

            CreateLevelScene("Field", levels[LevelFieldId], new[]
            {
                new PortalSeed("Portal_To_Town", LevelTownId, "Return", new Vector3(-6f, 0f, 0f)),
                new PortalSeed("Portal_To_Boss", LevelBossId, "Start", new Vector3(6f, 0f, 0f))
            });

            CreateLevelScene("Boss", levels[LevelBossId], new[]
            {
                new PortalSeed("Portal_To_Town", LevelTownId, "Return", new Vector3(0f, 0f, 6f))
            });

            EnsureSampleSceneBootstrap(levels[LevelTownId]);
            AddScenesToBuildSettings(new[] { MainMenuSceneName, "SampleScene", "Town", "Field", "Boss" });
            EnsureSceneFirstInBuildSettings(MainMenuSceneName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("可玩流程生成完成。");
        }

        private static void EnsureSampleScene()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath);
            if (sceneAsset != null)
            {
                return;
            }

            CombatSampleGenerator.GenerateSampleContent();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/_Game/ScriptableObjects", "Levels");
        }

        private static string EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }

            return path;
        }

        private static Dictionary<string, LevelDefinition> CreateOrUpdateLevels()
        {
            var levels = new Dictionary<string, LevelDefinition>();
            levels[LevelTownId] = LoadOrCreateLevel($"{LevelsFolder}/Level_Town.asset", "Level_Town", "Town", "Town");
            levels[LevelFieldId] = LoadOrCreateLevel($"{LevelsFolder}/Level_Field.asset", "Level_Field", "Field", "Field");
            levels[LevelBossId] = LoadOrCreateLevel($"{LevelsFolder}/Level_Boss.asset", "Level_Boss", "Boss", "Boss");
            return levels;
        }

        private static LevelDefinition LoadOrCreateLevel(string path, string id, string displayName, string sceneName)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, path);
            }

            var so = new SerializedObject(level);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("sceneName").stringValue = sceneName;
            so.FindProperty("defaultSpawnId").stringValue = "Start";
            so.FindProperty("returnSpawnId").stringValue = "Return";
#if UNITY_EDITOR
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>($"{ScenesFolder}/{sceneName}.unity");
            if (sceneAsset != null)
            {
                var sceneProp = so.FindProperty("sceneAsset");
                if (sceneProp != null)
                {
                    sceneProp.objectReferenceValue = sceneAsset;
                }
            }
#endif
            so.ApplyModifiedPropertiesWithoutUndo();

            return level;
        }

        private static void UpdateDatabase(Dictionary<string, LevelDefinition> levels)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            var so = new SerializedObject(database);
            var listProp = so.FindProperty("levels");
            if (listProp == null)
            {
                return;
            }

            var existing = new HashSet<Object>();
            for (int i = 0; i < listProp.arraySize; i++)
            {
                existing.Add(listProp.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            foreach (var level in levels.Values)
            {
                if (level == null || existing.Contains(level))
                {
                    continue;
                }

                listProp.arraySize += 1;
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = level;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool CreateLevelScene(string sceneName, LevelDefinition level, PortalSeed[] portals)
        {
            var scenePath = $"{ScenesFolder}/{sceneName}.unity";
            
            // 边界防护：如果目标场景和模板场景都不存在则跳过
            if (!System.IO.File.Exists(scenePath) && !System.IO.File.Exists(SampleScenePath))
            {
                Debug.LogWarning($"[PlayableFlowGenerator] 无法创建场景 '{sceneName}'：模板场景 SampleScene 不存在。");
                return false;
            }
            
            var scene = System.IO.File.Exists(scenePath)
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)
                : EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            SceneManager.SetActiveScene(scene);

            EnsureLevelFlow(level);
            EnsureSaveGameManager();
            EnsureUIRoot();
            EnsureSpawnPoints();
            EnsurePortals(portals);
            SetGameplayInitialScreen();

            EditorSceneManager.SaveScene(scene, scenePath);
            return true;
        }

        private static void EnsureMainMenuScene(LevelDefinition startLevel)
        {
            if (!System.IO.File.Exists(MainMenuScenePath))
            {
                if (System.IO.File.Exists(SampleScenePath))
                {
                    AssetDatabase.CopyAsset(SampleScenePath, MainMenuScenePath);
                }
                else
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(scene, MainMenuScenePath);
                }
            }

            var menuScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(menuScene);

            var flow = EnsureLevelFlow(startLevel);
            SetSerializedValue(flow, "startLevelId", LevelTownId);
            SetSerializedValue(flow, "startSpawnId", "Start");
            SetSerializedValue(flow, "persistAcrossScenes", true);

            EnsureSaveGameManager();
            EnsureUIRoot();
            SetMenuInitialScreen();

            EditorSceneManager.SaveScene(menuScene, MainMenuScenePath);
        }

        private static void EnsureSampleSceneBootstrap(LevelDefinition startLevel)
        {
            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            var flow = EnsureLevelFlow(startLevel);
            SetSerializedValue(flow, "startLevelId", LevelTownId);
            SetSerializedValue(flow, "startSpawnId", "Start");
            SetSerializedValue(flow, "persistAcrossScenes", true);

            EnsureSaveGameManager();
            EnsureUIRoot();
            SetMenuInitialScreen();

            EditorSceneManager.SaveScene(scene, SampleScenePath);
        }

        private static LevelFlowController EnsureLevelFlow(LevelDefinition startLevel)
        {
            var flow = Object.FindFirstObjectByType<LevelFlowController>();
            if (flow == null)
            {
                var root = new GameObject("LevelFlow");
                flow = root.AddComponent<LevelFlowController>();
            }

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            SetSerializedReference(flow, "database", database);
            SetSerializedValue(flow, "startLevelId", startLevel != null ? startLevel.Id : LevelTownId);
            SetSerializedValue(flow, "startSpawnId", "Start");
            SetSerializedValue(flow, "playerTag", "Player");
            SetSerializedValue(flow, "persistAcrossScenes", true);

            return flow;
        }

        private static void EnsureSaveGameManager()
        {
            var manager = Object.FindFirstObjectByType<SaveGameManager>();
            if (manager != null)
            {
                return;
            }

            var root = new GameObject("GameServices");
            root.AddComponent<SaveGameManager>();
        }

        private static void EnsureUIRoot()
        {
            var root = Object.FindFirstObjectByType<UIRoot>();
            if (root == null)
            {
                UIRootBuilder.CreateUIRoot();
                root = Object.FindFirstObjectByType<UIRoot>();
            }

            UIRootBuilder.BuildBasicUI();
            UIRootBuilder.NormalizeCanvasTransforms(root);
        }

        private static void EnsureSpawnPoints()
        {
            if (Object.FindFirstObjectByType<LevelSpawnPoint>() != null)
            {
                return;
            }

            var root = new GameObject("LevelSpawns");

            var start = new GameObject("Spawn_Start");
            start.transform.SetParent(root.transform);
            start.transform.position = Vector3.zero;
            var startPoint = start.AddComponent<LevelSpawnPoint>();
            SetSerializedValue(startPoint, "spawnId", "Start");
            SetSerializedValue(startPoint, "isDefault", true);

            var ret = new GameObject("Spawn_Return");
            ret.transform.SetParent(root.transform);
            ret.transform.position = new Vector3(-2f, 0f, 0f);
            var returnPoint = ret.AddComponent<LevelSpawnPoint>();
            SetSerializedValue(returnPoint, "spawnId", "Return");
            SetSerializedValue(returnPoint, "isReturn", true);
        }

        private static void EnsurePortals(PortalSeed[] portals)
        {
            var root = GameObject.Find("LevelPortals");
            if (root == null)
            {
                root = new GameObject("LevelPortals");
            }

            for (int i = 0; i < portals.Length; i++)
            {
                var seed = portals[i];
                if (GameObject.Find(seed.Name) != null)
                {
                    continue;
                }

                var portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                portal.name = seed.Name;
                portal.transform.SetParent(root.transform);
                portal.transform.position = seed.Position;
                portal.transform.localScale = new Vector3(2f, 1f, 2f);

                var collider = portal.GetComponent<Collider>();
                collider.isTrigger = true;

                var body = portal.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                var component = portal.AddComponent<LevelPortal>();
                SetSerializedValue(component, "targetLevelId", seed.TargetLevelId);
                SetSerializedValue(component, "targetSpawnId", seed.TargetSpawnId);
                SetSerializedValue(component, "requirePlayerTag", true);
                SetSerializedValue(component, "playerTag", "Player");
            }
        }

        private static void SetGameplayInitialScreen()
        {
            var root = Object.FindFirstObjectByType<UIRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                return;
            }

            var uiManager = root.Manager != null ? root.Manager : root.GetComponentInChildren<UIManager>(true);
            var inGame = root.GetComponentInChildren<InGameScreen>(true);
            if (uiManager == null || inGame == null)
            {
                return;
            }

            SetSerializedReference(uiManager, "initialScreen", inGame);
            SetSerializedValue(uiManager, "hideHudOnStart", false);
            SetSerializedValue(uiManager, "hideAllScreensOnStart", true);
        }

        private static void SetMenuInitialScreen()
        {
            var root = Object.FindFirstObjectByType<UIRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                return;
            }

            var uiManager = root.Manager != null ? root.Manager : root.GetComponentInChildren<UIManager>(true);
            var mainMenu = root.GetComponentInChildren<MainMenuScreen>(true);
            if (uiManager == null || mainMenu == null)
            {
                return;
            }

            SetSerializedReference(uiManager, "initialScreen", mainMenu);
            SetSerializedValue(uiManager, "hideHudOnStart", true);
            SetSerializedValue(uiManager, "hideAllScreensOnStart", true);
        }

        private static void AddScenesToBuildSettings(IEnumerable<string> sceneNames)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var sceneName in sceneNames)
            {
                var path = $"{ScenesFolder}/{sceneName}.unity";
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }

                var exists = false;
                for (int i = 0; i < scenes.Count; i++)
                {
                    if (scenes[i].path == path)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureSceneFirstInBuildSettings(string sceneName)
        {
            var path = $"{ScenesFolder}/{sceneName}.unity";
            if (!System.IO.File.Exists(path))
            {
                return;
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var index = -1;
            for (var i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                {
                    index = i;
                    break;
                }
            }

            var entry = new EditorBuildSettingsScene(path, true);
            if (index >= 0)
            {
                entry = scenes[index];
                entry.enabled = true;
                scenes.RemoveAt(index);
            }

            scenes.Insert(0, entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetDefinitionBase(SerializedObject so, string id, string displayName)
        {
            if (so == null)
            {
                return;
            }

            var idProp = so.FindProperty("id");
            if (idProp != null)
            {
                idProp.stringValue = id;
            }

            var nameProp = so.FindProperty("displayName");
            if (nameProp != null)
            {
                nameProp.stringValue = displayName;
            }
        }

        private static void SetSerializedReference(Object target, string propertyName, Object value)
        {
            if (target == null)
            {
                return;
            }

            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedValue(Object target, string propertyName, string value)
        {
            if (target == null)
            {
                return;
            }

            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                return;
            }

            prop.stringValue = value ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedValue(Object target, string propertyName, bool value)
        {
            if (target == null)
            {
                return;
            }

            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                return;
            }

            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct PortalSeed
        {
            public string Name;
            public string TargetLevelId;
            public string TargetSpawnId;
            public Vector3 Position;

            public PortalSeed(string name, string targetLevelId, string targetSpawnId, Vector3 position)
            {
                Name = name;
                TargetLevelId = targetLevelId;
                TargetSpawnId = targetSpawnId;
                Position = position;
            }
        }
    }
}
