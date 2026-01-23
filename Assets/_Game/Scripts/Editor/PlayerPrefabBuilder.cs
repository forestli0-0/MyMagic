using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CombatSystem.Editor
{
    public static class PlayerPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs";
        private const string PrefabPath = PrefabFolder + "/Player.prefab";
        private const string UnitPlayerPath = "Assets/_Game/ScriptableObjects/Units/Unit_Player.asset";

        [MenuItem("Combat/Player/Create Player Prefab")]
        public static void CreatePlayerPrefab()
        {
            var player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Sample_Player");
            if (player == null)
            {
                Debug.LogWarning("[PlayerPrefabBuilder] 未找到玩家对象（Tag: Player 或 Sample_Player）。");
                return;
            }

            EnsureComponent<InventoryComponent>(player);
            EnsureComponent<EquipmentComponent>(player);

            EnsureFolder("Assets", "_Game");
            EnsureFolder("Assets/_Game", "Prefabs");

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(player, PrefabPath, InteractionMode.UserAction);
            if (prefab == null)
            {
                Debug.LogWarning("[PlayerPrefabBuilder] 创建 Prefab 失败。");
                return;
            }

            var unitDef = AssetDatabase.LoadAssetAtPath<UnitDefinition>(UnitPlayerPath);
            if (unitDef == null)
            {
                var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Game/ScriptableObjects/Database/GameDatabase.asset");
                unitDef = database != null ? database.GetUnit("Unit_Player") : null;
            }

            if (unitDef != null)
            {
                var so = new SerializedObject(unitDef);
                var prop = so.FindProperty("prefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = prefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(unitDef);
                }
            }
            else
            {
                Debug.LogWarning("[PlayerPrefabBuilder] 未找到 Unit_Player.asset，Prefab 未绑定。");
            }

            EditorSceneManager.MarkSceneDirty(player.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerPrefabBuilder] Player prefab created at {PrefabPath}");
        }

        private static void EnsureComponent<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() == null)
            {
                target.AddComponent<T>();
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
