using System;

namespace CombatSystem.Persistence
{
    public enum MovementControlMode
    {
        KeyboardWASD = 0,
        RightClickMove = 1
    }

    [Serializable]
    public class SettingsData
    {
        public float masterVolume = 1f;
        public bool fullscreen = true;
        public bool vSync = false;
        public int qualityLevel = 0;
        public int targetFps = 60;
        public MovementControlMode movementControlMode = MovementControlMode.KeyboardWASD;
    }
}
