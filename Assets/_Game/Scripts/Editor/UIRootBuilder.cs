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

namespace CombatSystem.Editor
{
    public static class UIRootBuilder
    {
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
            var pauseModal = CreateModal<PauseMenuModal>("PauseMenuModal", modalCanvas.transform);

            mainMenu.gameObject.SetActive(true);
            saveSelect.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            inGame.gameObject.SetActive(false);
            pauseModal.gameObject.SetActive(false);

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

            SetSerialized(pauseModal, "uiManager", uiManager);
            SetSerialized(pauseModal, "mainMenuScreen", mainMenu);
            SetSerialized(pauseModal, "settingsScreen", settings);
            SetSerialized(pauseModal, "saveManager", saveManager);

            SetSerialized(pauseHotkey, "uiManager", uiManager);
            SetSerialized(pauseHotkey, "pauseModal", pauseModal);

            EnsureEventSystem();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Combat/UI/Build Basic UI")]
        public static void BuildBasicUI()
        {
            var root = Object.FindFirstObjectByType<UIRoot>();
            if (root == null)
            {
                Debug.LogWarning("[UIRootBuilder] No UIRoot found in the scene.");
                return;
            }

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
            var pauseModal = root.GetComponentInChildren<PauseMenuModal>(true);
            var pauseHotkey = root.GetComponentInChildren<PauseMenuHotkey>(true);
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

            if (pauseModal != null)
            {
                EnsureCanvasGroup(pauseModal);
                BuildPauseModalUI(pauseModal, sprite, font);
                SetSerialized(pauseModal, "uiManager", uiManager);
                SetSerialized(pauseModal, "settingsScreen", settings);
                SetSerialized(pauseModal, "saveManager", saveManager);
            }

            if (pauseHotkey != null)
            {
                SetSerialized(pauseHotkey, "uiManager", uiManager);
                SetSerialized(pauseHotkey, "pauseModal", pauseModal);
            }

            if (root.HudCanvas != null)
            {
                BuildUnitHealthBars(root.HudCanvas);
                BuildCombatHUD(root.HudCanvas, sprite, font);
            }

            if (mainMenu != null)
            {
                SetSerialized(uiManager, "initialScreen", mainMenu);
            }

            EnsureEventSystem();
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

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasReferenceWidth, CanvasReferenceHeight);

            return canvas;
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
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
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
                TryWireCombatHud(existingController);
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
                TryWireCombatHud(existing);
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
                Debug.LogWarning("[UIRootBuilder] SettingsScreen already has children. Skipping.");
                return;
            }

            CreateBackground(screen.transform, sprite, new Color(0f, 0f, 0f, 0.65f));
            var panel = CreatePanel(screen.transform, sprite, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Vector2(640f, 520f));
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

            var applyButton = CreateButton(panel, "Apply", sprite, font);
            UnityEventTools.AddPersistentListener(applyButton.onClick, screen.Apply);

            var backButton = CreateButton(panel, "Back", sprite, font);
            UnityEventTools.AddPersistentListener(backButton.onClick, screen.Back);

            SetSerialized(screen, "masterVolume", masterSlider);
            SetSerialized(screen, "fullscreenToggle", fullscreenToggle);
            SetSerialized(screen, "vSyncToggle", vSyncToggle);
            SetSerialized(screen, "qualityDropdown", qualityDropdown);
            SetSerialized(screen, "fpsDropdown", fpsDropdown);
            SetSerialized(screen, "applyButton", applyButton);
        }

        private static void BuildPauseModalUI(PauseMenuModal modal, Sprite sprite, Font font)
        {
            if (modal.transform.childCount > 0)
            {
                Debug.LogWarning("[UIRootBuilder] PauseMenuModal already has children. Skipping.");
                return;
            }

            var background = CreateBackground(modal.transform, sprite, new Color(0f, 0f, 0f, 0.7f));
            var backgroundButton = background.gameObject.AddComponent<Button>();
            backgroundButton.targetGraphic = background;
            UnityEventTools.AddPersistentListener(backgroundButton.onClick, modal.HandleBackgroundClick);

            var panel = CreatePanel(modal.transform, sprite, new Color(0.1f, 0.1f, 0.1f, 0.95f), new Vector2(420f, 360f));
            ConfigureVerticalLayout(panel, 20, 10, TextAnchor.MiddleCenter);

            CreateTitle(panel, "PAUSED", font, 30);

            var resumeButton = CreateButton(panel, "Resume", sprite, font);
            UnityEventTools.AddPersistentListener(resumeButton.onClick, modal.Resume);

            var saveButton = CreateButton(panel, "Save Game", sprite, font);
            UnityEventTools.AddPersistentListener(saveButton.onClick, modal.SaveGame);

            var settingsButton = CreateButton(panel, "Settings", sprite, font);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, modal.OpenSettings);

            var menuButton = CreateButton(panel, "Main Menu", sprite, font);
            UnityEventTools.AddPersistentListener(menuButton.onClick, modal.BackToMenu);
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
                slotBg.raycastTarget = false;

                var iconRect = CreateHudRect("Icon", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = sprite;
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

        private static SaveSlotEntry CreateSaveSlotEntryTemplate(Transform parent, Sprite sprite, Font font)
        {
            var root = new GameObject("SaveSlotTemplate", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Save Slot Template");
            root.transform.SetParent(parent, false);

            var rootImage = root.GetComponent<Image>();
            rootImage.sprite = sprite;
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
