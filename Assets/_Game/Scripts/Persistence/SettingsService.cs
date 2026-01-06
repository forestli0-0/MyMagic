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
