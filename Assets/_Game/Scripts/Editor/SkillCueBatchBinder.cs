using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CombatSystem.Editor
{
    /// <summary>
    /// Batch-binds presentation cues for player skills and generates minimal free runtime VFX variants.
    /// </summary>
    public static class SkillCueBatchBinder
    {
        private const string VfxThirdPartyFolder = "Assets/_Game/Art/VFX/ThirdPartyFree";
        private const string SfxThirdPartyFolder = "Assets/_Game/Art/SFX/ThirdPartyFree";
        private const string RuntimeVariantsFolder = "Assets/_Game/Art/VFX/RuntimeVariants";
        private const string RuntimeMaterialFolder = "Assets/_Game/Art/VFX/RuntimeVariants/Materials";
        private const string CfxrPrefabsInThirdParty = "Assets/_Game/Art/VFX/ThirdPartyFree/Cartoon FX Remaster/CFXR Prefabs";
        private const string CfxrPrefabsLegacyRoot = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string TownScenePath = "Assets/Scenes/Town.unity";

        private const string KenneySfxBase = "Assets/_Game/Art/UIThemes/KenneyUI/Sounds";

        private readonly struct BindSummary
        {
            public readonly int SkillCount;
            public readonly int ChangedSkillCount;
            public readonly int AddedCueCount;
            public readonly int SceneMountedCount;

            public BindSummary(int skillCount, int changedSkillCount, int addedCueCount, int sceneMountedCount)
            {
                SkillCount = skillCount;
                ChangedSkillCount = changedSkillCount;
                AddedCueCount = addedCueCount;
                SceneMountedCount = sceneMountedCount;
            }
        }

        private sealed class VfxPalette
        {
            public GameObject CastPulse;
            public GameObject HitSpark;
            public GameObject BuffAura;
            public GameObject DashTrail;
            public GameObject ProjectileTrail;
            public GameObject ProjectileImpact;
            public GameObject SummonCircle;
            public GameObject ShieldPulse;
            public GameObject RevealMark;
            public GameObject WindWallCast;
            public GameObject ReturnFlash;
            public GameObject SplitFlash;
            public GameObject MeleeSwing;
        }

        private sealed class AudioPalette
        {
            public AudioClip Cast;
            public AudioClip Hit;
            public AudioClip Buff;
        }

        [MenuItem("Combat/Tools/Skill Presentation/Apply Free VFX Plan (Player Skills)")]
        public static void ApplyFreeVfxPlan()
        {
            EnsureFoldersAndPlaceholders();
            var vfx = BuildVfxPalette();
            var audio = LoadAudioPalette();
            var summary = ApplyForPlayerSkillsInternal(vfx, audio);
            Debug.Log(
                $"[SkillCueBatchBinder] Applied. Skills={summary.SkillCount}, ChangedSkills={summary.ChangedSkillCount}, AddedCues={summary.AddedCueCount}, MountedScenes={summary.SceneMountedCount}");
        }

        [MenuItem("Combat/Tools/Skill Presentation/Bind Cues For Player Skills")]
        public static void BindCuesForPlayerSkills()
        {
            EnsureFoldersAndPlaceholders();
            var vfx = BuildVfxPalette();
            var audio = LoadAudioPalette();
            var summary = ApplyForPlayerSkillsInternal(vfx, audio, mountScenes: false);
            Debug.Log(
                $"[SkillCueBatchBinder] Cue binding done. Skills={summary.SkillCount}, ChangedSkills={summary.ChangedSkillCount}, AddedCues={summary.AddedCueCount}");
        }

        [MenuItem("Combat/Tools/Skill Presentation/Generate Runtime Variant VFX")]
        public static void GenerateRuntimeVariantVfx()
        {
            EnsureFoldersAndPlaceholders();
            EnsureRuntimeVariantPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SkillCueBatchBinder] Runtime variant VFX prefabs are ready.");
        }

        [MenuItem("Combat/Tools/Skill Presentation/Mount Presentation System In Sample+Town")]
        public static void MountPresentationSystemInScenes()
        {
            var mounted = EnsurePresentationSystemInScenes();
            Debug.Log($"[SkillCueBatchBinder] Mounted SkillPresentationSystem in {mounted} scene(s).");
        }

        public static void CliPing()
        {
            System.IO.File.WriteAllText("Temp/skill_cue_batch_binder_cli_ping.txt", DateTime.UtcNow.ToString("O"));
        }

        private static BindSummary ApplyForPlayerSkillsInternal(VfxPalette vfx, AudioPalette audio, bool mountScenes = true)
        {
            var player = FindPlayerUnitDefinition();
            if (player == null)
            {
                Debug.LogWarning("[SkillCueBatchBinder] Unit_Player not found.");
                return default;
            }

            var skills = CollectPlayerSkills(player);
            var changedSkills = 0;
            var addedCues = 0;

            for (int i = 0; i < skills.Count; i++)
            {
                if (ApplySkillAutoCues(skills[i], vfx, audio, out var added))
                {
                    changedSkills++;
                    addedCues += added;
                    EditorUtility.SetDirty(skills[i]);
                }
            }

            var mounted = 0;
            if (mountScenes)
            {
                mounted = EnsurePresentationSystemInScenes();
            }

            if (changedSkills > 0 || mounted > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return new BindSummary(skills.Count, changedSkills, addedCues, mounted);
        }

        private static List<SkillDefinition> CollectPlayerSkills(UnitDefinition player)
        {
            var result = new List<SkillDefinition>(16);
            var unique = new HashSet<SkillDefinition>();

            if (player.BasicAttack != null && unique.Add(player.BasicAttack))
            {
                result.Add(player.BasicAttack);
            }

            var startingSkills = player.StartingSkills;
            if (startingSkills == null)
            {
                return result;
            }

            for (int i = 0; i < startingSkills.Count; i++)
            {
                var skill = startingSkills[i];
                if (skill != null && unique.Add(skill))
                {
                    result.Add(skill);
                }
            }

            return result;
        }

        private static bool ApplySkillAutoCues(SkillDefinition skill, VfxPalette vfx, AudioPalette audio, out int addedCueCount)
        {
            addedCueCount = 0;
            if (skill == null || skill.Steps == null || skill.Steps.Count == 0)
            {
                return false;
            }

            var changed = false;
            var skillName = skill.name != null ? skill.name.ToLowerInvariant() : string.Empty;

            for (int i = 0; i < skill.Steps.Count; i++)
            {
                var step = skill.Steps[i];
                if (step == null)
                {
                    continue;
                }

                if (step.presentationCues == null)
                {
                    step.presentationCues = new List<SkillPresentationCue>(8);
                }

                var removed = RemoveAutoCues(step.presentationCues);
                if (removed > 0)
                {
                    changed = true;
                }

                var before = step.presentationCues.Count;
                GenerateAutoCues(skillName, step, step.presentationCues, vfx, audio);
                var delta = step.presentationCues.Count - before;
                if (delta > 0)
                {
                    changed = true;
                    addedCueCount += delta;
                }
            }

            return changed;
        }

        private static int RemoveAutoCues(List<SkillPresentationCue> cues)
        {
            if (cues == null || cues.Count == 0)
            {
                return 0;
            }

            return cues.RemoveAll(
                cue => cue != null
                       && !string.IsNullOrEmpty(cue.cueId)
                       && cue.cueId.StartsWith("Auto_", StringComparison.Ordinal));
        }

        private static void GenerateAutoCues(
            string skillName,
            SkillStep step,
            List<SkillPresentationCue> target,
            VfxPalette vfx,
            AudioPalette audio)
        {
            var isFireball = skillName.Contains("fireball");
            var isAmmoBurst = skillName.Contains("ammoburst");
            var isRevealBolt = skillName.Contains("revealbolt");
            var isReturnBlade = skillName.Contains("returnblade");
            var isShardVolley = skillName.Contains("shardvolley");

            var effects = step.effects;
            var hasProjectile = false;
            var hasDamage = false;
            var hasMove = false;
            var hasSummon = false;
            var hasCombatState = false;
            var hasApplyBuff = false;
            var hasReveal = false;
            var hasReturn = false;
            var hasSplit = false;

            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];
                    if (effect == null)
                    {
                        continue;
                    }

                    switch (effect.EffectType)
                    {
                        case EffectType.Projectile:
                            hasProjectile = true;
                            if (effect.Projectile != null)
                            {
                                hasReturn |= effect.Projectile.BehaviorType == ProjectileBehaviorType.Return;
                                hasSplit |= effect.Projectile.BehaviorType == ProjectileBehaviorType.Split || effect.Projectile.SplitCount > 0;
                            }

                            break;
                        case EffectType.Damage:
                            hasDamage = true;
                            hasReveal |= effect.RevealTarget;
                            break;
                        case EffectType.Move:
                            hasMove = true;
                            break;
                        case EffectType.Summon:
                            hasSummon = true;
                            break;
                        case EffectType.CombatState:
                            hasCombatState = true;
                            break;
                        case EffectType.ApplyBuff:
                            hasApplyBuff = true;
                            break;
                    }
                }
            }

            var castPrefab = vfx.CastPulse;
            var castAnchor = PresentationAnchorType.Caster;
            var castFollow = false;
            var castLifetime = 0.5f;
            var projectileSpawnPrefab = vfx.ProjectileTrail;
            var projectileHitPrefab = vfx.ProjectileImpact;
            var projectileReturnPrefab = vfx.ReturnFlash;
            var projectileSplitPrefab = vfx.SplitFlash;
            var revealMarkPrefab = vfx.RevealMark;

            if (hasMove || skillName.Contains("lunge"))
            {
                castPrefab = vfx.DashTrail;
                castFollow = true;
                castLifetime = 0.45f;
            }
            else if (hasSummon || skillName.Contains("windwall"))
            {
                castPrefab = vfx.WindWallCast;
                castAnchor = PresentationAnchorType.AimPoint;
                castLifetime = 0.8f;
            }
            else if (hasCombatState || hasApplyBuff || skillName.Contains("ghoststep") || skillName.Contains("ironshell") || skillName.Contains("aegis") || skillName.Contains("focus"))
            {
                castPrefab = skillName.Contains("shell") || skillName.Contains("aegis") ? vfx.ShieldPulse : vfx.BuffAura;
                castLifetime = 0.9f;
            }
            else if (skillName.Contains("basicattack"))
            {
                castPrefab = vfx.MeleeSwing;
                castLifetime = 0.35f;
            }

            if (isFireball)
            {
                castPrefab = PreferPrefab(castPrefab, "Fire/CFXR2 Firewall A.prefab");
            }

            if (isAmmoBurst)
            {
                projectileHitPrefab = PreferPrefab(
                    projectileHitPrefab,
                    "Impacts/CFXR Hit D 3D (Yellow).prefab");
            }

            if (isRevealBolt)
            {
                castPrefab = PreferPrefab(
                    vfx.BuffAura,
                    "Magic Misc/CFXR3 Magic Aura A (Runic).prefab");
                projectileHitPrefab = PreferPrefab(
                    projectileHitPrefab,
                    "Impacts/CFXR Hit D 3D (Yellow).prefab");
                revealMarkPrefab = PreferPrefab(
                    revealMarkPrefab,
                    "Magic Misc/CFXR3 Magic Aura A (Runic).prefab");
            }

            if (isReturnBlade)
            {
                var swordTrail = PreferPrefab(
                    vfx.MeleeSwing,
                    "Sword Trails/Plain/CFXR4 Sword Trail PLAIN (360 Thin Spiral).prefab");
                castPrefab = swordTrail;
                projectileSpawnPrefab = swordTrail;
                projectileHitPrefab = PreferPrefab(
                    projectileHitPrefab,
                    "Sword Trails/Plain/CFXR4 Sword Hit PLAIN (Cross).prefab",
                    "Sword Trails/Plain/CFXR4 Sword Hit PLAIN (360).prefab");
            }

            if (isShardVolley)
            {
                var electricHit = PreferPrefab(
                    vfx.SplitFlash,
                    "Electric/CFXR3 Hit Electric C (Air).prefab");
                castPrefab = electricHit;
                projectileHitPrefab = electricHit;
                projectileSplitPrefab = electricHit;
            }

            AddCue(
                target,
                new SkillPresentationCue
                {
                    cueId = "Auto_StepCast",
                    eventType = PresentationEventType.StepExecuted,
                    anchorType = castAnchor,
                    spawnSpace = PresentationSpawnSpace.World,
                    vfxPrefab = castPrefab,
                    followAnchor = castFollow,
                    maxLifetime = castLifetime,
                    sfx = audio.Cast,
                    audioBus = AudioBusType.Sfx,
                    audioVolume = 0.85f,
                    audioPitch = 1f,
                    audioSpatialBlend = 1f
                });

            if (hasProjectile)
            {
                AddCue(
                    target,
                    new SkillPresentationCue
                    {
                        cueId = "Auto_ProjectileSpawn",
                        eventType = PresentationEventType.ProjectileSpawn,
                        anchorType = PresentationAnchorType.Projectile,
                        spawnSpace = PresentationSpawnSpace.LocalToAnchor,
                        vfxPrefab = projectileSpawnPrefab,
                        followAnchor = true,
                        maxLifetime = 1f,
                        sfx = audio.Cast,
                        audioBus = AudioBusType.Sfx,
                        audioVolume = 0.9f,
                        audioPitch = 1f,
                        audioSpatialBlend = 1f
                    });

                AddCue(
                    target,
                    new SkillPresentationCue
                    {
                        cueId = "Auto_ProjectileHit",
                        eventType = PresentationEventType.ProjectileHit,
                        anchorType = PresentationAnchorType.PrimaryTarget,
                        spawnSpace = PresentationSpawnSpace.World,
                        vfxPrefab = projectileHitPrefab,
                        maxLifetime = 0.55f,
                        sfx = audio.Hit,
                        audioBus = AudioBusType.Sfx,
                        audioVolume = 0.9f,
                        audioPitch = 1f,
                        audioSpatialBlend = 1f
                    });

                if (hasReturn || skillName.Contains("returnblade"))
                {
                    AddCue(
                        target,
                        new SkillPresentationCue
                        {
                            cueId = "Auto_ProjectileReturn",
                            eventType = PresentationEventType.ProjectileReturn,
                            anchorType = PresentationAnchorType.Projectile,
                            spawnSpace = PresentationSpawnSpace.LocalToAnchor,
                            vfxPrefab = projectileReturnPrefab,
                            followAnchor = true,
                            maxLifetime = 0.5f,
                            sfx = audio.Cast,
                            audioBus = AudioBusType.Sfx,
                            audioVolume = 0.75f,
                            audioPitch = 1.08f,
                            audioSpatialBlend = 1f
                        });
                }

                if (hasSplit || skillName.Contains("shardvolley"))
                {
                    AddCue(
                        target,
                        new SkillPresentationCue
                        {
                            cueId = "Auto_ProjectileSplit",
                            eventType = PresentationEventType.ProjectileSplit,
                            anchorType = PresentationAnchorType.Projectile,
                            spawnSpace = PresentationSpawnSpace.World,
                            vfxPrefab = projectileSplitPrefab,
                            maxLifetime = 0.5f,
                            sfx = audio.Hit,
                            audioBus = AudioBusType.Sfx,
                            audioVolume = 0.75f,
                            audioPitch = 1.12f,
                            audioSpatialBlend = 1f
                        });
                }
            }

            if (hasDamage && !hasProjectile)
            {
                AddCue(
                    target,
                    new SkillPresentationCue
                    {
                        cueId = "Auto_DamageHit",
                        eventType = PresentationEventType.EffectAfterApply,
                        anchorType = PresentationAnchorType.PrimaryTarget,
                        spawnSpace = PresentationSpawnSpace.World,
                        filterByEffectType = true,
                        effectTypeFilter = EffectType.Damage,
                        vfxPrefab = vfx.HitSpark,
                        maxLifetime = 0.45f,
                        sfx = audio.Hit,
                        audioBus = AudioBusType.Sfx,
                        audioVolume = 0.9f,
                        audioPitch = 1f,
                        audioSpatialBlend = 1f
                    });
            }

            if (hasMove && !hasProjectile)
            {
                AddCue(
                    target,
                    new SkillPresentationCue
                    {
                        cueId = "Auto_MoveTrail",
                        eventType = PresentationEventType.StepExecuted,
                        anchorType = PresentationAnchorType.Caster,
                        spawnSpace = PresentationSpawnSpace.LocalToAnchor,
                        vfxPrefab = vfx.DashTrail,
                        followAnchor = true,
                        maxLifetime = 0.45f,
                        sfx = audio.Cast,
                        audioBus = AudioBusType.Sfx,
                        audioVolume = 0.8f,
                        audioPitch = 1f,
                        audioSpatialBlend = 1f
                    });
            }

            if (hasSummon)
            {
                AddCue(
                    target,
                    new SkillPresentationCue
                    {
                        cueId = "Auto_SummonCircle",
                        eventType = PresentationEventType.StepExecuted,
                        anchorType = PresentationAnchorType.AimPoint,
                        spawnSpace = PresentationSpawnSpace.World,
                        vfxPrefab = vfx.SummonCircle,
                        maxLifetime = 1f,
                        sfx = audio.Buff,
                        audioBus = AudioBusType.Sfx,
                        audioVolume = 0.8f,
                        audioPitch = 0.95f,
                        audioSpatialBlend = 1f
                    });
            }

            if (hasCombatState || hasApplyBuff)
            {
                AddCue(
                    target,
                    new SkillPresentationCue
                    {
                        cueId = "Auto_StateResult",
                        eventType = PresentationEventType.EffectAfterApply,
                        anchorType = PresentationAnchorType.Caster,
                        spawnSpace = PresentationSpawnSpace.LocalToAnchor,
                        vfxPrefab = skillName.Contains("shell") || skillName.Contains("aegis") ? vfx.ShieldPulse : vfx.BuffAura,
                        followAnchor = true,
                        maxLifetime = 0.9f,
                        sfx = audio.Buff,
                        audioBus = AudioBusType.Sfx,
                        audioVolume = 0.75f,
                        audioPitch = 1f,
                        audioSpatialBlend = 1f
                    });
            }

            if (hasReveal || skillName.Contains("revealbolt"))
            {
                AddCue(
                    target,
                    new SkillPresentationCue
                    {
                        cueId = "Auto_RevealMark",
                        eventType = PresentationEventType.EffectAfterApply,
                        anchorType = PresentationAnchorType.PrimaryTarget,
                        spawnSpace = PresentationSpawnSpace.World,
                        filterByEffectType = true,
                        effectTypeFilter = EffectType.Damage,
                        vfxPrefab = revealMarkPrefab,
                        maxLifetime = 0.75f,
                        sfx = audio.Buff,
                        audioBus = AudioBusType.Sfx,
                        audioVolume = 0.7f,
                        audioPitch = 1.1f,
                        audioSpatialBlend = 1f
                    });
            }

            if (skillName.Contains("ghoststep"))
            {
                AddCue(
                    target,
                    new SkillPresentationCue
                    {
                        cueId = "Auto_GhostStepTrail",
                        eventType = PresentationEventType.StepExecuted,
                        anchorType = PresentationAnchorType.Caster,
                        spawnSpace = PresentationSpawnSpace.LocalToAnchor,
                        vfxPrefab = vfx.DashTrail,
                        followAnchor = true,
                        maxLifetime = 0.6f
                    });
            }
        }

        private static void AddCue(List<SkillPresentationCue> cues, SkillPresentationCue cue)
        {
            if (cues == null || cue == null || string.IsNullOrWhiteSpace(cue.cueId) || !cue.HasPayload)
            {
                return;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                var existing = cues[i];
                if (existing == null)
                {
                    continue;
                }

                if (string.Equals(existing.cueId, cue.cueId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            cues.Add(cue);
        }

        private static GameObject PreferPrefab(GameObject fallback, params string[] relativePrefabPaths)
        {
            var loaded = LoadFirstPrefab(relativePrefabPaths);
            return loaded != null ? loaded : fallback;
        }

        private static VfxPalette EnsureRuntimeVariantPrefabs()
        {
            EnsureFolder(RuntimeVariantsFolder);
            EnsureFolder(RuntimeMaterialFolder);

            var matCyan = EnsureMaterial("Mat_AutoFx_Cyan.mat", new Color(0.35f, 0.95f, 1f, 0.8f));
            var matOrange = EnsureMaterial("Mat_AutoFx_Orange.mat", new Color(1f, 0.58f, 0.2f, 0.8f));
            var matBlue = EnsureMaterial("Mat_AutoFx_Blue.mat", new Color(0.35f, 0.6f, 1f, 0.8f));
            var matGreen = EnsureMaterial("Mat_AutoFx_Green.mat", new Color(0.4f, 1f, 0.55f, 0.75f));
            var matPurple = EnsureMaterial("Mat_AutoFx_Purple.mat", new Color(0.75f, 0.45f, 1f, 0.78f));
            var matWhite = EnsureMaterial("Mat_AutoFx_White.mat", new Color(0.95f, 0.95f, 1f, 0.8f));

            return new VfxPalette
            {
                CastPulse = EnsureMeshPrefab("VFX_Auto_CastPulse.prefab", PrimitiveType.Sphere, new Vector3(0.45f, 0.45f, 0.45f), matCyan),
                HitSpark = EnsureMeshPrefab("VFX_Auto_HitSpark.prefab", PrimitiveType.Sphere, new Vector3(0.25f, 0.25f, 0.25f), matOrange),
                BuffAura = EnsureMeshPrefab("VFX_Auto_BuffAura.prefab", PrimitiveType.Sphere, new Vector3(1.15f, 1.15f, 1.15f), matGreen),
                DashTrail = EnsureMeshPrefab("VFX_Auto_DashTrail.prefab", PrimitiveType.Capsule, new Vector3(0.2f, 0.2f, 0.9f), matCyan),
                ProjectileTrail = EnsureTrailPrefab("VFX_Auto_ProjectileTrail.prefab", matCyan),
                ProjectileImpact = EnsureMeshPrefab("VFX_Auto_ProjectileImpact.prefab", PrimitiveType.Sphere, new Vector3(0.36f, 0.36f, 0.36f), matOrange),
                SummonCircle = EnsureMeshPrefab("VFX_Auto_SummonCircle.prefab", PrimitiveType.Cylinder, new Vector3(1.2f, 0.03f, 1.2f), matPurple),
                ShieldPulse = EnsureMeshPrefab("VFX_Auto_ShieldPulse.prefab", PrimitiveType.Sphere, new Vector3(1.05f, 1.05f, 1.05f), matBlue),
                RevealMark = EnsureMeshPrefab("VFX_Auto_RevealMark.prefab", PrimitiveType.Cylinder, new Vector3(0.55f, 0.025f, 0.55f), matWhite),
                WindWallCast = EnsureMeshPrefab("VFX_Auto_WindWallCast.prefab", PrimitiveType.Cube, new Vector3(1.8f, 1.2f, 0.08f), matCyan),
                ReturnFlash = EnsureMeshPrefab("VFX_Auto_ReturnFlash.prefab", PrimitiveType.Sphere, new Vector3(0.32f, 0.32f, 0.32f), matWhite),
                SplitFlash = EnsureMeshPrefab("VFX_Auto_SplitFlash.prefab", PrimitiveType.Sphere, new Vector3(0.28f, 0.28f, 0.28f), matPurple),
                MeleeSwing = EnsureMeshPrefab("VFX_Auto_MeleeSwing.prefab", PrimitiveType.Cube, new Vector3(0.7f, 0.2f, 0.1f), matOrange)
            };
        }

        private static VfxPalette BuildVfxPalette()
        {
            var fallback = EnsureRuntimeVariantPrefabs();
            return OverrideWithCartoonFx(fallback);
        }

        private static VfxPalette OverrideWithCartoonFx(VfxPalette fallback)
        {
            if (fallback == null)
            {
                return null;
            }

            var overridden = new VfxPalette
            {
                CastPulse = fallback.CastPulse,
                HitSpark = fallback.HitSpark,
                BuffAura = fallback.BuffAura,
                DashTrail = fallback.DashTrail,
                ProjectileTrail = fallback.ProjectileTrail,
                ProjectileImpact = fallback.ProjectileImpact,
                SummonCircle = fallback.SummonCircle,
                ShieldPulse = fallback.ShieldPulse,
                RevealMark = fallback.RevealMark,
                WindWallCast = fallback.WindWallCast,
                ReturnFlash = fallback.ReturnFlash,
                SplitFlash = fallback.SplitFlash,
                MeleeSwing = fallback.MeleeSwing
            };

            var overrides = 0;
            overrides += TryOverride(ref overridden.CastPulse, fallback.CastPulse, "Misc/CFXR Flash.prefab");
            overrides += TryOverride(ref overridden.HitSpark, fallback.HitSpark, "Impacts/CFXR Hit D 3D (Yellow).prefab", "Impacts/CFXR Hit A (Red).prefab");
            overrides += TryOverride(ref overridden.BuffAura, fallback.BuffAura, "Magic Misc/CFXR3 Magic Aura A (Runic).prefab");
            overrides += TryOverride(ref overridden.DashTrail, fallback.DashTrail, "Nature/CFXR4 Wind Trails.prefab");
            overrides += TryOverride(ref overridden.ProjectileTrail, fallback.ProjectileTrail, "Light/CFXR3 LightGlow A (Loop).prefab");
            overrides += TryOverride(ref overridden.ProjectileImpact, fallback.ProjectileImpact, "Fire/CFXR3 Hit Fire B (Air).prefab", "Light/CFXR3 Hit Light B (Air).prefab");
            overrides += TryOverride(ref overridden.SummonCircle, fallback.SummonCircle, "Magic Misc/CFXR3 Magic Aura A (Runic).prefab");
            overrides += TryOverride(ref overridden.ShieldPulse, fallback.ShieldPulse, "Impacts/CFXR Impact Glowing HDR (Blue).prefab", "Nature/CFXR3 Shield Leaves A (Lit).prefab");
            overrides += TryOverride(ref overridden.RevealMark, fallback.RevealMark, "Light/CFXR3 LightGlow A (Loop).prefab");
            overrides += TryOverride(ref overridden.WindWallCast, fallback.WindWallCast, "Fire/CFXR2 Firewall A.prefab");
            overrides += TryOverride(ref overridden.ReturnFlash, fallback.ReturnFlash, "Misc/CFXR Flash.prefab");
            overrides += TryOverride(ref overridden.SplitFlash, fallback.SplitFlash, "Electric/CFXR3 Hit Electric C (Air).prefab", "Impacts/CFXR Hit D 3D (Yellow).prefab");
            overrides += TryOverride(ref overridden.MeleeSwing, fallback.MeleeSwing, "Sword Trails/Plain/CFXR4 Sword Trail PLAIN (360 Thin Spiral).prefab");

            if (overrides > 0)
            {
                Debug.Log($"[SkillCueBatchBinder] Using Cartoon FX Remaster prefabs for {overrides} cue slots.");
            }
            else
            {
                Debug.Log("[SkillCueBatchBinder] Cartoon FX Remaster prefabs not found, using runtime placeholder VFX.");
            }

            WritePaletteDiagnostics(overridden, overrides);

            return overridden;
        }

        private static int TryOverride(ref GameObject target, GameObject fallback, params string[] relativePrefabPaths)
        {
            var loaded = LoadFirstPrefab(relativePrefabPaths);
            if (loaded == null)
            {
                target = fallback;
                return 0;
            }

            target = loaded;
            return 1;
        }

        private static GameObject LoadFirstPrefab(params string[] relativePrefabPaths)
        {
            if (relativePrefabPaths == null || relativePrefabPaths.Length == 0)
            {
                return null;
            }

            var roots = new[] { CfxrPrefabsInThirdParty, CfxrPrefabsLegacyRoot };
            for (int i = 0; i < relativePrefabPaths.Length; i++)
            {
                var relative = relativePrefabPaths[i];
                if (string.IsNullOrWhiteSpace(relative))
                {
                    continue;
                }

                for (int r = 0; r < roots.Length; r++)
                {
                    var path = $"{roots[r]}/{relative}";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        return prefab;
                    }
                }
            }

            return null;
        }

        private static void WritePaletteDiagnostics(VfxPalette palette, int overrideCount)
        {
            try
            {
                var lines = new List<string>(32)
                {
                    $"overrideCount={overrideCount}",
                    $"rootExists:{CfxrPrefabsInThirdParty}={AssetDatabase.IsValidFolder(CfxrPrefabsInThirdParty)}",
                    $"rootExists:{CfxrPrefabsLegacyRoot}={AssetDatabase.IsValidFolder(CfxrPrefabsLegacyRoot)}",
                    $"CastPulse={AssetPathOf(palette.CastPulse)}",
                    $"HitSpark={AssetPathOf(palette.HitSpark)}",
                    $"BuffAura={AssetPathOf(palette.BuffAura)}",
                    $"DashTrail={AssetPathOf(palette.DashTrail)}",
                    $"ProjectileTrail={AssetPathOf(palette.ProjectileTrail)}",
                    $"ProjectileImpact={AssetPathOf(palette.ProjectileImpact)}",
                    $"SummonCircle={AssetPathOf(palette.SummonCircle)}",
                    $"ShieldPulse={AssetPathOf(palette.ShieldPulse)}",
                    $"RevealMark={AssetPathOf(palette.RevealMark)}",
                    $"WindWallCast={AssetPathOf(palette.WindWallCast)}",
                    $"ReturnFlash={AssetPathOf(palette.ReturnFlash)}",
                    $"SplitFlash={AssetPathOf(palette.SplitFlash)}",
                    $"MeleeSwing={AssetPathOf(palette.MeleeSwing)}"
                };

                System.IO.File.WriteAllLines("Temp/skill_cue_batch_binder_palette.txt", lines);
            }
            catch
            {
                // Ignore diagnostics write failures.
            }
        }

        private static string AssetPathOf(UnityEngine.Object asset)
        {
            return asset == null ? "<null>" : AssetDatabase.GetAssetPath(asset);
        }

        private static Material EnsureMaterial(string fileName, Color color)
        {
            var path = $"{RuntimeMaterialFolder}/{fileName}";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Standard");
                material = new Material(shader) { name = fileName.Replace(".mat", string.Empty) };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.4f);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject EnsureMeshPrefab(string prefabName, PrimitiveType primitiveType, Vector3 localScale, Material material)
        {
            var path = $"{RuntimeVariantsFolder}/{prefabName}";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return prefab;
            }

            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = prefabName.Replace(".prefab", string.Empty);
            go.transform.localScale = localScale;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject EnsureTrailPrefab(string prefabName, Material material)
        {
            var path = $"{RuntimeVariantsFolder}/{prefabName}";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return prefab;
            }

            var go = new GameObject(prefabName.Replace(".prefab", string.Empty));
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.minVertexDistance = 0.02f;
            trail.startWidth = 0.18f;
            trail.endWidth = 0f;
            trail.sharedMaterial = material;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.autodestruct = false;

            prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static AudioPalette LoadAudioPalette()
        {
            return new AudioPalette
            {
                Cast = LoadClip($"{KenneySfxBase}/switch-a.ogg"),
                Hit = LoadClip($"{KenneySfxBase}/tap-b.ogg"),
                Buff = LoadClip($"{KenneySfxBase}/switch-b.ogg")
            };
        }

        private static AudioClip LoadClip(string path)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static int EnsurePresentationSystemInScenes()
        {
            var scenePaths = new[] { SampleScenePath, TownScenePath };
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return 0;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var mounted = 0;
            try
            {
                for (int i = 0; i < scenePaths.Length; i++)
                {
                    var scenePath = scenePaths[i];
                    if (!System.IO.File.Exists(scenePath))
                    {
                        continue;
                    }

                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    if (!scene.IsValid())
                    {
                        continue;
                    }

                    if (EnsurePresentationSystemInLoadedScene())
                    {
                        mounted++;
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else if (!SceneManager.GetActiveScene().IsValid())
                {
                    // Batchmode may start with no loaded scene; keep editor state valid.
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }

            return mounted;
        }

        private static bool EnsurePresentationSystemInLoadedScene()
        {
            var changed = false;
            var system = UnityEngine.Object.FindFirstObjectByType<SkillPresentationSystem>(FindObjectsInactive.Include);
            if (system == null)
            {
                var root = new GameObject("SkillPresentation");
                system = root.AddComponent<SkillPresentationSystem>();
                changed = true;
            }

            if (system != null)
            {
                var eventHub = FindEventHubAsset();
                var so = new SerializedObject(system);
                var eventHubProp = so.FindProperty("eventHub");
                if (eventHubProp != null && eventHubProp.objectReferenceValue == null && eventHub != null)
                {
                    eventHubProp.objectReferenceValue = eventHub;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(system);
                    changed = true;
                }
            }

            return changed;
        }

        private static CombatEventHub FindEventHubAsset()
        {
            var guids = AssetDatabase.FindAssets("t:CombatEventHub");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var hub = AssetDatabase.LoadAssetAtPath<CombatEventHub>(path);
                if (hub != null)
                {
                    return hub;
                }
            }

            return null;
        }

        private static UnitDefinition FindPlayerUnitDefinition()
        {
            var guids = AssetDatabase.FindAssets("t:UnitDefinition");
            UnitDefinition fallback = null;
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
                if (unit == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = unit;
                }

                if (string.Equals(unit.Id, "Unit_Player", StringComparison.OrdinalIgnoreCase))
                {
                    return unit;
                }

                if (unit.name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fallback = unit;
                }
            }

            return fallback;
        }

        private static void EnsureFoldersAndPlaceholders()
        {
            EnsureFolder(VfxThirdPartyFolder);
            EnsureFolder(SfxThirdPartyFolder);
            EnsureFolder(RuntimeVariantsFolder);
            EnsureFolder(RuntimeMaterialFolder);

            EnsureReadme(
                $"{VfxThirdPartyFolder}/README.txt",
                "Place imported free third-party VFX assets here (Asset Store / CC0 only).");
            EnsureReadme(
                $"{SfxThirdPartyFolder}/README.txt",
                "Place imported free third-party SFX assets here (CC0 only).");
            EnsureReadme(
                $"{RuntimeVariantsFolder}/README.txt",
                "Auto-generated runtime variant VFX prefabs for cue mapping fallback.");
        }

        private static void EnsureReadme(string path, string content)
        {
            if (System.IO.File.Exists(path))
            {
                return;
            }

            System.IO.File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var segments = path.Split('/');
            if (segments.Length == 0)
            {
                return;
            }

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
