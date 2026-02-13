using System;
using System.Collections;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.Persistence
{
    public class SaveGameManager : MonoBehaviour
    {
        private const string LastSlotKey = "CombatSystem.LastSaveSlotId";

        [Header("Settings")]
        [SerializeField] private GameDatabase database;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool savePosition = true;
        [SerializeField] private bool saveRotation = true;
        [SerializeField] private bool saveHealth = true;
        [SerializeField] private bool saveResource = true;
        [SerializeField] private bool saveCurrency = true;
        [SerializeField] private bool saveProgression = true;
        [SerializeField] private bool saveInventory = true;
        [SerializeField] private bool saveEquipment = true;
        [SerializeField] private bool saveQuests = true;
        [SerializeField] private string quickSaveName = "Quick Save";

        private string currentSlotId;
        private string currentSlotName;
        private SaveData pendingLoad;

        public List<SaveSlotInfo> GetSlots()
        {
            return SaveService.ListSlots();
        }

        public SaveSlotInfo SaveNew(string displayName)
        {
            var data = Capture();
            data.slotInfo.slotId = SaveService.CreateSlotId();
            data.slotInfo.displayName = ResolveDisplayName(displayName, null);
            SaveService.Save(data);
            RememberSlot(data.slotInfo);
            return data.slotInfo;
        }

        public SaveSlotInfo SaveToSlot(string slotId, string displayName)
        {
            var data = Capture();
            data.slotInfo.slotId = slotId;
            data.slotInfo.displayName = ResolveDisplayName(displayName, currentSlotName);
            SaveService.Save(data);
            RememberSlot(data.slotInfo);
            return data.slotInfo;
        }

        public SaveSlotInfo SaveCurrentOrNew(string displayName)
        {
            if (!string.IsNullOrEmpty(currentSlotId))
            {
                return SaveToSlot(currentSlotId, displayName);
            }

            var resolved = string.IsNullOrWhiteSpace(displayName) ? quickSaveName : displayName;
            return SaveNew(resolved);
        }

        public bool SaveCurrent()
        {
            if (string.IsNullOrEmpty(currentSlotId))
            {
                return false;
            }

            SaveToSlot(currentSlotId, currentSlotName);
            return true;
        }

        public bool LoadSlot(string slotId)
        {
            if (!SaveService.TryLoad(slotId, out var data))
            {
                return false;
            }

            RememberSlot(data.slotInfo);

            if (ShouldLoadScene(data))
            {
                BeginSceneLoad(data);
                return true;
            }

            Apply(data);
            return true;
        }

        public bool TryLoadLatest()
        {
            var lastSlotId = PlayerPrefs.GetString(LastSlotKey, string.Empty);
            if (!string.IsNullOrEmpty(lastSlotId) && LoadSlot(lastSlotId))
            {
                return true;
            }

            var slots = SaveService.ListSlots();
            if (slots.Count == 0)
            {
                return false;
            }

            return LoadSlot(slots[0].slotId);
        }

        public void DeleteSlot(string slotId)
        {
            SaveService.Delete(slotId);
        }

        private SaveData Capture()
        {
            var data = new SaveData();
            data.slotInfo.displayName = GetDefaultSaveName();
            data.slotInfo.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
            data.slotInfo.sceneName = SceneManager.GetActiveScene().name;

            var player = FindPlayer();
            if (player != null)
            {
                // 先缓存位姿，再处理属性/背包/装备，避免引用被覆盖时丢失
                if (savePosition)
                {
                    data.player.position = player.transform.position;
                }

                if (saveRotation)
                {
                    data.player.rotation = player.transform.rotation;
                }

                if (saveHealth)
                {
                    var health = player.GetComponent<HealthComponent>();
                    if (health != null)
                    {
                        data.player.health = health.Current;
                    }
                }

                if (saveResource)
                {
                    var resource = player.GetComponent<ResourceComponent>();
                    if (resource != null)
                    {
                        data.player.hasResource = true;
                        data.player.resourceType = (int)resource.ResourceType;
                        data.player.resource = resource.Current;
                    }
                }

                if (saveCurrency)
                {
                    var currency = player.GetComponent<CurrencyComponent>();
                    if (currency != null)
                    {
                        data.player.hasCurrency = true;
                        data.player.currency = currency.Amount;
                    }
                }

                if (saveProgression)
                {
                    var progression = player.GetComponent<PlayerProgression>();
                    if (progression != null)
                    {
                        data.player.hasProgression = true;
                        data.player.level = progression.Level;
                        data.player.experience = progression.CurrentExperience;
                        data.player.attributePoints = progression.UnspentAttributePoints;
                    }
                }

                if (saveInventory)
                {
                    var inventory = player.GetComponent<InventoryComponent>();
                    if (inventory != null)
                    {
                        // 固定槽位保存，保证存读后位置一致
                        data.inventory = CaptureInventory(inventory);
                    }
                }

                if (saveEquipment)
                {
                    var equipment = player.GetComponent<EquipmentComponent>();
                    if (equipment != null)
                    {
                        // 记录每个装备槽位的索引与物品
                        data.equipment = CaptureEquipment(equipment);
                    }
                }
            }

            var levelFlow = FindFirstObjectByType<LevelFlowController>();
            if (levelFlow != null)
            {
                data.player.levelId = levelFlow.CurrentLevelId;
                data.player.spawnPointId = levelFlow.CurrentSpawnPointId;
            }

            if (saveQuests)
            {
                var questTracker = FindFirstObjectByType<QuestTracker>();
                if (questTracker != null)
                {
                    data.quests = questTracker.CaptureSaveData();
                }
            }

            return data;
        }

        private void Apply(SaveData data)
        {
            if (data == null)
            {
                return;
            }

            var player = FindPlayer();
            if (player == null)
            {
                Debug.LogWarning("[SaveGameManager] Player not found. Load aborted.", this);
                return;
            }

            var spawnApplied = false;
            if (savePosition)
            {
                // 优先用出生点覆盖位置，避免关卡变化导致掉落到无效位置
                if (!string.IsNullOrWhiteSpace(data.player.spawnPointId))
                {
                    var levelFlow = FindFirstObjectByType<LevelFlowController>();
                    if (levelFlow != null)
                    {
                        spawnApplied = levelFlow.TryApplySpawn(data.player.spawnPointId, player);
                    }
                }

                if (!spawnApplied)
                {
                    player.transform.position = data.player.position;
                }
            }

            if (saveRotation && !spawnApplied)
            {
                player.transform.rotation = data.player.rotation;
            }

            if (saveHealth)
            {
                var health = player.GetComponent<HealthComponent>();
                if (health != null)
                {
                    health.SetCurrent(data.player.health);
                }
            }

            if (saveResource && data.player.hasResource)
            {
                var resource = player.GetComponent<ResourceComponent>();
                if (resource != null && (int)resource.ResourceType == data.player.resourceType)
                {
                    resource.SetCurrent(data.player.resource);
                }
            }

            if (saveCurrency && data.player.hasCurrency)
            {
                var currency = player.GetComponent<CurrencyComponent>();
                if (currency != null)
                {
                    currency.SetAmount(data.player.currency);
                }
            }

            if (saveProgression && data.player.hasProgression)
            {
                var progression = player.GetComponent<PlayerProgression>();
                if (progression != null)
                {
                    // 先恢复成长系统，后续装备能拿到正确的基础数值
                    progression.ApplyState(data.player.level, data.player.experience, data.player.attributePoints, true);
                }
            }

            var resolvedDatabase = ResolveDatabase();
            if (saveInventory && data.inventory != null && resolvedDatabase != null)
            {
                var inventory = player.GetComponent<InventoryComponent>();
                if (inventory != null)
                {
                    // 背包先恢复，再处理装备，确保可在背包中找到替换物
                    ApplyInventory(data.inventory, inventory, resolvedDatabase);
                }
            }

            if (saveEquipment && data.equipment != null && resolvedDatabase != null)
            {
                var equipment = player.GetComponent<EquipmentComponent>();
                if (equipment != null)
                {
                    // 装备恢复会触发属性刷新，放在背包之后
                    ApplyEquipment(data.equipment, equipment, resolvedDatabase);
                }
            }

            if (saveQuests && data.quests != null)
            {
                var questTracker = FindFirstObjectByType<QuestTracker>();
                if (questTracker != null)
                {
                    questTracker.ApplySaveData(data.quests);
                }
            }
        }

        private bool ShouldLoadScene(SaveData data)
        {
            if (data == null || data.slotInfo == null)
            {
                return false;
            }

            var sceneName = data.slotInfo.sceneName;
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            return SceneManager.GetActiveScene().name != sceneName;
        }

        private void BeginSceneLoad(SaveData data)
        {
            pendingLoad = data;
            if (data != null && data.player != null)
            {
                var levelFlow = FindFirstObjectByType<LevelFlowController>();
                if (levelFlow != null)
                {
                    levelFlow.PrepareSpawn(data.player.levelId, data.player.spawnPointId);
                }
            }
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.LoadScene(data.slotInfo.sceneName);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (pendingLoad == null)
            {
                return;
            }

            StartCoroutine(ApplyAfterSceneLoad(pendingLoad));
            pendingLoad = null;
        }

        private IEnumerator ApplyAfterSceneLoad(SaveData data)
        {
            yield return null;
            Apply(data);
        }

        private GameDatabase ResolveDatabase()
        {
            if (database != null)
            {
                return database;
            }

            var assets = Resources.FindObjectsOfTypeAll<GameDatabase>();
            if (assets != null && assets.Length > 0)
            {
                database = assets[0];
            }

            return database;
        }

        private static InventorySaveData CaptureInventory(InventoryComponent inventory)
        {
            var data = new InventorySaveData
            {
                capacity = inventory.Capacity
            };

            var items = inventory.Items;
            if (items != null && items.Count > 0)
            {
                data.items = new ItemInstanceSaveData[items.Count];
                for (int i = 0; i < items.Count; i++)
                {
                    data.items[i] = BuildItemSaveData(items[i]);
                }
            }

            return data;
        }

        private static EquipmentSaveData CaptureEquipment(EquipmentComponent equipment)
        {
            var data = new EquipmentSaveData();
            var slots = equipment.Slots;
            if (slots != null && slots.Count > 0)
            {
                var entries = new List<EquippedItemSaveData>(slots.Count);
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot == null || slot.Item == null || slot.Item.Definition == null)
                    {
                        continue;
                    }

                    entries.Add(new EquippedItemSaveData
                    {
                        slotIndex = i,
                        item = BuildItemSaveData(slot.Item)
                    });
                }

                data.slots = entries.ToArray();
            }

            return data;
        }

        private static ItemInstanceSaveData BuildItemSaveData(ItemInstance item)
        {
            var data = new ItemInstanceSaveData();
            if (item == null || item.Definition == null)
            {
                return data;
            }

            data.itemId = item.Definition.Id;
            data.stack = item.Stack;
            data.rarity = (int)item.Rarity;

            var affixes = item.Affixes;
            if (affixes != null && affixes.Count > 0)
            {
                var ids = new List<string>(affixes.Count);
                for (int i = 0; i < affixes.Count; i++)
                {
                    var affix = affixes[i];
                    if (affix != null && !string.IsNullOrWhiteSpace(affix.Id))
                    {
                        ids.Add(affix.Id);
                    }
                }

                data.affixIds = ids.Count > 0 ? ids.ToArray() : null;
            }

            return data;
        }

        private static ItemInstance BuildItemInstance(ItemInstanceSaveData data, GameDatabase database)
        {
            if (data == null || database == null || string.IsNullOrWhiteSpace(data.itemId))
            {
                return null;
            }

            var definition = database.GetItem(data.itemId);
            if (definition == null)
            {
                return null;
            }

            var affixes = new List<AffixDefinition>();
            if (data.affixIds != null)
            {
                for (int i = 0; i < data.affixIds.Length; i++)
                {
                    var id = data.affixIds[i];
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    var affix = database.GetAffix(id);
                    if (affix != null)
                    {
                        affixes.Add(affix);
                    }
                }
            }

            var rarity = Enum.IsDefined(typeof(ItemRarity), data.rarity)
                ? (ItemRarity)data.rarity
                : ItemRarity.Common;

            var stack = Mathf.Max(1, data.stack);
            return new ItemInstance(definition, stack, rarity, affixes);
        }

        private static void ApplyInventory(InventorySaveData data, InventoryComponent inventory, GameDatabase database)
        {
            if (data == null || inventory == null || database == null)
            {
                return;
            }

            // 按容量恢复固定长度列表，缺失位置用 null 占位
            var capacity = data.capacity > 0 ? data.capacity : inventory.Capacity;
            var items = new List<ItemInstance>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                ItemInstance instance = null;
                if (data.items != null && i < data.items.Length)
                {
                    instance = BuildItemInstance(data.items[i], database);
                }

                items.Add(instance);
            }

            inventory.LoadItems(items);
        }

        private static void ApplyEquipment(EquipmentSaveData data, EquipmentComponent equipment, GameDatabase database)
        {
            if (data == null || equipment == null || database == null)
            {
                return;
            }

            equipment.ClearAll();
            if (data.slots == null)
            {
                return;
            }

            for (int i = 0; i < data.slots.Length; i++)
            {
                var slotData = data.slots[i];
                if (slotData == null)
                {
                    continue;
                }

                var instance = BuildItemInstance(slotData.item, database);
                if (instance != null)
                {
                    equipment.TryEquipToSlot(instance, slotData.slotIndex, null);
                }
            }
        }

        private GameObject FindPlayer()
        {
            if (!string.IsNullOrEmpty(playerTag))
            {
                var playerObj = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObj != null)
                {
                    return playerObj;
                }
            }

            var unit = FindFirstObjectByType<UnitRoot>();
            return unit != null ? unit.gameObject : null;
        }

        private void RememberSlot(SaveSlotInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.slotId))
            {
                return;
            }

            currentSlotId = info.slotId;
            currentSlotName = info.displayName;
            PlayerPrefs.SetString(LastSlotKey, currentSlotId);
            PlayerPrefs.Save();
        }

        private string ResolveDisplayName(string displayName, string fallback)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return string.IsNullOrWhiteSpace(fallback) ? GetDefaultSaveName() : fallback;
            }

            return displayName.Trim();
        }

        private static string GetDefaultSaveName()
        {
            return $"Save {DateTime.Now:yyyy-MM-dd HH:mm}";
        }
    }
}
