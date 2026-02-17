using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Debugging;
using CombatSystem.Gameplay;
using CombatSystem.Persistence;
using CombatSystem.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace CombatSystem.Editor
{
    public static class UIRootBuilder
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const float CanvasReferenceWidth = 1920f;
        private const float CanvasReferenceHeight = 1080f;

        [MenuItem("Combat/UI/Create UIRoot")]
        public static void CreateUIRoot()
        {
            if (Object.FindFirstObjectByType<UIRoot>() != null)
            {
                Debug.LogWarning("[UIRootBuilder] UIRoot already exists in the scene.");
                return;
            }

            var root = new GameObject("UIRoot");
            Undo.RegisterCreatedObjectUndo(root, "Create UIRoot");

            var uiRoot = root.AddComponent<UIRoot>();
            var uiManager = root.AddComponent<UIManager>();
            var pauseHotkey = root.AddComponent<PauseMenuHotkey>();
            var inventoryHotkey = root.AddComponent<InventoryHotkey>();
            var eventSystemBootstrapper = root.AddComponent<UIEventSystemBootstrapper>();
            var saveManager = root.AddComponent<SaveGameManager>();
            root.AddComponent<SettingsBootstrapper>();

            var screensCanvas = CreateCanvas("Canvas_Screens", root.transform, 0);
            var hudCanvas = CreateCanvas("Canvas_HUD", root.transform, 100);
            var modalCanvas = CreateCanvas("Canvas_Modal", root.transform, 200);
            var overlayCanvas = CreateCanvas("Canvas_Overlay", root.transform, 300);

            var mainMenu = CreateScreen<MainMenuScreen>("MainMenuScreen", screensCanvas.transform);
            var saveSelect = CreateScreen<SaveSelectScreen>("SaveSelectScreen", screensCanvas.transform);
            var settings = CreateScreen<SettingsScreen>("SettingsScreen", screensCanvas.transform);
            var inGame = CreateScreen<InGameScreen>("InGameScreen", screensCanvas.transform);
            var inventoryScreen = CreateScreen<InventoryScreen>("InventoryScreen", screensCanvas.transform);
            var questJournalScreen = CreateScreen<QuestJournalScreen>("QuestJournalScreen", screensCanvas.transform);
            var pauseModal = CreateModal<PauseMenuModal>("PauseMenuModal", modalCanvas.transform);
            var questGiverModal = CreateModal<QuestGiverModal>("QuestGiverModal", modalCanvas.transform);

            mainMenu.gameObject.SetActive(true);
            saveSelect.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            inGame.gameObject.SetActive(false);
            inventoryScreen.gameObject.SetActive(false);
            questJournalScreen.gameObject.SetActive(false);
            pauseModal.gameObject.SetActive(false);
            questGiverModal.gameObject.SetActive(false);

            SetSerialized(uiRoot, "screensCanvas", screensCanvas);
            SetSerialized(uiRoot, "hudCanvas", hudCanvas);
            SetSerialized(uiRoot, "modalCanvas", modalCanvas);
            SetSerialized(uiRoot, "overlayCanvas", overlayCanvas);
            SetSerialized(uiRoot, "dontDestroyOnLoad", true);
            SetSerialized(uiRoot, "uiManager", uiManager);

            SetSerialized(uiManager, "initialScreen", mainMenu);
            SetSerialized(uiManager, "hideAllScreensOnStart", true);
            SetSerialized(uiManager, "hideHudOnStart", true);

            SetSerialized(mainMenu, "uiManager", uiManager);
            SetSerialized(mainMenu, "saveSelectScreen", saveSelect);
            SetSerialized(mainMenu, "settingsScreen", settings);
            SetSerialized(mainMenu, "inGameScreen", inGame);
            SetSerialized(mainMenu, "saveManager", saveManager);
            SetSerializedEnum(mainMenu, "inputMode", (int)UIInputMode.UI);

            SetSerialized(saveSelect, "uiManager", uiManager);
            SetSerialized(saveSelect, "mainMenuScreen", mainMenu);
            SetSerialized(saveSelect, "inGameScreen", inGame);
            SetSerializedEnum(saveSelect, "inputMode", (int)UIInputMode.UI);

            SetSerialized(settings, "uiManager", uiManager);
            SetSerialized(settings, "backScreen", mainMenu);
            SetSerializedEnum(settings, "inputMode", (int)UIInputMode.UI);

            SetSerialized(inGame, "uiManager", uiManager);
            SetSerializedEnum(inGame, "inputMode", (int)UIInputMode.Gameplay);

            SetSerialized(inventoryScreen, "uiManager", uiManager);
            SetSerializedEnum(inventoryScreen, "inputMode", (int)UIInputMode.UI);

            SetSerialized(questJournalScreen, "uiManager", uiManager);
            SetSerializedEnum(questJournalScreen, "inputMode", (int)UIInputMode.UI);

            SetSerialized(pauseModal, "uiManager", uiManager);
            SetSerialized(pauseModal, "mainMenuScreen", mainMenu);
            SetSerialized(pauseModal, "settingsScreen", settings);
            SetSerialized(pauseModal, "questJournalScreen", questJournalScreen);
            SetSerialized(pauseModal, "saveManager", saveManager);

            SetSerialized(questGiverModal, "uiManager", uiManager);

            SetSerialized(pauseHotkey, "uiManager", uiManager);
            SetSerialized(pauseHotkey, "pauseModal", pauseModal);

            SetSerialized(inventoryHotkey, "uiManager", uiManager);
            SetSerialized(inventoryHotkey, "inventoryScreen", inventoryScreen);

            SetSerialized(eventSystemBootstrapper, "actionsAsset",
                AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Input/CombatInputActions.inputactions"));

            EnsureEventSystem();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Combat/UI/Build Basic UI")]
        public static void BuildBasicUI()
        {
            var root = ResolveRootForBuild();
            if (root == null)
            {
                Debug.LogWarning("[UIRootBuilder] No UIRoot found in the scene.");
                return;
            }

            NormalizeCanvasTransforms(root);

            var uiManager = root.Manager != null ? root.Manager : root.GetComponentInChildren<UIManager>(true);
            if (uiManager == null)
            {
                Debug.LogWarning("[UIRootBuilder] UIManager not found under UIRoot.");
                return;
            }

            var mainMenu = root.GetComponentInChildren<MainMenuScreen>(true);
            var saveSelect = root.GetComponentInChildren<SaveSelectScreen>(true);
            var settings = root.GetComponentInChildren<SettingsScreen>(true);
            var inGame = root.GetComponentInChildren<InGameScreen>(true);
            var inventoryScreen = root.GetComponentInChildren<InventoryScreen>(true);
            var questJournalScreen = root.GetComponentInChildren<QuestJournalScreen>(true);
            var pauseModal = root.GetComponentInChildren<PauseMenuModal>(true);
            var questGiverModal = root.GetComponentInChildren<QuestGiverModal>(true);
            var pauseHotkey = root.GetComponentInChildren<PauseMenuHotkey>(true);
            var inventoryHotkey = root.GetComponentInChildren<InventoryHotkey>(true);
            var eventSystemBootstrapper = root.GetComponentInChildren<UIEventSystemBootstrapper>(true);
            var saveManager = root.GetComponentInChildren<SaveGameManager>(true);
            if (saveManager == null)
            {
                saveManager = root.gameObject.AddComponent<SaveGameManager>();
            }

            var settingsBootstrapper = root.GetComponentInChildren<SettingsBootstrapper>(true);
            if (settingsBootstrapper == null)
            {
                settingsBootstrapper = root.gameObject.AddComponent<SettingsBootstrapper>();
            }

            if (inventoryScreen == null && root.ScreensCanvas != null)
            {
                inventoryScreen = CreateScreen<InventoryScreen>("InventoryScreen", root.ScreensCanvas.transform);
                inventoryScreen.gameObject.SetActive(false);
            }

            if (questJournalScreen == null && root.ScreensCanvas != null)
            {
                questJournalScreen = CreateScreen<QuestJournalScreen>("QuestJournalScreen", root.ScreensCanvas.transform);
                questJournalScreen.gameObject.SetActive(false);
            }

            if (questGiverModal == null && root.ModalCanvas != null)
            {
                questGiverModal = CreateModal<QuestGiverModal>("QuestGiverModal", root.ModalCanvas.transform);
                questGiverModal.gameObject.SetActive(false);
            }

            if (inventoryHotkey == null)
            {
                inventoryHotkey = root.gameObject.AddComponent<InventoryHotkey>();
            }

            if (eventSystemBootstrapper == null)
            {
                eventSystemBootstrapper = root.gameObject.AddComponent<UIEventSystemBootstrapper>();
            }

            var sprite = GetDefaultUISprite();
            var font = GetDefaultFont();

            if (mainMenu != null)
            {
                EnsureCanvasGroup(mainMenu);
                BuildMainMenuUI(mainMenu, sprite, font);
                SetSerialized(mainMenu, "uiManager", uiManager);
                SetSerializedEnum(mainMenu, "inputMode", (int)UIInputMode.UI);
            }

            if (saveSelect != null)
            {
                EnsureCanvasGroup(saveSelect);
                BuildSaveSelectUI(saveSelect, sprite, font, saveManager);
                SetSerialized(saveSelect, "uiManager", uiManager);
                SetSerialized(saveSelect, "saveManager", saveManager);
                SetSerializedEnum(saveSelect, "inputMode", (int)UIInputMode.UI);
            }

            if (settings != null)
            {
                EnsureCanvasGroup(settings);
                BuildSettingsUI(settings, sprite, font);
                SetSerialized(settings, "uiManager", uiManager);
                SetSerializedEnum(settings, "inputMode", (int)UIInputMode.UI);
            }

            if (inGame != null)
            {
                EnsureCanvasGroup(inGame);
                SetSerialized(inGame, "uiManager", uiManager);
                SetSerializedEnum(inGame, "inputMode", (int)UIInputMode.Gameplay);
            }

            if (inventoryScreen != null)
            {
                EnsureCanvasGroup(inventoryScreen);
                BuildInventoryUI(inventoryScreen, sprite, font);
                SetSerialized(inventoryScreen, "uiManager", uiManager);
                SetSerializedEnum(inventoryScreen, "inputMode", (int)UIInputMode.UI);
            }

            if (questJournalScreen != null)
            {
                EnsureCanvasGroup(questJournalScreen);
                BuildQuestJournalUI(questJournalScreen, sprite, font);
                SetSerialized(questJournalScreen, "uiManager", uiManager);
                SetSerializedEnum(questJournalScreen, "inputMode", (int)UIInputMode.UI);
            }

            if (pauseModal != null)
            {
                EnsureCanvasGroup(pauseModal);
                BuildPauseModalUI(pauseModal, sprite, font);
                SetSerialized(pauseModal, "uiManager", uiManager);
                SetSerialized(pauseModal, "settingsScreen", settings);
                SetSerialized(pauseModal, "questJournalScreen", questJournalScreen);
                SetSerialized(pauseModal, "saveManager", saveManager);
            }

            if (questGiverModal != null)
            {
                EnsureCanvasGroup(questGiverModal);
                BuildQuestGiverModalUI(questGiverModal, sprite, font);
                SetSerialized(questGiverModal, "uiManager", uiManager);
            }

            if (pauseHotkey != null)
            {
                SetSerialized(pauseHotkey, "uiManager", uiManager);
                SetSerialized(pauseHotkey, "pauseModal", pauseModal);
            }

            if (inventoryHotkey != null)
            {
                SetSerialized(inventoryHotkey, "uiManager", uiManager);
                SetSerialized(inventoryHotkey, "inventoryScreen", inventoryScreen);
            }

            if (eventSystemBootstrapper != null)
            {
                SetSerialized(eventSystemBootstrapper, "actionsAsset",
                    AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Input/CombatInputActions.inputactions"));
            }

            if (root.HudCanvas != null)
            {
                BuildUnitHealthBars(root.HudCanvas);
                BuildCombatHUD(root.HudCanvas, sprite, font);
            }

            LinkQuestGiverTriggers(uiManager, questGiverModal);

            if (uiManager != null)
            {
                var sceneName = SceneManager.GetActiveScene().name;
                var isMainMenuScene = !string.IsNullOrEmpty(sceneName) &&
                    sceneName.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase);

                if (isMainMenuScene && mainMenu != null)
                {
                    SetSerialized(uiManager, "initialScreen", mainMenu);
                    SetSerialized(uiManager, "hideHudOnStart", true);
                }
                else if (inGame != null)
                {
                    SetSerialized(uiManager, "initialScreen", inGame);
                    SetSerialized(uiManager, "hideHudOnStart", false);
                }
                else if (mainMenu != null)
                {
                    SetSerialized(uiManager, "initialScreen", mainMenu);
                    SetSerialized(uiManager, "hideHudOnStart", true);
                }
            }

            EnsureEventSystem();
            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);

            if (Application.isBatchMode)
            {
                if (!EditorSceneManager.SaveScene(activeScene))
                {
                    Debug.LogError($"[UIRootBuilder] Failed to save scene: {activeScene.path}");
                }

                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Combat/UI/Fix UIRoot Canvases")]
        public static void FixUIRootCanvases()
        {
            var root = Object.FindFirstObjectByType<UIRoot>();
            if (root == null)
            {
                Debug.LogWarning("[UIRootBuilder] No UIRoot found in the scene.");
                return;
            }

            NormalizeCanvasTransforms(root);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create UI Canvas");
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            NormalizeCanvasTransform(canvas);

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasReferenceWidth, CanvasReferenceHeight);

            return canvas;
        }

        internal static void NormalizeCanvasTransforms(UIRoot root)
        {
            if (root == null)
            {
                return;
            }

            NormalizeCanvasTransform(root.ScreensCanvas);
            NormalizeCanvasTransform(root.HudCanvas);
            NormalizeCanvasTransform(root.ModalCanvas);
            NormalizeCanvasTransform(root.OverlayCanvas);

            var rootTransform = root.transform;
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                var canvas = rootTransform.GetChild(i).GetComponent<Canvas>();
                NormalizeCanvasTransform(canvas);
            }
        }

        private static UIRoot ResolveRootForBuild()
        {
            var mainMenuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
            if (mainMenuScene == null)
            {
                Debug.LogWarning($"[UIRootBuilder] MainMenu scene not found at {MainMenuScenePath}.");
                return null;
            }

            var activeScene = SceneManager.GetActiveScene();
            var activeScenePath = activeScene.path ?? string.Empty;
            var isMainMenuActive = !string.IsNullOrEmpty(activeScenePath) &&
                activeScenePath.Equals(MainMenuScenePath, System.StringComparison.OrdinalIgnoreCase);

            if (!isMainMenuActive)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[UIRootBuilder] Build Basic UI canceled.");
                    return null;
                }

                var openedScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
                if (!openedScene.IsValid() || !openedScene.isLoaded)
                {
                    Debug.LogWarning($"[UIRootBuilder] Failed to open scene: {MainMenuScenePath}");
                    return null;
                }
            }

            return Object.FindFirstObjectByType<UIRoot>();
        }

        private static void NormalizeCanvasTransform(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            var rect = canvas.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = canvas.gameObject.AddComponent<RectTransform>();
            }

            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static T CreateScreen<T>(string name, Transform parent) where T : UIScreenBase
        {
            var go = CreateUIRootObject(name, parent);
            var screen = go.AddComponent<T>();
            AssignCanvasGroup(screen, go.GetComponent<CanvasGroup>());
            return screen;
        }

        private static T CreateModal<T>(string name, Transform parent) where T : UIModalBase
        {
            var go = CreateUIRootObject(name, parent);
            var modal = go.AddComponent<T>();
            AssignCanvasGroup(modal, go.GetComponent<CanvasGroup>());
            return modal;
        }

        private static GameObject CreateUIRootObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(go, "Create UI Element");
            go.transform.SetParent(parent, false);
            StretchRect(go.GetComponent<RectTransform>());
            return go;
        }

        private static void StretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                var legacy = existing.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    Object.DestroyImmediate(legacy);
                }

                var module = existing.GetComponent<InputSystemUIInputModule>();
                if (module == null)
                {
                    module = existing.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                ConfigureInputSystemUiModule(module);
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ConfigureInputSystemUiModule(eventSystem.GetComponent<InputSystemUIInputModule>());
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        private static void ConfigureInputSystemUiModule(InputSystemUIInputModule module)
        {
            if (module == null)
            {
                return;
            }

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Input/CombatInputActions.inputactions");
            if (actions == null)
            {
                Debug.LogWarning("[UIRootBuilder] CombatInputActions not found. UI input bindings not configured.");
                return;
            }

            var so = new SerializedObject(module);
            SetSerializedReference(so, "m_ActionsAsset", actions);
            SetSerializedReference(so, "actionsAsset", actions);
            SetSerializedReference(so, "m_PointAction", CreateActionReference(actions, "UI/Point"));
            SetSerializedReference(so, "point", CreateActionReference(actions, "UI/Point"));
            SetSerializedReference(so, "m_LeftClickAction", CreateActionReference(actions, "UI/Click"));
            SetSerializedReference(so, "leftClick", CreateActionReference(actions, "UI/Click"));
            SetSerializedReference(so, "m_ScrollWheelAction", CreateActionReference(actions, "UI/ScrollWheel"));
            SetSerializedReference(so, "scrollWheel", CreateActionReference(actions, "UI/ScrollWheel"));
            SetSerializedReference(so, "m_MoveAction", CreateActionReference(actions, "UI/Navigate"));
            SetSerializedReference(so, "move", CreateActionReference(actions, "UI/Navigate"));
            SetSerializedReference(so, "m_SubmitAction", CreateActionReference(actions, "UI/Submit"));
            SetSerializedReference(so, "submit", CreateActionReference(actions, "UI/Submit"));
            SetSerializedReference(so, "m_CancelAction", CreateActionReference(actions, "UI/Cancel"));
            SetSerializedReference(so, "cancel", CreateActionReference(actions, "UI/Cancel"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InputActionReference CreateActionReference(InputActionAsset asset, string actionPath)
        {
            var action = asset != null ? asset.FindAction(actionPath) : null;
            return action != null ? InputActionReference.Create(action) : null;
        }

        private static void SetSerializedReference(SerializedObject serializedObject, string propertyName, Object value)
        {
            var prop = serializedObject.FindProperty(propertyName);
            if (prop == null)
            {
                return;
            }

            prop.objectReferenceValue = value;
        }

        private static void SetSerialized(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[UIRootBuilder] Missing property: {propertyName} on {target.name}");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[UIRootBuilder] Missing property: {propertyName} on {target.name}");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedEnum(Object target, string propertyName, int enumIndex)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[UIRootBuilder] Missing property: {propertyName} on {target.name}");
                return;
            }

            property.enumValueIndex = enumIndex;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedIfNull(Object target, string propertyName, Object value)
        {
            if (target == null || value == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignCanvasGroup(UIScreenBase screen, CanvasGroup group)
        {
            SetSerialized(screen, "canvasGroup", group);
        }

        private static void AssignCanvasGroup(UIModalBase modal, CanvasGroup group)
        {
            SetSerialized(modal, "canvasGroup", group);
        }

        private static void BuildUnitHealthBars(Canvas hudCanvas)
        {
            var barsRoot = FindOrCreateChild(hudCanvas.transform, "UnitBarsRoot");
            var rect = barsRoot.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = barsRoot.AddComponent<RectTransform>();
            }

            StretchRect(rect);

            var manager = barsRoot.GetComponent<UnitHealthBarManager>();
            if (manager == null)
            {
                manager = barsRoot.AddComponent<UnitHealthBarManager>();
            }

            SetSerialized(manager, "barsRoot", rect);
        }

        private static void BuildCombatHUD(Canvas hudCanvas, Sprite sprite, Font font)
        {
            if (hudCanvas == null)
            {
                return;
            }

            var existingController = hudCanvas.GetComponentInChildren<CombatHUDController>(true);
            if (existingController == null)
            {
                existingController = Object.FindFirstObjectByType<CombatHUDController>();
                if (existingController != null)
                {
                    existingController.transform.SetParent(hudCanvas.transform, false);
                }
            }

            if (existingController != null)
            {
                NormalizeHudRoot(existingController.transform, hudCanvas.transform);
                WireCombatHudFromRoot(existingController, existingController.transform);
                EnsureHudBackgroundsSliced(existingController.transform, sprite);
                TryWireCombatHud(existingController);
                EnsureProgressionHud(existingController.transform, sprite, font);
                CleanupDuplicateHudRoots(hudCanvas, existingController);
                return;
            }

            var hudRoot = FindOrCreateChild(hudCanvas.transform, "HUD");
            var hudRect = hudRoot.GetComponent<RectTransform>();
            if (hudRect == null)
            {
                hudRect = hudRoot.AddComponent<RectTransform>();
            }

            StretchRect(hudRect);
            hudRoot.transform.SetAsLastSibling();

            if (hudRoot.transform.childCount > 0)
            {
                var existing = hudRoot.GetComponent<CombatHUDController>();
                if (existing == null)
                {
                    existing = hudRoot.AddComponent<CombatHUDController>();
                }

                WireCombatHudFromRoot(existing, hudRoot.transform);
                EnsureHudBackgroundsSliced(hudRoot.transform, sprite);
                TryWireCombatHud(existing);
                EnsureProgressionHud(hudRoot.transform, sprite, font);
                CleanupDuplicateHudRoots(hudCanvas, existing);
                return;
            }

            var hudConfig = FindHudConfig();
            var maxSkillSlots = hudConfig != null ? hudConfig.MaxSkillSlots : 6;
            var maxBuffSlots = hudConfig != null ? hudConfig.MaxBuffSlots : 12;

            var healthBar = CreateHudValueBar(
                "HealthBar",
                hudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(220f, 20f),
                new Vector2(130f, -20f),
                sprite,
                font,
                new Color(0f, 0f, 0f, 0.6f),
                new Color(0.8f, 0.1f, 0.1f, 1f));

            var resourceBar = CreateHudValueBar(
                "ResourceBar",
                hudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(220f, 16f),
                new Vector2(130f, -44f),
                sprite,
                font,
                new Color(0f, 0f, 0f, 0.6f),
                new Color(0.1f, 0.4f, 0.9f, 1f));

            var castBar = CreateHudCastBar(hudRect, sprite, font);
            var skillBar = CreateHudSkillBar(hudRect, sprite, font, maxSkillSlots);
            var buffBar = CreateHudBuffBar(hudRect, sprite, font, maxBuffSlots);
            var combatLog = CreateHudCombatLog(hudRect, sprite, font);
            var floatingText = CreateHudFloatingText(hudRect, font);
            var debugOverlay = CreateHudDebugOverlay(hudRect, sprite, font);

            var controller = hudRoot.AddComponent<CombatHUDController>();
            SetSerialized(controller, "healthBar", healthBar);
            SetSerialized(controller, "resourceBar", resourceBar);
            SetSerialized(controller, "skillBar", skillBar);
            SetSerialized(controller, "buffBar", buffBar);
            SetSerialized(controller, "castBar", castBar);
            SetSerialized(controller, "combatLog", combatLog);
            SetSerialized(controller, "floatingText", floatingText);
            SetSerializedIfNull(controller, "eventHub", FindEventHub());
            SetSerializedIfNull(controller, "hudConfig", hudConfig);
            SetSerializedIfNull(controller, "targetUnit", Object.FindFirstObjectByType<UnitRoot>());
            SetSerializedIfNull(controller, "worldCamera", FindMainCamera());
            EnsureTargetUnitIsPlayer(controller);

            if (debugOverlay != null)
            {
                var targetUnit = FindPlayerUnit() ?? Object.FindFirstObjectByType<UnitRoot>();
                SetSerializedIfNull(debugOverlay, "targetUnit", targetUnit);
                SetSerializedIfNull(debugOverlay, "projectilePool", Object.FindFirstObjectByType<ProjectilePool>());
                SetSerializedIfNull(debugOverlay, "floatingText", floatingText);
                SetSerialized(debugOverlay, "visible", true);
            }

            EnsureProgressionHud(hudRoot.transform, sprite, font);
            EnsureHudBackgroundsSliced(hudRoot.transform, sprite);
            CleanupDuplicateHudRoots(hudCanvas, controller);
        }

        private static void TryWireCombatHud(CombatHUDController controller)
        {
            if (controller == null)
            {
                return;
            }

            WireCombatHudFromRoot(controller, controller.transform);
            SetSerializedIfNull(controller, "eventHub", FindEventHub());
            SetSerializedIfNull(controller, "hudConfig", FindHudConfig());
            SetSerializedIfNull(controller, "targetUnit", Object.FindFirstObjectByType<UnitRoot>());
            SetSerializedIfNull(controller, "worldCamera", FindMainCamera());
            EnsureTargetUnitIsPlayer(controller);
        }

        private static void EnsureProgressionHud(Transform hudRoot, Sprite sprite, Font font)
        {
            if (hudRoot == null)
            {
                return;
            }

            var controller = hudRoot.GetComponentInChildren<ProgressionHUDController>(true);
            if (controller != null)
            {
                WireProgressionHudFromRoot(controller, controller.transform);
                SetSerializedIfNull(controller, "eventHub", FindEventHub());
                SetSerializedIfNull(controller, "progression", FindPlayerProgression());
                return;
            }

            var root = FindOrCreateChild(hudRoot, "ProgressionHUD");
            var rect = root.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = root.AddComponent<RectTransform>();
            }

            StretchRect(rect);

            var experienceBar = FindChildComponentByName<ValueBarUI>(root.transform, "ExperienceBar");
            if (experienceBar == null)
            {
                experienceBar = CreateHudValueBar(
                    "ExperienceBar",
                    rect,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(220f, 10f),
                    new Vector2(130f, -64f),
                    sprite,
                    font,
                    new Color(0f, 0f, 0f, 0.6f),
                    new Color(0.8f, 0.6f, 0.2f, 1f));
                SetSerialized(experienceBar, "showPercent", true);
                SetSerialized(experienceBar, "showMax", false);
            }

            var levelText = FindChildComponentByName<Text>(root.transform, "LevelText");
            if (levelText == null)
            {
                var levelRect = CreateHudRect(
                    "LevelText",
                    rect,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(90f, 16f),
                    new Vector2(280f, -20f));
                levelText = CreateText(levelRect.gameObject, "Lv 1", font, 12, TextAnchor.MiddleLeft);
                levelText.color = Color.white;
            }

            var pointsText = FindChildComponentByName<Text>(root.transform, "PointsText");
            if (pointsText == null)
            {
                var pointsRect = CreateHudRect(
                    "PointsText",
                    rect,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(120f, 16f),
                    new Vector2(280f, -44f));
                pointsText = CreateText(pointsRect.gameObject, "Points: 0", font, 12, TextAnchor.MiddleLeft);
                pointsText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            }

            controller = root.AddComponent<ProgressionHUDController>();
            SetSerialized(controller, "experienceBar", experienceBar);
            SetSerialized(controller, "levelText", levelText);
            SetSerialized(controller, "pointsText", pointsText);
            SetSerializedIfNull(controller, "eventHub", FindEventHub());
            SetSerializedIfNull(controller, "progression", FindPlayerProgression());
        }

        private static void WireProgressionHudFromRoot(ProgressionHUDController controller, Transform root)
        {
            if (controller == null || root == null)
            {
                return;
            }

            var experienceBar = FindChildComponentByName<ValueBarUI>(root, "ExperienceBar");
            if (experienceBar != null)
            {
                SetSerialized(controller, "experienceBar", experienceBar);
            }

            var levelText = FindChildComponentByName<Text>(root, "LevelText");
            if (levelText != null)
            {
                SetSerialized(controller, "levelText", levelText);
            }

            var pointsText = FindChildComponentByName<Text>(root, "PointsText");
            if (pointsText != null)
            {
                SetSerialized(controller, "pointsText", pointsText);
            }
        }

        private static HUDConfig FindHudConfig()
        {
            var guids = AssetDatabase.FindAssets("t:HUDConfig");
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<HUDConfig>(path);
        }

        private static CombatEventHub FindEventHub()
        {
            var playerUnit = FindPlayerUnit();
            if (playerUnit != null && playerUnit.EventHub != null)
            {
                return playerUnit.EventHub;
            }

            var guids = AssetDatabase.FindAssets("t:CombatEventHub");
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<CombatEventHub>(path);
        }

        private static PlayerProgression FindPlayerProgression()
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                var progression = playerObject.GetComponent<PlayerProgression>();
                if (progression != null)
                {
                    return progression;
                }
            }

            return Object.FindFirstObjectByType<PlayerProgression>();
        }

        private static UnitRoot FindPlayerUnit()
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                return null;
            }

            return playerObject.GetComponent<UnitRoot>();
        }

        private static void EnsureTargetUnitIsPlayer(CombatHUDController controller)
        {
            if (controller == null)
            {
                return;
            }

            var playerUnit = FindPlayerUnit();
            if (playerUnit == null)
            {
                return;
            }

            var serialized = new SerializedObject(controller);
            var property = serialized.FindProperty("targetUnit");
            if (property == null)
            {
                return;
            }

            var current = property.objectReferenceValue as UnitRoot;
            if (current == null || current.gameObject == null || !current.gameObject.CompareTag("Player"))
            {
                property.objectReferenceValue = playerUnit;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Camera FindMainCamera()
        {
            var cameraObject = GameObject.FindGameObjectWithTag("MainCamera");
            if (cameraObject != null)
            {
                return cameraObject.GetComponent<Camera>();
            }

            return Object.FindFirstObjectByType<Camera>();
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child.gameObject;
                }
            }

            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void NormalizeHudRoot(Transform hudRoot, Transform hudParent)
        {
            if (hudRoot == null || hudParent == null)
            {
                return;
            }

            if (hudRoot.parent != hudParent)
            {
                hudRoot.SetParent(hudParent, false);
            }

            StripHudCanvasComponents(hudRoot.gameObject);

            var rect = hudRoot as RectTransform;
            if (rect == null)
            {
                rect = hudRoot.gameObject.AddComponent<RectTransform>();
            }

            rect.localScale = Vector3.one;
            StretchRect(rect);
        }

        private static void StripHudCanvasComponents(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var canvas = go.GetComponent<Canvas>();
            if (canvas != null)
            {
                Object.DestroyImmediate(canvas);
            }

            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                Object.DestroyImmediate(scaler);
            }

            var raycaster = go.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                Object.DestroyImmediate(raycaster);
            }
        }

        private static void CleanupDuplicateHudRoots(Canvas hudCanvas, CombatHUDController keep)
        {
            if (hudCanvas == null || keep == null)
            {
                return;
            }

            var controllers = Object.FindObjectsByType<CombatHUDController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller == null || controller == keep)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(controller.gameObject);
            }
        }

        private static void WireCombatHudFromRoot(CombatHUDController controller, Transform root)
        {
            if (controller == null || root == null)
            {
                return;
            }

            var healthBar = FindChildComponentByName<ValueBarUI>(root, "HealthBar");
            var resourceBar = FindChildComponentByName<ValueBarUI>(root, "ResourceBar");
            var skillBar = FindChildComponentByName<SkillBarUI>(root, "SkillBar");
            var buffBar = FindChildComponentByName<BuffBarUI>(root, "BuffBar");
            var castBar = FindChildComponentByName<CastBarUI>(root, "CastBar");
            var combatLog = FindChildComponentByName<CombatLogUI>(root, "CombatLog");
            var floatingText = FindChildComponentByName<FloatingTextManager>(root, "FloatingText");

            if (healthBar != null)
            {
                SetSerializedIfNull(controller, "healthBar", healthBar);
            }

            if (resourceBar != null)
            {
                SetSerializedIfNull(controller, "resourceBar", resourceBar);
            }

            if (skillBar != null)
            {
                SetSerializedIfNull(controller, "skillBar", skillBar);
            }

            if (buffBar != null)
            {
                SetSerializedIfNull(controller, "buffBar", buffBar);
            }

            if (castBar != null)
            {
                SetSerializedIfNull(controller, "castBar", castBar);
            }

            if (combatLog != null)
            {
                SetSerializedIfNull(controller, "combatLog", combatLog);
            }

            if (floatingText != null)
            {
                SetSerializedIfNull(controller, "floatingText", floatingText);
            }
        }

        private static void EnsureHudBackgroundsSliced(Transform root, Sprite defaultSprite)
        {
            if (root == null || defaultSprite == null)
            {
                return;
            }

            EnsureSlicedBackground(FindChildRecursive(root, "CombatLog"), defaultSprite);
            EnsureSlicedBackground(FindChildRecursive(root, "DebugOverlay"), defaultSprite);
        }

        private static void EnsureSlicedBackground(Transform target, Sprite defaultSprite)
        {
            if (target == null || defaultSprite == null)
            {
                return;
            }

            var image = target.GetComponent<Image>();
            if (image != null && image.sprite == defaultSprite && image.type != Image.Type.Sliced)
            {
                image.type = Image.Type.Sliced;
            }
        }

        private static T FindChildComponentByName<T>(Transform root, string name) where T : Component
        {
            var child = FindChildRecursive(root, name);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var result = FindChildRecursive(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void EnsureCanvasGroup(UIScreenBase screen)
        {
            var group = screen.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = screen.gameObject.AddComponent<CanvasGroup>();
            }

            AssignCanvasGroup(screen, group);
        }

        private static void EnsureCanvasGroup(UIModalBase modal)
        {
            var group = modal.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = modal.gameObject.AddComponent<CanvasGroup>();
            }

            AssignCanvasGroup(modal, group);
        }

        private static void BuildMainMenuUI(MainMenuScreen screen, Sprite sprite, Font font)
        {
            if (screen.transform.childCount > 0)
            {
                Debug.LogWarning("[UIRootBuilder] MainMenuScreen already has children. Skipping.");
                return;
            }

            CreateBackground(screen.transform, sprite, new Color(0f, 0f, 0f, 0.65f));
            var panel = CreatePanel(screen.transform, sprite, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Vector2(520f, 520f));
            ConfigureVerticalLayout(panel, 24, 12, TextAnchor.MiddleCenter);

            CreateTitle(panel, "COMBAT SYSTEM", font, 36);

            var continueButton = CreateButton(panel, "Continue", sprite, font);
            UnityEventTools.AddPersistentListener(continueButton.onClick, screen.ContinueGame);

            var newGameButton = CreateButton(panel, "New Game", sprite, font);
            UnityEventTools.AddPersistentListener(newGameButton.onClick, screen.StartNewGame);

            var loadButton = CreateButton(panel, "Load Game", sprite, font);
            UnityEventTools.AddPersistentListener(loadButton.onClick, screen.OpenSaveSelect);

            var settingsButton = CreateButton(panel, "Settings", sprite, font);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, screen.OpenSettings);

            var quitButton = CreateButton(panel, "Quit", sprite, font);
            UnityEventTools.AddPersistentListener(quitButton.onClick, screen.QuitGame);
        }

        private static void BuildSaveSelectUI(SaveSelectScreen screen, Sprite sprite, Font font, SaveGameManager saveManager)
        {
            if (screen.transform.childCount > 0)
            {
                Debug.LogWarning("[UIRootBuilder] SaveSelectScreen already has children. Skipping.");
                return;
            }

            CreateBackground(screen.transform, sprite, new Color(0f, 0f, 0f, 0.65f));
            var panel = CreatePanel(screen.transform, sprite, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Vector2(620f, 580f));
            ConfigureVerticalLayout(panel, 24, 12, TextAnchor.MiddleCenter);

            CreateTitle(panel, "SELECT SAVE", font, 32);

            var inputField = CreateInputField(panel, sprite, font, "Save Name (optional)", 400f);

            var slotsRoot = CreateSlotContainer(panel);
            var emptyLabel = CreateLabel(slotsRoot, "No saves found.", font, 16);
            emptyLabel.color = new Color(0.75f, 0.75f, 0.75f, 1f);

            var slotTemplate = CreateSaveSlotEntryTemplate(slotsRoot, sprite, font);
            slotTemplate.gameObject.SetActive(false);

            var newSaveButton = CreateButton(panel, "New Save", sprite, font);
            UnityEventTools.AddPersistentListener(newSaveButton.onClick, screen.CreateNewSave);

            var backButton = CreateButton(panel, "Back", sprite, font);
            UnityEventTools.AddPersistentListener(backButton.onClick, screen.BackToMenu);

            SetSerialized(screen, "saveManager", saveManager);
            SetSerialized(screen, "slotContainer", slotsRoot);
            SetSerialized(screen, "slotTemplate", slotTemplate);
            SetSerialized(screen, "saveNameInput", inputField);
            SetSerialized(screen, "emptyLabel", emptyLabel);
            SetSerialized(screen, "newSaveButton", newSaveButton);
        }

        private static void BuildSettingsUI(SettingsScreen screen, Sprite sprite, Font font)
        {
            if (screen.transform.childCount > 0)
            {
                EnsureSettingsMoveMode(screen, sprite, font);
                return;
            }

            CreateBackground(screen.transform, sprite, new Color(0f, 0f, 0f, 0.65f));
            var panel = CreatePanel(screen.transform, sprite, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Vector2(640f, 560f));
            ConfigureVerticalLayout(panel, 20, 12, TextAnchor.UpperCenter);

            CreateTitle(panel, "SETTINGS", font, 32);
            CreateLabel(panel, "Audio / Video / Performance", font, 18);

            var resources = GetDefaultResources(sprite);

            var masterRow = CreateSettingRow(panel, "Master Volume", font, 44f);
            var masterSlider = CreateSlider(masterRow, resources, 0f, 1f);

            var fullscreenRow = CreateSettingRow(panel, "Fullscreen", font, 36f);
            var fullscreenToggle = CreateToggle(fullscreenRow, resources);

            var vSyncRow = CreateSettingRow(panel, "VSync", font, 36f);
            var vSyncToggle = CreateToggle(vSyncRow, resources);

            var qualityRow = CreateSettingRow(panel, "Quality", font, 40f);
            var qualityDropdown = CreateDropdown(qualityRow, resources);

            var fpsRow = CreateSettingRow(panel, "Target FPS", font, 40f);
            var fpsDropdown = CreateDropdown(fpsRow, resources);

            var moveModeRow = CreateSettingRow(panel, "Move Mode", font, 40f);
            var moveModeDropdown = CreateDropdown(moveModeRow, resources);

            var cameraModeRow = CreateSettingRow(panel, "Camera Mode", font, 40f);
            var cameraModeDropdown = CreateDropdown(cameraModeRow, resources);

            var edgePanRow = CreateSettingRow(panel, "Edge Pan", font, 36f);
            var edgePanToggle = CreateToggle(edgePanRow, resources);

            var applyButton = CreateButton(panel, "Apply", sprite, font);
            UnityEventTools.AddPersistentListener(applyButton.onClick, screen.Apply);

            var backButton = CreateButton(panel, "Back", sprite, font);
            UnityEventTools.AddPersistentListener(backButton.onClick, screen.Back);

            SetSerialized(screen, "masterVolume", masterSlider);
            SetSerialized(screen, "fullscreenToggle", fullscreenToggle);
            SetSerialized(screen, "vSyncToggle", vSyncToggle);
            SetSerialized(screen, "qualityDropdown", qualityDropdown);
            SetSerialized(screen, "fpsDropdown", fpsDropdown);
            SetSerialized(screen, "movementModeDropdown", moveModeDropdown);
            SetSerialized(screen, "cameraModeDropdown", cameraModeDropdown);
            SetSerialized(screen, "edgePanToggle", edgePanToggle);
            SetSerialized(screen, "applyButton", applyButton);
        }

        private static void EnsureSettingsMoveMode(SettingsScreen screen, Sprite sprite, Font font)
        {
            if (screen == null)
            {
                return;
            }

            var panel = screen.transform.Find("Panel");
            if (panel == null)
            {
                return;
            }

            var moveModeRow = panel.Find("MoveMode_Row");
            Dropdown moveModeDropdown = null;
            var cameraModeRow = panel.Find("CameraMode_Row");
            Dropdown cameraModeDropdown = null;
            var edgePanRow = panel.Find("EdgePan_Row");
            Toggle edgePanToggle = null;

            if (moveModeRow == null)
            {
                var resources = GetDefaultResources(sprite);
                var row = CreateSettingRow(panel, "Move Mode", font, 40f);
                moveModeDropdown = CreateDropdown(row, resources);

                var fpsRow = panel.Find("TargetFPS_Row");
                if (fpsRow != null)
                {
                    row.SetSiblingIndex(fpsRow.GetSiblingIndex() + 1);
                }
            }
            else
            {
                moveModeDropdown = moveModeRow.GetComponentInChildren<Dropdown>(true);
            }

            if (cameraModeRow == null)
            {
                var resources = GetDefaultResources(sprite);
                var row = CreateSettingRow(panel, "Camera Mode", font, 40f);
                cameraModeDropdown = CreateDropdown(row, resources);

                var moveRowForInsert = panel.Find("MoveMode_Row");
                if (moveRowForInsert != null)
                {
                    row.SetSiblingIndex(moveRowForInsert.GetSiblingIndex() + 1);
                }
            }
            else
            {
                cameraModeDropdown = cameraModeRow.GetComponentInChildren<Dropdown>(true);
            }

            if (edgePanRow == null)
            {
                var resources = GetDefaultResources(sprite);
                var row = CreateSettingRow(panel, "Edge Pan", font, 36f);
                edgePanToggle = CreateToggle(row, resources);

                var cameraRowForInsert = panel.Find("CameraMode_Row");
                if (cameraRowForInsert != null)
                {
                    row.SetSiblingIndex(cameraRowForInsert.GetSiblingIndex() + 1);
                }
            }
            else
            {
                edgePanToggle = edgePanRow.GetComponentInChildren<Toggle>(true);
            }

            if (moveModeDropdown != null)
            {
                SetSerialized(screen, "movementModeDropdown", moveModeDropdown);
            }

            if (cameraModeDropdown != null)
            {
                SetSerialized(screen, "cameraModeDropdown", cameraModeDropdown);
            }

            if (edgePanToggle != null)
            {
                SetSerialized(screen, "edgePanToggle", edgePanToggle);
            }
        }

        private static void BuildPauseModalUI(PauseMenuModal modal, Sprite sprite, Font font)
        {
            if (modal.transform.childCount > 0)
            {
                EnsurePauseModalQuestButton(modal, sprite, font);
                return;
            }

            var background = CreateBackground(modal.transform, sprite, new Color(0f, 0f, 0f, 0.7f));
            var backgroundButton = background.gameObject.AddComponent<Button>();
            backgroundButton.targetGraphic = background;
            UnityEventTools.AddPersistentListener(backgroundButton.onClick, modal.HandleBackgroundClick);

            var panel = CreatePanel(modal.transform, sprite, new Color(0.1f, 0.1f, 0.1f, 0.95f), new Vector2(420f, 420f));
            ConfigureVerticalLayout(panel, 20, 10, TextAnchor.MiddleCenter);

            CreateTitle(panel, "PAUSED", font, 30);

            var resumeButton = CreateButton(panel, "Resume", sprite, font);
            UnityEventTools.AddPersistentListener(resumeButton.onClick, modal.Resume);

            var saveButton = CreateButton(panel, "Save Game", sprite, font);
            UnityEventTools.AddPersistentListener(saveButton.onClick, modal.SaveGame);

            var questButton = CreateButton(panel, "Quests", sprite, font);
            UnityEventTools.AddPersistentListener(questButton.onClick, modal.OpenQuestJournal);

            var settingsButton = CreateButton(panel, "Settings", sprite, font);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, modal.OpenSettings);

            var menuButton = CreateButton(panel, "Main Menu", sprite, font);
            UnityEventTools.AddPersistentListener(menuButton.onClick, modal.BackToMenu);
        }

        private static void EnsurePauseModalQuestButton(PauseMenuModal modal, Sprite sprite, Font font)
        {
            if (modal == null)
            {
                return;
            }

            var panel = modal.transform.Find("Panel");
            if (panel == null)
            {
                return;
            }

            if (panel.Find("Button_Quests") != null)
            {
                return;
            }

            var rect = panel as RectTransform;
            if (rect != null && rect.sizeDelta.y < 420f)
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, 420f);
            }

            var questButton = CreateButton(panel, "Quests", sprite, font);
            SetLayoutSize(questButton.gameObject, 48f, 320f);
            questButton.transform.SetSiblingIndex(3);
            UnityEventTools.AddPersistentListener(questButton.onClick, modal.OpenQuestJournal);
        }

        private static void BuildQuestGiverModalUI(QuestGiverModal modal, Sprite sprite, Font font)
        {
            if (modal.transform.childCount > 0)
            {
                return;
            }

            var background = CreateBackground(modal.transform, sprite, new Color(0f, 0f, 0f, 0.72f));
            var backgroundButton = background.gameObject.AddComponent<Button>();
            backgroundButton.targetGraphic = background;
            UnityEventTools.AddPersistentListener(backgroundButton.onClick, modal.HandleBackgroundClick);

            var panel = CreatePanel(modal.transform, sprite, new Color(0.09f, 0.11f, 0.16f, 0.97f), new Vector2(760f, 560f));
            ConfigureVerticalLayout(panel, 16, 10, TextAnchor.UpperLeft);

            var titleGo = CreateUIElement("Title", panel.transform);
            var titleText = CreateText(titleGo, "任务", font, 28, TextAnchor.MiddleLeft);
            titleText.color = Color.white;
            AddLayoutElement(titleGo, 42f);

            var summaryGo = CreateUIElement("Summary", panel.transform);
            var summaryText = CreateText(summaryGo, string.Empty, font, 16, TextAnchor.UpperLeft);
            summaryText.color = new Color(0.9f, 0.9f, 0.92f, 1f);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            AddLayoutElement(summaryGo, 72f);

            var statusGo = CreateUIElement("Status", panel.transform);
            var statusText = CreateText(statusGo, string.Empty, font, 16, TextAnchor.MiddleLeft);
            statusText.color = new Color(0.95f, 0.83f, 0.45f, 1f);
            AddLayoutElement(statusGo, 30f);

            var objectivesGo = CreateUIElement("Objectives", panel.transform);
            var objectivesText = CreateText(objectivesGo, string.Empty, font, 16, TextAnchor.UpperLeft);
            objectivesText.color = Color.white;
            objectivesText.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectivesText.verticalOverflow = VerticalWrapMode.Overflow;
            var objectivesLayout = objectivesGo.AddComponent<LayoutElement>();
            objectivesLayout.flexibleHeight = 1f;
            objectivesLayout.preferredHeight = 220f;

            var rewardGo = CreateUIElement("Reward", panel.transform);
            var rewardText = CreateText(rewardGo, string.Empty, font, 15, TextAnchor.MiddleLeft);
            rewardText.color = new Color(0.72f, 0.95f, 0.78f, 1f);
            AddLayoutElement(rewardGo, 30f);

            var feedbackGo = CreateUIElement("Feedback", panel.transform);
            var feedbackText = CreateText(feedbackGo, string.Empty, font, 15, TextAnchor.MiddleLeft);
            feedbackText.color = new Color(0.8f, 0.85f, 0.95f, 1f);
            AddLayoutElement(feedbackGo, 28f);

            var buttonsRow = CreateUIElement("Buttons", panel.transform);
            var buttonsLayout = buttonsRow.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 10f;
            buttonsLayout.childAlignment = TextAnchor.MiddleRight;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childControlWidth = false;
            buttonsLayout.childForceExpandHeight = false;
            buttonsLayout.childForceExpandWidth = false;
            AddLayoutElement(buttonsRow, 56f);

            var primaryButton = CreateButton(buttonsRow.transform, "继续", sprite, font);
            var primaryButtonText = primaryButton.GetComponentInChildren<Text>();
            SetLayoutSize(primaryButton.gameObject, 48f, 220f);

            var tradeButton = CreateButton(buttonsRow.transform, "交易", sprite, font);
            SetLayoutSize(tradeButton.gameObject, 48f, 180f);

            var closeButton = CreateButton(buttonsRow.transform, "关闭", sprite, font);
            SetLayoutSize(closeButton.gameObject, 48f, 180f);

            SetSerialized(modal, "titleText", titleText);
            SetSerialized(modal, "summaryText", summaryText);
            SetSerialized(modal, "statusText", statusText);
            SetSerialized(modal, "objectivesText", objectivesText);
            SetSerialized(modal, "rewardText", rewardText);
            SetSerialized(modal, "feedbackText", feedbackText);
            SetSerialized(modal, "primaryButton", primaryButton);
            SetSerialized(modal, "primaryButtonText", primaryButtonText);
            SetSerialized(modal, "tradeButton", tradeButton);
            SetSerialized(modal, "closeButton", closeButton);
        }

        private static void LinkQuestGiverTriggers(UIManager uiManager, QuestGiverModal questGiverModal)
        {
            if (questGiverModal == null)
            {
                return;
            }

            var triggers = Object.FindObjectsByType<QuestGiverTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (triggers == null || triggers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < triggers.Length; i++)
            {
                var trigger = triggers[i];
                if (trigger == null)
                {
                    continue;
                }

                SetSerialized(trigger, "uiManager", uiManager);
                SetSerialized(trigger, "questModal", questGiverModal);
                SetSerialized(trigger, "useDialogUi", true);
            }
        }

        private static void BuildQuestJournalUI(QuestJournalScreen screen, Sprite sprite, Font font)
        {
            if (screen.transform.childCount > 0)
            {
                Debug.LogWarning("[UIRootBuilder] QuestJournalScreen already has children. Skipping.");
                return;
            }

            CreateBackground(screen.transform, sprite, new Color(0f, 0f, 0f, 0.78f));

            var root = CreateUIElement("JournalLayout", screen.transform);
            var rootRect = root.GetComponent<RectTransform>();
            StretchRect(rootRect);

            var rootLayout = root.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(24, 24, 24, 24);
            rootLayout.spacing = 12f;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlHeight = true;
            rootLayout.childControlWidth = true;
            rootLayout.childForceExpandHeight = true;
            rootLayout.childForceExpandWidth = true;

            var titleRow = CreateUIElement("TitleRow", root.transform);
            var titleRowLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleRowLayout.spacing = 12f;
            titleRowLayout.childAlignment = TextAnchor.MiddleLeft;
            titleRowLayout.childControlHeight = true;
            titleRowLayout.childControlWidth = false;
            titleRowLayout.childForceExpandHeight = false;
            titleRowLayout.childForceExpandWidth = false;
            AddLayoutElement(titleRow, 56f);

            var titleGo = CreateUIElement("Title", titleRow.transform);
            var titleText = CreateText(titleGo, "QUEST JOURNAL", font, 30, TextAnchor.MiddleLeft);
            titleText.color = Color.white;
            AddLayoutElement(titleGo, 56f, 480f);

            var backButton = CreateButton(titleRow.transform, "Back", sprite, font);
            SetLayoutSize(backButton.gameObject, 44f, 180f);

            var content = CreateUIElement("Content", root.transform);
            var contentLayout = content.AddComponent<HorizontalLayoutGroup>();
            contentLayout.spacing = 16f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = true;
            contentLayout.childForceExpandWidth = true;
            var contentElement = content.AddComponent<LayoutElement>();
            contentElement.flexibleHeight = 1f;
            contentElement.flexibleWidth = 1f;

            var listPanel = CreateUIElement("QuestListPanel", content.transform);
            var listImage = listPanel.AddComponent<Image>();
            listImage.sprite = sprite;
            listImage.type = Image.Type.Sliced;
            listImage.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
            listImage.raycastTarget = true;
            var listElement = listPanel.AddComponent<LayoutElement>();
            listElement.preferredWidth = 460f;
            listElement.flexibleHeight = 1f;

            var listLayout = listPanel.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(12, 12, 12, 12);
            listLayout.spacing = 8f;
            listLayout.childAlignment = TextAnchor.UpperLeft;
            listLayout.childControlHeight = true;
            listLayout.childControlWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childForceExpandWidth = true;

            var listTitle = CreateText(CreateUIElement("ListTitle", listPanel.transform), "任务列表", font, 22, TextAnchor.MiddleLeft);
            listTitle.color = Color.white;
            AddLayoutElement(listTitle.gameObject, 34f);

            var listViewport = CreateUIElement("QuestListRoot", listPanel.transform);
            var listViewportRect = listViewport.GetComponent<RectTransform>();
            var listRootLayout = listViewport.AddComponent<VerticalLayoutGroup>();
            listRootLayout.spacing = 8f;
            listRootLayout.childAlignment = TextAnchor.UpperLeft;
            listRootLayout.childControlHeight = true;
            listRootLayout.childControlWidth = true;
            listRootLayout.childForceExpandHeight = false;
            listRootLayout.childForceExpandWidth = true;
            var listRootElement = listViewport.AddComponent<LayoutElement>();
            listRootElement.flexibleHeight = 1f;
            listRootElement.flexibleWidth = 1f;

            var emptyLabelGo = CreateUIElement("EmptyLabel", listViewport.transform);
            var emptyLabel = CreateText(emptyLabelGo, "暂无已接任务。", font, 16, TextAnchor.MiddleLeft);
            emptyLabel.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            AddLayoutElement(emptyLabelGo, 28f);

            var entryTemplate = CreateQuestJournalEntryTemplate(listViewport.transform, sprite, font);
            entryTemplate.gameObject.SetActive(false);

            var detailPanel = CreateUIElement("QuestDetailPanel", content.transform);
            var detailImage = detailPanel.AddComponent<Image>();
            detailImage.sprite = sprite;
            detailImage.type = Image.Type.Sliced;
            detailImage.color = new Color(0.1f, 0.12f, 0.17f, 0.95f);
            detailImage.raycastTarget = true;
            var detailElement = detailPanel.AddComponent<LayoutElement>();
            detailElement.flexibleWidth = 1f;
            detailElement.flexibleHeight = 1f;

            var detailLayout = detailPanel.AddComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset(14, 14, 14, 14);
            detailLayout.spacing = 10f;
            detailLayout.childAlignment = TextAnchor.UpperLeft;
            detailLayout.childControlHeight = true;
            detailLayout.childControlWidth = true;
            detailLayout.childForceExpandHeight = false;
            detailLayout.childForceExpandWidth = true;

            var detailTitleGo = CreateUIElement("DetailTitle", detailPanel.transform);
            var detailTitle = CreateText(detailTitleGo, "任务详情", font, 24, TextAnchor.MiddleLeft);
            detailTitle.color = Color.white;
            AddLayoutElement(detailTitleGo, 40f);

            var detailSummaryGo = CreateUIElement("DetailSummary", detailPanel.transform);
            var detailSummary = CreateText(detailSummaryGo, "当前没有可查看的任务。", font, 16, TextAnchor.UpperLeft);
            detailSummary.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            detailSummary.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailSummary.verticalOverflow = VerticalWrapMode.Overflow;
            AddLayoutElement(detailSummaryGo, 68f);

            var detailStatusGo = CreateUIElement("DetailStatus", detailPanel.transform);
            var detailStatus = CreateText(detailStatusGo, string.Empty, font, 16, TextAnchor.MiddleLeft);
            detailStatus.color = new Color(0.95f, 0.85f, 0.5f, 1f);
            AddLayoutElement(detailStatusGo, 28f);

            var detailObjectivesGo = CreateUIElement("DetailObjectives", detailPanel.transform);
            var detailObjectives = CreateText(detailObjectivesGo, string.Empty, font, 16, TextAnchor.UpperLeft);
            detailObjectives.color = Color.white;
            detailObjectives.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailObjectives.verticalOverflow = VerticalWrapMode.Overflow;
            var objectivesElement = detailObjectivesGo.AddComponent<LayoutElement>();
            objectivesElement.flexibleHeight = 1f;
            objectivesElement.preferredHeight = 220f;

            var detailRewardGo = CreateUIElement("DetailReward", detailPanel.transform);
            var detailReward = CreateText(detailRewardGo, string.Empty, font, 16, TextAnchor.MiddleLeft);
            detailReward.color = new Color(0.72f, 0.95f, 0.75f, 1f);
            AddLayoutElement(detailRewardGo, 30f);

            SetSerialized(screen, "listRoot", listViewportRect);
            SetSerialized(screen, "entryTemplate", entryTemplate);
            SetSerialized(screen, "emptyLabel", emptyLabel);
            SetSerialized(screen, "detailTitleText", detailTitle);
            SetSerialized(screen, "detailSummaryText", detailSummary);
            SetSerialized(screen, "detailObjectivesText", detailObjectives);
            SetSerialized(screen, "detailStatusText", detailStatus);
            SetSerialized(screen, "detailRewardText", detailReward);
            SetSerialized(screen, "backButton", backButton);
        }

        private static void BuildInventoryUI(InventoryScreen screen, Sprite sprite, Font font)
        {
            if (screen.transform.childCount > 0)
            {
                Debug.LogWarning("[UIRootBuilder] InventoryScreen already has children. Skipping.");
                return;
            }

            CreateBackground(screen.transform, sprite, new Color(0f, 0f, 0f, 0.75f));

            var layoutRoot = CreateUIElement("InventoryLayout", screen.transform);
            var layoutRect = layoutRoot.GetComponent<RectTransform>();
            StretchRect(layoutRect);

            var layout = layoutRoot.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            var inventoryPanel = CreateUIElement("InventoryPanel", layoutRoot.transform);
            var inventoryImage = inventoryPanel.AddComponent<Image>();
            inventoryImage.sprite = sprite;
            inventoryImage.type = Image.Type.Sliced;
            inventoryImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            inventoryImage.raycastTarget = true;

            var inventoryLayout = inventoryPanel.AddComponent<VerticalLayoutGroup>();
            inventoryLayout.padding = new RectOffset(20, 20, 20, 20);
            inventoryLayout.spacing = 12f;
            inventoryLayout.childAlignment = TextAnchor.UpperCenter;
            inventoryLayout.childControlHeight = true;
            inventoryLayout.childControlWidth = true;
            inventoryLayout.childForceExpandHeight = false;
            inventoryLayout.childForceExpandWidth = true;

            var inventoryElement = inventoryPanel.AddComponent<LayoutElement>();
            inventoryElement.preferredWidth = 980f;
            inventoryElement.flexibleWidth = 1f;
            inventoryElement.flexibleHeight = 1f;

            CreateTitle(inventoryPanel.transform, "INVENTORY", font, 28);

            var gridRoot = CreateUIElement("InventoryGrid", inventoryPanel.transform);
            var gridRect = gridRoot.GetComponent<RectTransform>();
            var gridLayout = gridRoot.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(80f, 80f);
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 6;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            var gridElement = gridRoot.AddComponent<LayoutElement>();
            gridElement.flexibleHeight = 1f;
            gridElement.flexibleWidth = 1f;

            var inventoryGrid = gridRoot.AddComponent<InventoryGridUI>();
            var inventorySlotTemplate = CreateInventorySlotTemplate(gridRoot.transform, sprite, font);
            inventorySlotTemplate.gameObject.SetActive(false);
            SetSerialized(inventoryGrid, "slotsRoot", gridRect);
            SetSerialized(inventoryGrid, "slotTemplate", inventorySlotTemplate);

            var sidePanel = CreateUIElement("SidePanel", layoutRoot.transform);
            var sideImage = sidePanel.AddComponent<Image>();
            sideImage.sprite = sprite;
            sideImage.type = Image.Type.Sliced;
            sideImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            sideImage.raycastTarget = true;

            var sideLayout = sidePanel.AddComponent<VerticalLayoutGroup>();
            sideLayout.padding = new RectOffset(20, 20, 20, 20);
            sideLayout.spacing = 16f;
            sideLayout.childAlignment = TextAnchor.UpperCenter;
            sideLayout.childControlHeight = true;
            sideLayout.childControlWidth = true;
            sideLayout.childForceExpandHeight = false;
            sideLayout.childForceExpandWidth = true;

            var sideElement = sidePanel.AddComponent<LayoutElement>();
            sideElement.preferredWidth = 720f;
            sideElement.flexibleWidth = 1f;
            sideElement.flexibleHeight = 1f;

            var equipmentSection = CreateUIElement("EquipmentSection", sidePanel.transform);
            var equipmentImage = equipmentSection.AddComponent<Image>();
            equipmentImage.sprite = sprite;
            equipmentImage.type = Image.Type.Sliced;
            equipmentImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            equipmentImage.raycastTarget = true;

            var equipmentLayout = equipmentSection.AddComponent<VerticalLayoutGroup>();
            equipmentLayout.padding = new RectOffset(16, 16, 16, 16);
            equipmentLayout.spacing = 8f;
            equipmentLayout.childAlignment = TextAnchor.UpperCenter;
            equipmentLayout.childControlHeight = true;
            equipmentLayout.childControlWidth = true;
            equipmentLayout.childForceExpandHeight = false;
            equipmentLayout.childForceExpandWidth = true;

            var equipmentElement = equipmentSection.AddComponent<LayoutElement>();
            equipmentElement.preferredHeight = 320f;
            equipmentElement.flexibleHeight = 0f;

            CreateTitle(equipmentSection.transform, "EQUIPMENT", font, 22);

            var equipmentSlotsRoot = CreateUIElement("EquipmentSlots", equipmentSection.transform);
            var equipmentSlotsRect = equipmentSlotsRoot.GetComponent<RectTransform>();
            var equipmentSlotsLayout = equipmentSlotsRoot.AddComponent<VerticalLayoutGroup>();
            equipmentSlotsLayout.spacing = 8f;
            equipmentSlotsLayout.childAlignment = TextAnchor.UpperCenter;
            equipmentSlotsLayout.childControlHeight = true;
            equipmentSlotsLayout.childControlWidth = true;
            equipmentSlotsLayout.childForceExpandHeight = false;
            equipmentSlotsLayout.childForceExpandWidth = true;

            var equipmentPanel = equipmentSlotsRoot.AddComponent<EquipmentPanelUI>();
            var equipmentSlotTemplate = CreateEquipmentSlotTemplate(equipmentSlotsRoot.transform, sprite, font);
            equipmentSlotTemplate.gameObject.SetActive(false);
            SetSerialized(equipmentPanel, "slotsRoot", equipmentSlotsRect);
            SetSerialized(equipmentPanel, "slotTemplate", equipmentSlotTemplate);

            var detailsSection = CreateUIElement("DetailsSection", sidePanel.transform);
            var detailsImage = detailsSection.AddComponent<Image>();
            detailsImage.sprite = sprite;
            detailsImage.type = Image.Type.Sliced;
            detailsImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            detailsImage.raycastTarget = true;

            var detailsLayout = detailsSection.AddComponent<VerticalLayoutGroup>();
            detailsLayout.padding = new RectOffset(16, 16, 16, 16);
            detailsLayout.spacing = 10f;
            detailsLayout.childAlignment = TextAnchor.UpperCenter;
            detailsLayout.childControlHeight = true;
            detailsLayout.childControlWidth = true;
            detailsLayout.childForceExpandHeight = false;
            detailsLayout.childForceExpandWidth = true;

            var detailsElement = detailsSection.AddComponent<LayoutElement>();
            detailsElement.flexibleHeight = 1f;
            detailsElement.flexibleWidth = 1f;

            CreateTitle(detailsSection.transform, "DETAILS", font, 22);

            var headerRow = CreateUIElement("Header", detailsSection.transform);
            var headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 12f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlHeight = true;
            headerLayout.childControlWidth = true;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childForceExpandWidth = false;
            AddLayoutElement(headerRow, 96f);

            var iconGo = CreateUIElement("Icon", headerRow.transform);
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            SetLayoutSize(iconGo, 88f, 88f);

            var infoRoot = CreateUIElement("Info", headerRow.transform);
            var infoLayout = infoRoot.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 4f;
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.childControlHeight = true;
            infoLayout.childControlWidth = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.childForceExpandWidth = true;
            AddLayoutElement(infoRoot, 88f, 0f);

            var nameGo = CreateUIElement("Name", infoRoot.transform);
            var nameText = CreateText(nameGo, "Item Name", font, 20, TextAnchor.MiddleLeft);
            nameText.color = Color.white;
            AddLayoutElement(nameGo, 28f);

            var slotGo = CreateUIElement("Slot", infoRoot.transform);
            var slotText = CreateText(slotGo, "Slot", font, 14, TextAnchor.MiddleLeft);
            slotText.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            AddLayoutElement(slotGo, 20f);

            var descriptionGo = CreateUIElement("Description", detailsSection.transform);
            var descriptionText = CreateText(descriptionGo, string.Empty, font, 14, TextAnchor.UpperLeft);
            descriptionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            AddLayoutElement(descriptionGo, 84f);

            var statsRoot = CreateUIElement("Stats", detailsSection.transform);
            var statsLayout = statsRoot.AddComponent<VerticalLayoutGroup>();
            statsLayout.spacing = 4f;
            statsLayout.childAlignment = TextAnchor.UpperLeft;
            statsLayout.childControlHeight = true;
            statsLayout.childControlWidth = true;
            statsLayout.childForceExpandHeight = false;
            statsLayout.childForceExpandWidth = true;

            var statTemplateGo = CreateUIElement("StatLine", statsRoot.transform);
            var statTemplate = CreateText(statTemplateGo, "Stat +0", font, 14, TextAnchor.MiddleLeft);
            statTemplate.color = Color.white;
            AddLayoutElement(statTemplateGo, 20f);
            statTemplateGo.SetActive(false);

            var buttonsRow = CreateUIElement("Buttons", detailsSection.transform);
            var buttonsLayout = buttonsRow.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 12f;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childForceExpandHeight = false;
            buttonsLayout.childForceExpandWidth = true;
            AddLayoutElement(buttonsRow, 52f);

            var equipButton = CreateButton(buttonsRow.transform, "Equip", sprite, font);
            var unequipButton = CreateButton(buttonsRow.transform, "Unequip", sprite, font);
            SetLayoutSize(equipButton.gameObject, 40f, 160f);
            SetLayoutSize(unequipButton.gameObject, 40f, 160f);

            var comparePanel = detailsSection.AddComponent<ItemComparePanelUI>();
            SetSerialized(comparePanel, "icon", iconImage);
            SetSerialized(comparePanel, "nameText", nameText);
            SetSerialized(comparePanel, "slotText", slotText);
            SetSerialized(comparePanel, "descriptionText", descriptionText);
            SetSerialized(comparePanel, "statsRoot", statsRoot.GetComponent<RectTransform>());
            SetSerialized(comparePanel, "statTemplate", statTemplate);

            SetSerialized(screen, "inventoryGrid", inventoryGrid);
            SetSerialized(screen, "equipmentPanel", equipmentPanel);
            SetSerialized(screen, "comparePanel", comparePanel);
            SetSerialized(screen, "equipButton", equipButton);
            SetSerialized(screen, "unequipButton", unequipButton);
        }

        internal static RectTransform CreateHudRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 anchoredPos)
        {
            var go = CreateUIElement(name, parent);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            return rect;
        }

        internal static ValueBarUI CreateHudValueBar(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 anchoredPos,
            Sprite sprite,
            Font font,
            Color backgroundColor,
            Color fillColor)
        {
            var root = CreateHudRect(name, parent, anchorMin, anchorMax, size, anchoredPos);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Sliced;
            bg.color = backgroundColor;
            bg.raycastTarget = false;

            var fillRect = CreateHudRect("Fill", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = sprite;
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var textRect = CreateHudRect("Text", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = CreateText(textRect.gameObject, string.Empty, font, 12, TextAnchor.MiddleCenter);
            text.color = Color.white;

            var bar = root.gameObject.AddComponent<ValueBarUI>();
            SetSerialized(bar, "fill", fill);
            SetSerialized(bar, "valueText", text);

            return bar;
        }

        internal static CastBarUI CreateHudCastBar(Transform parent, Sprite sprite, Font font)
        {
            var root = CreateHudRect("CastBar", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300f, 16f), new Vector2(0f, 80f));
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            var fillRect = CreateHudRect("Fill", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = sprite;
            fill.color = new Color(0.9f, 0.7f, 0.2f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            var labelRect = CreateHudRect("Label", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var label = CreateText(labelRect.gameObject, string.Empty, font, 12, TextAnchor.MiddleCenter);
            label.color = Color.white;

            var castBar = root.gameObject.AddComponent<CastBarUI>();
            SetSerialized(castBar, "fill", fill);
            SetSerialized(castBar, "label", label);
            return castBar;
        }

        internal static SkillBarUI CreateHudSkillBar(Transform parent, Sprite sprite, Font font, int slots)
        {
            var root = CreateHudRect("SkillBar", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(320f, 56f), new Vector2(0f, 20f));
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            var slotCount = Mathf.Max(1, slots);
            for (var i = 0; i < slotCount; i++)
            {
                var slot = CreateHudRect($"Slot_{i + 1}", root, Vector2.zero, Vector2.zero, new Vector2(48f, 48f), Vector2.zero);
                var slotBg = slot.gameObject.AddComponent<Image>();
                slotBg.color = new Color(0f, 0f, 0f, 0.6f);
                slotBg.sprite = sprite;
                slotBg.type = Image.Type.Sliced;
                slotBg.raycastTarget = false;

                var iconRect = CreateHudRect("Icon", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = sprite;
                icon.type = Image.Type.Sliced;
                icon.raycastTarget = false;

                var cooldownRect = CreateHudRect("Cooldown", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var cooldown = cooldownRect.gameObject.AddComponent<Image>();
                cooldown.sprite = sprite;
                cooldown.color = new Color(0f, 0f, 0f, 0.6f);
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Radial360;
                cooldown.fillOrigin = 2;
                cooldown.fillAmount = 0f;
                cooldown.raycastTarget = false;

                var cooldownTextRect = CreateHudRect("CooldownText", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var cooldownText = CreateText(cooldownTextRect.gameObject, string.Empty, font, 14, TextAnchor.MiddleCenter);
                cooldownText.color = Color.white;

                var keyRect = CreateHudRect("Key", slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, 16f), new Vector2(12f, -8f));
                var keyText = CreateText(keyRect.gameObject, string.Empty, font, 10, TextAnchor.MiddleLeft);
                keyText.color = Color.white;

                var slotUi = slot.gameObject.AddComponent<SkillSlotUI>();
                SetSerialized(slotUi, "icon", icon);
                SetSerialized(slotUi, "cooldownFill", cooldown);
                SetSerialized(slotUi, "cooldownText", cooldownText);
                SetSerialized(slotUi, "keyText", keyText);
            }

            return root.gameObject.AddComponent<SkillBarUI>();
        }

        internal static BuffBarUI CreateHudBuffBar(Transform parent, Sprite sprite, Font font, int slots)
        {
            var root = CreateHudRect("BuffBar", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(320f, 36f), new Vector2(-170f, -20f));
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 4f;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            var slotCount = Mathf.Max(1, slots);
            for (var i = 0; i < slotCount; i++)
            {
                var iconRoot = CreateHudRect($"Buff_{i + 1}", root, Vector2.zero, Vector2.zero, new Vector2(28f, 28f), Vector2.zero);
                var icon = iconRoot.gameObject.AddComponent<Image>();
                icon.sprite = sprite;
                icon.type = Image.Type.Sliced;
                icon.color = new Color(0f, 0f, 0f, 0.6f);
                icon.raycastTarget = false;

                var stackRect = CreateHudRect("Stacks", iconRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(18f, 14f), new Vector2(-2f, 2f));
                var stackText = CreateText(stackRect.gameObject, string.Empty, font, 10, TextAnchor.LowerRight);
                stackText.color = Color.white;

                var timerRect = CreateHudRect("Timer", iconRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, 14f), new Vector2(2f, -2f));
                var timerText = CreateText(timerRect.gameObject, string.Empty, font, 10, TextAnchor.UpperLeft);
                timerText.color = Color.white;

                var iconUi = iconRoot.gameObject.AddComponent<BuffIconUI>();
                SetSerialized(iconUi, "icon", icon);
                SetSerialized(iconUi, "stackText", stackText);
                SetSerialized(iconUi, "timerText", timerText);
            }

            return root.gameObject.AddComponent<BuffBarUI>();
        }

        internal static CombatLogUI CreateHudCombatLog(Transform parent, Sprite sprite, Font font)
        {
            // 设置 pivot 为左下角 (0, 0) 以便正确对齐
            var root = CreateHudRect("CombatLog", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(320f, 120f), new Vector2(10f, 10f));
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(10f, 10f);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.4f);
            bg.raycastTarget = false;
            bg.sprite = sprite;
            bg.type = Image.Type.Sliced;

            var textRect = CreateHudRect("LogText", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            textRect.offsetMin = new Vector2(6f, 6f);
            textRect.offsetMax = new Vector2(-6f, -6f);
            var text = CreateText(textRect.gameObject, string.Empty, font, 12, TextAnchor.LowerLeft);
            text.color = Color.white;

            var log = root.gameObject.AddComponent<CombatLogUI>();
            SetSerialized(log, "logText", text);
            return log;
        }

        internal static FloatingTextManager CreateHudFloatingText(Transform parent, Font font)
        {
            var root = CreateHudRect("FloatingText", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var template = CreateHudRect("FloatingTextTemplate", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 24f), Vector2.zero);
            var label = CreateText(template.gameObject, "0", font, 16, TextAnchor.MiddleCenter);
            label.color = Color.white;
            var group = template.gameObject.AddComponent<CanvasGroup>();
            var item = template.gameObject.AddComponent<FloatingTextItem>();
            SetSerialized(item, "label", label);
            SetSerialized(item, "canvasGroup", group);
            template.gameObject.SetActive(false);

            var manager = root.gameObject.AddComponent<FloatingTextManager>();
            SetSerialized(manager, "itemPrefab", item);
            SetSerialized(manager, "root", root);
            return manager;
        }

        internal static CombatDebugOverlay CreateHudDebugOverlay(Transform parent, Sprite sprite, Font font)
        {
            // 设置 pivot 为右上角 (1, 1) 以便正确对齐
            var root = CreateHudRect("DebugOverlay", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(260f, 160f), new Vector2(-10f, -10f));
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-10f, -10f);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.5f);
            bg.raycastTarget = false;

            var textRect = CreateHudRect("Text", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            textRect.offsetMin = new Vector2(6f, 6f);
            textRect.offsetMax = new Vector2(-6f, -6f);
            var text = CreateText(textRect.gameObject, string.Empty, font, 12, TextAnchor.UpperLeft);
            text.color = Color.white;

            var overlay = root.gameObject.AddComponent<CombatDebugOverlay>();
            SetSerialized(overlay, "outputText", text);
            SetSerialized(overlay, "background", bg);
            return overlay;
        }

        private static Image CreateBackground(Transform parent, Sprite sprite, Color color)
        {
            var go = CreateUIRootObject("Background", parent);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        private static RectTransform CreatePanel(Transform parent, Sprite sprite, Color color, Vector2 size)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create UI Panel");
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = true;

            return rect;
        }

        private static void ConfigureVerticalLayout(RectTransform root, int padding, float spacing, TextAnchor alignment)
        {
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
        }

        private static RectTransform CreateSettingRow(Transform parent, string label, Font font, float height)
        {
            var row = new GameObject(label.Replace(" ", string.Empty) + "_Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(row, "Create Setting Row");
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            AddLayoutElement(row, height);

            var labelGo = CreateUIElement("Label", row.transform);
            var text = CreateText(labelGo, label, font, 18, TextAnchor.MiddleLeft);
            text.color = Color.white;
            AddLayoutElement(labelGo, height, 200f);

            return row.GetComponent<RectTransform>();
        }

        private static Slider CreateSlider(Transform parent, DefaultControls.Resources resources, float min, float max)
        {
            var sliderGo = DefaultControls.CreateSlider(resources);
            sliderGo.name = "Slider";
            sliderGo.transform.SetParent(parent, false);
            AddLayoutElement(sliderGo, 24f, 320f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = max;
            return slider;
        }

        private static Toggle CreateToggle(Transform parent, DefaultControls.Resources resources)
        {
            var toggleGo = DefaultControls.CreateToggle(resources);
            toggleGo.name = "Toggle";
            toggleGo.transform.SetParent(parent, false);
            AddLayoutElement(toggleGo, 24f, 120f);

            return toggleGo.GetComponent<Toggle>();
        }

        private static Dropdown CreateDropdown(Transform parent, DefaultControls.Resources resources)
        {
            var dropdownGo = DefaultControls.CreateDropdown(resources);
            dropdownGo.name = "Dropdown";
            dropdownGo.transform.SetParent(parent, false);
            AddLayoutElement(dropdownGo, 32f, 280f);

            return dropdownGo.GetComponent<Dropdown>();
        }

        private static InputField CreateInputField(Transform parent, Sprite sprite, Font font, string placeholder, float width)
        {
            var resources = GetDefaultResources(sprite);
            var inputGo = DefaultControls.CreateInputField(resources);
            inputGo.name = "SaveNameInput";
            inputGo.transform.SetParent(parent, false);
            AddLayoutElement(inputGo, 32f, width);

            var input = inputGo.GetComponent<InputField>();
            var text = input.textComponent;
            if (text != null)
            {
                text.font = font;
                text.fontSize = 16;
                text.color = Color.white;
            }

            var placeholderText = input.placeholder as Text;
            if (placeholderText != null)
            {
                placeholderText.text = placeholder;
                placeholderText.font = font;
                placeholderText.fontSize = 14;
                placeholderText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            }

            return input;
        }

        private static RectTransform CreateSlotContainer(Transform parent)
        {
            var container = new GameObject("SlotContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            Undo.RegisterCreatedObjectUndo(container, "Create Slot Container");
            container.transform.SetParent(parent, false);

            var layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = container.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            AddLayoutElement(container, 260f);

            return container.GetComponent<RectTransform>();
        }

        private static QuestJournalEntryUI CreateQuestJournalEntryTemplate(Transform parent, Sprite sprite, Font font)
        {
            var root = new GameObject("QuestEntryTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Quest Entry Template");
            root.transform.SetParent(parent, false);

            var background = root.GetComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.14f, 0.17f, 0.22f, 1f);
            background.raycastTarget = true;

            var button = root.GetComponent<Button>();
            button.targetGraphic = background;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            AddLayoutElement(root, 64f);

            var textRoot = CreateUIElement("Texts", root.transform);
            var textLayout = textRoot.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2f;
            textLayout.childAlignment = TextAnchor.MiddleLeft;
            textLayout.childControlHeight = true;
            textLayout.childControlWidth = true;
            textLayout.childForceExpandHeight = false;
            textLayout.childForceExpandWidth = true;
            var textElement = textRoot.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;
            textElement.preferredHeight = 48f;

            var titleGo = CreateUIElement("Title", textRoot.transform);
            var title = CreateText(titleGo, "Quest", font, 18, TextAnchor.MiddleLeft);
            title.color = Color.white;
            AddLayoutElement(titleGo, 26f);

            var statusGo = CreateUIElement("Status", textRoot.transform);
            var status = CreateText(statusGo, "进行中", font, 14, TextAnchor.MiddleLeft);
            status.color = new Color(0.8f, 0.86f, 0.94f, 1f);
            AddLayoutElement(statusGo, 20f);

            var entry = root.AddComponent<QuestJournalEntryUI>();
            SetSerialized(entry, "button", button);
            SetSerialized(entry, "background", background);
            SetSerialized(entry, "titleText", title);
            SetSerialized(entry, "statusText", status);
            return entry;
        }

        private static SaveSlotEntry CreateSaveSlotEntryTemplate(Transform parent, Sprite sprite, Font font)
        {
            var root = new GameObject("SaveSlotTemplate", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Save Slot Template");
            root.transform.SetParent(parent, false);

            var rootImage = root.GetComponent<Image>();
            rootImage.sprite = sprite;
            rootImage.type = Image.Type.Sliced;
            rootImage.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            rootImage.raycastTarget = true;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            AddLayoutElement(root, 56f, 520f);

            var infoRoot = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup));
            infoRoot.transform.SetParent(root.transform, false);
            var infoLayout = infoRoot.GetComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 2f;
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.childControlHeight = true;
            infoLayout.childControlWidth = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.childForceExpandWidth = true;
            AddLayoutElement(infoRoot, 56f, 320f);

            var titleGo = CreateUIElement("Title", infoRoot.transform);
            var titleText = CreateText(titleGo, "Save Slot", font, 18, TextAnchor.MiddleLeft);
            titleText.color = Color.white;
            AddLayoutElement(titleGo, 26f);

            var detailGo = CreateUIElement("Detail", infoRoot.transform);
            var detailText = CreateText(detailGo, "Scene | Time", font, 12, TextAnchor.MiddleLeft);
            detailText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            AddLayoutElement(detailGo, 20f);

            var buttonsRoot = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttonsRoot.transform.SetParent(root.transform, false);
            var buttonsLayout = buttonsRoot.GetComponent<VerticalLayoutGroup>();
            buttonsLayout.spacing = 4f;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childForceExpandHeight = false;
            buttonsLayout.childForceExpandWidth = true;
            AddLayoutElement(buttonsRoot, 56f, 140f);

            var loadButton = CreateButton(buttonsRoot.transform, "Load", sprite, font);
            var deleteButton = CreateButton(buttonsRoot.transform, "Delete", sprite, font);
            SetLayoutSize(loadButton.gameObject, 32f, 120f);
            SetLayoutSize(deleteButton.gameObject, 32f, 120f);

            var entry = root.AddComponent<SaveSlotEntry>();
            SetSerialized(entry, "loadButton", loadButton);
            SetSerialized(entry, "deleteButton", deleteButton);
            SetSerialized(entry, "titleText", titleText);
            SetSerialized(entry, "detailText", detailText);

            return entry;
        }

        private static InventorySlotUI CreateInventorySlotTemplate(Transform parent, Sprite sprite, Font font)
        {
            var root = new GameObject("InventorySlotTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(root, "Create Inventory Slot Template");
            root.transform.SetParent(parent, false);

            var background = root.GetComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            background.raycastTarget = true;

            var button = root.GetComponent<Button>();
            button.targetGraphic = background;

            var iconGo = CreateUIElement("Icon", root.transform);
            var icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(6f, 6f);
            iconRect.offsetMax = new Vector2(-6f, -6f);

            var stackGo = CreateUIElement("StackText", root.transform);
            var stackText = CreateText(stackGo, string.Empty, font, 14, TextAnchor.LowerRight);
            stackText.color = Color.white;
            var stackRect = stackGo.GetComponent<RectTransform>();
            stackRect.anchorMin = Vector2.zero;
            stackRect.anchorMax = Vector2.one;
            stackRect.offsetMin = new Vector2(6f, 6f);
            stackRect.offsetMax = new Vector2(-6f, -6f);

            var selectionGo = CreateUIElement("Selection", root.transform);
            var selection = selectionGo.AddComponent<Image>();
            selection.color = new Color(1f, 1f, 1f, 0.15f);
            selection.raycastTarget = false;
            StretchRect(selectionGo.GetComponent<RectTransform>());
            selection.enabled = false;

            var slot = root.AddComponent<InventorySlotUI>();
            SetSerialized(slot, "button", button);
            SetSerialized(slot, "background", background);
            SetSerialized(slot, "icon", icon);
            SetSerialized(slot, "selection", selection);
            SetSerialized(slot, "stackText", stackText);

            return slot;
        }

        private static EquipmentSlotUI CreateEquipmentSlotTemplate(Transform parent, Sprite sprite, Font font)
        {
            var root = new GameObject("EquipmentSlotTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Equipment Slot Template");
            root.transform.SetParent(parent, false);

            var background = root.GetComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            background.raycastTarget = true;

            var button = root.GetComponent<Button>();
            button.targetGraphic = background;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            AddLayoutElement(root, 64f);

            var iconGo = CreateUIElement("Icon", root.transform);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetLayoutSize(iconGo, 48f, 48f);

            var labelsRoot = CreateUIElement("Labels", root.transform);
            var labelsLayout = labelsRoot.AddComponent<VerticalLayoutGroup>();
            labelsLayout.spacing = 2f;
            labelsLayout.childAlignment = TextAnchor.MiddleLeft;
            labelsLayout.childControlHeight = true;
            labelsLayout.childControlWidth = true;
            labelsLayout.childForceExpandHeight = false;
            labelsLayout.childForceExpandWidth = true;
            AddLayoutElement(labelsRoot, 48f, 0f);

            var slotLabelGo = CreateUIElement("SlotLabel", labelsRoot.transform);
            var slotLabel = CreateText(slotLabelGo, "Slot", font, 14, TextAnchor.MiddleLeft);
            slotLabel.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            AddLayoutElement(slotLabelGo, 20f);

            var itemLabelGo = CreateUIElement("ItemLabel", labelsRoot.transform);
            var itemLabel = CreateText(itemLabelGo, "Empty", font, 12, TextAnchor.MiddleLeft);
            itemLabel.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            AddLayoutElement(itemLabelGo, 18f);

            var selectionGo = CreateUIElement("Selection", root.transform);
            var selection = selectionGo.AddComponent<Image>();
            selection.color = new Color(1f, 1f, 1f, 0.12f);
            selection.raycastTarget = false;
            StretchRect(selectionGo.GetComponent<RectTransform>());
            selection.enabled = false;

            var slot = root.AddComponent<EquipmentSlotUI>();
            SetSerialized(slot, "button", button);
            SetSerialized(slot, "background", background);
            SetSerialized(slot, "icon", icon);
            SetSerialized(slot, "selection", selection);
            SetSerialized(slot, "slotLabel", slotLabel);
            SetSerialized(slot, "itemLabel", itemLabel);

            return slot;
        }

        private static DefaultControls.Resources GetDefaultResources(Sprite sprite)
        {
            return new DefaultControls.Resources
            {
                standard = sprite,
                background = sprite,
                inputField = sprite,
                knob = sprite,
                checkmark = sprite,
                dropdown = sprite,
                mask = sprite
            };
        }

        private static void CreateTitle(Transform parent, string text, Font font, int fontSize)
        {
            var go = CreateUIElement("Title", parent);
            var label = CreateText(go, text, font, fontSize, TextAnchor.MiddleCenter);
            label.color = Color.white;
            AddLayoutElement(go, 72f);
        }

        private static Text CreateLabel(Transform parent, string text, Font font, int fontSize)
        {
            var go = CreateUIElement("Label", parent);
            var label = CreateText(go, text, font, fontSize, TextAnchor.MiddleCenter);
            label.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            AddLayoutElement(go, 40f);
            return label;
        }

        private static Button CreateButton(Transform parent, string label, Sprite sprite, Font font)
        {
            var go = CreateUIElement($"Button_{label.Replace(" ", string.Empty)}", parent);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            AddLayoutElement(go, 48f, 320f);

            var textGo = CreateUIElement("Label", go.transform);
            var text = CreateText(textGo, label, font, 20, TextAnchor.MiddleCenter);
            text.color = Color.white;

            StretchRect(textGo.GetComponent<RectTransform>());

            return button;
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create UI Element");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateText(GameObject go, string text, Font font, int size, TextAnchor alignment)
        {
            var label = go.AddComponent<Text>();
            label.text = text;
            label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void AddLayoutElement(GameObject go, float preferredHeight, float preferredWidth = -1f)
        {
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
            }
        }

        private static void SetLayoutSize(GameObject go, float preferredHeight, float preferredWidth)
        {
            var layout = go.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = go.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = preferredHeight;
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
            }
        }

        internal static Sprite GetDefaultUISprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        internal static Font GetDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
