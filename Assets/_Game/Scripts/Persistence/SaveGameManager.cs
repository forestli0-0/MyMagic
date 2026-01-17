using System;
using System.Collections;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.Persistence
{
    public class SaveGameManager : MonoBehaviour
    {
        private const string LastSlotKey = "CombatSystem.LastSaveSlotId";

        [Header("Settings")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool savePosition = true;
        [SerializeField] private bool saveRotation = true;
        [SerializeField] private bool saveHealth = true;
        [SerializeField] private bool saveResource = true;
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
            }

            var levelFlow = FindFirstObjectByType<LevelFlowController>();
            if (levelFlow != null)
            {
                data.player.levelId = levelFlow.CurrentLevelId;
                data.player.spawnPointId = levelFlow.CurrentSpawnPointId;
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
