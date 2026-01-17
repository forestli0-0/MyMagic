using System;
using UnityEngine;

namespace CombatSystem.Persistence
{
    [Serializable]
    public class SaveSlotInfo
    {
        public string slotId;
        public string displayName;
        public long lastSavedUtcTicks;
        public string sceneName;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public bool hasResource;
        public int resourceType;
        public float resource;
        public string levelId;
        public string spawnPointId;
    }

    [Serializable]
    public class SaveData
    {
        public SaveSlotInfo slotInfo = new SaveSlotInfo();
        public PlayerSaveData player = new PlayerSaveData();
    }
}
