using System.Collections.Generic;
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
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace CombatSystem.EditorTools
{
    public static class Day4SetupUtility
    {
        private const string LootTablePath = "Assets/_Game/ScriptableObjects/Loot/LootTable_Default.asset";
        private const string VendorPath = "Assets/_Game/ScriptableObjects/Vendors/Vendor_Default.asset";
        private const string LootPickupPrefabPath = "Assets/_Game/Prefabs/LootPickup.prefab";
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player.prefab";
        private const string GameDatabasePath = "Assets/_Game/ScriptableObjects/Database/GameDatabase.asset";
        private const string InputActionsPath = "Assets/_Game/Input/CombatInputActions.inputactions";
        private const string VendorSceneName = "Vendor";

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
                prefab.AddComponent<CurrencyComponent>();
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

            var vendor = AssetDatabase.LoadAssetAtPath<VendorDefinition>(VendorPath);
            if (vendor == null)
            {
                vendor = CreateOrUpdateVendor();
            }

            EnsureEventSystemInput();
            var vendorService = EnsureVendorService(vendor);
            var vendorScreen = EnsureVendorScreen(vendorService, false);
            EnsureVendorNpc(vendorScreen);

            if (vendorScreen != null)
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
            backdropImage.color = new Color(0f, 0f, 0f, 0.55f);

            var window = CreateUiPanel("Window", screen.transform, Vector2.one);
            var windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.06f, 0.06f);
            windowRect.anchorMax = new Vector2(0.94f, 0.94f);
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;
            var windowImage = window.GetComponent<Image>();
            windowImage.color = new Color(0.11f, 0.13f, 0.18f, 0.96f);

            var windowLayout = window.AddComponent<VerticalLayoutGroup>();
            windowLayout.padding = new RectOffset(14, 14, 14, 14);
            windowLayout.spacing = 12f;
            windowLayout.childAlignment = TextAnchor.UpperLeft;
            windowLayout.childControlHeight = true;
            windowLayout.childControlWidth = true;
            windowLayout.childForceExpandHeight = false;
            windowLayout.childForceExpandWidth = true;

            var header = CreateUiPanel("Header", window.transform, Vector2.one);
            var headerImage = header.GetComponent<Image>();
            headerImage.color = new Color(0.15f, 0.18f, 0.24f, 1f);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(12, 12, 8, 8);
            headerLayout.spacing = 8f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlHeight = true;
            headerLayout.childControlWidth = true;
            headerLayout.childForceExpandHeight = true;
            headerLayout.childForceExpandWidth = false;
            header.AddComponent<LayoutElement>().preferredHeight = 54f;

            var titleText = CreateText("Title", header.transform, font, "Vendor");
            titleText.fontSize = 24;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var hintText = CreateText("Hint", header.transform, font, "Select an item to trade");
            hintText.fontSize = 13;
            hintText.alignment = TextAnchor.MiddleRight;
            hintText.color = new Color(0.85f, 0.9f, 1f, 0.95f);
            hintText.GetComponent<LayoutElement>().preferredWidth = 260f;

            var body = CreateUiPanel("Body", window.transform, Vector2.one);
            var bodyImage = body.GetComponent<Image>();
            bodyImage.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
            var bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.padding = new RectOffset(10, 10, 10, 10);
            bodyLayout.spacing = 12f;
            bodyLayout.childAlignment = TextAnchor.UpperLeft;
            bodyLayout.childControlHeight = true;
            bodyLayout.childControlWidth = true;
            bodyLayout.childForceExpandHeight = true;
            bodyLayout.childForceExpandWidth = false;
            var bodyElement = body.AddComponent<LayoutElement>();
            bodyElement.flexibleHeight = 1f;
            bodyElement.minHeight = 420f;

            var vendorPanel = CreateUiPanel("VendorPanel", body.transform, Vector2.one);
            vendorPanel.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 0.95f);
            var vendorElement = vendorPanel.AddComponent<LayoutElement>();
            vendorElement.preferredWidth = 320f;
            vendorElement.minWidth = 300f;
            var vendorLayout = vendorPanel.AddComponent<VerticalLayoutGroup>();
            vendorLayout.padding = new RectOffset(10, 10, 10, 10);
            vendorLayout.spacing = 8f;
            vendorLayout.childAlignment = TextAnchor.UpperLeft;
            vendorLayout.childControlHeight = true;
            vendorLayout.childControlWidth = true;
            vendorLayout.childForceExpandHeight = false;
            vendorLayout.childForceExpandWidth = true;

            var vendorTitle = CreateText("VendorTitle", vendorPanel.transform, font, "Vendor");
            vendorTitle.fontSize = 18;
            vendorTitle.alignment = TextAnchor.MiddleLeft;
            vendorTitle.GetComponent<LayoutElement>().preferredHeight = 30f;

            var vendorListRoot = CreateUiPanel("VendorList", vendorPanel.transform, Vector2.one);
            vendorListRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);
            var vendorListElement = vendorListRoot.AddComponent<LayoutElement>();
            vendorListElement.flexibleHeight = 1f;
            vendorListElement.minHeight = 320f;
            var vendorListGrid = vendorListRoot.AddComponent<GridLayoutGroup>();
            vendorListGrid.padding = new RectOffset(8, 8, 8, 8);
            vendorListGrid.cellSize = new Vector2(82f, 82f);
            vendorListGrid.spacing = new Vector2(8f, 8f);
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
            detailPanel.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 0.95f);
            var detailElement = detailPanel.AddComponent<LayoutElement>();
            detailElement.preferredWidth = 360f;
            detailElement.minWidth = 320f;
            var detailLayout = detailPanel.AddComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset(12, 12, 12, 12);
            detailLayout.spacing = 6f;
            detailLayout.childAlignment = TextAnchor.UpperLeft;
            detailLayout.childControlHeight = true;
            detailLayout.childControlWidth = true;
            detailLayout.childForceExpandHeight = false;
            detailLayout.childForceExpandWidth = true;

            var detailsTitle = CreateText("DetailsTitle", detailPanel.transform, font, "Item Details");
            detailsTitle.fontSize = 18;
            detailsTitle.alignment = TextAnchor.MiddleLeft;
            detailsTitle.GetComponent<LayoutElement>().preferredHeight = 30f;

            var detailTitle = CreateText("DetailTitle", detailPanel.transform, font, "No item selected");
            detailTitle.fontSize = 17;
            detailTitle.alignment = TextAnchor.UpperLeft;
            detailTitle.GetComponent<LayoutElement>().preferredHeight = 30f;
            var detailMeta = CreateText("DetailMeta", detailPanel.transform, font, "Click a vendor or inventory slot.");
            detailMeta.fontSize = 14;
            detailMeta.alignment = TextAnchor.UpperLeft;
            detailMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailMeta.verticalOverflow = VerticalWrapMode.Overflow;
            detailMeta.GetComponent<LayoutElement>().preferredHeight = 42f;
            var detailPrice = CreateText("DetailPrice", detailPanel.transform, font, string.Empty);
            detailPrice.fontSize = 14;
            detailPrice.alignment = TextAnchor.UpperLeft;
            detailPrice.GetComponent<LayoutElement>().preferredHeight = 32f;
            var detailDescription = CreateText("DetailDescription", detailPanel.transform, font, "Item info, price and stock will appear here.");
            detailDescription.fontSize = 13;
            detailDescription.alignment = TextAnchor.UpperLeft;
            detailDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailDescription.verticalOverflow = VerticalWrapMode.Overflow;
            var detailDescriptionElement = detailDescription.GetComponent<LayoutElement>();
            detailDescriptionElement.preferredHeight = 120f;
            detailDescriptionElement.flexibleHeight = 1f;

            var inventoryPanel = CreateUiPanel("InventoryPanel", body.transform, Vector2.one);
            inventoryPanel.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 0.95f);
            var inventoryElement = inventoryPanel.AddComponent<LayoutElement>();
            inventoryElement.preferredWidth = 560f;
            inventoryElement.minWidth = 500f;
            var inventoryLayout = inventoryPanel.AddComponent<VerticalLayoutGroup>();
            inventoryLayout.padding = new RectOffset(10, 10, 10, 10);
            inventoryLayout.spacing = 8f;
            inventoryLayout.childAlignment = TextAnchor.UpperLeft;
            inventoryLayout.childControlHeight = true;
            inventoryLayout.childControlWidth = true;
            inventoryLayout.childForceExpandHeight = false;
            inventoryLayout.childForceExpandWidth = true;

            var inventoryTitle = CreateText("InventoryTitle", inventoryPanel.transform, font, "Inventory");
            inventoryTitle.fontSize = 18;
            inventoryTitle.alignment = TextAnchor.MiddleLeft;
            inventoryTitle.GetComponent<LayoutElement>().preferredHeight = 30f;

            var inventoryGridRoot = CreateUiPanel("InventoryGrid", inventoryPanel.transform, Vector2.one);
            inventoryGridRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);
            var inventoryGridElement = inventoryGridRoot.AddComponent<LayoutElement>();
            inventoryGridElement.flexibleHeight = 1f;
            inventoryGridElement.minHeight = 320f;
            var inventoryGridLayout = inventoryGridRoot.AddComponent<GridLayoutGroup>();
            inventoryGridLayout.padding = new RectOffset(8, 8, 8, 8);
            inventoryGridLayout.cellSize = new Vector2(80f, 80f);
            inventoryGridLayout.spacing = new Vector2(8f, 8f);
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

            SetPrivateField(inventoryGrid, "slotsRoot", inventoryGridRoot.GetComponent<RectTransform>());
            SetPrivateField(inventoryGrid, "slotTemplate", inventorySlotTemplate);

            var footer = CreateUiPanel("Footer", window.transform, Vector2.one);
            footer.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.24f, 1f);
            var footerElement = footer.AddComponent<LayoutElement>();
            footerElement.preferredHeight = 64f;
            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.padding = new RectOffset(12, 12, 8, 8);
            footerLayout.spacing = 12f;
            footerLayout.childAlignment = TextAnchor.MiddleLeft;
            footerLayout.childControlHeight = true;
            footerLayout.childControlWidth = true;
            footerLayout.childForceExpandWidth = false;

            var buyButton = CreateButton("BuyButton", footer.transform, font, "Buy");
            var sellButton = CreateButton("SellButton", footer.transform, font, "Sell");
            var statusText = CreateText("StatusText", footer.transform, font, string.Empty);
            statusText.alignment = TextAnchor.MiddleLeft;
            statusText.color = new Color(1f, 0.93f, 0.55f, 1f);
            var statusElement = statusText.GetComponent<LayoutElement>();
            statusElement.preferredWidth = 420f;
            statusElement.flexibleWidth = 1f;

            var currencyLabel = CreateText("CurrencyLabel", footer.transform, font, "Gold");
            currencyLabel.alignment = TextAnchor.MiddleRight;
            currencyLabel.fontSize = 16;
            var currencyText = CreateText("CurrencyText", footer.transform, font, "0");
            currencyText.alignment = TextAnchor.MiddleRight;
            currencyText.fontSize = 20;
            currencyText.color = new Color(1f, 0.84f, 0.3f, 1f);
            currencyText.GetComponent<LayoutElement>().preferredWidth = 90f;

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
            serialized.FindProperty("statusText").objectReferenceValue = statusText;
            serialized.FindProperty("detailTitleText").objectReferenceValue = detailTitle;
            serialized.FindProperty("detailMetaText").objectReferenceValue = detailMeta;
            serialized.FindProperty("detailPriceText").objectReferenceValue = detailPrice;
            serialized.FindProperty("detailDescriptionText").objectReferenceValue = detailDescription;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(screen);
        }

        private static void EnsureVendorNpc(VendorScreen vendorScreen)
        {
            var npc = GameObject.Find("VendorNPC");
            if (npc == null)
            {
                npc = new GameObject("VendorNPC");
                npc.transform.position = Vector3.zero;
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

            var serialized = new SerializedObject(trigger);
            serialized.FindProperty("vendorScreen").objectReferenceValue = vendorScreen;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(trigger);
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
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.3f);
            iconRect.anchorMax = new Vector2(0.9f, 0.9f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

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

            var priceText = CreateText("Price", root.transform, font, "0");
            var priceRect = priceText.GetComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.05f, 0.05f);
            priceRect.anchorMax = new Vector2(0.45f, 0.25f);
            priceRect.offsetMin = Vector2.zero;
            priceRect.offsetMax = Vector2.zero;

            var stockText = CreateText("Stock", root.transform, font, "inf");
            var stockRect = stockText.GetComponent<RectTransform>();
            stockRect.anchorMin = new Vector2(0.55f, 0.05f);
            stockRect.anchorMax = new Vector2(0.95f, 0.25f);
            stockRect.offsetMin = Vector2.zero;
            stockRect.offsetMax = Vector2.zero;

            var slot = root.AddComponent<VendorItemSlotUI>();
            SetPrivateField(slot, "button", button);
            SetPrivateField(slot, "background", root.GetComponent<Image>());
            SetPrivateField(slot, "icon", icon.GetComponent<Image>());
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
            iconRect.anchorMin = new Vector2(0.1f, 0.3f);
            iconRect.anchorMax = new Vector2(0.9f, 0.9f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

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
            stackRect.anchorMin = new Vector2(0.05f, 0.05f);
            stackRect.anchorMax = new Vector2(0.4f, 0.25f);
            stackRect.offsetMin = Vector2.zero;
            stackRect.offsetMax = Vector2.zero;

            var slot = root.AddComponent<InventorySlotUI>();
            SetPrivateField(slot, "button", button);
            SetPrivateField(slot, "background", root.GetComponent<Image>());
            SetPrivateField(slot, "icon", icon.GetComponent<Image>());
            SetPrivateField(slot, "selection", selection.GetComponent<Image>());
            SetPrivateField(slot, "stackText", stackText);
            return slot;
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
