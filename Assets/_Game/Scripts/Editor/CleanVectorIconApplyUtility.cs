using System;
using System.Collections.Generic;
using System.IO;
using CombatSystem.Data;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.Editor
{
    /// <summary>
    /// Applies icon mappings from the "Clean Vector Icons" package to gameplay definitions.
    /// </summary>
    public static class CleanVectorIconApplyUtility
    {
        private const string CleanVectorRoot = "Assets/Clean Vector Icons";

        private static readonly Dictionary<string, string> SkillIconByAssetName = new Dictionary<string, string>
        {
            { "skill_arcanebolt", "t_11_star_" },
            { "skill_arcanefocus", "t_1_magnifier_" },
            { "skill_basicattack", "t_5_sword_" },
            { "skill_bleedstrike", "t_3_sword_cut_" },
            { "skill_boxfield", "t_1_column_" },
            { "skill_chainlightning", "t_2_lighthing_" },
            { "skill_cleave", "t_7_waraxe_" },
            { "skill_dash", "t_12_arrow_up_" },
            { "skill_execute", "t_5_x_" },
            { "skill_fireball", "t_3_magic_fire_" },
            { "skill_heal", "t_7_bottle_heal_" },
            { "skill_linestrike", "t_1_sword90_" },
            { "skill_magicward", "t_3_shield_magic_" },
            { "skill_manasurge", "t_8_bottle_magic_" },
            { "skill_poisondart", "t_12_spit_" },
            { "skill_quickcast", "t_23_gear_" },
            { "skill_randomshot", "t_7_question_" },
            { "skill_shockwave", "t_6_cloud_" },
            { "skill_stoneskin", "t_6_stone_" },
            { "skill_summontotem", "t_12_tent_" },
            { "skill_testcharm", "t_8_heart_" },
            { "skill_testfear", "bonus_yell" },
            { "skill_testroot", "t_7_plant1_" },
            { "skill_teststun", "t_20_stop_" },
            { "skill_testsuppression", "t_11_no_" },
            { "skill_testtaunt", "t_10_!_" },
            { "skill_timewarp", "t_26_clock_" },
            { "skill_triggerfocus", "t_4_magnifier_eye_" }
        };

        private static readonly Dictionary<string, string> BuffIconByAssetName = new Dictionary<string, string>
        {
            { "buff_arcanefocus", "t_11_star_" },
            { "buff_bleed", "t_4_eye_bleed_" },
            { "buff_burn", "t_1_fire_" },
            { "buff_magicward", "t_3_shield_magic_" },
            { "buff_poison", "t_12_spit_" },
            { "buff_quickcast", "t_23_gear_" },
            { "buff_stoneskin", "t_6_stone_" },
            { "buff_testcharm", "t_8_heart_" },
            { "buff_testfear", "bonus_sad" },
            { "buff_testroot", "t_7_plant1_" },
            { "buff_teststun", "t_20_stop_" },
            { "buff_testsuppression", "t_11_no_" },
            { "buff_testtaunt", "t_10_!_" },
            { "buff_timewarp", "t_26_clock_" }
        };

        private static readonly Dictionary<string, string> ItemIconByAssetName = new Dictionary<string, string>
        {
            { "item_accessory_test", "t_24_diamond_" },
            { "item_clothes_test", "t_1_shield_" },
            { "item_headband_test", "t_16_crown_" },
            { "item_potion_test", "t_7_bottle_heal_" },
            { "item_shoes_test", "t_8_arrow_right_" },
            { "item_weapon_test", "t_5_sword_" }
        };

        [MenuItem("Combat/UI/Icons/Apply Clean Vector Icons")]
        public static void ApplyCleanVectorIcons()
        {
            ApplyInternal(true);
        }

        /// <summary>
        /// Batchmode entrypoint:
        /// Unity.exe -batchmode -projectPath ... -executeMethod CombatSystem.Editor.CleanVectorIconApplyUtility.ApplyCleanVectorIconsBatch -quit
        /// </summary>
        public static void ApplyCleanVectorIconsBatch()
        {
            ApplyInternal(false);
        }

        [MenuItem("Combat/UI/Icons/Print Clean Vector Icon Report")]
        public static void PrintReport()
        {
            if (!Directory.Exists(CleanVectorRoot))
            {
                Debug.LogWarning($"[UI][Icons] Missing folder: {CleanVectorRoot}");
                return;
            }

            var spriteIndex = BuildSpriteIndex();
            Debug.Log($"[UI][Icons] Clean Vector sprites indexed: {spriteIndex.Count}");

            ReportMissingMappings(
                "Skills",
                "Assets/_Game/ScriptableObjects/Skills",
                SkillIconByAssetName,
                spriteIndex);
            ReportMissingMappings(
                "Buffs",
                "Assets/_Game/ScriptableObjects/Buffs",
                BuffIconByAssetName,
                spriteIndex);
            ReportMissingMappings(
                "Items",
                "Assets/_Game/ScriptableObjects/Items",
                ItemIconByAssetName,
                spriteIndex);
        }

        private static void ApplyInternal(bool interactive)
        {
            if (!Directory.Exists(CleanVectorRoot))
            {
                Debug.LogError($"[UI][Icons] Missing folder: {CleanVectorRoot}");
                return;
            }

            EnsureTextureImportAsSprite();
            var spriteIndex = BuildSpriteIndex();
            if (spriteIndex.Count == 0)
            {
                Debug.LogError("[UI][Icons] No sprites found in Clean Vector package.");
                return;
            }

            var summary = new List<string>(8);

            var skillResult = ApplyIconsToDefinitions(
                "Assets/_Game/ScriptableObjects/Skills",
                SkillIconByAssetName,
                spriteIndex);
            summary.Add($"Skills changed={skillResult.changed}, unmapped={skillResult.unmapped.Count}, missingSprite={skillResult.missingSprite.Count}");

            var buffResult = ApplyIconsToDefinitions(
                "Assets/_Game/ScriptableObjects/Buffs",
                BuffIconByAssetName,
                spriteIndex);
            summary.Add($"Buffs changed={buffResult.changed}, unmapped={buffResult.unmapped.Count}, missingSprite={buffResult.missingSprite.Count}");

            var itemResult = ApplyIconsToDefinitions(
                "Assets/_Game/ScriptableObjects/Items",
                ItemIconByAssetName,
                spriteIndex);
            summary.Add($"Items changed={itemResult.changed}, unmapped={itemResult.unmapped.Count}, missingSprite={itemResult.missingSprite.Count}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[UI][Icons] Apply Clean Vector Icons finished:\n- " + string.Join("\n- ", summary));
            PrintWarnings("Skills", skillResult);
            PrintWarnings("Buffs", buffResult);
            PrintWarnings("Items", itemResult);

            if (interactive)
            {
                EditorUtility.DisplayDialog(
                    "Clean Vector Icons",
                    "图标替换完成。\n\n" + string.Join("\n", summary),
                    "OK");
            }
        }

        private static ApplyResult ApplyIconsToDefinitions(
            string root,
            IReadOnlyDictionary<string, string> mapping,
            IReadOnlyDictionary<string, Sprite> spriteIndex)
        {
            var result = new ApplyResult();
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { root });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj == null)
                {
                    continue;
                }

                var type = obj.GetType();
                if (type != typeof(SkillDefinition) &&
                    type != typeof(BuffDefinition) &&
                    type != typeof(ItemDefinition))
                {
                    continue;
                }

                var assetName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!mapping.TryGetValue(assetName, out var iconKey))
                {
                    result.unmapped.Add(assetName);
                    continue;
                }

                if (!spriteIndex.TryGetValue(iconKey, out var sprite) || sprite == null)
                {
                    result.missingSprite.Add($"{assetName} -> {iconKey}");
                    continue;
                }

                var so = new SerializedObject(obj);
                var iconProp = so.FindProperty("icon");
                if (iconProp == null)
                {
                    continue;
                }

                if (iconProp.objectReferenceValue == sprite)
                {
                    continue;
                }

                iconProp.objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
                result.changed++;
            }

            return result;
        }

        private static void EnsureTextureImportAsSprite()
        {
            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { CleanVectorRoot });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                var changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }

                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static Dictionary<string, Sprite> BuildSpriteIndex()
        {
            var index = new Dictionary<string, Sprite>(256);
            var spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { CleanVectorRoot });
            for (int i = 0; i < spriteGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    continue;
                }

                var key = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!index.ContainsKey(key))
                {
                    index.Add(key, sprite);
                }
            }

            return index;
        }

        private static void ReportMissingMappings(
            string label,
            string root,
            IReadOnlyDictionary<string, string> mapping,
            IReadOnlyDictionary<string, Sprite> spriteIndex)
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { root });
            var missing = new List<string>(32);
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var assetName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!mapping.TryGetValue(assetName, out var iconKey))
                {
                    missing.Add($"{assetName} (no mapping)");
                    continue;
                }

                if (!spriteIndex.ContainsKey(iconKey))
                {
                    missing.Add($"{assetName} -> {iconKey} (sprite missing)");
                }
            }

            if (missing.Count == 0)
            {
                Debug.Log($"[UI][Icons] {label}: mapping complete.");
                return;
            }

            Debug.LogWarning($"[UI][Icons] {label}: {missing.Count} mapping issues\n- {string.Join("\n- ", missing)}");
        }

        private static void PrintWarnings(string label, ApplyResult result)
        {
            if (result.unmapped.Count > 0)
            {
                Debug.LogWarning($"[UI][Icons] {label} unmapped ({result.unmapped.Count})\n- {string.Join("\n- ", result.unmapped)}");
            }

            if (result.missingSprite.Count > 0)
            {
                Debug.LogWarning($"[UI][Icons] {label} missing sprites ({result.missingSprite.Count})\n- {string.Join("\n- ", result.missingSprite)}");
            }
        }

        private sealed class ApplyResult
        {
            public int changed;
            public readonly List<string> unmapped = new List<string>(32);
            public readonly List<string> missingSprite = new List<string>(32);
        }
    }
}

