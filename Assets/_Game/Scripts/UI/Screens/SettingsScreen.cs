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
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Dropdown fpsDropdown;
        [SerializeField] private Dropdown movementModeDropdown;
        [SerializeField] private Button applyButton;
        [SerializeField] private bool pauseGameplay = false;

        private static readonly int[] FpsOptions = { -1, 30, 60, 120 };
        private static readonly string[] FpsLabels = { "Unlimited", "30", "60", "120" };
        private static readonly string[] MovementModeLabels = { "WASD", "Right Click Move" };
        private bool initialized;
        private bool pauseRequested;
        private bool pausedByScreen;
        private float cachedTimeScale;
        private bool useStackBack;

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

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            BuildQualityOptions();
            BuildFpsOptions();
            BuildMovementModeOptions();

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
        }

        private SettingsData ReadFromUI()
        {
            var data = new SettingsData();

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
    }
}
