using System;
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

        public static void SetupAndApplyToUnitsBatch()
        {
            SetupInternal(applyProfilesToUnits: true, interactive: false);
        }

        private static void SetupInternal(bool applyProfilesToUnits, bool interactive)
        {
            EnsureFolder(ControllerFolder);
            EnsureFolder(ProfileFolder);

            var spiderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPrefabPath);
            var cyberPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CyberPrefabPath);
            var spiderIdle = LoadClip(SpiderIdlePath);
            var spiderMove = LoadClip(SpiderMovePath);
            var spiderAttack = LoadClip(SpiderAttackPath);
            var spiderCast = LoadClip(SpiderCastPath) ?? spiderAttack;
            var spiderHit = LoadClip(SpiderHitPath) ?? spiderIdle;
            var spiderDie = LoadClip(SpiderDiePath);

            var cyberIdle = LoadPrimaryClip(CyberIdlePath);
            var cyberMove = LoadPrimaryClip(CyberMovePath);
            var cyberAttack = LoadPrimaryClip(CyberAttackPath);
            var cyberCast = LoadPrimaryClip(CyberCastPath) ?? cyberAttack;
            var cyberHit = cyberIdle;
            var cyberDie = LoadPrimaryClip(CyberDiePath);

            if (!ValidateRequiredAssets(
                    spiderPrefab,
                    cyberPrefab,
                    spiderIdle,
                    spiderMove,
                    spiderAttack,
                    spiderDie,
                    cyberIdle,
                    cyberMove,
                    cyberAttack,
                    cyberDie,
                    interactive))
            {
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
            var cyberAvatar = LoadPrimaryAvatar(CyberAvatarSourcePath);

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

            var preferredName = System.IO.Path.GetFileNameWithoutExtension(path);
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
