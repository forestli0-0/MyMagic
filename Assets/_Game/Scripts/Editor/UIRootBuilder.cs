using CombatSystem.UI;
using UnityEditor;
using UnityEditor.Events;
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
                BuildSaveSelectUI(saveSelect, sprite, font);
                SetSerialized(saveSelect, "uiManager", uiManager);
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
            }

            if (pauseHotkey != null)
            {
                SetSerialized(pauseHotkey, "uiManager", uiManager);
                SetSerialized(pauseHotkey, "pauseModal", pauseModal);
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

        private static void AssignCanvasGroup(UIScreenBase screen, CanvasGroup group)
        {
            SetSerialized(screen, "canvasGroup", group);
        }

        private static void AssignCanvasGroup(UIModalBase modal, CanvasGroup group)
        {
            SetSerialized(modal, "canvasGroup", group);
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

        private static void BuildSaveSelectUI(SaveSelectScreen screen, Sprite sprite, Font font)
        {
            if (screen.transform.childCount > 0)
            {
                Debug.LogWarning("[UIRootBuilder] SaveSelectScreen already has children. Skipping.");
                return;
            }

            CreateBackground(screen.transform, sprite, new Color(0f, 0f, 0f, 0.65f));
            var panel = CreatePanel(screen.transform, sprite, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Vector2(560f, 520f));
            ConfigureVerticalLayout(panel, 24, 12, TextAnchor.MiddleCenter);

            CreateTitle(panel, "SELECT SAVE", font, 32);
            CreateLabel(panel, "Save slots will appear here.", font, 18);

            var startButton = CreateButton(panel, "Start", sprite, font);
            UnityEventTools.AddPersistentListener(startButton.onClick, screen.StartGame);

            var backButton = CreateButton(panel, "Back", sprite, font);
            UnityEventTools.AddPersistentListener(backButton.onClick, screen.BackToMenu);
        }

        private static void BuildSettingsUI(SettingsScreen screen, Sprite sprite, Font font)
        {
            if (screen.transform.childCount > 0)
            {
                Debug.LogWarning("[UIRootBuilder] SettingsScreen already has children. Skipping.");
                return;
            }

            CreateBackground(screen.transform, sprite, new Color(0f, 0f, 0f, 0.65f));
            var panel = CreatePanel(screen.transform, sprite, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Vector2(560f, 420f));
            ConfigureVerticalLayout(panel, 24, 12, TextAnchor.MiddleCenter);

            CreateTitle(panel, "SETTINGS", font, 32);
            CreateLabel(panel, "Audio / Video / Controls", font, 18);

            var backButton = CreateButton(panel, "Back", sprite, font);
            UnityEventTools.AddPersistentListener(backButton.onClick, screen.Back);
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

            var settingsButton = CreateButton(panel, "Settings", sprite, font);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, modal.OpenSettings);

            var menuButton = CreateButton(panel, "Main Menu", sprite, font);
            UnityEventTools.AddPersistentListener(menuButton.onClick, modal.BackToMenu);
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

        private static void CreateTitle(Transform parent, string text, Font font, int fontSize)
        {
            var go = CreateUIElement("Title", parent);
            var label = CreateText(go, text, font, fontSize, TextAnchor.MiddleCenter);
            label.color = Color.white;
            AddLayoutElement(go, 72f);
        }

        private static void CreateLabel(Transform parent, string text, Font font, int fontSize)
        {
            var go = CreateUIElement("Label", parent);
            var label = CreateText(go, text, font, fontSize, TextAnchor.MiddleCenter);
            label.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            AddLayoutElement(go, 40f);
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

        private static Sprite GetDefaultUISprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static Font GetDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
