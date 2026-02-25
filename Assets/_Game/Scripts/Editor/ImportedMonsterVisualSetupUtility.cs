using System;
using System.IO;
using System.Linq;
using CombatSystem.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace CombatSystem.Editor
{
    /// <summary>
    /// 导入第三方怪物资源后的接入工具：
    /// - 生成 Spider/Cyber 的战斗 AnimatorController
    /// - 生成可复用的 UnitVisualProfile
    /// - 可选：直接绑定到现有 Unit_Enemy / Unit_Enemy_high_hp
    /// </summary>
    public static class ImportedMonsterVisualSetupUtility
    {
        private const string ControllerFolder = "Assets/_Game/Art/Monsters/Controllers";
        private const string ProfileFolder = "Assets/_Game/ScriptableObjects/Units/VisualProfiles/Imported";

        private const string SpiderProfilePath = ProfileFolder + "/Unit_Enemy_Spider_Visual.asset";
        private const string CyberProfilePath = ProfileFolder + "/Unit_Enemy_Cyber_Visual.asset";
        private const string SpiderControllerPath = ControllerFolder + "/Unit_Monster_Spider.controller";
        private const string CyberControllerPath = ControllerFolder + "/Unit_Monster_Cyber.controller";
        private const string CyberStableClipFolder = "Assets/_Game/Art/Monsters/Animations/Cyber";
        private const string CyberStableIdleClipPath = CyberStableClipFolder + "/Cyber_Idle.anim";
        private const string CyberStableRunClipPath = CyberStableClipFolder + "/Cyber_Run.anim";
        private const string CyberStableAttackClipPath = CyberStableClipFolder + "/Cyber_Attack.anim";
        private const string CyberStableCastClipPath = CyberStableClipFolder + "/Cyber_Cast.anim";
        private const string CyberStableDieClipPath = CyberStableClipFolder + "/Cyber_Die.anim";

        private const string UnitEnemyPath = "Assets/_Game/ScriptableObjects/Units/Unit_Enemy.asset";
        private const string UnitEnemyHighHpPath = "Assets/_Game/ScriptableObjects/Units/Unit_Enemy_high_hp.asset";

        private const string SpiderPrefabPath = "Assets/Spiders/Prefabs/Black Widow.prefab";
        private const string SpiderAvatarSourcePath = "Assets/Spiders/Models/Spider.fbx";
        private const string SpiderIdlePath = "Assets/Spiders/Animations/Idle.anim";
        private const string SpiderMovePath = "Assets/Spiders/Animations/Walk.anim";
        private const string SpiderAttackPath = "Assets/Spiders/Animations/Attack1.anim";
        private const string SpiderCastPath = "Assets/Spiders/Animations/Attack_2.anim";
        private const string SpiderHitPath = "Assets/Spiders/Animations/TakeDamage.002.anim";
        private const string SpiderDiePath = "Assets/Spiders/Animations/Death.anim";

        private const string CyberPrefabPath = "Assets/Cyber Monsters 2/Prefab/Cyber Monsters 2.prefab";
        private const string CyberAvatarSourcePath = "Assets/Cyber Monsters 2/Base mesh/Cyber_Monsters_2.fbx";
        private const string CyberIdlePath = "Assets/Cyber Monsters 2/Animation/Anim_Cyber_Monsters_2@Idle.fbx";
        private const string CyberMovePath = "Assets/Cyber Monsters 2/Animation/Anim_Cyber_Monsters_2@Run.fbx";
        private const string CyberWalkPath = "Assets/Cyber Monsters 2/Animation/Anim_Cyber_Monsters_2@Walking.fbx";
        private const string CyberAttackPath = "Assets/Cyber Monsters 2/Animation/Anim_Cyber_Monsters_2@sword attack.fbx";
        private const string CyberCastPath = "Assets/Cyber Monsters 2/Animation/Anim_Cyber_Monsters_2@shoots gun_2.fbx";
        private const string CyberDiePath = "Assets/Cyber Monsters 2/Animation/Anim_Cyber_Monsters_2@Death.fbx";

        [MenuItem("Combat/Visual/Imported Monsters/Setup Spider + Cyber Profiles")]
        public static void SetupProfiles()
        {
            SetupInternal(applyProfilesToUnits: false, interactive: true);
        }

        [MenuItem("Combat/Visual/Imported Monsters/Setup And Apply To Existing Enemies")]
        public static void SetupAndApplyToUnits()
        {
            SetupInternal(applyProfilesToUnits: true, interactive: true);
        }

        [MenuItem("Combat/Visual/Imported Monsters/Fix Imported Materials For URP")]
        public static void FixImportedMaterialsForUrp()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog(
                    "Imported Monster Setup",
                    "未找到 URP/Lit Shader。请确认项目已启用 URP。",
                    "OK");
                return;
            }

            var materialGuids = AssetDatabase.FindAssets(
                "t:Material",
                new[]
                {
                    "Assets/Spiders/Materials",
                    "Assets/Cyber Monsters 2/Materials"
                });

            var changed = 0;
            for (int i = 0; i < materialGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                if (UpgradeMaterialToUrpLit(material, urpLit))
                {
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ImportedMonsterSetup] URP material upgrade completed. changed={changed}");
            EditorUtility.DisplayDialog(
                "Imported Monster Setup",
                $"材质修复完成。\n修改材质数量: {changed}",
                "OK");
        }

        [MenuItem("Combat/Visual/Imported Monsters/Fix Cyber Locomotion Loop")]
        public static void FixCyberLocomotionLoop()
        {
            FixCyberLocomotionLoopInternal(interactive: true);
        }

        [MenuItem("Combat/Visual/Imported Monsters/Dump Cyber Animation Diagnostics")]
        public static void DumpCyberAnimationDiagnostics()
        {
            DumpCyberAnimationDiagnosticsInternal(interactive: true);
        }

        public static void DumpCyberAnimationDiagnosticsBatch()
        {
            DumpCyberAnimationDiagnosticsInternal(interactive: false);
        }

        public static void FixCyberLocomotionLoopBatch()
        {
            FixCyberLocomotionLoopInternal(interactive: false);
        }

        private static void FixCyberLocomotionLoopInternal(bool interactive)
        {
            var changed = 0;
            changed += EnsureModelClipLooping(CyberIdlePath) ? 1 : 0;
            changed += EnsureModelClipLooping(CyberMovePath) ? 1 : 0;
            changed += EnsureModelClipLooping(CyberWalkPath) ? 1 : 0;

            // 重新导入 FBX 后，子动画的 local file id 可能变化，旧 Controller 会出现 Missing(Motion)。
            // 因此这里强制重建一次 Cyber Controller 并回写到 Cyber 视觉配置。
            if (RebuildCyberControllerAndProfileBinding())
            {
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ImportedMonsterSetup] Cyber locomotion loop fix completed. changed={changed}");
            if (interactive)
            {
                EditorUtility.DisplayDialog(
                    "Imported Monster Setup",
                    $"赛博怪移动动画循环修复完成。\n变更文件数: {changed}",
                    "OK");
            }
        }

        private static bool RebuildCyberControllerAndProfileBinding()
        {
            if (!TryPrepareCyberControllerClips(
                    out var cyberIdle,
                    out var cyberMove,
                    out var cyberAttack,
                    out var cyberCast,
                    out var cyberHit,
                    out var cyberDie))
            {
                Debug.LogError("[ImportedMonsterSetup] Rebuild cyber controller failed: unable to prepare stable cyber clips.");
                return false;
            }

            var cyberController = BuildOrReplaceController(
                CyberControllerPath,
                cyberIdle,
                cyberMove,
                cyberAttack,
                cyberCast,
                cyberHit,
                cyberDie);

            var cyberProfile = AssetDatabase.LoadAssetAtPath<UnitVisualProfile>(CyberProfilePath);
            if (cyberProfile == null)
            {
                return cyberController != null;
            }

            var so = new SerializedObject(cyberProfile);
            var changed = SetObjectIfDifferent(so, "animatorController", cyberController);
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cyberProfile);
            }

            return cyberController != null || changed;
        }

        private static void DumpCyberAnimationDiagnosticsInternal(bool interactive)
        {
            var profile = AssetDatabase.LoadAssetAtPath<UnitVisualProfile>(CyberProfilePath);
            var idle = LoadPrimaryClip(CyberIdlePath);
            var run = LoadPrimaryClip(CyberMovePath);
            var walk = LoadPrimaryClip(CyberWalkPath);
            var cast = LoadPrimaryClip(CyberCastPath);
            var attack = LoadPrimaryClip(CyberAttackPath);
            var die = LoadPrimaryClip(CyberDiePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CyberPrefabPath);
            var prefabAnimator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;

            var lines = new[]
            {
                "[ImportedMonsterSetup] Cyber diagnostics:",
                DescribeClip("Idle", idle),
                DescribeClip("Run", run),
                DescribeClip("Walk", walk),
                DescribeClip("Cast", cast),
                DescribeClip("Attack", attack),
                DescribeClip("Die", die),
                $"Profile: {(profile != null ? profile.name : "<null>")}, controller={(profile != null && profile.AnimatorController != null ? profile.AnimatorController.name : "<null>")}, avatar={(profile != null && profile.AvatarOverride != null ? profile.AvatarOverride.name : "<null>")}",
                $"Prefab Animator: {(prefabAnimator != null ? prefabAnimator.name : "<null>")}, controller={(prefabAnimator != null && prefabAnimator.runtimeAnimatorController != null ? prefabAnimator.runtimeAnimatorController.name : "<null>")}, avatar={(prefabAnimator != null && prefabAnimator.avatar != null ? prefabAnimator.avatar.name : "<null>")}"
            };

            var message = string.Join("\n", lines);
            Debug.Log(message);

            if (interactive)
            {
                EditorUtility.DisplayDialog("Imported Monster Setup", message, "OK");
            }
        }

        public static void SetupAndApplyToUnitsBatch()
        {
            SetupInternal(applyProfilesToUnits: true, interactive: false);
        }

        private static void SetupInternal(bool applyProfilesToUnits, bool interactive)
        {
            EnsureFolder(ControllerFolder);
            EnsureFolder(ProfileFolder);

            // 某些第三方 FBX 默认不是循环，移动状态会在几步后停在末帧。
            EnsureModelClipLooping(CyberIdlePath);
            EnsureModelClipLooping(CyberMovePath);
            EnsureModelClipLooping(CyberWalkPath);

            var spiderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPrefabPath);
            var cyberPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CyberPrefabPath);
            var spiderIdle = LoadClip(SpiderIdlePath);
            var spiderMove = LoadClip(SpiderMovePath);
            var spiderAttack = LoadClip(SpiderAttackPath);
            var spiderCast = LoadClip(SpiderCastPath) ?? spiderAttack;
            var spiderHit = LoadClip(SpiderHitPath) ?? spiderIdle;
            var spiderDie = LoadClip(SpiderDiePath);

            var cyberIdleSource = LoadPrimaryClip(CyberIdlePath);
            var cyberMoveSource = LoadPrimaryClip(CyberMovePath);
            var cyberAttackSource = LoadPrimaryClip(CyberAttackPath);
            var cyberDieSource = LoadPrimaryClip(CyberDiePath);

            if (!ValidateRequiredAssets(
                    spiderPrefab,
                    cyberPrefab,
                    spiderIdle,
                    spiderMove,
                    spiderAttack,
                    spiderDie,
                    cyberIdleSource,
                    cyberMoveSource,
                    cyberAttackSource,
                    cyberDieSource,
                    interactive))
            {
                return;
            }

            if (!TryPrepareCyberControllerClips(
                    out var cyberIdle,
                    out var cyberMove,
                    out var cyberAttack,
                    out var cyberCast,
                    out var cyberHit,
                    out var cyberDie))
            {
                var message = "[ImportedMonsterSetup] Unable to prepare stable cyber clips.";
                Debug.LogError(message);
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Imported Monster Setup", message, "OK");
                }

                return;
            }

            var spiderController = BuildOrReplaceController(
                SpiderControllerPath,
                spiderIdle,
                spiderMove,
                spiderAttack,
                spiderCast,
                spiderHit,
                spiderDie);

            var cyberController = BuildOrReplaceController(
                CyberControllerPath,
                cyberIdle,
                cyberMove,
                cyberAttack,
                cyberCast,
                cyberHit,
                cyberDie);

            var spiderAvatar = LoadPrimaryAvatar(SpiderAvatarSourcePath);
            // Cyber 动画与 Idle 源 Avatar 绑定最稳定，优先使用 Idle 的 Avatar。
            var cyberAvatar = LoadPrimaryAvatar(CyberIdlePath) ?? LoadPrimaryAvatar(CyberAvatarSourcePath);

            var spiderProfile = LoadOrCreateProfile(SpiderProfilePath, "Unit_Enemy_Spider_Visual");
            var cyberProfile = LoadOrCreateProfile(CyberProfilePath, "Unit_Enemy_Cyber_Visual");

            var changedProfiles = 0;
            if (ConfigureProfile(
                    spiderProfile,
                    spiderPrefab,
                    spiderController,
                    spiderAvatar,
                    new Vector3(0f, 180f, 0f),
                    new Vector3(0.28f, 0.28f, 0.28f)))
            {
                changedProfiles++;
            }

            if (ConfigureProfile(
                    cyberProfile,
                    cyberPrefab,
                    cyberController,
                    cyberAvatar,
                    Vector3.zero,
                    Vector3.one))
            {
                changedProfiles++;
            }

            var changedUnits = 0;
            if (applyProfilesToUnits)
            {
                changedUnits += AssignUnitVisualProfile(UnitEnemyPath, spiderProfile) ? 1 : 0;
                changedUnits += AssignUnitVisualProfile(UnitEnemyHighHpPath, cyberProfile) ? 1 : 0;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary =
                $"SpiderProfile={AssetDatabase.GetAssetPath(spiderProfile)}, CyberProfile={AssetDatabase.GetAssetPath(cyberProfile)}, changedProfiles={changedProfiles}, changedUnits={changedUnits}";
            Debug.Log("[ImportedMonsterSetup] Completed: " + summary);

            if (!interactive)
            {
                return;
            }

            EditorUtility.DisplayDialog(
                "Imported Monster Setup",
                "完成。\n\n" + summary + "\n\n如需直接应用到现有敌人，请运行：\nCombat/Visual/Imported Monsters/Setup And Apply To Existing Enemies",
                "OK");
        }

        private static bool ConfigureProfile(
            UnitVisualProfile profile,
            GameObject modelPrefab,
            RuntimeAnimatorController controller,
            Avatar avatar,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            if (profile == null)
            {
                return false;
            }

            var so = new SerializedObject(profile);
            var changed = false;

            changed |= SetObjectIfDifferent(so, "modelPrefab", modelPrefab);
            changed |= SetVector3IfDifferent(so, "localPosition", Vector3.zero);
            changed |= SetVector3IfDifferent(so, "localEulerAngles", localEulerAngles);
            changed |= SetVector3IfDifferent(so, "localScale", localScale);
            changed |= SetBoolIfDifferent(so, "hideRootRenderersWhenModelActive", true);
            changed |= SetObjectIfDifferent(so, "animatorController", controller);
            changed |= SetObjectIfDifferent(so, "avatarOverride", avatar);
            changed |= SetBoolIfDifferent(so, "applyRootMotion", false);

            changed |= SetStringIfDifferent(so, "moveSpeedFloat", "MoveSpeed");
            changed |= SetStringIfDifferent(so, "movingBool", "IsMoving");
            changed |= SetStringIfDifferent(so, "attackTrigger", "Attack");
            changed |= SetStringIfDifferent(so, "castTrigger", "Cast");
            changed |= SetStringIfDifferent(so, "hitTrigger", "Hit");
            changed |= SetStringIfDifferent(so, "dieTrigger", "Die");
            changed |= SetStringIfDifferent(so, "deadBool", "IsDead");
            changed |= SetStringIfDifferent(so, "castingBool", "IsCasting");

            changed |= SetBoolIfDifferent(so, "useAttackTriggerForBasicAttack", true);
            changed |= SetFloatIfDifferent(so, "moveSpeedSmoothing", 14f);
            changed |= SetFloatIfDifferent(so, "movingThreshold", 0.06f);

            if (!changed)
            {
                return false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return true;
        }

        private static bool AssignUnitVisualProfile(string unitPath, UnitVisualProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(unitPath);
            if (unit == null)
            {
                Debug.LogWarning("[ImportedMonsterSetup] UnitDefinition not found: " + unitPath);
                return false;
            }

            var so = new SerializedObject(unit);
            var changed = SetObjectIfDifferent(so, "visualProfile", profile);
            if (!changed)
            {
                return false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unit);
            return true;
        }

        private static AnimatorController BuildOrReplaceController(
            string outputPath,
            AnimationClip idleClip,
            AnimationClip moveClip,
            AnimationClip attackClip,
            AnimationClip castClip,
            AnimationClip hitClip,
            AnimationClip dieClip)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(outputPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(outputPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCasting", AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;
            var layers = controller.layers;
            if (layers != null && layers.Length > 0)
            {
                layers[0].defaultWeight = 1f;
                controller.layers = layers;
            }
            var locomotionState = stateMachine.AddState("Locomotion");
            var attackState = stateMachine.AddState("Attack");
            var castState = stateMachine.AddState("Cast");
            var hitState = stateMachine.AddState("Hit");
            var dieState = stateMachine.AddState("Die");

            var blendTree = new BlendTree
            {
                name = "Locomotion_BlendTree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "MoveSpeed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(idleClip, 0f);
            blendTree.AddChild(moveClip, 1f);
            locomotionState.motion = blendTree;

            attackState.motion = attackClip;
            castState.motion = castClip;
            hitState.motion = hitClip;
            dieState.motion = dieClip;

            stateMachine.defaultState = locomotionState;

            CreateAnyStateTriggerTransition(stateMachine, attackState, "Attack", 0.02f);
            CreateAnyStateTriggerTransition(stateMachine, castState, "Cast", 0.02f);
            CreateAnyStateTriggerTransition(stateMachine, hitState, "Hit", 0.02f);
            CreateAnyStateTriggerTransition(stateMachine, dieState, "Die", 0.02f);
            CreateAnyStateBoolTransition(stateMachine, dieState, "IsDead", 0.02f);

            CreateReturnToLocomotion(attackState, locomotionState);
            CreateReturnToLocomotion(castState, locomotionState);
            CreateReturnToLocomotion(hitState, locomotionState);

            return controller;
        }

        private static bool TryPrepareCyberControllerClips(
            out AnimationClip cyberIdle,
            out AnimationClip cyberMove,
            out AnimationClip cyberAttack,
            out AnimationClip cyberCast,
            out AnimationClip cyberHit,
            out AnimationClip cyberDie)
        {
            cyberIdle = null;
            cyberMove = null;
            cyberAttack = null;
            cyberCast = null;
            cyberHit = null;
            cyberDie = null;

            var sourceIdle = LoadPrimaryClip(CyberIdlePath);
            var sourceMove = LoadPrimaryClip(CyberMovePath);
            var sourceAttack = LoadPrimaryClip(CyberAttackPath);
            var sourceCast = LoadPrimaryClip(CyberCastPath);
            var sourceDie = LoadPrimaryClip(CyberDiePath);
            if (sourceIdle == null || sourceMove == null || sourceAttack == null || sourceDie == null)
            {
                return false;
            }

            EnsureFolder(CyberStableClipFolder);

            cyberIdle = CreateOrUpdateClipAsset(sourceIdle, CyberStableIdleClipPath, "Cyber_Idle");
            cyberMove = CreateOrUpdateClipAsset(sourceMove, CyberStableRunClipPath, "Cyber_Run");
            cyberAttack = CreateOrUpdateClipAsset(sourceAttack, CyberStableAttackClipPath, "Cyber_Attack");
            cyberDie = CreateOrUpdateClipAsset(sourceDie, CyberStableDieClipPath, "Cyber_Die");
            if (sourceCast != null)
            {
                cyberCast = CreateOrUpdateClipAsset(sourceCast, CyberStableCastClipPath, "Cyber_Cast");
            }

            cyberCast ??= cyberAttack;
            cyberHit = cyberIdle;
            return cyberIdle != null && cyberMove != null && cyberAttack != null && cyberCast != null && cyberDie != null;
        }

        private static AnimationClip CreateOrUpdateClipAsset(AnimationClip sourceClip, string targetPath, string targetName)
        {
            if (sourceClip == null || string.IsNullOrWhiteSpace(targetPath))
            {
                return null;
            }

            var folder = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder.Replace('\\', '/'));
            }

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            if (existing == null)
            {
                var copy = UnityEngine.Object.Instantiate(sourceClip);
                copy.name = targetName;
                AssetDatabase.CreateAsset(copy, targetPath);
                return copy;
            }

            EditorUtility.CopySerialized(sourceClip, existing);
            existing.name = targetName;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void CreateAnyStateTriggerTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState targetState,
            string triggerParameter,
            float duration)
        {
            var transition = stateMachine.AddAnyStateTransition(targetState);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = Mathf.Max(0f, duration);
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerParameter);
        }

        private static void CreateAnyStateBoolTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState targetState,
            string boolParameter,
            float duration)
        {
            var transition = stateMachine.AddAnyStateTransition(targetState);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = Mathf.Max(0f, duration);
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, boolParameter);
        }

        private static void CreateReturnToLocomotion(AnimatorState fromState, AnimatorState toState)
        {
            var transition = fromState.AddTransition(toState);
            transition.hasExitTime = true;
            transition.exitTime = 0.92f;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
        }

        private static UnitVisualProfile LoadOrCreateProfile(string path, string assetName)
        {
            var profile = AssetDatabase.LoadAssetAtPath<UnitVisualProfile>(path);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<UnitVisualProfile>();
            profile.name = assetName;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static AnimationClip LoadClip(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static AnimationClip LoadPrimaryClip(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                return null;
            }

            var preferredName = Path.GetFileNameWithoutExtension(path);
            foreach (var asset in assets)
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(clip.name, preferredName, StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return assets.OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        }

        private static Avatar LoadPrimaryAvatar(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(a => a != null && a.isValid);
        }

        private static string DescribeClip(string label, AnimationClip clip)
        {
            if (clip == null)
            {
                return $"{label}: <null>";
            }

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var varyingCurves = 0;
            var rootCurves = 0;
            var sampleBindings = new System.Collections.Generic.List<string>(4);
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.path.Length == 0)
                {
                    rootCurves++;
                }

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.keys == null || curve.keys.Length == 0)
                {
                    continue;
                }

                var min = curve.keys[0].value;
                var max = min;
                for (int k = 1; k < curve.keys.Length; k++)
                {
                    var value = curve.keys[k].value;
                    if (value < min)
                    {
                        min = value;
                    }
                    else if (value > max)
                    {
                        max = value;
                    }
                }

                if (Mathf.Abs(max - min) > 0.0001f)
                {
                    varyingCurves++;
                    if (sampleBindings.Count < 4)
                    {
                        sampleBindings.Add($"{binding.path}/{binding.propertyName}");
                    }
                }
            }

            var samples = sampleBindings.Count > 0 ? string.Join(" | ", sampleBindings) : "<none>";
            return $"{label}: name={clip.name}, length={clip.length:F3}s, frameRate={clip.frameRate:F1}, curves={bindings.Length}, varying={varyingCurves}, rootCurves={rootCurves}, legacy={clip.legacy}, humanMotion={clip.humanMotion}, samples={samples}";
        }

        private static bool ValidateRequiredAssets(
            GameObject spiderPrefab,
            GameObject cyberPrefab,
            AnimationClip spiderIdle,
            AnimationClip spiderMove,
            AnimationClip spiderAttack,
            AnimationClip spiderDie,
            AnimationClip cyberIdle,
            AnimationClip cyberMove,
            AnimationClip cyberAttack,
            AnimationClip cyberDie,
            bool interactive)
        {
            var missing = new System.Collections.Generic.List<string>(8);
            if (spiderPrefab == null) missing.Add(SpiderPrefabPath);
            if (cyberPrefab == null) missing.Add(CyberPrefabPath);
            if (spiderIdle == null) missing.Add(SpiderIdlePath);
            if (spiderMove == null) missing.Add(SpiderMovePath);
            if (spiderAttack == null) missing.Add(SpiderAttackPath);
            if (spiderDie == null) missing.Add(SpiderDiePath);
            if (cyberIdle == null) missing.Add(CyberIdlePath);
            if (cyberMove == null) missing.Add(CyberMovePath);
            if (cyberAttack == null) missing.Add(CyberAttackPath);
            if (cyberDie == null) missing.Add(CyberDiePath);

            if (missing.Count == 0)
            {
                return true;
            }

            var message = "[ImportedMonsterSetup] Missing required assets:\n- " + string.Join("\n- ", missing);
            Debug.LogError(message);
            if (interactive)
            {
                EditorUtility.DisplayDialog("Imported Monster Setup", message, "OK");
            }

            return false;
        }

        private static bool SetObjectIfDifferent(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            var property = so.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value)
            {
                return false;
            }

            property.objectReferenceValue = value;
            return true;
        }

        private static bool SetStringIfDifferent(SerializedObject so, string propertyName, string value)
        {
            var property = so.FindProperty(propertyName);
            if (property == null || property.stringValue == value)
            {
                return false;
            }

            property.stringValue = value;
            return true;
        }

        private static bool SetBoolIfDifferent(SerializedObject so, string propertyName, bool value)
        {
            var property = so.FindProperty(propertyName);
            if (property == null || property.boolValue == value)
            {
                return false;
            }

            property.boolValue = value;
            return true;
        }

        private static bool SetFloatIfDifferent(SerializedObject so, string propertyName, float value)
        {
            var property = so.FindProperty(propertyName);
            if (property == null || Mathf.Approximately(property.floatValue, value))
            {
                return false;
            }

            property.floatValue = value;
            return true;
        }

        private static bool SetVector3IfDifferent(SerializedObject so, string propertyName, Vector3 value)
        {
            var property = so.FindProperty(propertyName);
            if (property == null || property.vector3Value == value)
            {
                return false;
            }

            property.vector3Value = value;
            return true;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var normalized = folderPath.Replace('\\', '/');
            var segments = normalized.Split('/');
            var current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static bool EnsureModelClipLooping(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ImportedMonsterSetup] ModelImporter not found: {assetPath}");
                return false;
            }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[ImportedMonsterSetup] No clips found in importer: {assetPath}");
                return false;
            }

            var changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (!clip.loopTime)
                {
                    clip.loopTime = true;
                    changed = true;
                }

                if (!clip.loopPose)
                {
                    clip.loopPose = true;
                    changed = true;
                }

                if (clip.wrapMode != WrapMode.Loop)
                {
                    clip.wrapMode = WrapMode.Loop;
                    changed = true;
                }

                clips[i] = clip;
            }

            if (!changed)
            {
                return false;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            return true;
        }

        private static bool UpgradeMaterialToUrpLit(Material material, Shader urpLit)
        {
            if (material == null || urpLit == null)
            {
                return false;
            }

            var oldMainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            var oldColor = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            var oldBumpMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
            var oldBumpScale = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1f;
            var oldMetallicMap = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
            var oldMetallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            var oldSmoothness = material.HasProperty("_GlossMapScale") ? material.GetFloat("_GlossMapScale") : 0.5f;
            var oldOcclusionMap = material.HasProperty("_OcclusionMap") ? material.GetTexture("_OcclusionMap") : null;
            var oldOcclusionStrength = material.HasProperty("_OcclusionStrength") ? material.GetFloat("_OcclusionStrength") : 1f;
            var oldEmissionMap = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;
            var oldEmissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
            var oldMode = material.HasProperty("_Mode") ? Mathf.RoundToInt(material.GetFloat("_Mode")) : 0;
            var hadEmission = oldEmissionMap != null || oldEmissionColor.maxColorComponent > 0.0001f;

            var changed = false;
            if (material.shader != urpLit)
            {
                material.shader = urpLit;
                changed = true;
            }

            changed |= SetTexture(material, "_BaseMap", oldMainTex);
            changed |= SetColor(material, "_BaseColor", oldColor);
            changed |= SetTexture(material, "_BumpMap", oldBumpMap);
            changed |= SetFloat(material, "_BumpScale", oldBumpScale);
            changed |= SetTexture(material, "_MetallicGlossMap", oldMetallicMap);
            changed |= SetFloat(material, "_Metallic", oldMetallic);
            changed |= SetFloat(material, "_Smoothness", Mathf.Clamp01(oldSmoothness));
            changed |= SetTexture(material, "_OcclusionMap", oldOcclusionMap);
            changed |= SetFloat(material, "_OcclusionStrength", oldOcclusionStrength);
            changed |= SetTexture(material, "_EmissionMap", oldEmissionMap);
            changed |= SetColor(material, "_EmissionColor", oldEmissionColor);

            // Legacy Standard _Mode:
            // 0=Opaque, 1=Cutout, 2=Fade, 3=Transparent
            var alphaClip = oldMode == 1 ? 1f : 0f;
            var transparent = oldMode >= 2 ? 1f : 0f;
            changed |= SetFloat(material, "_AlphaClip", alphaClip);
            changed |= SetFloat(material, "_Surface", transparent);
            changed |= SetFloat(material, "_Blend", transparent > 0f ? 0f : 0f);

            if (alphaClip > 0f && material.HasProperty("_Cutoff") && material.HasProperty("_Cutoff"))
            {
                // keep existing cutoff if present
                changed |= SetFloat(material, "_Cutoff", Mathf.Clamp01(material.GetFloat("_Cutoff")));
            }

            CoreUtils.SetKeyword(material, "_NORMALMAP", oldBumpMap != null);
            CoreUtils.SetKeyword(material, "_METALLICSPECGLOSSMAP", oldMetallicMap != null);
            CoreUtils.SetKeyword(material, "_OCCLUSIONMAP", oldOcclusionMap != null);
            CoreUtils.SetKeyword(material, "_EMISSION", hadEmission);

            if (!changed)
            {
                return false;
            }

            EditorUtility.SetDirty(material);
            return true;
        }

        private static bool SetFloat(Material material, string propertyName, float value)
        {
            if (!material.HasProperty(propertyName))
            {
                return false;
            }

            var current = material.GetFloat(propertyName);
            if (Mathf.Approximately(current, value))
            {
                return false;
            }

            material.SetFloat(propertyName, value);
            return true;
        }

        private static bool SetColor(Material material, string propertyName, Color value)
        {
            if (!material.HasProperty(propertyName))
            {
                return false;
            }

            var current = material.GetColor(propertyName);
            if (current == value)
            {
                return false;
            }

            material.SetColor(propertyName, value);
            return true;
        }

        private static bool SetTexture(Material material, string propertyName, Texture value)
        {
            if (!material.HasProperty(propertyName))
            {
                return false;
            }

            var current = material.GetTexture(propertyName);
            if (current == value)
            {
                return false;
            }

            material.SetTexture(propertyName, value);
            return true;
        }
    }
}
