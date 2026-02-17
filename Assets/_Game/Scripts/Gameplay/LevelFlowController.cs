using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 关卡流程控制器，负责场景加载与玩家出生点放置。
    /// </summary>
    public class LevelFlowController : MonoBehaviour
    {
        public static LevelFlowController Instance { get; private set; }

        [Header("数据库")]
        [SerializeField] private GameDatabase database;

        [Header("初始配置")]
        [Tooltip("游戏开始时加载的关卡 ID")]
        [SerializeField] private string startLevelId = "Level_Town";
        [Tooltip("游戏开始时的出生点 ID")]
        [SerializeField] private string startSpawnId = "Start";

        [Header("玩家")]
        [Tooltip("玩家对象的标签")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("玩家单位定义（优先使用）")]
        [SerializeField] private UnitDefinition playerDefinition;
        [Tooltip("玩家单位定义 ID（当未指定定义时使用）")]
        [SerializeField] private string playerUnitId = "Unit_Player";
        [Tooltip("是否跨场景持久化此控制器")]
        [SerializeField] private bool persistAcrossScenes = true;

        private string pendingLevelId;
        private string pendingSpawnId;

        /// <summary>场景内出生点缓存，避免重复查找</summary>
        private LevelSpawnPoint[] cachedSpawnPoints;
        /// <summary>InGameScreen 缓存</summary>
        private InGameScreen cachedInGameScreen;
        /// <summary>当前场景名称缓存</summary>
        private string cachedSceneName;
        /// <summary>场景切换时临时保存的玩家状态</summary>
        private PlayerStateCache cachedPlayerState;

        public string CurrentLevelId { get; private set; }
        public string CurrentSpawnPointId { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public void StartNewGame()
        {
            LoadLevel(startLevelId, startSpawnId);
        }

        public void LoadLevel(string levelId, string spawnId)
        {
            var level = ResolveLevel(levelId);
            if (level == null)
            {
                Debug.LogWarning($"[LevelFlow] 未找到关卡 '{levelId}'。");
                return;
            }

            CachePlayerState();

            pendingLevelId = levelId;
            pendingSpawnId = string.IsNullOrWhiteSpace(spawnId) ? level.DefaultSpawnId : spawnId;

            if (string.IsNullOrWhiteSpace(level.SceneName))
            {
                Debug.LogWarning($"[LevelFlow] 关卡 '{levelId}' 缺少场景名称。");
                return;
            }

            SceneManager.LoadScene(level.SceneName);
        }

        public void PrepareSpawn(string levelId, string spawnId)
        {
            pendingLevelId = levelId;
            pendingSpawnId = spawnId;
        }

        public bool TryApplySpawn(string spawnId, GameObject player)
        {
            if (player == null)
            {
                return false;
            }

            var spawn = FindSpawnPoint(spawnId);
            if (spawn == null)
            {
                return false;
            }

            spawn.ApplyTo(player.transform);
            CurrentSpawnPointId = spawn.SpawnId;
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 场景切换时清除缓存
            InvalidateSceneCache(scene.name);

            var level = ResolveLevelByScene(scene.name);
            CurrentLevelId = level != null ? level.Id : pendingLevelId;

            if (level == null)
            {
                pendingLevelId = null;
                pendingSpawnId = null;
                return;
            }

            var player = FindPlayer();
            if (player == null)
            {
                player = SpawnPlayer(level);
            }
            if (player == null)
            {
                pendingLevelId = null;
                pendingSpawnId = null;
                return;
            }

            var spawnId = pendingSpawnId;
            if (string.IsNullOrWhiteSpace(spawnId))
            {
                spawnId = level.DefaultSpawnId;
            }

            if (!TryApplySpawn(spawnId, player))
            {
                var fallback = level.DefaultSpawnId;
                TryApplySpawn(fallback, player);
            }

            ApplyCachedPlayerState(player);
            EnsureGameplayScreen();

            pendingLevelId = null;
            pendingSpawnId = null;
        }

        /// <summary>
        /// 场景切换时清除缓存。
        /// </summary>
        private void InvalidateSceneCache(string newSceneName)
        {
            if (cachedSceneName != newSceneName)
            {
                cachedSpawnPoints = null;
                cachedInGameScreen = null;
                cachedSceneName = newSceneName;
            }
        }

        private GameObject FindPlayer()
        {
            if (!string.IsNullOrEmpty(playerTag))
            {
                try
                {
                    var playerObj = GameObject.FindGameObjectWithTag(playerTag);
                    if (playerObj != null)
                    {
                        return playerObj;
                    }
                }
                catch (UnityException)
                {
                }
            }

            var movementDriver = FindFirstObjectByType<PlayerMovementDriver>();
            if (movementDriver != null)
            {
                return movementDriver.gameObject;
            }

            var units = FindObjectsByType<CombatSystem.Core.UnitRoot>(FindObjectsSortMode.None);
            if (units != null && units.Length > 0)
            {
                if (playerDefinition != null)
                {
                    for (int i = 0; i < units.Length; i++)
                    {
                        if (units[i] != null && units[i].Definition == playerDefinition)
                        {
                            return units[i].gameObject;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(playerUnitId))
                {
                    for (int i = 0; i < units.Length; i++)
                    {
                        var unit = units[i];
                        if (unit != null && unit.Definition != null && unit.Definition.Id == playerUnitId)
                        {
                            return unit.gameObject;
                        }
                    }
                }
            }

            if (units != null && units.Length == 1)
            {
                return units[0].gameObject;
            }

            return null;
        }

        private GameObject SpawnPlayer(LevelDefinition level)
        {
            var definition = ResolvePlayerDefinition();
            if (definition == null)
            {
                Debug.LogWarning("[LevelFlow] 玩家单位定义未配置，无法生成玩家。");
                return null;
            }

            if (definition.Prefab == null)
            {
                Debug.LogWarning("[LevelFlow] 玩家单位定义缺少 Prefab，无法生成玩家。");
                return null;
            }

            var instance = Instantiate(definition.Prefab);
            instance.name = string.IsNullOrWhiteSpace(definition.DisplayName) ? "Player" : definition.DisplayName;
            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                instance.tag = playerTag;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (instance.scene != activeScene)
            {
                var fromScene = instance.scene.name;
                SceneManager.MoveGameObjectToScene(instance, activeScene);
            }

            var unitRoot = instance.GetComponent<CombatSystem.Core.UnitRoot>();
            if (unitRoot != null && unitRoot.Definition != definition)
            {
                unitRoot.Initialize(definition);
            }

            var spawnId = pendingSpawnId;
            if (string.IsNullOrWhiteSpace(spawnId))
            {
                spawnId = level != null ? level.DefaultSpawnId : "Start";
            }

            if (!TryApplySpawn(spawnId, instance))
            {
                var fallback = level != null ? level.DefaultSpawnId : "Start";
                if (!TryApplySpawn(fallback, instance))
                {
                }
            }

            return instance;
        }

        private UnitDefinition ResolvePlayerDefinition()
        {
            if (playerDefinition != null)
            {
                return playerDefinition;
            }

            var resolvedDatabase = database != null ? database : FindFirstObjectByType<GameDatabase>();
            if (resolvedDatabase == null || string.IsNullOrWhiteSpace(playerUnitId))
            {
                return null;
            }

            var resolved = resolvedDatabase.GetUnit(playerUnitId);
            return resolved;
        }

        private LevelDefinition ResolveLevel(string levelId)
        {
            if (database == null)
            {
                database = FindFirstObjectByType<GameDatabase>();
            }

            if (database == null || string.IsNullOrWhiteSpace(levelId))
            {
                return null;
            }

            return database.GetLevel(levelId);
        }

        private LevelDefinition ResolveLevelByScene(string sceneName)
        {
            if (database == null)
            {
                database = FindFirstObjectByType<GameDatabase>();
            }

            if (database == null || database.Levels == null)
            {
                return null;
            }

            for (int i = 0; i < database.Levels.Count; i++)
            {
                var level = database.Levels[i];
                if (level != null && level.SceneName == sceneName)
                {
                    return level;
                }
            }

            return null;
        }

        /// <summary>
        /// 查找指定 ID 的出生点，使用缓存避免重复查找。
        /// </summary>
        private LevelSpawnPoint FindSpawnPoint(string spawnId)
        {
            // 使用缓存
            if (cachedSpawnPoints == null)
            {
                cachedSpawnPoints = FindObjectsByType<LevelSpawnPoint>(FindObjectsSortMode.None);
            }

            if (cachedSpawnPoints == null || cachedSpawnPoints.Length == 0)
            {
                return null;
            }

            LevelSpawnPoint fallback = null;
            for (int i = 0; i < cachedSpawnPoints.Length; i++)
            {
                var point = cachedSpawnPoints[i];
                if (point == null)
                {
                    continue;
                }

                if (point.IsDefault)
                {
                    fallback = point;
                }

                if (string.IsNullOrWhiteSpace(spawnId))
                {
                    continue;
                }

                if (string.Equals(point.SpawnId, spawnId, System.StringComparison.Ordinal))
                {
                    return point;
                }
            }

            return fallback;
        }

        private void EnsureGameplayScreen()
        {
            var root = UIRoot.Instance != null ? UIRoot.Instance : FindFirstObjectByType<UIRoot>();
            if (root == null || root.Manager == null)
            {
                return;
            }

            root.Manager.CloseAllModals();

            var inGame = GetCachedInGameScreen();
            if (inGame != null)
            {
                root.Manager.ShowScreen(inGame, true);
            }
            else
            {
                root.Manager.SetHudVisible(true);
            }

            if (Time.timeScale != 1f)
            {
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// 缓存当前玩家状态，用于场景切换时保留数据。
        /// </summary>
        /// <remarks>
        /// 缓存内容包括：
        /// - 生命值 / 资源值
        /// - 等级 / 经验 / 属性点
        /// - 背包物品 / 已装备物品
        /// </remarks>
        private void CachePlayerState()
        {
            cachedPlayerState = default;
            var player = FindPlayer();
            if (player == null)
            {
                return;
            }

            cachedPlayerState.hasState = true;

            // 缓存生命值
            var health = player.GetComponent<CombatSystem.Core.HealthComponent>();
            if (health != null)
            {
                cachedPlayerState.hasHealth = true;
                cachedPlayerState.health = health.Current;
            }

            // 缓存资源值
            var resource = player.GetComponent<CombatSystem.Core.ResourceComponent>();
            if (resource != null)
            {
                cachedPlayerState.hasResource = true;
                cachedPlayerState.resourceType = resource.ResourceType;
                cachedPlayerState.resource = resource.Current;
            }

            // 缓存成长系统状态
            var progression = player.GetComponent<CombatSystem.Core.PlayerProgression>();
            if (progression != null)
            {
                cachedPlayerState.hasProgression = true;
                cachedPlayerState.level = progression.Level;
                cachedPlayerState.experience = progression.CurrentExperience;
                cachedPlayerState.attributePoints = progression.UnspentAttributePoints;
            }

            var characterScreen = FindFirstObjectByType<CharacterScreen>(FindObjectsInactive.Include);
            if (characterScreen != null)
            {
                characterScreen.GetAllocationPoints(
                    out cachedPlayerState.allocatedMaxHealthPoints,
                    out cachedPlayerState.allocatedAttackPowerPoints,
                    out cachedPlayerState.allocatedArmorPoints,
                    out cachedPlayerState.allocatedMoveSpeedPoints);
            }

            // 缓存背包物品（深拷贝）
            var inventory = player.GetComponent<InventoryComponent>();
            if (inventory != null)
            {
                cachedPlayerState.inventoryItems = CloneItems(inventory.Items);
            }

            // 缓存已装备物品（深拷贝）
            var equipment = player.GetComponent<EquipmentComponent>();
            if (equipment != null && equipment.Slots != null)
            {
                cachedPlayerState.equipmentItems = CloneEquippedItems(equipment.Slots);
            }
        }

        /// <summary>
        /// 将缓存的玩家状态应用到新场景的玩家对象上。
        /// </summary>
        /// <param name="player">目标玩家对象</param>
        /// <remarks>
        /// 应用顺序：成长系统 → 背包 → 装备 → 生命值 → 资源值
        /// 先恢复装备再恢复生命值，确保最大生命值正确计算
        /// </remarks>
        private void ApplyCachedPlayerState(GameObject player)
        {
            if (!cachedPlayerState.hasState || player == null)
            {
                return;
            }

            // 1. 恢复成长系统状态（等级/经验/属性点）
            var progression = player.GetComponent<CombatSystem.Core.PlayerProgression>();
            if (cachedPlayerState.hasProgression && progression != null)
            {
                progression.ApplyState(cachedPlayerState.level, cachedPlayerState.experience, cachedPlayerState.attributePoints, true);
            }

            var characterScreen = FindFirstObjectByType<CharacterScreen>(FindObjectsInactive.Include);
            if (characterScreen != null)
            {
                characterScreen.SetAllocationPoints(
                    cachedPlayerState.allocatedMaxHealthPoints,
                    cachedPlayerState.allocatedAttackPowerPoints,
                    cachedPlayerState.allocatedArmorPoints,
                    cachedPlayerState.allocatedMoveSpeedPoints);
            }

            // 2. 恢复背包物品
            var inventory = player.GetComponent<InventoryComponent>();
            if (inventory != null && cachedPlayerState.inventoryItems != null)
            {
                inventory.LoadItems(cachedPlayerState.inventoryItems);
            }

            // 3. 恢复装备（按槽位索引逐个穿戴）
            var equipment = player.GetComponent<EquipmentComponent>();
            if (equipment != null && cachedPlayerState.equipmentItems != null)
            {
                equipment.ClearAll();
                var max = Mathf.Min(cachedPlayerState.equipmentItems.Count, equipment.Slots.Count);
                for (int i = 0; i < max; i++)
                {
                    var item = cachedPlayerState.equipmentItems[i];
                    if (item == null)
                    {
                        continue;
                    }

                    equipment.TryEquipToSlot(item, i, null);
                }
            }

            // 4. 恢复生命值
            var health = player.GetComponent<CombatSystem.Core.HealthComponent>();
            if (cachedPlayerState.hasHealth && health != null)
            {
                health.SetCurrent(cachedPlayerState.health);
            }

            // 5. 恢复资源值（仅当资源类型匹配时）
            var resource = player.GetComponent<CombatSystem.Core.ResourceComponent>();
            if (cachedPlayerState.hasResource && resource != null && resource.ResourceType == cachedPlayerState.resourceType)
            {
                resource.SetCurrent(cachedPlayerState.resource);
            }

            // 清空缓存
            cachedPlayerState = default;
        }

        /// <summary>
        /// 深拷贝物品列表。
        /// </summary>
        /// <param name="items">源物品列表</param>
        /// <returns>新的物品列表副本</returns>
        private static List<ItemInstance> CloneItems(IReadOnlyList<ItemInstance> items)
        {
            if (items == null)
            {
                return null;
            }

            var list = new List<ItemInstance>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                list.Add(CloneItem(items[i]));
            }

            return list;
        }

        /// <summary>
        /// 深拷贝装备槽位中的物品，保留槽位索引对应关系。
        /// </summary>
        /// <param name="slots">源装备槽位列表</param>
        /// <returns>按槽位索引排列的物品列表副本</returns>
        private static List<ItemInstance> CloneEquippedItems(IReadOnlyList<EquipmentComponent.EquipmentSlotState> slots)
        {
            if (slots == null)
            {
                return null;
            }

            var list = new List<ItemInstance>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                list.Add(CloneItem(slots[i].Item));
            }

            return list;
        }

        /// <summary>
        /// 深拷贝单个物品实例。
        /// </summary>
        private static ItemInstance CloneItem(ItemInstance item)
        {
            return item != null ? item.CloneWithStack(item.Stack) : null;
        }

        /// <summary>
        /// 玩家状态缓存结构，用于场景切换时临时保存玩家数据。
        /// </summary>
        private struct PlayerStateCache
        {
            /// <summary>是否有有效的缓存数据</summary>
            public bool hasState;

            // -------- 生命值 --------
            public bool hasHealth;
            public float health;

            // -------- 资源值 --------
            public bool hasResource;
            public ResourceType resourceType;
            public float resource;

            // -------- 成长系统 --------
            public bool hasProgression;
            public int level;
            public int experience;
            public int attributePoints;
            public int allocatedMaxHealthPoints;
            public int allocatedAttackPowerPoints;
            public int allocatedArmorPoints;
            public int allocatedMoveSpeedPoints;

            // -------- 物品 --------
            /// <summary>背包物品深拷贝</summary>
            public List<ItemInstance> inventoryItems;
            /// <summary>装备物品深拷贝（按槽位索引）</summary>
            public List<ItemInstance> equipmentItems;
        }

        private InGameScreen GetCachedInGameScreen()
        {
            // 检查缓存是否有效
            if (cachedInGameScreen != null && cachedInGameScreen.gameObject.scene.isLoaded)
            {
                return cachedInGameScreen;
            }

            var screens = Resources.FindObjectsOfTypeAll<InGameScreen>();
            if (screens == null || screens.Length == 0)
            {
                cachedInGameScreen = null;
                return null;
            }

            // 优先返回已加载场景中的屏幕
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                if (screen != null && screen.gameObject.scene.isLoaded)
                {
                    cachedInGameScreen = screen;
                    return screen;
                }
            }

            cachedInGameScreen = screens[0];
            return cachedInGameScreen;
        }
    }
}
