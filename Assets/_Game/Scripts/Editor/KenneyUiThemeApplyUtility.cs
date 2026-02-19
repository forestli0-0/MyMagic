using System;
using System.Collections.Generic;
using System.IO;
using CombatSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CombatSystem.Editor
{
    /// <summary>
    /// Applies Kenney UI Pack sprites to existing UGUI screens.
    /// </summary>
    public static class KenneyUiThemeApplyUtility
    {
        private const string ThemeAssetPath = "Assets/_Game/ScriptableObjects/UI/UITheme_KenneyBlue.asset";
        private const string PackRoot = "Assets/_Game/Art/UIThemes/KenneyUI/PNG/Blue/Default";
        private const string PackExtraRoot = "Assets/_Game/Art/UIThemes/KenneyUI/PNG/Extra/Default";

        private const string PanelSpritePath = PackRoot + "/button_rectangle_line.png";
        private const string PanelAltSpritePath = PackRoot + "/button_rectangle_depth_line.png";
        private const string ButtonSpritePath = PackRoot + "/button_rectangle_depth_gradient.png";
        private const string SlotSpritePath = PackRoot + "/button_square_line.png";
        private const string InputSpritePath = PackExtraRoot + "/input_rectangle.png";
        private const string ToggleBackgroundSpritePath = PackRoot + "/check_square_grey.png";
        private const string ToggleCheckmarkSpritePath = PackRoot + "/check_square_color_checkmark.png";
        private const string SliderBackgroundSpritePath = PackRoot + "/slide_horizontal_grey_section_wide.png";
        private const string SliderFillSpritePath = PackRoot + "/slide_horizontal_color_section_wide.png";
        private const string SliderHandleSpritePath = PackRoot + "/slide_hangle.png";
        private const string DropdownArrowSpritePath = PackRoot + "/arrow_basic_s_small.png";

        private static readonly string[] SpritePaths =
        {
            PanelSpritePath,
            PanelAltSpritePath,
            ButtonSpritePath,
            SlotSpritePath,
            InputSpritePath,
            ToggleBackgroundSpritePath,
            ToggleCheckmarkSpritePath,
            SliderBackgroundSpritePath,
            SliderFillSpritePath,
            SliderHandleSpritePath,
            DropdownArrowSpritePath,
        };

        [MenuItem("Combat/UI/Themes/Apply Kenney Blue (Current Scene)")]
        public static void ApplyCurrentScene()
        {
            ApplyToCurrentSceneInternal(true);
        }

        [MenuItem("Combat/UI/Themes/Apply Kenney Blue (All Scenes)")]
        public static void ApplyAllScenes()
        {
            ApplyToAllScenesInternal(true);
        }

        /// <summary>
        /// Batch entrypoint.
        /// Unity.exe -batchmode -projectPath ... -executeMethod CombatSystem.Editor.KenneyUiThemeApplyUtility.ApplyKenneyBlueBatch -quit
        /// </summary>
        public static void ApplyKenneyBlueBatch()
        {
            ApplyToAllScenesInternal(false);
        }

        private static void ApplyToCurrentSceneInternal(bool interactive)
        {
            if (!ValidatePack())
            {
                return;
            }

            PrepareImportSettings();
            var sprites = LoadSprites();
            if (sprites == null)
            {
                return;
            }

            var theme = EnsureThemeAsset(sprites);
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[UI][Theme] Active scene is not a saved project scene.");
                return;
            }

            var stats = ApplyToLoadedScene(theme, sprites);
            if (stats.Changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = $"Scene={scene.path}, roots={stats.RootsTouched}, images={stats.ImagesChanged}, buttons={stats.ButtonsSkinned}, inputs={stats.InputsSkinned}, slots={stats.SlotsSkinned}";
            Debug.Log("[UI][Theme] Apply Kenney Blue (Current Scene) complete: " + summary);

            if (interactive)
            {
                EditorUtility.DisplayDialog("Kenney Theme", "替换完成。\n\n" + summary, "OK");
            }
        }

        private static void ApplyToAllScenesInternal(bool interactive)
        {
            if (!ValidatePack())
            {
                return;
            }

            PrepareImportSettings();
            var sprites = LoadSprites();
            if (sprites == null)
            {
                return;
            }

            var theme = EnsureThemeAsset(sprites);

            var activeScenePath = SceneManager.GetActiveScene().path;
            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });

            var touchedScenes = 0;
            var touchedRoots = 0;
            var changedImages = 0;
            var changedButtons = 0;
            var changedInputs = 0;
            var changedSlots = 0;

            for (int i = 0; i < scenePaths.Length; i++)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(scenePaths[i]);
                if (string.IsNullOrEmpty(scenePath))
                {
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var stats = ApplyToLoadedScene(theme, sprites);
                if (!stats.Changed)
                {
                    continue;
                }

                touchedScenes++;
                touchedRoots += stats.RootsTouched;
                changedImages += stats.ImagesChanged;
                changedButtons += stats.ButtonsSkinned;
                changedInputs += stats.InputsSkinned;
                changedSlots += stats.SlotsSkinned;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = $"Scenes={touchedScenes}, roots={touchedRoots}, images={changedImages}, buttons={changedButtons}, inputs={changedInputs}, slots={changedSlots}";
            Debug.Log("[UI][Theme] Apply Kenney Blue (All Scenes) complete: " + summary);

            if (interactive)
            {
                EditorUtility.DisplayDialog("Kenney Theme", "替换完成。\n\n" + summary, "OK");
            }
        }

        private static bool ValidatePack()
        {
            if (!Directory.Exists("Assets/_Game/Art/UIThemes/KenneyUI"))
            {
                Debug.LogError("[UI][Theme] Missing Kenney pack folder: Assets/_Game/Art/UIThemes/KenneyUI");
                return false;
            }

            return true;
        }

        private static void PrepareImportSettings()
        {
            ConfigureSpriteTexture(PanelSpritePath, new Vector4(16f, 16f, 16f, 16f));
            ConfigureSpriteTexture(PanelAltSpritePath, new Vector4(16f, 16f, 16f, 16f));
            ConfigureSpriteTexture(ButtonSpritePath, new Vector4(16f, 16f, 16f, 16f));
            ConfigureSpriteTexture(SlotSpritePath, new Vector4(8f, 8f, 8f, 8f));
            ConfigureSpriteTexture(InputSpritePath, new Vector4(16f, 16f, 16f, 16f));
            ConfigureSpriteTexture(ToggleBackgroundSpritePath, new Vector4(4f, 4f, 4f, 4f));
            ConfigureSpriteTexture(ToggleCheckmarkSpritePath, Vector4.zero);
            ConfigureSpriteTexture(SliderBackgroundSpritePath, new Vector4(12f, 12f, 12f, 12f));
            ConfigureSpriteTexture(SliderFillSpritePath, new Vector4(12f, 12f, 12f, 12f));
            ConfigureSpriteTexture(SliderHandleSpritePath, Vector4.zero);
            ConfigureSpriteTexture(DropdownArrowSpritePath, Vector4.zero);
        }

        private static void ConfigureSpriteTexture(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
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

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                changed = true;
            }

            if ((settings.spriteBorder - border).sqrMagnitude > 0.001f)
            {
                settings.spriteBorder = border;
                changed = true;
            }

            if (changed)
            {
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static SpriteSet LoadSprites()
        {
            var set = new SpriteSet
            {
                Panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath),
                PanelAlt = AssetDatabase.LoadAssetAtPath<Sprite>(PanelAltSpritePath),
                Button = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath),
                Slot = AssetDatabase.LoadAssetAtPath<Sprite>(SlotSpritePath),
                Input = AssetDatabase.LoadAssetAtPath<Sprite>(InputSpritePath),
                ToggleBackground = AssetDatabase.LoadAssetAtPath<Sprite>(ToggleBackgroundSpritePath),
                ToggleCheckmark = AssetDatabase.LoadAssetAtPath<Sprite>(ToggleCheckmarkSpritePath),
                SliderBackground = AssetDatabase.LoadAssetAtPath<Sprite>(SliderBackgroundSpritePath),
                SliderFill = AssetDatabase.LoadAssetAtPath<Sprite>(SliderFillSpritePath),
                SliderHandle = AssetDatabase.LoadAssetAtPath<Sprite>(SliderHandleSpritePath),
                DropdownArrow = AssetDatabase.LoadAssetAtPath<Sprite>(DropdownArrowSpritePath),
            };

            if (!set.IsValid)
            {
                Debug.LogError("[UI][Theme] Failed to load one or more Kenney sprites. Verify imported files.");
                return null;
            }

            set.Cache = new HashSet<Sprite>
            {
                set.Panel,
                set.PanelAlt,
                set.Button,
                set.Slot,
                set.Input,
                set.ToggleBackground,
                set.ToggleCheckmark,
                set.SliderBackground,
                set.SliderFill,
                set.SliderHandle,
                set.DropdownArrow,
            };

            return set;
        }

        private static UIThemeConfig EnsureThemeAsset(SpriteSet sprites)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            if (theme == null)
            {
                var folder = Path.GetDirectoryName(ThemeAssetPath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                theme = ScriptableObject.CreateInstance<UIThemeConfig>();
                theme.name = "UITheme_KenneyBlue";
                AssetDatabase.CreateAsset(theme, ThemeAssetPath);
                theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            }

            if (theme == null)
            {
                throw new InvalidOperationException("Failed to create/load UITheme_KenneyBlue asset.");
            }

            var so = new SerializedObject(theme);
            so.FindProperty("defaultSprite").objectReferenceValue = sprites.Panel;
            so.FindProperty("gameplayOverlayColor").colorValue = new Color(0.02f, 0.03f, 0.05f, 0.62f);
            so.FindProperty("gameplayHeaderColor").colorValue = new Color(0.56f, 0.66f, 0.84f, 0.95f);
            so.FindProperty("gameplayPanelColor").colorValue = new Color(0.46f, 0.56f, 0.76f, 0.93f);
            so.FindProperty("gameplayPanelAltColor").colorValue = new Color(0.38f, 0.48f, 0.68f, 0.93f);
            so.FindProperty("tabActiveColor").colorValue = new Color(0.74f, 0.86f, 1f, 1f);
            so.FindProperty("tabInactiveColor").colorValue = new Color(0.5f, 0.6f, 0.78f, 1f);
            so.FindProperty("tabActiveTextColor").colorValue = new Color(1f, 1f, 1f, 1f);
            so.FindProperty("tabInactiveTextColor").colorValue = new Color(0.91f, 0.95f, 1f, 1f);
            so.FindProperty("footerHintBackgroundColor").colorValue = new Color(0.22f, 0.31f, 0.45f, 0.96f);
            so.FindProperty("footerHintTextColor").colorValue = new Color(0.93f, 0.97f, 1f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static ApplyStats ApplyToLoadedScene(UIThemeConfig theme, SpriteSet sprites)
        {
            var stats = new ApplyStats();
            var roots = UnityEngine.Object.FindObjectsByType<UIRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                {
                    continue;
                }

                stats.RootsTouched++;
                if (AssignTheme(root, theme))
                {
                    stats.Changed = true;
                }

                if (ApplySpritesToHierarchy(root.transform, sprites, ref stats))
                {
                    stats.Changed = true;
                }
            }

            return stats;
        }

        private static bool AssignTheme(UIRoot root, UIThemeConfig theme)
        {
            var changed = false;
            var themeController = root.ThemeController != null
                ? root.ThemeController
                : root.GetComponent<UIThemeController>();

            if (themeController == null)
            {
                themeController = root.gameObject.AddComponent<UIThemeController>();
                changed = true;
            }

            var rootSo = new SerializedObject(root);
            var themeControllerProp = rootSo.FindProperty("themeController");
            if (themeControllerProp != null && themeControllerProp.objectReferenceValue != themeController)
            {
                themeControllerProp.objectReferenceValue = themeController;
                rootSo.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (themeController.Theme != theme)
            {
                themeController.SetTheme(theme, false);
                changed = true;
            }

            themeController.ApplyTheme();
            EditorUtility.SetDirty(themeController);
            EditorUtility.SetDirty(root);
            return changed;
        }

        private static bool ApplySpritesToHierarchy(Transform root, SpriteSet sprites, ref ApplyStats stats)
        {
            var changed = false;
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image == null)
                {
                    continue;
                }

                if (ShouldSkip(image))
                {
                    continue;
                }

                if (!TryResolveSprite(image, sprites, out var targetSprite, out var targetType, out var kind))
                {
                    continue;
                }

                if (!CanReplace(image, sprites))
                {
                    continue;
                }

                if (image.sprite != targetSprite || image.type != targetType)
                {
                    image.sprite = targetSprite;
                    image.type = targetType;
                    image.preserveAspect = false;
                    EditorUtility.SetDirty(image);
                    stats.ImagesChanged++;
                    changed = true;

                    if (kind == SkinKind.Button)
                    {
                        stats.ButtonsSkinned++;
                    }
                    else if (kind == SkinKind.Input)
                    {
                        stats.InputsSkinned++;
                    }
                    else if (kind == SkinKind.Slot)
                    {
                        stats.SlotsSkinned++;
                    }
                }
            }

            return changed;
        }

        private static bool ShouldSkip(Image image)
        {
            var name = image.gameObject.name.ToLowerInvariant();
            if (name.Contains("icon") || name.Contains("portrait") || name.Contains("avatar") || name.Contains("thumbnail"))
            {
                return true;
            }

            var path = image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : string.Empty;
            if (!string.IsNullOrEmpty(path) && path.Contains("Clean Vector Icons"))
            {
                return true;
            }

            return false;
        }

        private static bool CanReplace(Image image, SpriteSet sprites)
        {
            if (image.sprite == null)
            {
                return true;
            }

            if (sprites.Cache.Contains(image.sprite))
            {
                return true;
            }

            var path = AssetDatabase.GetAssetPath(image.sprite);
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            if (path.StartsWith("Assets/_Game/Art/UIThemes/KenneyUI", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var name = image.gameObject.name.ToLowerInvariant();
            if (name.Contains("panel") || name.Contains("background") || name.Contains("frame") || name.Contains("button") || name.Contains("slot"))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveSprite(Image image, SpriteSet sprites, out Sprite sprite, out Image.Type type, out SkinKind kind)
        {
            sprite = null;
            type = Image.Type.Simple;
            kind = SkinKind.Other;

            var name = image.gameObject.name.ToLowerInvariant();

            var toggle = image.GetComponentInParent<Toggle>();
            if (toggle != null)
            {
                if (name.Contains("checkmark") || name.Contains("tick") || name.Contains("mark"))
                {
                    sprite = sprites.ToggleCheckmark;
                    type = Image.Type.Simple;
                    kind = SkinKind.Other;
                    return true;
                }

                if (toggle.targetGraphic == image)
                {
                    sprite = sprites.ToggleBackground;
                    type = Image.Type.Sliced;
                    kind = SkinKind.Input;
                    return true;
                }
            }

            var slider = image.GetComponentInParent<Slider>();
            if (slider != null)
            {
                if (slider.fillRect != null && slider.fillRect.GetComponent<Image>() == image)
                {
                    sprite = sprites.SliderFill;
                    type = Image.Type.Sliced;
                    kind = SkinKind.Input;
                    return true;
                }

                if (slider.handleRect != null && slider.handleRect.GetComponent<Image>() == image)
                {
                    sprite = sprites.SliderHandle;
                    type = Image.Type.Simple;
                    kind = SkinKind.Input;
                    return true;
                }

                if (name.Contains("background") || name.Contains("track"))
                {
                    sprite = sprites.SliderBackground;
                    type = Image.Type.Sliced;
                    kind = SkinKind.Input;
                    return true;
                }
            }

            var dropdown = image.GetComponentInParent<Dropdown>();
            if (dropdown != null)
            {
                if (name.Contains("arrow"))
                {
                    sprite = sprites.DropdownArrow;
                    type = Image.Type.Simple;
                    kind = SkinKind.Input;
                    return true;
                }

                sprite = sprites.Input;
                type = Image.Type.Sliced;
                kind = SkinKind.Input;
                return true;
            }

            var input = image.GetComponentInParent<InputField>();
            if (input != null)
            {
                if (name.Contains("placeholder") || name.Contains("text"))
                {
                    return false;
                }

                sprite = sprites.Input;
                type = Image.Type.Sliced;
                kind = SkinKind.Input;
                return true;
            }

            var button = image.GetComponent<Button>();
            if (button != null || name.Contains("button") || name.Contains("btn"))
            {
                sprite = sprites.Button;
                type = Image.Type.Sliced;
                kind = SkinKind.Button;
                return true;
            }

            if (name.Contains("slot") || name.Contains("cell") || name.Contains("griditem"))
            {
                sprite = sprites.Slot;
                type = Image.Type.Sliced;
                kind = SkinKind.Slot;
                return true;
            }

            if (name.Contains("panel") || name.Contains("frame") || name.Contains("window") ||
                name.Contains("backdrop") || name.Contains("background") || name.Contains("header") ||
                name.Contains("footer") || name.Contains("container") || name.Contains("body"))
            {
                sprite = name.Contains("header") || name.Contains("footer") || name.Contains("title")
                    ? sprites.PanelAlt
                    : sprites.Panel;
                type = Image.Type.Sliced;
                kind = SkinKind.Other;
                return true;
            }

            return false;
        }

        private sealed class SpriteSet
        {
            public Sprite Panel;
            public Sprite PanelAlt;
            public Sprite Button;
            public Sprite Slot;
            public Sprite Input;
            public Sprite ToggleBackground;
            public Sprite ToggleCheckmark;
            public Sprite SliderBackground;
            public Sprite SliderFill;
            public Sprite SliderHandle;
            public Sprite DropdownArrow;
            public HashSet<Sprite> Cache;

            public bool IsValid =>
                Panel != null &&
                PanelAlt != null &&
                Button != null &&
                Slot != null &&
                Input != null &&
                ToggleBackground != null &&
                ToggleCheckmark != null &&
                SliderBackground != null &&
                SliderFill != null &&
                SliderHandle != null &&
                DropdownArrow != null;
        }

        private struct ApplyStats
        {
            public bool Changed;
            public int RootsTouched;
            public int ImagesChanged;
            public int ButtonsSkinned;
            public int InputsSkinned;
            public int SlotsSkinned;
        }

        private enum SkinKind
        {
            Other,
            Button,
            Input,
            Slot,
        }
    }
}
