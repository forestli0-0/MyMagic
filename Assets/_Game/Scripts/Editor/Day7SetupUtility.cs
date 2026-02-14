using System.IO;
using CombatSystem.Core;
using CombatSystem.Gameplay;
using CombatSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.EditorTools
{
    /// <summary>
    /// Day7 一键搭建工具：关键战斗反馈（命中闪烁、命中音、相机轻抖）。
    /// </summary>
    public static class Day7SetupUtility
    {
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player.prefab";
        private const string EnemyPrefabPath = "Assets/_Game/Prefabs/Enemy_Default.prefab";
        private const string BossPrefabPath = "Assets/_Game/Prefabs/Enemy_Boss.prefab";

        [MenuItem("Combat/Day7/Setup Combat Feedback (Current Scene)")]
        public static void SetupCombatFeedbackCurrentScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Day7] No active scene.");
                return;
            }

            EnsurePrefabHitFlash(PlayerPrefabPath, true);
            EnsurePrefabHitFlash(EnemyPrefabPath, false);
            EnsurePrefabHitFlash(BossPrefabPath, false);

            var player = ResolvePlayerUnit();
            var eventHub = ResolveEventHub(player);
            EnsureSceneHitFlashReceivers(player, eventHub);
            var controller = EnsureFeedbackController(player, eventHub);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeObject = controller != null ? controller.gameObject : null;
            Debug.Log($"[Day7] Combat feedback setup complete for scene '{scene.name}'.");
        }

        [MenuItem("Combat/Day7/Setup Combat Feedback (Town + Field + Boss)")]
        public static void SetupCombatFeedbackPlayableScenes()
        {
            EnsurePrefabHitFlash(PlayerPrefabPath, true);
            EnsurePrefabHitFlash(EnemyPrefabPath, false);
            EnsurePrefabHitFlash(BossPrefabPath, false);

            SetupScene("Assets/Scenes/Town.unity");
            SetupScene("Assets/Scenes/Field.unity");
            SetupScene("Assets/Scenes/Boss.unity");

            AssetDatabase.SaveAssets();
            Debug.Log("[Day7] Combat feedback setup complete for Town + Field + Boss.");
        }

        private static void SetupScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Day7] Scene not found: {scenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            SetupCombatFeedbackCurrentScene();
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureSceneHitFlashReceivers(UnitRoot player, CombatEventHub fallbackHub)
        {
            var healthComponents = Object.FindObjectsByType<HealthComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < healthComponents.Length; i++)
            {
                var health = healthComponents[i];
                if (health == null)
                {
                    continue;
                }

                var receiver = health.GetComponent<HitFlashReceiver>();
                if (receiver == null)
                {
                    receiver = health.gameObject.AddComponent<HitFlashReceiver>();
                }

                var unit = health.GetComponent<UnitRoot>();
                var eventHub = unit != null && unit.EventHub != null ? unit.EventHub : fallbackHub;
                var isPlayer = player != null && health == player.GetComponent<HealthComponent>();

                var serialized = new SerializedObject(receiver);
                serialized.FindProperty("eventHub").objectReferenceValue = eventHub;
                serialized.FindProperty("targetHealth").objectReferenceValue = health;
                serialized.FindProperty("flashColor").colorValue = isPlayer
                    ? new Color(1f, 0.45f, 0.45f, 1f)
                    : new Color(1f, 0.95f, 0.85f, 1f);
                serialized.FindProperty("flashStrength").floatValue = isPlayer ? 0.5f : 0.62f;
                serialized.FindProperty("flashDuration").floatValue = isPlayer ? 0.1f : 0.08f;
                serialized.FindProperty("onlyWhenHealthDamage").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(receiver);
            }
        }

        private static CombatFeedbackController EnsureFeedbackController(UnitRoot player, CombatEventHub eventHub)
        {
            var controller = Object.FindFirstObjectByType<CombatFeedbackController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                var root = new GameObject("CombatFeedback");
                controller = root.AddComponent<CombatFeedbackController>();
            }

            var camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
            }

            CameraShakeController shake = null;
            if (camera != null)
            {
                shake = camera.GetComponent<CameraShakeController>();
                if (shake == null)
                {
                    shake = camera.gameObject.AddComponent<CameraShakeController>();
                }
            }

            var audioSource = controller.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = controller.gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("eventHub").objectReferenceValue = eventHub;
            serialized.FindProperty("playerUnit").objectReferenceValue = player;
            serialized.FindProperty("cameraShake").objectReferenceValue = shake;
            serialized.FindProperty("audioSource").objectReferenceValue = audioSource;
            serialized.FindProperty("shakeOnEnemyHit").floatValue = 0.1f;
            serialized.FindProperty("shakeOnPlayerHit").floatValue = 0.16f;
            serialized.FindProperty("criticalBonus").floatValue = 0.05f;
            serialized.FindProperty("minEnemyDamageForShake").floatValue = 1f;
            serialized.FindProperty("onlyWhenPlayerInvolved").boolValue = player != null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(audioSource);
            if (shake != null)
            {
                var shakeSerialized = new SerializedObject(shake);
                shakeSerialized.FindProperty("traumaDecay").floatValue = 1.45f;
                shakeSerialized.FindProperty("noiseFrequency").floatValue = 24f;
                shakeSerialized.FindProperty("maxOffset").floatValue = 0.17f;
                shakeSerialized.FindProperty("maxRoll").floatValue = 1.15f;
                shakeSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(shake);
            }

            return controller;
        }

        private static void EnsurePrefabHitFlash(string prefabPath, bool isPlayer)
        {
            if (!File.Exists(prefabPath))
            {
                Debug.LogWarning($"[Day7] Prefab not found: {prefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[Day7] Failed to load prefab: {prefabPath}");
                return;
            }

            var health = root.GetComponent<HealthComponent>();
            if (health == null)
            {
                health = root.GetComponentInChildren<HealthComponent>(true);
            }

            if (health == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning($"[Day7] Prefab has no HealthComponent: {prefabPath}");
                return;
            }

            var receiver = health.GetComponent<HitFlashReceiver>();
            if (receiver == null)
            {
                receiver = health.gameObject.AddComponent<HitFlashReceiver>();
            }

            var unit = health.GetComponent<UnitRoot>();
            if (unit == null)
            {
                unit = root.GetComponent<UnitRoot>();
            }

            var serialized = new SerializedObject(receiver);
            serialized.FindProperty("eventHub").objectReferenceValue = unit != null ? unit.EventHub : null;
            serialized.FindProperty("targetHealth").objectReferenceValue = health;
            serialized.FindProperty("flashColor").colorValue = isPlayer
                ? new Color(1f, 0.45f, 0.45f, 1f)
                : new Color(1f, 0.95f, 0.85f, 1f);
            serialized.FindProperty("flashStrength").floatValue = isPlayer ? 0.5f : 0.62f;
            serialized.FindProperty("flashDuration").floatValue = isPlayer ? 0.1f : 0.08f;
            serialized.FindProperty("onlyWhenHealthDamage").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static UnitRoot ResolvePlayerUnit()
        {
            return PlayerUnitLocator.FindPlayerUnit();
        }

        private static CombatEventHub ResolveEventHub(UnitRoot player)
        {
            if (player != null && player.EventHub != null)
            {
                return player.EventHub;
            }

            var units = Object.FindObjectsByType<UnitRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].EventHub != null)
                {
                    return units[i].EventHub;
                }
            }

            var eventHubGuids = AssetDatabase.FindAssets("t:CombatEventHub", new[] { "Assets/_Game/ScriptableObjects" });
            if (eventHubGuids.Length <= 0)
            {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(eventHubGuids[0]);
            return AssetDatabase.LoadAssetAtPath<CombatEventHub>(assetPath);
        }
    }
}
