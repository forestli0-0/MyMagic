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
        [Tooltip("是否跨场景持久化此控制器")]
        [SerializeField] private bool persistAcrossScenes = true;

        private string pendingLevelId;
        private string pendingSpawnId;

        private LevelSpawnPoint[] cachedSpawnPoints;
        private InGameScreen cachedInGameScreen;
        private string cachedSceneName;

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

            var player = FindPlayer();
            if (player == null)
            {
                pendingLevelId = null;
                pendingSpawnId = null;
                return;
            }

            if (level == null)
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
                var playerObj = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObj != null)
                {
                    return playerObj;
                }
            }

            var unit = FindFirstObjectByType<CombatSystem.Core.UnitRoot>();
            return unit != null ? unit.gameObject : null;
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

            var inGame = GetCachedInGameScreen();
            if (inGame == null)
            {
                return;
            }

            root.Manager.ShowScreen(inGame, true);
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
