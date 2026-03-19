using System.Collections.Generic;
using System.IO;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using CombatSystem.Input;
using CombatSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace CombatSystem.EditorTools
{
    public static class Day4SetupUtility
    {
        private const int DefaultPlayerStartingCurrency = 300;
        private const string LootTablePath = "Assets/_Game/ScriptableObjects/Loot/LootTable_Default.asset";
        private const string VendorPath = "Assets/_Game/ScriptableObjects/Vendors/Vendor_Default.asset";
        private const string LootPickupPrefabPath = "Assets/_Game/Prefabs/LootPickup.prefab";
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player.prefab";
        private const string GameDatabasePath = "Assets/_Game/ScriptableObjects/Database/GameDatabase.asset";
        private const string InputActionsPath = "Assets/_Game/Input/CombatInputActions.inputactions";
        private const string VendorSceneName = "Vendor";
        private const string VendorScenePath = "Assets/Scenes/Vendor.unity";
        private const string TownScenePath = "Assets/Scenes/Town.unity";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Combat/Day4/Setup Assets (Loot/Vendor/Pickup)")]
        public static void SetupAssets()
        {
            EnsureFolder("Assets/_Game/ScriptableObjects/Loot");
            EnsureFolder("Assets/_Game/ScriptableObjects/Vendors");
            EnsureFolder("Assets/_Game/Prefabs");

            var lootTable = CreateOrUpdateLootTable();
            var vendor = CreateOrUpdateVendor();
            var pickupPrefab = CreateOrUpdateLootPickupPrefab();
            UpdateGameDatabase(lootTable, vendor);

            Debug.Log("[Day4] Assets setup complete.", lootTable);
            Selection.activeObject = lootTable != null ? lootTable : vendor;
        }

        [MenuItem("Combat/Day4/Setup Player Prefab (Currency)")]
        public static void SetupPlayerPrefab()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Day4] Player prefab not found at " + PlayerPrefabPath);
                return;
            }

            var currency = prefab.GetComponent<CurrencyComponent>();
            if (currency == null)
            {
                currency = prefab.AddComponent<CurrencyComponent>();
            }

            if (currency != null)
            {
                var currencySerialized = new SerializedObject(currency);
                currencySerialized.FindProperty("startingAmount").intValue = DefaultPlayerStartingCurrency;
                currencySerialized.FindProperty("maxAmount").intValue = 0;
                currencySerialized.FindProperty("initializeOnAwake").boolValue = true;
                currencySerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(currency);
            }

            PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefab);
            Debug.Log("[Day4] Player prefab currency setup complete.");
        }

        [MenuItem("Combat/Day4/Setup Scene Loot (Health -> LootDropper)")]
        public static void SetupSceneLoot()
        {
            var lootTable = AssetDatabase.LoadAssetAtPath<LootTableDefinition>(LootTablePath);
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<LootPickup>(LootPickupPrefabPath);
            if (lootTable == null || pickupPrefab == null)
            {
                Debug.LogWarning("[Day4] Missing loot table or pickup prefab. Run Setup Assets first.");
                return;
            }

            var healths = Object.FindObjectsOfType<HealthComponent>(true);
            var modified = 0;
            foreach (var health in healths)
            {
                if (health == null || health.CompareTag("Player"))
                {
                    continue;
                }

                var dropper = health.GetComponent<LootDropper>();
                if (dropper == null)
                {
                    dropper = health.gameObject.AddComponent<LootDropper>();
                }

                SetPrivateField(dropper, "lootTable", lootTable);
                SetPrivateField(dropper, "pickupPrefab", pickupPrefab);

                var despawn = health.GetComponent<DespawnOnDeath>();
                if (despawn == null)
                {
                    health.gameObject.AddComponent<DespawnOnDeath>();
                }
                modified++;
            }

            Debug.Log($"[Day4] Scene loot setup complete. Modified: {modified}");
        }

        [MenuItem("Combat/Day4/Setup Vendor NPC & UI")]
        public static void SetupVendorNpcAndUi()
        {
            if (!ValidateActiveSceneForVendorSetup())
            {
                return;
            }

            SetupVendorNpcAndUiInActiveScene(true);
        }

        [MenuItem("Combat/Day4/Rebuild Vendor UI (Town + Vendor)")]
        public static void RebuildVendorUiForCoreScenes()
        {
            SetupPlayerPrefab();
            SetupVendorRuntimeForScene(TownScenePath);
            SetupVendorRuntimeForScene(VendorScenePath);
            Debug.Log("[Day4] Vendor UI rebuilt for Town and Vendor scenes.");
        }

        [MenuItem("Combat/Day4/Migrate Vendor Runtime To Town")]
        public static void MigrateVendorRuntimeToTown()
        {
            SetupVendorRuntimeForScene(TownScenePath);
            Selection.activeObject = null;
            var removedCount = CleanupVendorRuntimeForScene(MainMenuScenePath);
            if (File.Exists(TownScenePath))
            {
                var townScene = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
                SceneManager.SetActiveScene(townScene);
            }

            Selection.activeObject = null;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Day4] Vendor runtime migration complete. Town updated, MainMenu removed roots: {removedCount}.");
        }

        private static void SetupVendorRuntimeForScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Day4] Scene not found: {scenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            SetupVendorNpcAndUiInActiveScene(true);
            Day5SetupUtility.SetupQuestRuntime();
            if (Application.isBatchMode)
            {
                Selection.activeObject = null;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static int CleanupVendorRuntimeForScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Day4] Scene not found: {scenePath}");
                return 0;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            var removed = RemoveVendorRuntimeRootsInActiveScene();
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return removed;
        }

        private static int RemoveVendorRuntimeRootsInActiveScene()
        {
            var roots = new HashSet<GameObject>();
            CollectRootObjects<VendorService>(roots);
            CollectRootObjects<VendorTrigger>(roots);
            CollectRootObjects<VendorScreen>(roots);
            CollectRootObjects<QuestTracker>(roots);
            CollectRootObjects<QuestTrackerHUD>(roots);
            CollectRootObjects<QuestGiverTrigger>(roots);

            var removed = 0;
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                Object.DestroyImmediate(root);
                removed++;
            }

            return removed;
        }

        private static void CollectRootObjects<T>(HashSet<GameObject> roots) where T : Component
        {
            if (roots == null)
            {
                return;
            }

            var components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    roots.Add(components[i].gameObject);
                }
            }
        }

        private static void SetupVendorNpcAndUiInActiveScene(bool forceRebuild)
        {
            var vendor = AssetDatabase.LoadAssetAtPath<VendorDefinition>(VendorPath);
            if (vendor == null)
            {
                vendor = CreateOrUpdateVendor();
            }

            EnsureEventSystemInput();
            var vendorService = EnsureVendorService(vendor);
            var vendorScreen = EnsureVendorScreen(vendorService, forceRebuild);
            EnsureVendorNpc(vendorScreen);

            if (!Application.isBatchMode && vendorScreen != null)
            {
                Selection.activeObject = vendorScreen.gameObject;
            }

            Debug.Log("[Day4] Vendor NPC & UI setup complete.");
        }

        private static bool ValidateActiveSceneForVendorSetup()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogWarning("[Day4] No valid active scene. Open Vendor scene and try again.");
                return false;
            }

            if (!string.Equals(activeScene.name, VendorSceneName, System.StringComparison.Ordinal))
            {
                Debug.LogWarning($"[Day4] Active scene is '{activeScene.name}'. Please open '{VendorSceneName}' before running Setup Vendor NPC & UI.");
                return false;
            }

            return true;
        }

        private static LootTableDefinition CreateOrUpdateLootTable()
        {
            var lootTable = AssetDatabase.LoadAssetAtPath<LootTableDefinition>(LootTablePath);
            if (lootTable == null)
            {
                lootTable = ScriptableObject.CreateInstance<LootTableDefinition>();
                AssetDatabase.CreateAsset(lootTable, LootTablePath);
            }

            var items = FindItemDefinitions();
            var serialized = new SerializedObject(lootTable);
            serialized.FindProperty("minRolls").intValue = 1;
            serialized.FindProperty("maxRolls").intValue = 2;

            var entries = serialized.FindProperty("entries");
            entries.ClearArray();
            entries.arraySize = 2;

            var currencyEntry = entries.GetArrayElementAtIndex(0);
            currencyEntry.FindPropertyRelative("type").enumValueIndex = (int)LootEntryType.Currency;
            currencyEntry.FindPropertyRelative("weight").intValue = 4;
            currencyEntry.FindPropertyRelative("minCurrency").intValue = 5;
            currencyEntry.FindPropertyRelative("maxCurrency").intValue = 15;

            var itemEntry = entries.GetArrayElementAtIndex(1);
            itemEntry.FindPropertyRelative("type").enumValueIndex = (int)LootEntryType.Item;
            itemEntry.FindPropertyRelative("weight").intValue = 6;
            itemEntry.FindPropertyRelative("minStack").intValue = 1;
            itemEntry.FindPropertyRelative("maxStack").intValue = 1;

            if (items.Count > 0)
            {
                itemEntry.FindPropertyRelative("item").objectReferenceValue = items[0];
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(lootTable);
            AssetDatabase.SaveAssets();
            return lootTable;
        }

        private static VendorDefinition CreateOrUpdateVendor()
        {
            var vendor = AssetDatabase.LoadAssetAtPath<VendorDefinition>(VendorPath);
            if (vendor == null)
            {
                vendor = ScriptableObject.CreateInstance<VendorDefinition>();
                AssetDatabase.CreateAsset(vendor, VendorPath);
            }

            var items = FindItemDefinitions();
            EnsureItemEconomyDefaults(items);
            var serialized = new SerializedObject(vendor);
            serialized.FindProperty("vendorRuntimeVersion").intValue = 1;
            serialized.FindProperty("buyPriceMultiplier").floatValue = 1f;
            serialized.FindProperty("sellPriceMultiplier").floatValue = 0.5f;
            serialized.FindProperty("allowBuyBack").boolValue = true;
            serialized.FindProperty("maxBuyBackEntries").intValue = 12;
            serialized.FindProperty("refreshMode").enumValueIndex = (int)VendorRefreshMode.Never;
            serialized.FindProperty("refreshIntervalMinutes").intValue = 30;
            serialized.FindProperty("restockOnOpen").boolValue = false;

            var entries = serialized.FindProperty("items");
            entries.ClearArray();
            if (items.Count > 0)
            {
                entries.arraySize = Mathf.Min(3, items.Count);
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("item").objectReferenceValue = items[i];
                    entry.FindPropertyRelative("priceOverride").intValue = -1;
                    entry.FindPropertyRelative("stock").intValue = 3;
                    entry.FindPropertyRelative("infiniteStock").boolValue = true;
                }
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(vendor);
            AssetDatabase.SaveAssets();
            return vendor;
        }

        private static LootPickup CreateOrUpdateLootPickupPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootPickupPrefabPath);
            if (prefab == null)
            {
                var temp = new GameObject("LootPickup");
                var collider = temp.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.6f;
                temp.AddComponent<LootPickup>();
                PrefabUtility.SaveAsPrefabAsset(temp, LootPickupPrefabPath);
                Object.DestroyImmediate(temp);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootPickupPrefabPath);
            }

            if (prefab == null)
            {
                return null;
            }

            var root = PrefabUtility.LoadPrefabContents(LootPickupPrefabPath);
            EnsurePickupVisual(root);
            PrefabUtility.SaveAsPrefabAsset(root, LootPickupPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<LootPickup>(LootPickupPrefabPath);
        }

        private static void UpdateGameDatabase(LootTableDefinition lootTable, VendorDefinition vendor)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabasePath);
            if (database == null)
            {
                Debug.LogWarning("[Day4] GameDatabase not found at " + GameDatabasePath);
                return;
            }

            var serialized = new SerializedObject(database);
            AddToList(serialized.FindProperty("lootTables"), lootTable);
            AddToList(serialized.FindProperty("vendors"), vendor);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
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

            var index = list.arraySize;
            list.arraySize++;
            list.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }

        private static List<ItemDefinition> FindItemDefinitions()
        {
            var results = new List<ItemDefinition>();
            var guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game/ScriptableObjects/Items" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item != null)
                {
                    results.Add(item);
                }
            }

            results.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return results;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void EnsurePickupVisual(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var existing = root.transform.Find("Visual");
            if (existing != null)
            {
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Icons/Item.png");
            if (sprite == null)
            {
                return;
            }

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            visual.transform.localScale = Vector3.one * 0.2f;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 10;
        }

        private static VendorService EnsureVendorService(VendorDefinition vendor)
        {
            var service = Object.FindFirstObjectByType<VendorService>();
            if (service == null)
            {
                var go = new GameObject("VendorService");
                service = go.AddComponent<VendorService>();
            }

            var serialized = new SerializedObject(service);
            serialized.FindProperty("vendorDefinition").objectReferenceValue = vendor;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(service);
            return service;
        }

        private static void EnsureEventSystemInput()
        {
            var system = Object.FindFirstObjectByType<EventSystem>();
            if (system == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                system = go.GetComponent<EventSystem>();
            }

            var module = system.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = system.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            var legacy = system.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy);
            }

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
            {
                Debug.LogWarning("[Day4] InputActionAsset not found at " + InputActionsPath);
                return;
            }

            ConfigureModule(module, actions);
            EnsureInputReader(actions);
            EditorUtility.SetDirty(system);
        }

        private static void ConfigureModule(InputSystemUIInputModule module, InputActionAsset actions)
        {
            if (module == null || actions == null)
            {
                return;
            }

            module.actionsAsset = actions;
            module.point = CreateReference(actions, "UI/Point");
            module.leftClick = CreateReference(actions, "UI/Click");
            module.scrollWheel = CreateReference(actions, "UI/ScrollWheel");
            module.move = CreateReference(actions, "UI/Navigate");
            module.submit = CreateReference(actions, "UI/Submit");
            module.cancel = CreateReference(actions, "UI/Cancel");
        }

        private static InputActionReference CreateReference(InputActionAsset asset, string actionPath)
        {
            var action = asset != null ? asset.FindAction(actionPath) : null;
            return action != null ? InputActionReference.Create(action) : null;
        }

        private static void EnsureInputReader(InputActionAsset actions)
        {
            if (actions == null)
            {
                return;
            }

            var reader = Object.FindFirstObjectByType<InputReader>();
            if (reader == null)
            {
                var go = new GameObject("InputRoot");
                reader = go.AddComponent<InputReader>();
            }

            var uiRoot = Object.FindFirstObjectByType<UIRoot>();
            var manager = uiRoot != null ? uiRoot.Manager : Object.FindFirstObjectByType<UIManager>();
            var serialized = new SerializedObject(reader);
            var uiManagerProp = serialized.FindProperty("uiManager");
            if (uiManagerProp != null)
            {
                uiManagerProp.objectReferenceValue = manager;
            }

            var autoFindProp = serialized.FindProperty("autoFindUiManager");
            if (autoFindProp != null)
            {
                autoFindProp.boolValue = true;
            }

            serialized.ApplyModifiedProperties();
            reader.SetActions(actions);
            EditorUtility.SetDirty(reader);
        }

        private static VendorScreen EnsureVendorScreen(VendorService vendorService, bool forceRebuild)
        {
            var screen = Object.FindFirstObjectByType<VendorScreen>();
            if (screen != null && forceRebuild)
            {
                Object.DestroyImmediate(screen.gameObject);
                screen = null;
            }

            if (screen != null)
            {
                return screen;
            }

            var uiRoot = Object.FindFirstObjectByType<UIRoot>();
            if (uiRoot == null || uiRoot.ScreensCanvas == null)
            {
                Debug.LogWarning("[Day4] UIRoot/ScreensCanvas not found. Create UI root first.");
                return null;
            }

            var root = new GameObject("VendorScreen", typeof(RectTransform), typeof(CanvasGroup));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(uiRoot.ScreensCanvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            screen = root.AddComponent<VendorScreen>();
            var screenSerialized = new SerializedObject(screen);
            var inputProp = screenSerialized.FindProperty("inputMode");
            if (inputProp != null)
            {
                inputProp.enumValueIndex = (int)UIInputMode.UI;
                screenSerialized.ApplyModifiedProperties();
            }

            BuildVendorScreenUi(screen, uiRoot, vendorService);
            return screen;
        }

        private static void BuildVendorScreenUi(VendorScreen screen, UIRoot uiRoot, VendorService vendorService)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var backdrop = CreateUiPanel("Backdrop", screen.transform, Vector2.one);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.02f, 0.04f, 0.08f, 0.72f);

            var window = CreateUiPanel("Window", screen.transform, Vector2.one);
            var windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.05f, 0.05f);
            windowRect.anchorMax = new Vector2(0.95f, 0.95f);
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;
            var windowImage = window.GetComponent<Image>();
            windowImage.color = new Color(0.08f, 0.12f, 0.18f, 0.97f);
            var windowOutline = window.AddComponent<Outline>();
            windowOutline.effectColor = new Color(0.18f, 0.31f, 0.52f, 0.9f);
            windowOutline.effectDistance = new Vector2(2f, -2f);

            var windowLayout = window.AddComponent<VerticalLayoutGroup>();
            windowLayout.padding = new RectOffset(18, 18, 18, 18);
            windowLayout.spacing = 14f;
            windowLayout.childAlignment = TextAnchor.UpperLeft;
            windowLayout.childControlHeight = true;
            windowLayout.childControlWidth = true;
            windowLayout.childForceExpandHeight = false;
            windowLayout.childForceExpandWidth = true;

            var header = CreateUiPanel("Header", window.transform, Vector2.one);
            var headerImage = header.GetComponent<Image>();
            headerImage.color = new Color(0.1f, 0.16f, 0.24f, 1f);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(18, 18, 14, 14);
            headerLayout.spacing = 18f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlHeight = true;
            headerLayout.childControlWidth = true;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childForceExpandWidth = false;
            header.AddComponent<LayoutElement>().preferredHeight = 68f;

            var titleBlock = new GameObject("TitleBlock", typeof(RectTransform), typeof(LayoutElement));
            titleBlock.transform.SetParent(header.transform, false);
            var titleBlockLayout = titleBlock.AddComponent<VerticalLayoutGroup>();
            titleBlockLayout.spacing = 0f;
            titleBlockLayout.childAlignment = TextAnchor.MiddleLeft;
            titleBlockLayout.childControlHeight = true;
            titleBlockLayout.childControlWidth = true;
            titleBlockLayout.childForceExpandHeight = false;
            titleBlockLayout.childForceExpandWidth = true;
            var titleBlockElement = titleBlock.GetComponent<LayoutElement>();
            titleBlockElement.flexibleWidth = 1f;
            titleBlockElement.minWidth = 0f;

            var titleText = CreateText("ScreenTitle", titleBlock.transform, font, "商人交易");
            titleText.fontSize = 30;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.GetComponent<LayoutElement>().preferredHeight = 34f;

            var currencyText = CreateText("CurrencyText", header.transform, font, "0G");
            currencyText.fontSize = 30;
            currencyText.fontStyle = FontStyle.Bold;
            currencyText.alignment = TextAnchor.MiddleRight;
            currencyText.color = new Color(1f, 0.84f, 0.32f, 1f);
            var currencyTextElement = currencyText.GetComponent<LayoutElement>();
            currencyTextElement.preferredWidth = 150f;
            currencyTextElement.minWidth = 120f;
            currencyTextElement.preferredHeight = 40f;

            var body = CreateUiPanel("Body", window.transform, Vector2.one);
            var bodyImage = body.GetComponent<Image>();
            bodyImage.color = new Color(0.05f, 0.08f, 0.13f, 0.96f);
            var bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.padding = new RectOffset(14, 14, 14, 14);
            bodyLayout.spacing = 14f;
            bodyLayout.childAlignment = TextAnchor.UpperLeft;
            bodyLayout.childControlHeight = true;
            bodyLayout.childControlWidth = true;
            bodyLayout.childForceExpandHeight = true;
            bodyLayout.childForceExpandWidth = false;
            var bodyElement = body.AddComponent<LayoutElement>();
            bodyElement.flexibleHeight = 1f;
            bodyElement.minHeight = 540f;

            var vendorPanel = CreateUiPanel("VendorPanel", body.transform, Vector2.one);
            vendorPanel.GetComponent<Image>().color = new Color(0.09f, 0.13f, 0.2f, 0.98f);
            var vendorElement = vendorPanel.AddComponent<LayoutElement>();
            vendorElement.preferredWidth = 370f;
            vendorElement.minWidth = 340f;
            vendorElement.flexibleWidth = 0.9f;
            var vendorLayout = vendorPanel.AddComponent<VerticalLayoutGroup>();
            vendorLayout.padding = new RectOffset(12, 12, 12, 12);
            vendorLayout.spacing = 10f;
            vendorLayout.childAlignment = TextAnchor.UpperLeft;
            vendorLayout.childControlHeight = true;
            vendorLayout.childControlWidth = true;
            vendorLayout.childForceExpandHeight = false;
            vendorLayout.childForceExpandWidth = true;

            var vendorHeader = CreateUiPanel("VendorHeader", vendorPanel.transform, Vector2.one);
            vendorHeader.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.16f, 0.98f);
            vendorHeader.AddComponent<LayoutElement>().preferredHeight = 46f;
            var vendorHeaderLayout = vendorHeader.AddComponent<VerticalLayoutGroup>();
            vendorHeaderLayout.padding = new RectOffset(12, 12, 9, 9);
            vendorHeaderLayout.spacing = 0f;
            vendorHeaderLayout.childAlignment = TextAnchor.UpperLeft;
            vendorHeaderLayout.childControlHeight = true;
            vendorHeaderLayout.childControlWidth = true;
            vendorHeaderLayout.childForceExpandHeight = false;
            vendorHeaderLayout.childForceExpandWidth = true;

            var vendorTitle = CreateText("VendorTitle", vendorHeader.transform, font, "商店货架");
            vendorTitle.fontSize = 20;
            vendorTitle.fontStyle = FontStyle.Bold;
            vendorTitle.alignment = TextAnchor.MiddleLeft;
            vendorTitle.GetComponent<LayoutElement>().preferredHeight = 28f;

            var vendorListRoot = CreateUiPanel("VendorList", vendorPanel.transform, Vector2.one);
            vendorListRoot.GetComponent<Image>().color = new Color(0.03f, 0.06f, 0.1f, 0.92f);
            var vendorListElement = vendorListRoot.AddComponent<LayoutElement>();
            vendorListElement.flexibleHeight = 1f;
            vendorListElement.minHeight = 360f;
            var vendorListGrid = vendorListRoot.AddComponent<GridLayoutGroup>();
            vendorListGrid.padding = new RectOffset(10, 10, 10, 10);
            vendorListGrid.cellSize = new Vector2(96f, 96f);
            vendorListGrid.spacing = new Vector2(10f, 10f);
            vendorListGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            vendorListGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            vendorListGrid.childAlignment = TextAnchor.UpperLeft;
            vendorListGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            vendorListGrid.constraintCount = 3;

            var vendorList = vendorListRoot.AddComponent<VendorListUI>();
            var vendorSlotTemplate = CreateVendorSlotTemplate(vendorListRoot.transform, font);
            SetPrivateField(vendorList, "slotsRoot", vendorListRoot.GetComponent<RectTransform>());
            SetPrivateField(vendorList, "slotTemplate", vendorSlotTemplate);

            var detailPanel = CreateUiPanel("SelectionDetails", body.transform, Vector2.one);
            detailPanel.GetComponent<Image>().color = new Color(0.09f, 0.13f, 0.2f, 0.98f);
            var detailElement = detailPanel.AddComponent<LayoutElement>();
            detailElement.preferredWidth = 430f;
            detailElement.minWidth = 400f;
            detailElement.flexibleWidth = 1f;
            var detailLayout = detailPanel.AddComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset(12, 12, 12, 12);
            detailLayout.spacing = 10f;
            detailLayout.childAlignment = TextAnchor.UpperLeft;
            detailLayout.childControlHeight = true;
            detailLayout.childControlWidth = true;
            detailLayout.childForceExpandHeight = false;
            detailLayout.childForceExpandWidth = true;

            var detailHeader = CreateUiPanel("DetailHeader", detailPanel.transform, Vector2.one);
            detailHeader.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.16f, 0.98f);
            detailHeader.AddComponent<LayoutElement>().preferredHeight = 124f;
            var detailHeaderLayout = detailHeader.AddComponent<HorizontalLayoutGroup>();
            detailHeaderLayout.padding = new RectOffset(14, 14, 14, 14);
            detailHeaderLayout.spacing = 12f;
            detailHeaderLayout.childAlignment = TextAnchor.UpperLeft;
            detailHeaderLayout.childControlHeight = true;
            detailHeaderLayout.childControlWidth = true;
            detailHeaderLayout.childForceExpandHeight = false;
            detailHeaderLayout.childForceExpandWidth = false;

            var detailIconFrame = CreateUiPanel("DetailIconFrame", detailHeader.transform, new Vector2(92f, 92f));
            detailIconFrame.GetComponent<Image>().color = new Color(0.12f, 0.2f, 0.31f, 1f);
            var detailIconFrameElement = detailIconFrame.AddComponent<LayoutElement>();
            detailIconFrameElement.preferredWidth = 92f;
            detailIconFrameElement.preferredHeight = 92f;
            detailIconFrameElement.minWidth = 92f;
            detailIconFrameElement.minHeight = 92f;
            var detailIcon = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
            detailIcon.transform.SetParent(detailIconFrame.transform, false);
            var detailIconRect = detailIcon.GetComponent<RectTransform>();
            detailIconRect.anchorMin = Vector2.zero;
            detailIconRect.anchorMax = Vector2.one;
            detailIconRect.offsetMin = new Vector2(10f, 10f);
            detailIconRect.offsetMax = new Vector2(-10f, -10f);
            var detailIconImage = detailIcon.GetComponent<Image>();
            detailIconImage.preserveAspect = true;
            detailIconImage.raycastTarget = false;

            var detailTextBlock = new GameObject("DetailTextBlock", typeof(RectTransform), typeof(LayoutElement));
            detailTextBlock.transform.SetParent(detailHeader.transform, false);
            var detailTextLayout = detailTextBlock.AddComponent<VerticalLayoutGroup>();
            detailTextLayout.spacing = 4f;
            detailTextLayout.childAlignment = TextAnchor.UpperLeft;
            detailTextLayout.childControlHeight = true;
            detailTextLayout.childControlWidth = true;
            detailTextLayout.childForceExpandHeight = false;
            detailTextLayout.childForceExpandWidth = true;
            var detailTextBlockElement = detailTextBlock.GetComponent<LayoutElement>();
            detailTextBlockElement.flexibleWidth = 1f;
            detailTextBlockElement.minWidth = 0f;

            var detailsTitle = CreateText("DetailsTitle", detailTextBlock.transform, font, "交易详情");
            detailsTitle.fontSize = 12;
            detailsTitle.color = new Color(0.74f, 0.8f, 0.9f, 0.9f);
            detailsTitle.alignment = TextAnchor.MiddleLeft;
            detailsTitle.GetComponent<LayoutElement>().preferredHeight = 20f;

            var detailTitle = CreateText("DetailTitle", detailTextBlock.transform, font, "未选中物品");
            detailTitle.fontSize = 22;
            detailTitle.fontStyle = FontStyle.Bold;
            detailTitle.alignment = TextAnchor.MiddleLeft;
            detailTitle.GetComponent<LayoutElement>().preferredHeight = 34f;

            var detailMeta = CreateText("DetailMeta", detailTextBlock.transform, font, "从左侧商店货架或右侧背包中选择一件物品。");
            detailMeta.fontSize = 13;
            detailMeta.color = new Color(0.82f, 0.87f, 0.94f, 0.95f);
            detailMeta.alignment = TextAnchor.UpperLeft;
            detailMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailMeta.verticalOverflow = VerticalWrapMode.Overflow;
            detailMeta.GetComponent<LayoutElement>().preferredHeight = 34f;

            var quoteCard = CreateUiPanel("QuoteCard", detailPanel.transform, Vector2.one);
            quoteCard.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.98f);
            quoteCard.AddComponent<LayoutElement>().preferredHeight = 54f;
            var quoteLayout = quoteCard.AddComponent<VerticalLayoutGroup>();
            quoteLayout.padding = new RectOffset(14, 14, 12, 12);
            quoteLayout.spacing = 0f;
            quoteLayout.childAlignment = TextAnchor.UpperLeft;
            quoteLayout.childControlHeight = true;
            quoteLayout.childControlWidth = true;
            quoteLayout.childForceExpandHeight = false;
            quoteLayout.childForceExpandWidth = true;

            var detailPrice = CreateText("DetailPrice", quoteCard.transform, font, string.Empty);
            detailPrice.fontSize = 22;
            detailPrice.alignment = TextAnchor.UpperLeft;
            detailPrice.color = new Color(0.97f, 0.84f, 0.42f, 1f);
            detailPrice.GetComponent<LayoutElement>().preferredHeight = 28f;

            var actionRow = new GameObject("ActionRow", typeof(RectTransform), typeof(LayoutElement));
            actionRow.transform.SetParent(detailPanel.transform, false);
            var actionRowLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
            actionRowLayout.spacing = 10f;
            actionRowLayout.childAlignment = TextAnchor.MiddleCenter;
            actionRowLayout.childControlHeight = true;
            actionRowLayout.childControlWidth = true;
            actionRowLayout.childForceExpandHeight = false;
            actionRowLayout.childForceExpandWidth = true;
            actionRow.GetComponent<LayoutElement>().preferredHeight = 46f;

            var buyButton = CreateButton("BuyButton", actionRow.transform, font, "购买");
            var buyButtonLayout = buyButton.GetComponent<LayoutElement>();
            buyButtonLayout.minWidth = 150f;
            buyButtonLayout.preferredWidth = 0f;
            buyButtonLayout.flexibleWidth = 1f;
            buyButtonLayout.minHeight = 46f;
            buyButtonLayout.preferredHeight = 46f;

            var sellButton = CreateButton("SellButton", actionRow.transform, font, "出售");
            var sellButtonLayout = sellButton.GetComponent<LayoutElement>();
            sellButtonLayout.minWidth = 150f;
            sellButtonLayout.preferredWidth = 0f;
            sellButtonLayout.flexibleWidth = 1f;
            sellButtonLayout.minHeight = 46f;
            sellButtonLayout.preferredHeight = 46f;

            var detailDescriptionPanel = CreateUiPanel("DescriptionPanel", detailPanel.transform, Vector2.one);
            detailDescriptionPanel.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.98f);
            var detailDescriptionPanelElement = detailDescriptionPanel.AddComponent<LayoutElement>();
            detailDescriptionPanelElement.flexibleHeight = 1f;
            detailDescriptionPanelElement.minHeight = 168f;
            var detailDescriptionLayout = detailDescriptionPanel.AddComponent<VerticalLayoutGroup>();
            detailDescriptionLayout.padding = new RectOffset(14, 14, 12, 12);
            detailDescriptionLayout.spacing = 6f;
            detailDescriptionLayout.childAlignment = TextAnchor.UpperLeft;
            detailDescriptionLayout.childControlHeight = true;
            detailDescriptionLayout.childControlWidth = true;
            detailDescriptionLayout.childForceExpandHeight = false;
            detailDescriptionLayout.childForceExpandWidth = true;

            var descriptionTitle = CreateText("DescriptionTitle", detailDescriptionPanel.transform, font, "物品说明");
            descriptionTitle.fontSize = 14;
            descriptionTitle.fontStyle = FontStyle.Bold;
            descriptionTitle.alignment = TextAnchor.MiddleLeft;
            descriptionTitle.GetComponent<LayoutElement>().preferredHeight = 24f;

            var detailDescription = CreateText("DetailDescription", detailDescriptionPanel.transform, font, "这里会显示物品说明、价格和库存等信息。");
            detailDescription.fontSize = 13;
            detailDescription.alignment = TextAnchor.UpperLeft;
            detailDescription.color = new Color(0.85f, 0.89f, 0.95f, 0.98f);
            detailDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailDescription.verticalOverflow = VerticalWrapMode.Overflow;
            var detailDescriptionElement = detailDescription.GetComponent<LayoutElement>();
            detailDescriptionElement.preferredHeight = 150f;
            detailDescriptionElement.flexibleHeight = 1f;

            var inventoryPanel = CreateUiPanel("InventoryPanel", body.transform, Vector2.one);
            inventoryPanel.GetComponent<Image>().color = new Color(0.09f, 0.13f, 0.2f, 0.98f);
            var inventoryElement = inventoryPanel.AddComponent<LayoutElement>();
            inventoryElement.preferredWidth = 620f;
            inventoryElement.minWidth = 560f;
            inventoryElement.flexibleWidth = 1.2f;
            var inventoryLayout = inventoryPanel.AddComponent<VerticalLayoutGroup>();
            inventoryLayout.padding = new RectOffset(12, 12, 12, 12);
            inventoryLayout.spacing = 10f;
            inventoryLayout.childAlignment = TextAnchor.UpperLeft;
            inventoryLayout.childControlHeight = true;
            inventoryLayout.childControlWidth = true;
            inventoryLayout.childForceExpandHeight = false;
            inventoryLayout.childForceExpandWidth = true;

            var inventoryHeader = CreateUiPanel("InventoryHeader", inventoryPanel.transform, Vector2.one);
            inventoryHeader.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.16f, 0.98f);
            inventoryHeader.AddComponent<LayoutElement>().preferredHeight = 46f;
            var inventoryHeaderLayout = inventoryHeader.AddComponent<VerticalLayoutGroup>();
            inventoryHeaderLayout.padding = new RectOffset(12, 12, 9, 9);
            inventoryHeaderLayout.spacing = 0f;
            inventoryHeaderLayout.childAlignment = TextAnchor.UpperLeft;
            inventoryHeaderLayout.childControlHeight = true;
            inventoryHeaderLayout.childControlWidth = true;
            inventoryHeaderLayout.childForceExpandHeight = false;
            inventoryHeaderLayout.childForceExpandWidth = true;

            var inventoryTitle = CreateText("InventoryTitle", inventoryHeader.transform, font, "我的背包");
            inventoryTitle.fontSize = 20;
            inventoryTitle.fontStyle = FontStyle.Bold;
            inventoryTitle.alignment = TextAnchor.MiddleLeft;
            inventoryTitle.GetComponent<LayoutElement>().preferredHeight = 28f;

            var inventoryGridRoot = CreateUiPanel("InventoryGrid", inventoryPanel.transform, Vector2.one);
            inventoryGridRoot.GetComponent<Image>().color = new Color(0.03f, 0.06f, 0.1f, 0.92f);
            var inventoryGridElement = inventoryGridRoot.AddComponent<LayoutElement>();
            inventoryGridElement.flexibleHeight = 1f;
            inventoryGridElement.minHeight = 360f;
            var inventoryGridLayout = inventoryGridRoot.AddComponent<GridLayoutGroup>();
            inventoryGridLayout.padding = new RectOffset(10, 10, 10, 10);
            inventoryGridLayout.cellSize = new Vector2(80f, 80f);
            inventoryGridLayout.spacing = new Vector2(10f, 10f);
            inventoryGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            inventoryGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            inventoryGridLayout.childAlignment = TextAnchor.UpperLeft;
            inventoryGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            inventoryGridLayout.constraintCount = 6;
            var inventoryGrid = inventoryGridRoot.AddComponent<InventoryGridUI>();

            var inventorySlotTemplate = FindInventorySlotTemplate();
            if (inventorySlotTemplate == null)
            {
                inventorySlotTemplate = CreateInventorySlotTemplate(inventoryGridRoot.transform, font);
            }
            else
            {
                inventorySlotTemplate = PrefabUtility.InstantiatePrefab(inventorySlotTemplate, inventoryGridRoot.transform) as InventorySlotUI;
                if (inventorySlotTemplate != null)
                {
                    inventorySlotTemplate.gameObject.SetActive(false);
                }
            }

            NormalizeInventorySlotTemplate(inventorySlotTemplate, font);
            SetPrivateField(inventoryGrid, "slotsRoot", inventoryGridRoot.GetComponent<RectTransform>());
            SetPrivateField(inventoryGrid, "slotTemplate", inventorySlotTemplate);

            var footer = CreateUiPanel("Footer", window.transform, Vector2.one);
            footer.GetComponent<Image>().color = new Color(0.09f, 0.14f, 0.22f, 1f);
            var footerElement = footer.AddComponent<LayoutElement>();
            footerElement.preferredHeight = 30f;
            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.padding = new RectOffset(14, 14, 8, 8);
            footerLayout.spacing = 0f;
            footerLayout.childAlignment = TextAnchor.MiddleRight;
            footerLayout.childControlHeight = true;
            footerLayout.childControlWidth = true;
            footerLayout.childForceExpandWidth = false;

            var footerClose = CreateText("FooterClose", footer.transform, font, "Esc 关闭");
            footerClose.fontSize = 12;
            footerClose.color = new Color(0.74f, 0.8f, 0.9f, 0.92f);
            footerClose.alignment = TextAnchor.MiddleRight;
            footerClose.GetComponent<LayoutElement>().preferredWidth = 100f;

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("uiManager").objectReferenceValue = uiRoot.Manager;
            serialized.FindProperty("vendorService").objectReferenceValue = vendorService;
            serialized.FindProperty("playerInventory").objectReferenceValue = Object.FindFirstObjectByType<InventoryComponent>();
            serialized.FindProperty("playerCurrency").objectReferenceValue = Object.FindFirstObjectByType<CurrencyComponent>();
            serialized.FindProperty("vendorList").objectReferenceValue = vendorList;
            serialized.FindProperty("inventoryGrid").objectReferenceValue = inventoryGrid;
            serialized.FindProperty("buyButton").objectReferenceValue = buyButton;
            serialized.FindProperty("sellButton").objectReferenceValue = sellButton;
            serialized.FindProperty("currencyText").objectReferenceValue = currencyText;
            serialized.FindProperty("statusText").objectReferenceValue = null;
            serialized.FindProperty("screenTitleText").objectReferenceValue = titleText;
            serialized.FindProperty("detailTitleText").objectReferenceValue = detailTitle;
            serialized.FindProperty("detailMetaText").objectReferenceValue = detailMeta;
            serialized.FindProperty("detailPriceText").objectReferenceValue = detailPrice;
            serialized.FindProperty("detailDescriptionText").objectReferenceValue = detailDescription;
            serialized.FindProperty("detailIconImage").objectReferenceValue = detailIconImage;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(screen);
        }

        private static void EnsureVendorNpc(VendorScreen vendorScreen)
        {
            var npc = GameObject.Find("VendorNPC");
            var created = false;
            if (npc == null)
            {
                npc = new GameObject("VendorNPC");
                npc.transform.position = new Vector3(2f, 0f, 1f);
                created = true;
            }

            var collider = npc.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = npc.AddComponent<SphereCollider>();
            }

            collider.isTrigger = true;
            collider.radius = 1.5f;

            var trigger = npc.GetComponent<VendorTrigger>();
            if (trigger == null)
            {
                trigger = npc.AddComponent<VendorTrigger>();
            }

            EnsureVendorNpcVisual(npc, created);

            var serialized = new SerializedObject(trigger);
            serialized.FindProperty("vendorScreen").objectReferenceValue = vendorScreen;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(trigger);
        }

        private static void EnsureVendorNpcVisual(GameObject npc, bool created)
        {
            if (npc == null)
            {
                return;
            }

            var visual = npc.transform.Find("Visual");
            if (visual == null)
            {
                var visualGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visualGo.name = "Visual";
                visualGo.transform.SetParent(npc.transform, false);
                visualGo.transform.localPosition = new Vector3(0f, 1f, 0f);
                visualGo.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);

                var collider = visualGo.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }

                visual = visualGo.transform;
            }

            if (visual != null && visual.localPosition.y < 0.2f)
            {
                visual.localPosition = new Vector3(visual.localPosition.x, 1f, visual.localPosition.z);
            }

            if (created)
            {
                var renderer = visual != null ? visual.GetComponent<Renderer>() : null;
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    var instanceMaterial = new Material(renderer.sharedMaterial);
                    instanceMaterial.color = new Color(0.82f, 0.74f, 0.45f, 1f);
                    renderer.sharedMaterial = instanceMaterial;
                }
            }
        }

        private static GameObject CreateUiPanel(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.25f);
            return go;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(108f, 34f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.27f, 0.38f, 1f);

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.27f, 0.38f, 1f);
            colors.highlightedColor = new Color(0.27f, 0.35f, 0.48f, 1f);
            colors.pressedColor = new Color(0.12f, 0.19f, 0.3f, 1f);
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.65f);
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            var buttonLayout = go.GetComponent<LayoutElement>();
            buttonLayout.minWidth = 96f;
            buttonLayout.preferredWidth = 108f;
            buttonLayout.minHeight = 34f;
            buttonLayout.preferredHeight = 34f;
            buttonLayout.flexibleWidth = 0f;

            var text = CreateText("Text", go.transform, font, label);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 14;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private static Text CreateText(string name, Transform parent, Font font, string value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(160f, 32f);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.color = Color.white;
            text.fontSize = 16;
            return text;
        }

        private static VendorItemSlotUI CreateVendorSlotTemplate(Transform parent, Font font)
        {
            var root = new GameObject("VendorSlotTemplate", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            root.SetActive(false);
            var button = root.AddComponent<Button>();
            var rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0.03f, 0.09f, 0.16f, 0.96f);
            var rootOutline = root.AddComponent<Outline>();
            rootOutline.effectColor = new Color(0.14f, 0.24f, 0.38f, 0.95f);
            rootOutline.effectDistance = new Vector2(1f, -1f);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.12f, 0.26f);
            iconRect.anchorMax = new Vector2(0.88f, 0.82f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var selection = new GameObject("Selection", typeof(RectTransform), typeof(Image));
            selection.transform.SetParent(root.transform, false);
            var selectionRect = selection.GetComponent<RectTransform>();
            selectionRect.anchorMin = Vector2.zero;
            selectionRect.anchorMax = Vector2.one;
            selectionRect.offsetMin = Vector2.zero;
            selectionRect.offsetMax = Vector2.zero;
            var selectionImage = selection.GetComponent<Image>();
            selectionImage.color = new Color(1f, 1f, 1f, 0.15f);
            selectionImage.enabled = false;

            var priceChip = new GameObject("PriceChip", typeof(RectTransform), typeof(Image));
            priceChip.transform.SetParent(root.transform, false);
            var priceChipRect = priceChip.GetComponent<RectTransform>();
            priceChipRect.anchorMin = new Vector2(0.05f, 0.06f);
            priceChipRect.anchorMax = new Vector2(0.56f, 0.24f);
            priceChipRect.offsetMin = Vector2.zero;
            priceChipRect.offsetMax = Vector2.zero;
            var priceChipImage = priceChip.GetComponent<Image>();
            priceChipImage.color = new Color(0.1f, 0.14f, 0.22f, 0.94f);
            priceChipImage.raycastTarget = false;

            var priceText = CreateText("Price", priceChip.transform, font, "0G");
            priceText.fontSize = 11;
            priceText.fontStyle = FontStyle.Bold;
            priceText.alignment = TextAnchor.MiddleCenter;
            priceText.color = new Color(1f, 0.86f, 0.42f, 1f);
            var priceRect = priceText.GetComponent<RectTransform>();
            priceRect.anchorMin = Vector2.zero;
            priceRect.anchorMax = Vector2.one;
            priceRect.offsetMin = Vector2.zero;
            priceRect.offsetMax = Vector2.zero;

            var stockChip = new GameObject("StockChip", typeof(RectTransform), typeof(Image));
            stockChip.transform.SetParent(root.transform, false);
            var stockChipRect = stockChip.GetComponent<RectTransform>();
            stockChipRect.anchorMin = new Vector2(0.58f, 0.04f);
            stockChipRect.anchorMax = new Vector2(0.95f, 0.24f);
            stockChipRect.offsetMin = Vector2.zero;
            stockChipRect.offsetMax = Vector2.zero;
            var stockChipImage = stockChip.GetComponent<Image>();
            stockChipImage.color = new Color(0f, 0f, 0f, 0f);
            stockChipImage.raycastTarget = false;

            var stockText = CreateText("Stock", stockChip.transform, font, "∞");
            stockText.fontSize = 14;
            stockText.fontStyle = FontStyle.Bold;
            stockText.alignment = TextAnchor.MiddleRight;
            stockText.color = new Color(0.79f, 0.86f, 0.94f, 1f);
            stockText.raycastTarget = false;
            var stockRect = stockText.GetComponent<RectTransform>();
            stockRect.anchorMin = Vector2.zero;
            stockRect.anchorMax = Vector2.one;
            stockRect.offsetMin = Vector2.zero;
            stockRect.offsetMax = Vector2.zero;
            var stockOutline = stockText.gameObject.AddComponent<Outline>();
            stockOutline.effectColor = new Color(0.02f, 0.05f, 0.1f, 0.92f);
            stockOutline.effectDistance = new Vector2(1f, -1f);

            var slot = root.AddComponent<VendorItemSlotUI>();
            SetPrivateField(slot, "button", button);
            SetPrivateField(slot, "background", rootImage);
            SetPrivateField(slot, "icon", iconImage);
            SetPrivateField(slot, "selection", selection.GetComponent<Image>());
            SetPrivateField(slot, "priceText", priceText);
            SetPrivateField(slot, "stockText", stockText);
            return slot;
        }

        private static InventorySlotUI FindInventorySlotTemplate()
        {
            var inventoryScreen = Object.FindFirstObjectByType<InventoryScreen>();
            if (inventoryScreen == null)
            {
                return null;
            }

            var serialized = new SerializedObject(inventoryScreen);
            var gridProp = serialized.FindProperty("inventoryGrid");
            var grid = gridProp != null ? gridProp.objectReferenceValue as InventoryGridUI : null;
            if (grid == null)
            {
                return null;
            }

            var gridSerialized = new SerializedObject(grid);
            var templateProp = gridSerialized.FindProperty("slotTemplate");
            return templateProp != null ? templateProp.objectReferenceValue as InventorySlotUI : null;
        }

        private static InventorySlotUI CreateInventorySlotTemplate(Transform parent, Font font)
        {
            var root = new GameObject("InventorySlotTemplate", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            root.SetActive(false);
            var button = root.AddComponent<Button>();

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.14f, 0.14f);
            iconRect.anchorMax = new Vector2(0.86f, 0.86f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var selection = new GameObject("Selection", typeof(RectTransform), typeof(Image));
            selection.transform.SetParent(root.transform, false);
            var selectionRect = selection.GetComponent<RectTransform>();
            selectionRect.anchorMin = Vector2.zero;
            selectionRect.anchorMax = Vector2.one;
            selectionRect.offsetMin = Vector2.zero;
            selectionRect.offsetMax = Vector2.zero;
            var selectionImage = selection.GetComponent<Image>();
            selectionImage.color = new Color(1f, 1f, 1f, 0.15f);
            selectionImage.enabled = false;

            var stackText = CreateText("Stack", root.transform, font, string.Empty);
            var stackRect = stackText.GetComponent<RectTransform>();
            stackRect.anchorMin = new Vector2(0.64f, 0.04f);
            stackRect.anchorMax = new Vector2(0.94f, 0.24f);
            stackRect.offsetMin = Vector2.zero;
            stackRect.offsetMax = Vector2.zero;
            stackText.fontSize = 18;
            stackText.fontStyle = FontStyle.Bold;
            stackText.alignment = TextAnchor.MiddleRight;
            stackText.color = Color.white;
            stackText.raycastTarget = false;
            var stackOutline = stackText.gameObject.AddComponent<Outline>();
            stackOutline.effectColor = new Color(0.04f, 0.07f, 0.12f, 0.94f);
            stackOutline.effectDistance = new Vector2(1f, -1f);

            var slot = root.AddComponent<InventorySlotUI>();
            SetPrivateField(slot, "button", button);
            SetPrivateField(slot, "background", root.GetComponent<Image>());
            SetPrivateField(slot, "icon", iconImage);
            SetPrivateField(slot, "selection", selection.GetComponent<Image>());
            SetPrivateField(slot, "stackText", stackText);
            return slot;
        }

        private static void NormalizeInventorySlotTemplate(InventorySlotUI slot, Font font)
        {
            if (slot == null)
            {
                return;
            }

            var icon = GetPrivateFieldObject<Image>(slot, "icon");
            if (icon != null)
            {
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.14f, 0.14f);
                iconRect.anchorMax = new Vector2(0.86f, 0.86f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
            }

            var stackText = GetPrivateFieldObject<Text>(slot, "stackText");
            if (stackText != null)
            {
                if (font != null)
                {
                    stackText.font = font;
                }

                stackText.fontSize = 18;
                stackText.fontStyle = FontStyle.Bold;
                stackText.alignment = TextAnchor.MiddleRight;
                stackText.color = Color.white;
                stackText.raycastTarget = false;
                var stackRect = stackText.rectTransform;
                stackRect.anchorMin = new Vector2(0.64f, 0.04f);
                stackRect.anchorMax = new Vector2(0.94f, 0.24f);
                stackRect.offsetMin = Vector2.zero;
                stackRect.offsetMax = Vector2.zero;

                if (stackText.GetComponent<Outline>() == null)
                {
                    var outline = stackText.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.04f, 0.07f, 0.12f, 0.94f);
                    outline.effectDistance = new Vector2(1f, -1f);
                }
            }
        }

        private static void SetPrivateField(Object target, string fieldName, Object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(fieldName);
            if (prop == null)
            {
                return;
            }

            prop.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static T GetPrivateFieldObject<T>(Object target, string fieldName) where T : Object
        {
            if (target == null)
            {
                return null;
            }

            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(fieldName);
            return prop != null ? prop.objectReferenceValue as T : null;
        }

        private static void EnsureItemEconomyDefaults(IReadOnlyList<ItemDefinition> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            var dirty = false;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                var serialized = new SerializedObject(item);
                var economyVersionProp = serialized.FindProperty("economyVersion");
                if (economyVersionProp != null && economyVersionProp.intValue <= 0)
                {
                    economyVersionProp.intValue = 1;
                }

                var basePriceProp = serialized.FindProperty("basePrice");
                if (basePriceProp == null || basePriceProp.intValue > 0)
                {
                    if (economyVersionProp != null && economyVersionProp.intValue > 0)
                    {
                        serialized.ApplyModifiedProperties();
                        EditorUtility.SetDirty(item);
                        dirty = true;
                    }

                    continue;
                }

                basePriceProp.intValue = 10 + (i * 5);

                var canBuyProp = serialized.FindProperty("canBuy");
                if (canBuyProp != null)
                {
                    canBuyProp.boolValue = true;
                }

                var canSellProp = serialized.FindProperty("canSell");
                if (canSellProp != null)
                {
                    canSellProp.boolValue = true;
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(item);
                dirty = true;
            }

            if (dirty)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
