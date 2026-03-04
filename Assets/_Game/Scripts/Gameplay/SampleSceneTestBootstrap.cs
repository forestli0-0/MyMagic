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
        private const int DummyCount = 6;
        private static readonly Vector3 DummyStartPosition = new Vector3(0f, 0f, 11f);
        private const float DummySpacing = 2.2f;

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
            var skillUser = samplePlayer.GetComponent<SkillUserComponent>();
            skillUser?.Initialize(playerDef);
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

            var skillUser = instance.GetComponent<SkillUserComponent>();
            if (skillUser != null && definition != null)
            {
                skillUser.Initialize(definition);
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
    }
}
