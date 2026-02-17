using System;
using UnityEngine;

namespace CombatSystem.Persistence
{
    public static class SettingsService
    {
        private const string PrefKey = "CombatSystem.Settings";
        private static SettingsData current;

        public static SettingsData Current => current;

        public static event Action<SettingsData> SettingsApplied;

        public static SettingsData LoadOrCreate()
        {
            if (current != null)
            {
                return current;
            }

            if (PlayerPrefs.HasKey(PrefKey))
            {
                var json = PlayerPrefs.GetString(PrefKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    current = JsonUtility.FromJson<SettingsData>(json);
                }
            }

            if (current == null)
            {
                current = new SettingsData();
            }

            Apply(current, false);
            return current;
        }

        public static void Apply(SettingsData data, bool save)
        {
            if (data == null)
            {
                data = new SettingsData();
            }

            current = data;
            if (current.cameraZoomDistance <= 0f)
            {
                current.cameraZoomDistance = 28.1f;
                current.edgePanEnabled = true;
            }
            else if (Mathf.Abs(current.cameraZoomDistance - 19.2f) <= 0.01f ||
                     Mathf.Abs(current.cameraZoomDistance - 17.5f) <= 0.01f ||
                     Mathf.Abs(current.cameraZoomDistance - 18.8f) <= 0.01f ||
                     Mathf.Abs(current.cameraZoomDistance - 24.4f) <= 0.01f)
            {
                // 旧版本默认缩放距离迁移到新的 LoL 观感预设。
                current.cameraZoomDistance = 28.1f;
            }

            var modeIndex = Mathf.Clamp((int)current.movementControlMode, 0, (int)MovementControlMode.RightClickMove);
            current.movementControlMode = (MovementControlMode)modeIndex;
            var cameraModeIndex = Mathf.Clamp((int)current.cameraControlMode, 0, (int)CameraControlMode.FreePan);
            current.cameraControlMode = (CameraControlMode)cameraModeIndex;
            current.cameraZoomDistance = Mathf.Clamp(current.cameraZoomDistance, 10f, 40f);

            AudioListener.volume = Mathf.Clamp01(current.masterVolume);
            Screen.fullScreen = current.fullscreen;

            var qualityIndex = Mathf.Clamp(current.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            if (QualitySettings.GetQualityLevel() != qualityIndex)
            {
                QualitySettings.SetQualityLevel(qualityIndex);
            }

            QualitySettings.vSyncCount = current.vSync ? 1 : 0;

            var fps = current.targetFps <= 0 ? -1 : current.targetFps;
            Application.targetFrameRate = fps;

            if (save)
            {
                Save(current);
            }

            SettingsApplied?.Invoke(current);
        }

        public static void Save(SettingsData data)
        {
            if (data == null)
            {
                return;
            }

            var json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PrefKey, json);
            PlayerPrefs.Save();
        }
    }
}
