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
        public bool hasCurrency;
        public int currency;
        public string levelId;
        public string spawnPointId;
        public bool hasProgression;
        public int level;
        public int experience;
        public int attributePoints;
    }

    [Serializable]
    public class SaveData
    {
        public SaveSlotInfo slotInfo = new SaveSlotInfo();
        public PlayerSaveData player = new PlayerSaveData();
        public InventorySaveData inventory = new InventorySaveData();
        public EquipmentSaveData equipment = new EquipmentSaveData();
    }

    [Serializable]
    public class ItemInstanceSaveData
    {
        public string itemId;
        public int stack;
        public int rarity;
        public string[] affixIds;
    }

    [Serializable]
    public class InventorySaveData
    {
        public int capacity;
        public ItemInstanceSaveData[] items;
    }

    [Serializable]
    public class EquippedItemSaveData
    {
        public int slotIndex;
        public ItemInstanceSaveData item;
    }

    [Serializable]
    public class EquipmentSaveData
    {
        public EquippedItemSaveData[] slots;
    }
}
