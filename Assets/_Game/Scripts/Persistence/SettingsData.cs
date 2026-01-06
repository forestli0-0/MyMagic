using System;

namespace CombatSystem.Persistence
{
    [Serializable]
    public class SettingsData
    {
        public float masterVolume = 1f;
        public bool fullscreen = true;
        public bool vSync = false;
        public int qualityLevel = 0;
        public int targetFps = 60;
    }
}
