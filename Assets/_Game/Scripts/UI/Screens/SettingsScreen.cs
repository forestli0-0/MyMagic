using System.Collections.Generic;
using CombatSystem.Persistence;
using UnityEngine;
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
        [SerializeField] private bool pauseGameplay = false;

        private static readonly int[] FpsOptions = { -1, 30, 60, 120 };
        private static readonly string[] FpsLabels = { "不限帧率", "30", "60", "120" };
        private static readonly string[] MovementModeLabels = { "键盘 WASD", "鼠标右键移动" };
        private static readonly string[] CameraModeLabels = { "锁定跟随", "自由平移（边缘滚屏）" };
        private const float CameraZoomMin = 10f;
        private const float CameraZoomMax = 40f;
        private bool initialized;
        private bool pauseRequested;
        private bool pausedByScreen;
        private float cachedTimeScale;
        private bool useStackBack;
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

        public override void OnEnter()
        {
            EnsureInitialized();
            var data = SettingsService.LoadOrCreate();
            ApplyToUI(data);
            ApplyCategory(activeCategory);
            ApplyPauseState();
        }

        public override void OnExit()
        {
            if (pausedByScreen)
            {
                Time.timeScale = cachedTimeScale;
                pausedByScreen = false;
            }

            pauseRequested = false;
            useStackBack = false;
        }

        public void Back()
        {
            if (uiManager == null)
            {
                return;
            }

            if (useStackBack)
            {
                useStackBack = false;
                uiManager.PopScreen();
                return;
            }

            if (backScreen != null)
            {
                uiManager.ShowScreen(backScreen, true);
            }
        }

        public void Apply()
        {
            var data = ReadFromUI();
            SettingsService.Apply(data, true);
        }

        public void RequestPauseGameplay(bool value)
        {
            pauseRequested = value;
        }

        public void RequestStackBack()
        {
            useStackBack = true;
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

        private void ApplyPauseState()
        {
            if (!pauseGameplay && !pauseRequested)
            {
                return;
            }

            if (!Mathf.Approximately(Time.timeScale, 0f))
            {
                cachedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                pausedByScreen = true;
            }
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

        private void ApplyCategoryButtonState(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = !active;

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = active ? activeCategoryColor : inactiveCategoryColor;
            }

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
    }
}
