using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.Editor
{
    /// <summary>
    /// 单位视觉管线初始化工具：
    /// - 为 UnitDefinition 自动创建/绑定 UnitVisualProfile
    /// - 为单位预制体挂载 UnitVisualPresenter 与 VisualRoot
    /// </summary>
    public static class UnitVisualSetupUtility
    {
        private const string UnitDefinitionFolder = "Assets/_Game/ScriptableObjects/Units";
        private const string VisualProfileFolder = "Assets/_Game/ScriptableObjects/Units/VisualProfiles";

        [MenuItem("Combat/Visual/Setup Unit Visual Pipeline")]
        public static void SetupUnitVisualPipeline()
        {
            SetupInternal(interactive: true);
        }

        /// <summary>
        /// 批处理入口：
        /// Unity.exe -batchmode -projectPath ... -executeMethod CombatSystem.Editor.UnitVisualSetupUtility.SetupUnitVisualPipelineBatch -quit
        /// </summary>
        public static void SetupUnitVisualPipelineBatch()
        {
            SetupInternal(interactive: false);
        }

        private static void SetupInternal(bool interactive)
        {
            EnsureFolder(VisualProfileFolder);

            var unitGuids = AssetDatabase.FindAssets("t:UnitDefinition", new[] { UnitDefinitionFolder });
            var changedDefinitions = 0;
            var changedPrefabs = 0;
            var createdProfiles = 0;

            for (int i = 0; i < unitGuids.Length; i++)
            {
                var definitionPath = AssetDatabase.GUIDToAssetPath(unitGuids[i]);
                var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(definitionPath);
                if (definition == null)
                {
                    continue;
                }

                var profilePath = $"{VisualProfileFolder}/{definition.name}_Visual.asset";
                var profile = AssetDatabase.LoadAssetAtPath<UnitVisualProfile>(profilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<UnitVisualProfile>();
                    profile.name = $"{definition.name}_Visual";
                    AssetDatabase.CreateAsset(profile, profilePath);
                    createdProfiles++;
                }

                if (ConfigureDefinition(definition, profile))
                {
                    changedDefinitions++;
                }

                var prefab = definition.Prefab;
                if (prefab == null)
                {
                    continue;
                }

                var prefabPath = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(prefabPath))
                {
                    continue;
                }

                if (ConfigureUnitPrefab(prefabPath))
                {
                    changedPrefabs++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = $"UnitDefinitions={unitGuids.Length}, changedDefinitions={changedDefinitions}, createdProfiles={createdProfiles}, changedPrefabs={changedPrefabs}";
            Debug.Log("[VisualSetup] Setup Unit Visual Pipeline complete: " + summary);

            if (interactive)
            {
                EditorUtility.DisplayDialog("Unit Visual Pipeline", "完成。\n\n" + summary, "OK");
            }
        }

        private static bool ConfigureDefinition(UnitDefinition definition, UnitVisualProfile profile)
        {
            var changed = false;
            var so = new SerializedObject(definition);
            var profileProp = so.FindProperty("visualProfile");
            if (profileProp != null && profileProp.objectReferenceValue != profile)
            {
                profileProp.objectReferenceValue = profile;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                changed = true;
            }

            return changed;
        }

        private static bool ConfigureUnitPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                return false;
            }

            var changed = false;
            var unitRoot = root.GetComponent<UnitRoot>();
            if (unitRoot == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return false;
            }

            var presenter = root.GetComponent<UnitVisualPresenter>();
            if (presenter == null)
            {
                presenter = root.AddComponent<UnitVisualPresenter>();
                changed = true;
            }

            var visualRoot = root.transform.Find("VisualRoot");
            if (visualRoot == null)
            {
                var visualRootGo = new GameObject("VisualRoot");
                visualRoot = visualRootGo.transform;
                visualRoot.SetParent(root.transform, false);
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;
                changed = true;
            }

            var so = new SerializedObject(presenter);
            changed |= AssignReference(so, "unitRoot", unitRoot);
            changed |= AssignReference(so, "movement", root.GetComponent<MovementComponent>());
            changed |= AssignReference(so, "skillUser", root.GetComponent<SkillUserComponent>());
            changed |= AssignReference(so, "health", root.GetComponent<HealthComponent>());
            changed |= AssignReference(so, "hitFlashReceiver", root.GetComponent<HitFlashReceiver>());
            changed |= AssignReference(so, "visualRoot", visualRoot);

            var useDefinitionProfile = so.FindProperty("useDefinitionProfile");
            if (useDefinitionProfile != null && !useDefinitionProfile.boolValue)
            {
                useDefinitionProfile.boolValue = true;
                changed = true;
            }

            var profileOverride = so.FindProperty("profileOverride");
            if (profileOverride != null && profileOverride.objectReferenceValue != null)
            {
                profileOverride.objectReferenceValue = null;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(presenter);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }

            PrefabUtility.UnloadPrefabContents(root);
            return changed;
        }

        private static bool AssignReference(SerializedObject so, string propertyName, Object target)
        {
            var property = so.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == target)
            {
                return false;
            }

            property.objectReferenceValue = target;
            return true;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var normalized = folderPath.Replace('\\', '/');
            var segments = normalized.Split('/');
            var current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
