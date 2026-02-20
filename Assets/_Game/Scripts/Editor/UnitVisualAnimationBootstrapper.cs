using System;
using System.IO;
using System.Linq;
using CombatSystem.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CombatSystem.Editor
{
    /// <summary>
    /// 单位视觉动画引导工具：
    /// - 生成项目默认战斗 Animator Controller
    /// - 将控制器与默认模型批量绑定到 UnitVisualProfile
    /// </summary>
    public static class UnitVisualAnimationBootstrapper
    {
        private const string OutputControllerFolder = "Assets/_Game/Art/Characters/Controllers";
        private const string OutputControllerPath = OutputControllerFolder + "/Unit_Combat_Default.controller";
        private const string VisualProfileFolder = "Assets/_Game/ScriptableObjects/Units/VisualProfiles";

        private const string IdleClipPath =
            "Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed/RPG-Character@Unarmed-Idle.FBX";
        private const string RunClipPath =
            "Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed/RPG-Character@Unarmed-Run-Forward.FBX";
        private const string AttackClipPath =
            "Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed/RPG-Character@Unarmed-Attack-R1.FBX";
        private const string CastClipPath =
            "Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed/RPG-Character@Unarmed-Run-Forward-Attack1-Right.FBX";
        private const string HitClipPath =
            "Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed/RPG-Character@Unarmed-GetHit-F1.FBX";
        private const string DieClipPath =
            "Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed/RPG-Character@Unarmed-Knockdown1.FBX";

        private const string RpgModelAssetPath =
            "Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Models/Characters/RPG-Character.FBX";
        private const string RpgLegacyPrefabPath =
            "Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Prefabs/Character/RPG-Character.prefab";
        private const string MedievalModelPrefabPath =
            "Assets/ThirdParty/Characters/JC_LP_MedievalCharacters_LITE/Prefabs/SM_MedievalMaleLite_01.prefab";
        private const string MannequinModelPrefabPath =
            "Assets/ThirdParty/Characters/AssetHunts_Mannequin/GameDev Essential Kit - Mannequin/Asset/Mannequin_Man.prefab";

        [MenuItem("Combat/Visual/Build Default Unit Animator And Bind Profiles")]
        public static void BuildDefaultUnitAnimatorAndBindProfiles()
        {
            RunInternal(interactive: true);
        }

        /// <summary>
        /// 批处理入口：
        /// Unity.exe -batchmode -projectPath ... -executeMethod CombatSystem.Editor.UnitVisualAnimationBootstrapper.BuildDefaultUnitAnimatorAndBindProfilesBatch -quit
        /// </summary>
        public static void BuildDefaultUnitAnimatorAndBindProfilesBatch()
        {
            RunInternal(interactive: false);
        }

        private static void RunInternal(bool interactive)
        {
            EnsureFolder(OutputControllerFolder);

            var idleClip = LoadPrimaryClip(IdleClipPath);
            var runClip = LoadPrimaryClip(RunClipPath);
            var attackClip = LoadPrimaryClip(AttackClipPath);
            var castClip = LoadPrimaryClip(CastClipPath) ?? attackClip;
            var hitClip = LoadPrimaryClip(HitClipPath);
            var dieClip = LoadPrimaryClip(DieClipPath);

            if (idleClip == null || runClip == null || attackClip == null || hitClip == null || dieClip == null)
            {
                var missing = string.Join(", ", new[]
                {
                    idleClip == null ? "Idle" : null,
                    runClip == null ? "Run" : null,
                    attackClip == null ? "Attack" : null,
                    hitClip == null ? "Hit" : null,
                    dieClip == null ? "Die" : null
                }.Where(s => !string.IsNullOrEmpty(s)));

                var msg = "[VisualAnimSetup] Missing required clips: " + missing;
                Debug.LogError(msg);
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Unit Visual Animation Setup", msg, "OK");
                }

                return;
            }

            var controller = BuildOrReplaceController(
                idleClip,
                runClip,
                attackClip,
                castClip,
                hitClip,
                dieClip);

            var rpgModel = AssetDatabase.LoadAssetAtPath<GameObject>(RpgModelAssetPath);
            var rpgLegacyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RpgLegacyPrefabPath);
            var medievalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MedievalModelPrefabPath);
            var mannequinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinModelPrefabPath);

            var profileGuids = AssetDatabase.FindAssets("t:UnitVisualProfile", new[] { VisualProfileFolder });
            var changedProfiles = 0;

            for (int i = 0; i < profileGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(profileGuids[i]);
                var profile = AssetDatabase.LoadAssetAtPath<UnitVisualProfile>(path);
                if (profile == null)
                {
                    continue;
                }

                if (BindProfile(profile, controller, rpgModel, rpgLegacyPrefab, medievalPrefab, mannequinPrefab))
                {
                    changedProfiles++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = $"profiles={profileGuids.Length}, changed={changedProfiles}, controller={OutputControllerPath}";
            Debug.Log("[VisualAnimSetup] Completed: " + summary);
            if (interactive)
            {
                EditorUtility.DisplayDialog("Unit Visual Animation Setup", "完成。\n\n" + summary, "OK");
            }
        }

        private static bool BindProfile(
            UnitVisualProfile profile,
            RuntimeAnimatorController controller,
            GameObject rpgModel,
            GameObject rpgLegacyPrefab,
            GameObject medievalPrefab,
            GameObject mannequinPrefab)
        {
            var so = new SerializedObject(profile);
            var changed = false;

            changed |= SetObjectReferenceIfDifferent(so, "animatorController", controller);
            changed |= SetBoolIfDifferent(so, "applyRootMotion", false);
            changed |= SetStringIfDifferent(so, "moveSpeedFloat", "MoveSpeed");
            changed |= SetStringIfDifferent(so, "movingBool", "IsMoving");
            changed |= SetStringIfDifferent(so, "attackTrigger", "Attack");
            changed |= SetStringIfDifferent(so, "castTrigger", "Cast");
            changed |= SetStringIfDifferent(so, "hitTrigger", "Hit");
            changed |= SetStringIfDifferent(so, "dieTrigger", "Die");
            changed |= SetStringIfDifferent(so, "deadBool", "IsDead");
            changed |= SetStringIfDifferent(so, "castingBool", "IsCasting");

            var modelProp = so.FindProperty("modelPrefab");
            if (modelProp != null)
            {
                var shouldReplaceModel = modelProp.objectReferenceValue == null || modelProp.objectReferenceValue == rpgLegacyPrefab;
                if (shouldReplaceModel)
                {
                    GameObject pickedPrefab;
                    if (profile.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pickedPrefab = rpgModel != null ? rpgModel : (medievalPrefab != null ? medievalPrefab : mannequinPrefab);
                    }
                    else if (profile.name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pickedPrefab = medievalPrefab != null ? medievalPrefab : (rpgModel != null ? rpgModel : mannequinPrefab);
                    }
                    else
                    {
                        pickedPrefab = mannequinPrefab != null ? mannequinPrefab : (medievalPrefab != null ? medievalPrefab : rpgModel);
                    }

                    if (pickedPrefab != null && modelProp.objectReferenceValue != pickedPrefab)
                    {
                        modelProp.objectReferenceValue = pickedPrefab;
                        changed = true;
                    }
                }
            }

            if (!changed)
            {
                return false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return true;
        }

        private static AnimatorController BuildOrReplaceController(
            AnimationClip idleClip,
            AnimationClip runClip,
            AnimationClip attackClip,
            AnimationClip castClip,
            AnimationClip hitClip,
            AnimationClip dieClip)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(OutputControllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputControllerPath);

            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCasting", AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;
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
            blendTree.AddChild(runClip, 1f);
            locomotionState.motion = blendTree;

            attackState.motion = attackClip;
            castState.motion = castClip;
            hitState.motion = hitClip;
            dieState.motion = dieClip;

            attackState.writeDefaultValues = true;
            castState.writeDefaultValues = true;
            hitState.writeDefaultValues = true;
            dieState.writeDefaultValues = true;

            stateMachine.defaultState = locomotionState;

            CreateAnyStateTransition(stateMachine, attackState, "Attack");
            CreateAnyStateTransition(stateMachine, castState, "Cast");
            CreateAnyStateTransition(stateMachine, hitState, "Hit");
            CreateAnyStateTransition(stateMachine, dieState, "Die");

            var deadTransition = stateMachine.AddAnyStateTransition(dieState);
            deadTransition.hasExitTime = false;
            deadTransition.hasFixedDuration = true;
            deadTransition.duration = 0.05f;
            deadTransition.canTransitionToSelf = false;
            deadTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");

            CreateReturnToLocomotion(attackState, locomotionState);
            CreateReturnToLocomotion(castState, locomotionState);
            CreateReturnToLocomotion(hitState, locomotionState);

            return controller;
        }

        private static void CreateAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState targetState, string triggerParameter)
        {
            var transition = stateMachine.AddAnyStateTransition(targetState);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerParameter);
        }

        private static void CreateReturnToLocomotion(AnimatorState fromState, AnimatorState toState)
        {
            var transition = fromState.AddTransition(toState);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
        }

        private static AnimationClip LoadPrimaryClip(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                return null;
            }

            var preferredName = Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                    string.Equals(clip.name, preferredName, StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        private static bool SetObjectReferenceIfDifferent(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null || prop.objectReferenceValue == value)
            {
                return false;
            }

            prop.objectReferenceValue = value;
            return true;
        }

        private static bool SetStringIfDifferent(SerializedObject so, string propertyName, string value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null || prop.stringValue == value)
            {
                return false;
            }

            prop.stringValue = value;
            return true;
        }

        private static bool SetBoolIfDifferent(SerializedObject so, string propertyName, bool value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null || prop.boolValue == value)
            {
                return false;
            }

            prop.boolValue = value;
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
    }
}
