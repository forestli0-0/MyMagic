using System.Collections;
using CombatSystem.AI;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// SampleScene 运行时测试引导：
    /// - 关闭 LevelFlow，避免按钮跳转 Town
    /// - 强制进入 Gameplay UI
    /// - 确保玩家使用亚索单位定义
    /// - 自动补齐训练木桩
    /// </summary>
    public sealed class SampleSceneTestBootstrap : MonoBehaviour
    {
        private const string SampleSceneName = "SampleScene";
        private const string PlayerYasuoUnitId = "Unit_PlayerYasuo";
        private const string DummyUnitId = "Unit_Enemy_high_hp";
        private const string DummyFallbackUnitId = "Unit_Enemy";
        private const string DummyRootName = "Sample_TrainingDummies";
        private const string AutoShooterRootName = "Sample_AutoShooters";
        private const string AutoShooterFrontName = "Sample_AutoShooter_Front";
        private const string AutoShooterFlankName = "Sample_AutoShooter_Flank";
        private const string AutoShooterSkillId = "Skill_ArcaneBolt";
        private const string AutoShooterSkillFallbackId = "Skill_LockOnBolt";
        private const int DummyCount = 6;
        private static readonly Vector3 DummyStartPosition = new Vector3(0f, 0f, 11f);
        private const float DummySpacing = 2.2f;
        private static readonly Vector3 FrontShooterOffset = new Vector3(0f, 0f, 8f);
        private static readonly Vector3 FlankShooterOffset = new Vector3(-6f, 0f, 3f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (!IsSampleScene(scene))
            {
                return;
            }

            var go = new GameObject("SampleScene_TestBootstrap_Runtime");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<SampleSceneTestBootstrap>();
        }

        private static bool IsSampleScene(Scene scene)
        {
            return scene.IsValid()
                   && !string.IsNullOrWhiteSpace(scene.name)
                   && string.Equals(scene.name, SampleSceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerator Start()
        {
            // 等一帧，确保 UI/单位组件初始化完毕后再校正状态。
            yield return null;

            var scene = SceneManager.GetActiveScene();
            if (!IsSampleScene(scene))
            {
                Destroy(gameObject);
                yield break;
            }

            DisableLevelFlow(scene);
            ForceGameplayUi();
            EnsureSamplePlayerUsesYasuo();
            EnsureTrainingDummies(scene);
            EnsureAutoShooters(scene);

            Destroy(gameObject);
        }

        private static void DisableLevelFlow(Scene scene)
        {
            var flows = FindObjectsByType<LevelFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < flows.Length; i++)
            {
                var flow = flows[i];
                if (flow == null || flow.gameObject.scene != scene)
                {
                    continue;
                }

                Destroy(flow.gameObject);
            }
        }

        private static void ForceGameplayUi()
        {
            var uiManager = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (uiManager != null)
            {
                uiManager.ForceReturnToGameplay();
                uiManager.SetHudVisible(true);
            }

            var mainMenu = FindFirstObjectByType<MainMenuScreen>(FindObjectsInactive.Include);
            if (mainMenu != null && mainMenu.gameObject.activeSelf)
            {
                mainMenu.gameObject.SetActive(false);
            }
        }

        private static void EnsureSamplePlayerUsesYasuo()
        {
            var samplePlayer = GameObject.Find("Sample_Player");
            if (samplePlayer == null)
            {
                return;
            }

            var unitRoot = samplePlayer.GetComponent<UnitRoot>();
            if (unitRoot == null)
            {
                return;
            }

            if (unitRoot.Definition != null
                && string.Equals(unitRoot.Definition.Id, PlayerYasuoUnitId, System.StringComparison.Ordinal))
            {
                return;
            }

            var playerDef = ResolveUnitDefinition(PlayerYasuoUnitId);
            if (playerDef == null)
            {
                return;
            }

            unitRoot.Initialize(playerDef);
        }

        private static void EnsureTrainingDummies(Scene scene)
        {
            var root = GameObject.Find(DummyRootName);
            if (root != null && root.transform.childCount >= DummyCount)
            {
                return;
            }

            if (root == null)
            {
                root = new GameObject(DummyRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            var dummyDefinition = ResolveUnitDefinition(DummyUnitId) ?? ResolveUnitDefinition(DummyFallbackUnitId);
            var dummyPrefab = dummyDefinition != null ? dummyDefinition.Prefab : null;
            if (dummyPrefab == null)
            {
                Debug.LogWarning("[SampleSceneTestBootstrap] Dummy prefab missing, skip dummy spawn.");
                return;
            }

            var existing = root.transform.childCount;
            for (int i = existing; i < DummyCount; i++)
            {
                var position = DummyStartPosition + new Vector3((i - (DummyCount - 1) * 0.5f) * DummySpacing, 0f, 0f);
                var instance = Instantiate(dummyPrefab, position, Quaternion.identity, root.transform);
                instance.name = $"Sample_Dummy_{i + 1}";
                ConfigureTrainingDummy(instance, dummyDefinition);
            }
        }

        private static UnitDefinition ResolveUnitDefinition(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            var databases = Resources.FindObjectsOfTypeAll<GameDatabase>();
            for (int i = 0; i < databases.Length; i++)
            {
                var db = databases[i];
                if (db == null)
                {
                    continue;
                }

                var unit = db.GetUnit(unitId);
                if (unit != null)
                {
                    return unit;
                }
            }

            return null;
        }

        private static SkillDefinition ResolveSkillDefinition(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return null;
            }

            var databases = Resources.FindObjectsOfTypeAll<GameDatabase>();
            for (int i = 0; i < databases.Length; i++)
            {
                var db = databases[i];
                if (db == null)
                {
                    continue;
                }

                var skill = db.GetSkill(skillId);
                if (skill != null)
                {
                    return skill;
                }
            }

            return null;
        }

        private static void ConfigureTrainingDummy(GameObject instance, UnitDefinition definition)
        {
            if (instance == null)
            {
                return;
            }

            var unitRoot = instance.GetComponent<UnitRoot>();
            if (unitRoot != null && definition != null)
            {
                unitRoot.Initialize(definition);
            }

            var team = instance.GetComponent<TeamComponent>();
            team?.SetTeamId(2);

            var ai = instance.GetComponent<CombatAIController>();
            if (ai != null)
            {
                ai.enabled = false;
            }

            var bossScheduler = instance.GetComponent<BossSkillScheduler>();
            if (bossScheduler != null)
            {
                bossScheduler.enabled = false;
            }

            var movement = instance.GetComponent<MovementComponent>();
            if (movement != null)
            {
                movement.enabled = false;
            }

            var characterController = instance.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.detectCollisions = false;
                characterController.enableOverlapRecovery = false;
                characterController.enabled = false;
            }

            var capsule = instance.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.isTrigger = true;
            }
        }

        private static void EnsureAutoShooters(Scene scene)
        {
            var player = PlayerUnitLocator.FindPlayerUnit();
            if (player == null)
            {
                return;
            }

            var shooterDefinition = ResolveUnitDefinition(DummyUnitId) ?? ResolveUnitDefinition(DummyFallbackUnitId);
            var shooterPrefab = shooterDefinition != null ? shooterDefinition.Prefab : null;
            if (shooterPrefab == null)
            {
                Debug.LogWarning("[SampleSceneTestBootstrap] Auto shooter prefab missing, skip auto shooter spawn.");
                return;
            }

            var projectileSkill = ResolveSkillDefinition(AutoShooterSkillId) ?? ResolveSkillDefinition(AutoShooterSkillFallbackId);
            if (projectileSkill == null)
            {
                Debug.LogWarning("[SampleSceneTestBootstrap] Auto shooter skill missing, skip auto shooter spawn.");
                return;
            }

            var root = GameObject.Find(AutoShooterRootName);
            if (root == null)
            {
                root = new GameObject(AutoShooterRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            var playerPosition = player.transform.position;
            EnsureAutoShooter(root.transform, shooterPrefab, shooterDefinition, AutoShooterFrontName, playerPosition + FrontShooterOffset, projectileSkill, 1.1f, 12f);
            EnsureAutoShooter(root.transform, shooterPrefab, shooterDefinition, AutoShooterFlankName, playerPosition + FlankShooterOffset, projectileSkill, 1.6f, 12f);
        }

        private static void EnsureAutoShooter(
            Transform root,
            GameObject prefab,
            UnitDefinition definition,
            string shooterName,
            Vector3 position,
            SkillDefinition projectileSkill,
            float intervalSeconds,
            float castDistance)
        {
            if (root == null || prefab == null)
            {
                return;
            }

            var existing = root.Find(shooterName);
            GameObject instance;
            if (existing != null)
            {
                instance = existing.gameObject;
                instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            }
            else
            {
                instance = Instantiate(prefab, position, Quaternion.identity, root);
                instance.name = shooterName;
            }

            ConfigureTrainingDummy(instance, definition);

            var autoShooter = instance.GetComponent<SampleSceneAutoShooter>();
            if (autoShooter == null)
            {
                autoShooter = instance.AddComponent<SampleSceneAutoShooter>();
            }

            autoShooter.Configure(projectileSkill, intervalSeconds, castDistance);

            var player = PlayerUnitLocator.FindPlayerUnit();
            if (player != null)
            {
                var toPlayer = player.transform.position - instance.transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    instance.transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                }
            }
        }
    }

    /// <summary>
    /// 仅供 SampleScene 使用的自动发射器。
    /// 复用现有 SkillUserComponent 施法链路，周期性向玩家释放配置好的技能。
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class SampleSceneAutoShooter : MonoBehaviour
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private float castInterval = 1.25f;
        [SerializeField] private float targetRefreshInterval = 0.5f;
        [SerializeField] private float maxCastDistance = 14f;
        [SerializeField] private bool faceTargetBeforeCast = true;
        [SerializeField] private bool refillResourceBeforeCast = true;

        private UnitRoot unitRoot;
        private SkillUserComponent skillUser;
        private ResourceComponent resource;
        private HealthComponent health;
        private UnitRoot targetUnit;
        private float nextCastTime;
        private float nextTargetRefreshTime;

        public void Configure(SkillDefinition skillDefinition, float intervalSeconds, float maxDistance)
        {
            skill = skillDefinition;
            castInterval = Mathf.Max(0.25f, intervalSeconds);
            maxCastDistance = Mathf.Max(0f, maxDistance);
            CacheComponentsIfMissing();
            EnsureSkillRegistered();
        }

        private void Awake()
        {
            CacheComponentsIfMissing();
        }

        private void OnEnable()
        {
            CacheComponentsIfMissing();
            EnsureSkillRegistered();
            nextTargetRefreshTime = 0f;
            nextCastTime = Time.time + Random.Range(0f, Mathf.Max(0.1f, castInterval * 0.35f));
        }

        private void Update()
        {
            if (!TryEnsureReady())
            {
                return;
            }

            RefreshTargetIfNeeded();
            if (!PlayerUnitLocator.IsPlayerUnit(targetUnit))
            {
                return;
            }

            if (!IsTargetInRange(targetUnit))
            {
                return;
            }

            if (Time.time < nextCastTime)
            {
                return;
            }

            if (faceTargetBeforeCast)
            {
                FaceTarget(targetUnit.transform.position);
            }

            RefillResourceIfNeeded();

            var casted = skillUser.TryCast(skill, targetUnit.gameObject);
            nextCastTime = Time.time + (casted ? castInterval : Mathf.Min(0.5f, castInterval));
        }

        private bool TryEnsureReady()
        {
            CacheComponentsIfMissing();

            if (skill == null || unitRoot == null || skillUser == null)
            {
                return false;
            }

            if (health != null && !health.IsAlive)
            {
                return false;
            }

            EnsureSkillRegistered();
            return true;
        }

        private void CacheComponentsIfMissing()
        {
            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            if (resource == null)
            {
                resource = GetComponent<ResourceComponent>();
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }
        }

        private void EnsureSkillRegistered()
        {
            if (skillUser == null || skill == null || skillUser.HasSkill(skill))
            {
                return;
            }

            skillUser.TryAddSkill(skill);
        }

        private void RefreshTargetIfNeeded()
        {
            if (Time.time < nextTargetRefreshTime && PlayerUnitLocator.IsPlayerUnit(targetUnit))
            {
                return;
            }

            nextTargetRefreshTime = Time.time + Mathf.Max(0.1f, targetRefreshInterval);
            targetUnit = PlayerUnitLocator.FindPlayerUnit();
        }

        private bool IsTargetInRange(UnitRoot target)
        {
            if (target == null || maxCastDistance <= 0f)
            {
                return target != null;
            }

            var offset = target.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= maxCastDistance * maxCastDistance;
        }

        private void FaceTarget(Vector3 worldTarget)
        {
            var flatDirection = worldTarget - transform.position;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        }

        private void RefillResourceIfNeeded()
        {
            if (!refillResourceBeforeCast || resource == null || skill == null)
            {
                return;
            }

            if (skill.ResourceDefinition != null)
            {
                resource.EnsureResource(skill.ResourceDefinition);
                resource.SetCurrent(skill.ResourceDefinition, resource.GetMax(skill.ResourceDefinition));
                return;
            }

            resource.SetCurrent(resource.Max);
        }
    }
}
