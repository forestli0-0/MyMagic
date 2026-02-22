using System.Collections.Generic;
using CombatSystem.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class SettingsScreen : UIScreenBase
    {
        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase backScreen;

        [Header("Controls")]
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Slider cameraZoomSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Dropdown fpsDropdown;
        [SerializeField] private Dropdown movementModeDropdown;
        [SerializeField] private Dropdown cameraModeDropdown;
        [SerializeField] private Toggle edgePanToggle;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button gameplayCategoryButton;
        [SerializeField] private Button controlCategoryButton;
        [SerializeField] private Button audioCategoryButton;
        [SerializeField] private Button videoCategoryButton;
        [SerializeField] private Button graphicsCategoryButton;
        [SerializeField] private GameObject gameplayCategoryPanel;
        [SerializeField] private GameObject controlCategoryPanel;
        [SerializeField] private GameObject audioCategoryPanel;
        [SerializeField] private GameObject videoCategoryPanel;
        [SerializeField] private GameObject graphicsCategoryPanel;
        [SerializeField] private Color activeCategoryColor = new Color(0.26f, 0.38f, 0.56f, 1f);
        [SerializeField] private Color inactiveCategoryColor = new Color(0.2f, 0.22f, 0.26f, 1f);
        [SerializeField] private Color activeCategoryTextColor = new Color(0.97f, 0.98f, 1f, 1f);
        [SerializeField] private Color inactiveCategoryTextColor = new Color(0.85f, 0.87f, 0.9f, 1f);
        [SerializeField] private PauseMenuModal pauseMenuModal;

        private static readonly int[] FpsOptions = { -1, 30, 60, 120 };
        private static readonly string[] FpsLabels = { "不限帧率", "30", "60", "120" };
        private static readonly string[] MovementModeLabels = { "键盘 WASD", "鼠标右键移动" };
        private static readonly string[] CameraModeLabels = { "锁定跟随", "自由平移（边缘滚屏）" };
        private const float CameraZoomMin = 10f;
        private const float CameraZoomMax = 40f;
        private bool initialized;
        private bool useStackBack;
        private bool reopenPauseModalOnBack;
        private SettingsCategory activeCategory = SettingsCategory.Gameplay;

        private enum SettingsCategory
        {
            Gameplay = 0,
            Control = 1,
            Audio = 2,
            Video = 3,
            Graphics = 4
        }

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            UIThemeRuntime.ThemeChanged += HandleThemeChanged;
            ApplyThemeColors();
        }

        private void OnDisable()
        {
            UIThemeRuntime.ThemeChanged -= HandleThemeChanged;
        }

        public override void OnEnter()
        {
            ApplyThemeColors();
            EnsureInitialized();
            var data = SettingsService.LoadOrCreate();
            ApplyToUI(data);
            ApplyCategory(activeCategory);
            if (uiManager != null)
            {
                uiManager.SetHudVisible(false);
            }
        }

        public override void OnExit()
        {
            useStackBack = false;
            reopenPauseModalOnBack = false;
        }

        public void Back()
        {
            if (uiManager == null)
            {
                return;
            }

            if (useStackBack)
            {
                var shouldReopenPause = reopenPauseModalOnBack;
                useStackBack = false;
                reopenPauseModalOnBack = false;
                uiManager.PopScreen();

                if (shouldReopenPause)
                {
                    EnsurePauseModalReference();
                    if (pauseMenuModal != null)
                    {
                        uiManager.PushModal(pauseMenuModal);
                    }
                }

                return;
            }

            if (backScreen != null)
            {
                uiManager.ShowScreen(backScreen, true);
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || uiManager == null)
            {
                return;
            }

            if (uiManager.CurrentScreen != this || uiManager.ModalCount > 0)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Back();
            }
        }

        public void Apply()
        {
            var data = ReadFromUI();
            SettingsService.Apply(data, true);
            UIToast.Success("设置已应用。");
        }

        public void RequestPauseGameplay(bool value)
        {
            if (!value)
            {
                reopenPauseModalOnBack = false;
            }
        }

        public void RequestStackBack()
        {
            useStackBack = true;
        }

        public void RequestReturnToPauseMenu(bool value)
        {
            reopenPauseModalOnBack = value;
            if (value)
            {
                useStackBack = true;
            }
        }

        public void ShowGameplayCategory()
        {
            ApplyCategory(SettingsCategory.Gameplay);
        }

        public void ShowControlCategory()
        {
            ApplyCategory(SettingsCategory.Control);
        }

        public void ShowAudioCategory()
        {
            ApplyCategory(SettingsCategory.Audio);
        }

        public void ShowVideoCategory()
        {
            ApplyCategory(SettingsCategory.Video);
        }

        public void ShowGraphicsCategory()
        {
            ApplyCategory(SettingsCategory.Graphics);
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            EnsurePauseModalReference();
            BuildQualityOptions();
            BuildFpsOptions();
            BuildMovementModeOptions();
            BuildCameraModeOptions();
            BindCategoryButtons();

            if (applyButton != null)
            {
                if (applyButton.onClick.GetPersistentEventCount() == 0)
                {
                    applyButton.onClick.RemoveListener(Apply);
                    applyButton.onClick.AddListener(Apply);
                }
            }

            initialized = true;
        }

        private void ApplyToUI(SettingsData data)
        {
            if (data == null)
            {
                return;
            }

            if (masterVolume != null)
            {
                masterVolume.SetValueWithoutNotify(Mathf.Clamp01(data.masterVolume));
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(data.fullscreen);
            }

            if (vSyncToggle != null)
            {
                vSyncToggle.SetIsOnWithoutNotify(data.vSync);
            }

            if (qualityDropdown != null)
            {
                var qualityIndex = Mathf.Clamp(data.qualityLevel, 0, Mathf.Max(0, qualityDropdown.options.Count - 1));
                qualityDropdown.SetValueWithoutNotify(qualityIndex);
            }

            if (fpsDropdown != null)
            {
                var index = GetFpsIndex(data.targetFps);
                fpsDropdown.SetValueWithoutNotify(index);
            }

            if (movementModeDropdown != null)
            {
                var index = Mathf.Clamp((int)data.movementControlMode, 0, MovementModeLabels.Length - 1);
                movementModeDropdown.SetValueWithoutNotify(index);
            }

            if (cameraModeDropdown != null)
            {
                var index = Mathf.Clamp((int)data.cameraControlMode, 0, CameraModeLabels.Length - 1);
                cameraModeDropdown.SetValueWithoutNotify(index);
            }

            if (edgePanToggle != null)
            {
                edgePanToggle.SetIsOnWithoutNotify(data.edgePanEnabled);
            }

            if (cameraZoomSlider != null)
            {
                cameraZoomSlider.SetValueWithoutNotify(Mathf.Clamp(data.cameraZoomDistance, CameraZoomMin, CameraZoomMax));
            }
        }

        private SettingsData ReadFromUI()
        {
            var data = (SettingsService.Current ?? SettingsService.LoadOrCreate())?.Clone() ?? new SettingsData();

            if (masterVolume != null)
            {
                data.masterVolume = masterVolume.value;
            }

            if (fullscreenToggle != null)
            {
                data.fullscreen = fullscreenToggle.isOn;
            }

            if (vSyncToggle != null)
            {
                data.vSync = vSyncToggle.isOn;
            }

            if (qualityDropdown != null)
            {
                data.qualityLevel = qualityDropdown.value;
            }

            if (fpsDropdown != null)
            {
                var index = Mathf.Clamp(fpsDropdown.value, 0, FpsOptions.Length - 1);
                data.targetFps = FpsOptions[index];
            }

            if (movementModeDropdown != null)
            {
                var index = Mathf.Clamp(movementModeDropdown.value, 0, MovementModeLabels.Length - 1);
                data.movementControlMode = (MovementControlMode)index;
            }

            if (cameraModeDropdown != null)
            {
                var index = Mathf.Clamp(cameraModeDropdown.value, 0, CameraModeLabels.Length - 1);
                data.cameraControlMode = (CameraControlMode)index;
            }

            if (edgePanToggle != null)
            {
                data.edgePanEnabled = edgePanToggle.isOn;
            }

            if (cameraZoomSlider != null)
            {
                data.cameraZoomDistance = Mathf.Clamp(cameraZoomSlider.value, CameraZoomMin, CameraZoomMax);
            }

            return data;
        }

        private void BuildQualityOptions()
        {
            if (qualityDropdown == null)
            {
                return;
            }

            qualityDropdown.ClearOptions();
            var options = new List<string>(QualitySettings.names);
            qualityDropdown.AddOptions(options);
        }

        private void BuildFpsOptions()
        {
            if (fpsDropdown == null)
            {
                return;
            }

            fpsDropdown.ClearOptions();
            fpsDropdown.AddOptions(new List<string>(FpsLabels));
        }

        private void BuildMovementModeOptions()
        {
            if (movementModeDropdown == null)
            {
                return;
            }

            movementModeDropdown.ClearOptions();
            movementModeDropdown.AddOptions(new List<string>(MovementModeLabels));
        }

        private void BuildCameraModeOptions()
        {
            if (cameraModeDropdown == null)
            {
                return;
            }

            cameraModeDropdown.ClearOptions();
            cameraModeDropdown.AddOptions(new List<string>(CameraModeLabels));
        }

        private static int GetFpsIndex(int fps)
        {
            for (var i = 0; i < FpsOptions.Length; i++)
            {
                if (FpsOptions[i] == fps)
                {
                    return i;
                }
            }

            return 0;
        }

        private void BindCategoryButtons()
        {
            BindCategoryButton(gameplayCategoryButton, ShowGameplayCategory);
            BindCategoryButton(controlCategoryButton, ShowControlCategory);
            BindCategoryButton(audioCategoryButton, ShowAudioCategory);
            BindCategoryButton(videoCategoryButton, ShowVideoCategory);
            BindCategoryButton(graphicsCategoryButton, ShowGraphicsCategory);
        }

        private static void BindCategoryButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null || handler == null)
            {
                return;
            }

            button.onClick.RemoveListener(handler);
            button.onClick.AddListener(handler);
        }

        private void ApplyCategory(SettingsCategory category)
        {
            activeCategory = category;

            SetPanelActive(gameplayCategoryPanel, category == SettingsCategory.Gameplay);
            SetPanelActive(controlCategoryPanel, category == SettingsCategory.Control);
            SetPanelActive(audioCategoryPanel, category == SettingsCategory.Audio);
            SetPanelActive(videoCategoryPanel, category == SettingsCategory.Video);
            SetPanelActive(graphicsCategoryPanel, category == SettingsCategory.Graphics);

            ApplyCategoryButtonState(gameplayCategoryButton, category == SettingsCategory.Gameplay);
            ApplyCategoryButtonState(controlCategoryButton, category == SettingsCategory.Control);
            ApplyCategoryButtonState(audioCategoryButton, category == SettingsCategory.Audio);
            ApplyCategoryButtonState(videoCategoryButton, category == SettingsCategory.Video);
            ApplyCategoryButtonState(graphicsCategoryButton, category == SettingsCategory.Graphics);
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private void EnsurePauseModalReference()
        {
            if (pauseMenuModal != null)
            {
                return;
            }

            pauseMenuModal = FindFirstObjectByType<PauseMenuModal>(FindObjectsInactive.Include);
        }

        private void ApplyCategoryButtonState(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            // Keep active category tabs interactable to avoid Unity disabled tint overriding active style.
            button.interactable = true;

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = active ? activeCategoryColor : inactiveCategoryColor;
            }
            UIStyleKit.ApplyButtonStateColors(
                button,
                active ? activeCategoryColor : inactiveCategoryColor,
                active ? 0.06f : 0.1f,
                0.18f,
                0.52f,
                0.08f);

            var labels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                {
                    continue;
                }

                label.color = active ? activeCategoryTextColor : inactiveCategoryTextColor;
            }
        }

        public override string GetFooterHintText()
        {
            return "{BACK} 返回上一页    {CONFIRM} 调整选项    点击应用保存设置";
        }

        private void HandleThemeChanged(UIThemeConfig theme)
        {
            ApplyThemeColors();
            ApplyCategory(activeCategory);
        }

        private void ApplyThemeColors()
        {
            activeCategoryColor = UIStyleKit.TabActiveColor;
            inactiveCategoryColor = UIStyleKit.TabInactiveColor;
            activeCategoryTextColor = UIStyleKit.TabActiveTextColor;
            inactiveCategoryTextColor = UIStyleKit.TabInactiveTextColor;
            ApplyValueControlTheme();
        }

        private void ApplyValueControlTheme()
        {
            ApplyDropdownTheme(qualityDropdown);
            ApplyDropdownTheme(fpsDropdown);
            ApplyDropdownTheme(movementModeDropdown);
            ApplyDropdownTheme(cameraModeDropdown);

            ApplyToggleTheme(fullscreenToggle);
            ApplyToggleTheme(vSyncToggle);
            ApplyToggleTheme(edgePanToggle);

            ApplySliderTheme(masterVolume);
            ApplySliderTheme(cameraZoomSlider);
        }

        private static void ApplyDropdownTheme(Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            UIStyleKit.ApplySelectableStateColors(dropdown, UIStyleKit.TabInactiveColor, 0.1f, 0.16f, 0.5f, 0.08f);

            if (dropdown.targetGraphic is Image background)
            {
                background.color = UIStyleKit.TabInactiveColor;
            }

            if (dropdown.captionText != null)
            {
                dropdown.captionText.color = UIStyleKit.TabActiveTextColor;
            }

            var fallbackLabel = dropdown.transform.Find("Label") != null
                ? dropdown.transform.Find("Label").GetComponent<Text>()
                : null;
            if (fallbackLabel != null)
            {
                fallbackLabel.color = UIStyleKit.TabActiveTextColor;
            }

            var arrow = dropdown.transform.Find("Arrow") != null
                ? dropdown.transform.Find("Arrow").GetComponent<Image>()
                : null;
            if (arrow != null)
            {
                // DefaultControls uses an image placeholder for Arrow.
                // In this project the placeholder sprite is square, so hide it
                // and use a text caret for a clear dropdown affordance.
                arrow.enabled = false;
                arrow.raycastTarget = false;
            }
            EnsureDropdownArrowGlyph(dropdown);

            var template = dropdown.template;
            if (template != null)
            {
                var templateImage = template.GetComponent<Image>();
                if (templateImage != null)
                {
                    templateImage.color = UIStyleKit.GameplayPanelAltColor;
                }

                var viewportImage = template.Find("Viewport") != null
                    ? template.Find("Viewport").GetComponent<Image>()
                    : null;
                if (viewportImage != null)
                {
                    viewportImage.color = UIStyleKit.GameplayPanelColor;
                }

                var itemToggle = template.Find("Viewport/Content/Item") != null
                    ? template.Find("Viewport/Content/Item").GetComponent<Toggle>()
                    : null;
                if (itemToggle != null)
                {
                    UIStyleKit.ApplySelectableStateColors(
                        itemToggle,
                        UIStyleKit.TabInactiveColor,
                        0.12f,
                        0.18f,
                        0.52f,
                        0.08f,
                        0.08f);
                }

                var itemBackground = template.Find("Viewport/Content/Item/Item Background") != null
                    ? template.Find("Viewport/Content/Item/Item Background").GetComponent<Image>()
                    : null;
                if (itemBackground != null)
                {
                    itemBackground.color = UIStyleKit.TabInactiveColor;
                }

                var itemTexts = template.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < itemTexts.Length; i++)
                {
                    var text = itemTexts[i];
                    if (text == null)
                    {
                        continue;
                    }

                    text.color = UIStyleKit.TabActiveTextColor;
                }

                var checkmarks = template.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < checkmarks.Length; i++)
                {
                    var image = checkmarks[i];
                    if (image == null)
                    {
                        continue;
                    }

                    if (image.gameObject.name != "Checkmark")
                    {
                        continue;
                    }

                    image.color = UIStyleKit.TabActiveTextColor;
                }
            }

            ApplyDropdownListTheme(dropdown);
            dropdown.RefreshShownValue();
        }

        private static void EnsureDropdownArrowGlyph(Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            var existing = dropdown.transform.Find("ArrowGlyph");
            Text glyphText;
            if (existing == null)
            {
                var go = new GameObject("ArrowGlyph", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(dropdown.transform, false);
                glyphText = go.GetComponent<Text>();

                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(22f, 22f);
                rect.anchoredPosition = new Vector2(-10f, 0f);
            }
            else
            {
                glyphText = existing.GetComponent<Text>();
            }

            if (glyphText == null)
            {
                return;
            }

            glyphText.text = "▼";
            glyphText.font = UIStyleKit.ThemeFont;
            glyphText.fontSize = 16;
            glyphText.alignment = TextAnchor.MiddleCenter;
            glyphText.raycastTarget = false;
            glyphText.color = UIStyleKit.TabActiveTextColor;
        }

        private static void ApplyDropdownListTheme(Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            // Runtime instantiated list is named "Dropdown List".
            var listRoot = dropdown.transform.Find("Dropdown List");
            if (listRoot == null)
            {
                return;
            }

            var listImage = listRoot.GetComponent<Image>();
            if (listImage != null)
            {
                listImage.color = UIStyleKit.GameplayPanelAltColor;
            }

            var listTexts = listRoot.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < listTexts.Length; i++)
            {
                var text = listTexts[i];
                if (text == null)
                {
                    continue;
                }

                text.color = UIStyleKit.TabActiveTextColor;
            }

            var listToggles = listRoot.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < listToggles.Length; i++)
            {
                var toggle = listToggles[i];
                if (toggle == null)
                {
                    continue;
                }

                UIStyleKit.ApplySelectableStateColors(
                    toggle,
                    UIStyleKit.TabInactiveColor,
                    0.12f,
                    0.18f,
                    0.52f,
                    0.08f,
                    0.08f);
            }
        }

        private static void ApplyToggleTheme(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            UIStyleKit.ApplySelectableStateColors(toggle, UIStyleKit.TabInactiveColor, 0.1f, 0.16f, 0.5f, 0.08f);

            var toggleImage = toggle.targetGraphic as Image;
            if (toggleImage != null)
            {
                toggleImage.color = UIStyleKit.TabInactiveColor;
            }

            if (toggle.graphic is Image checkmark)
            {
                checkmark.color = UIStyleKit.TabActiveTextColor;
            }
        }

        private static void ApplySliderTheme(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            UIStyleKit.ApplySelectableStateColors(slider, UIStyleKit.TabInactiveColor, 0.1f, 0.16f, 0.5f, 0.08f);

            var handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (handle != null)
            {
                handle.color = UIStyleKit.TabActiveTextColor;
            }
        }
    }
}
