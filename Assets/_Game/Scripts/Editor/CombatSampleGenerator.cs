using System.Collections.Generic;
using CombatSystem.AI;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem.Editor
{
    public static class CombatSampleGenerator
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Combat/Generate Sample Content")]
        public static void GenerateSampleContent()
        {
            var folders = EnsureFolders();
            var assets = CreateAssets(folders);
            CreateSampleScene(assets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Combat sample content generated.");
        }

        private struct FolderPaths
        {
            public string Root;
            public string Stats;
            public string Tags;
            public string Targeting;
            public string Modifiers;
            public string Buffs;
            public string Effects;
            public string Skills;
            public string Units;
            public string AI;
            public string UI;
            public string Database;
            public string Runtime;
        }

        private struct SampleAssets
        {
            public StatDefinition MaxHealth;
            public StatDefinition HealthRegen;
            public StatDefinition MaxMana;
            public StatDefinition ManaRegen;

            public TagDefinition TagPlayer;
            public TagDefinition TagEnemy;
            public TagDefinition TagMagic;
            public TagDefinition TagFire;

            public TargetingDefinition TargetingSingleEnemy;
            public TargetingDefinition TargetingSelf;

            public ModifierDefinition ModifierSkillCost;
            public ModifierDefinition ModifierSkillCooldown;
            public ModifierDefinition ModifierFireBonus;
            public ModifierDefinition ModifierMaxMana;

            public BuffDefinition BuffBurn;
            public BuffDefinition BuffArcaneFocus;

            public EffectDefinition EffectBasicAttack;
            public EffectDefinition EffectFireball;
            public EffectDefinition EffectApplyBurn;
            public EffectDefinition EffectBurnTick;
            public EffectDefinition EffectApplyArcaneFocus;

            public SkillDefinition SkillBasicAttack;
            public SkillDefinition SkillFireball;
            public SkillDefinition SkillArcaneFocus;

            public UnitDefinition UnitPlayer;
            public UnitDefinition UnitEnemy;

            public AIProfile AIBasic;
            public HUDConfig HUDDefault;

            public GameDatabase Database;
            public CombatEventHub EventHub;
        }

        private static FolderPaths EnsureFolders()
        {
            EnsureFolder("Assets", "_Game");
            var root = EnsureFolder("Assets/_Game", "ScriptableObjects");

            return new FolderPaths
            {
                Root = root,
                Stats = EnsureFolder(root, "Stats"),
                Tags = EnsureFolder(root, "Tags"),
                Targeting = EnsureFolder(root, "Targeting"),
                Modifiers = EnsureFolder(root, "Modifiers"),
                Buffs = EnsureFolder(root, "Buffs"),
                Effects = EnsureFolder(root, "Effects"),
                Skills = EnsureFolder(root, "Skills"),
                Units = EnsureFolder(root, "Units"),
                AI = EnsureFolder(root, "AI"),
                UI = EnsureFolder(root, "UI"),
                Database = EnsureFolder(root, "Database"),
                Runtime = EnsureFolder(root, "Runtime")
            };
        }

        private static string EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }

            return path;
        }

        private static SampleAssets CreateAssets(FolderPaths folders)
        {
            var assets = new SampleAssets();

            AssetDatabase.StartAssetEditing();
            try
            {
                assets.MaxHealth = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_MaxHealth.asset");
                ConfigureStat(assets.MaxHealth, "Stat_MaxHealth", "Max Health", 100f, 0f, 9999f, true, false);

                assets.HealthRegen = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_HealthRegen.asset");
                ConfigureStat(assets.HealthRegen, "Stat_HealthRegen", "Health Regen", 1f, 0f, 9999f, false, false);

                assets.MaxMana = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_MaxMana.asset");
                ConfigureStat(assets.MaxMana, "Stat_MaxMana", "Max Mana", 100f, 0f, 9999f, true, false);

                assets.ManaRegen = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_ManaRegen.asset");
                ConfigureStat(assets.ManaRegen, "Stat_ManaRegen", "Mana Regen", 5f, 0f, 9999f, false, false);

                assets.TagPlayer = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Player.asset");
                ConfigureTag(assets.TagPlayer, "Tag_Player", "Player");

                assets.TagEnemy = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Enemy.asset");
                ConfigureTag(assets.TagEnemy, "Tag_Enemy", "Enemy");

                assets.TagMagic = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Magic.asset");
                ConfigureTag(assets.TagMagic, "Tag_Magic", "Magic");

                assets.TagFire = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Fire.asset");
                ConfigureTag(assets.TagFire, "Tag_Fire", "Fire");

                assets.TargetingSingleEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_SingleEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingSingleEnemy,
                    "Targeting_SingleEnemy",
                    "Single Enemy",
                    TargetingMode.Single,
                    TargetTeam.Enemy,
                    12f,
                    0f,
                    45f,
                    1,
                    TargetSort.Closest,
                    false,
                    null,
                    null);

                assets.TargetingSelf = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_Self.asset");
                ConfigureTargeting(
                    assets.TargetingSelf,
                    "Targeting_Self",
                    "Self",
                    TargetingMode.Self,
                    TargetTeam.Self,
                    0f,
                    0f,
                    0f,
                    1,
                    TargetSort.None,
                    true,
                    null,
                    null);

                assets.ModifierSkillCost = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_ArcaneFocus_Cost.asset");
                ConfigureModifier(
                    assets.ModifierSkillCost,
                    "Modifier_ArcaneFocus_Cost",
                    "Arcane Focus Skill Cost",
                    ModifierTargetType.Skill,
                    null,
                    ModifierParameters.SkillResourceCost,
                    ModifierOperation.Add,
                    -5f,
                    null,
                    new Object[] { assets.TagMagic },
                    null);

                assets.ModifierSkillCooldown = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_ArcaneFocus_Cooldown.asset");
                ConfigureModifier(
                    assets.ModifierSkillCooldown,
                    "Modifier_ArcaneFocus_Cooldown",
                    "Arcane Focus Cooldown",
                    ModifierTargetType.Skill,
                    null,
                    ModifierParameters.SkillCooldown,
                    ModifierOperation.Multiply,
                    -0.2f,
                    null,
                    new Object[] { assets.TagMagic },
                    null);

                assets.ModifierFireBonus = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_ArcaneFocus_FireBonus.asset");
                ConfigureModifier(
                    assets.ModifierFireBonus,
                    "Modifier_ArcaneFocus_FireBonus",
                    "Arcane Focus Fire Bonus",
                    ModifierTargetType.Effect,
                    null,
                    ModifierParameters.EffectValue,
                    ModifierOperation.Multiply,
                    0.5f,
                    null,
                    new Object[] { assets.TagFire },
                    null);

                assets.ModifierMaxMana = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_ArcaneFocus_MaxMana.asset");
                ConfigureModifier(
                    assets.ModifierMaxMana,
                    "Modifier_ArcaneFocus_MaxMana",
                    "Arcane Focus Max Mana",
                    ModifierTargetType.Stat,
                    assets.MaxMana,
                    null,
                    ModifierOperation.Add,
                    50f,
                    null,
                    null,
                    null);

                assets.BuffBurn = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_Burn.asset");
                assets.BuffArcaneFocus = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_ArcaneFocus.asset");

                assets.EffectBurnTick = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_BurnTickDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectBurnTick,
                    "Effect_BurnTickDamage",
                    "Burn Tick",
                    5f,
                    DamageType.Magical);

                assets.EffectApplyBurn = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyBurn.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyBurn,
                    "Effect_ApplyBurn",
                    "Apply Burn",
                    assets.BuffBurn);

                assets.EffectBasicAttack = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_BasicAttackDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectBasicAttack,
                    "Effect_BasicAttackDamage",
                    "Basic Attack Damage",
                    10f,
                    DamageType.Physical);

                assets.EffectFireball = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_FireballDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectFireball,
                    "Effect_FireballDamage",
                    "Fireball Damage",
                    25f,
                    DamageType.Magical);

                assets.EffectApplyArcaneFocus = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyArcaneFocus.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyArcaneFocus,
                    "Effect_ApplyArcaneFocus",
                    "Apply Arcane Focus",
                    assets.BuffArcaneFocus);

                ConfigureBuffBurn(assets.BuffBurn, assets.TagFire, assets.EffectBurnTick);
                ConfigureBuffArcaneFocus(
                    assets.BuffArcaneFocus,
                    assets.TagMagic,
                    new Object[] { assets.ModifierSkillCost, assets.ModifierSkillCooldown, assets.ModifierFireBonus, assets.ModifierMaxMana });

                assets.SkillBasicAttack = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_BasicAttack.asset");
                ConfigureSkill(
                    assets.SkillBasicAttack,
                    "Skill_BasicAttack",
                    "Basic Attack",
                    ResourceType.Mana,
                    0f,
                    1f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSingleEnemy,
                    null,
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectBasicAttack }
                    });

                assets.SkillFireball = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_Fireball.asset");
                ConfigureSkill(
                    assets.SkillFireball,
                    "Skill_Fireball",
                    "Fireball",
                    ResourceType.Mana,
                    10f,
                    3f,
                    0.2f,
                    0f,
                    false,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagMagic, assets.TagFire },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectFireball, assets.EffectApplyBurn }
                    });

                assets.SkillArcaneFocus = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_ArcaneFocus.asset");
                ConfigureSkill(
                    assets.SkillArcaneFocus,
                    "Skill_ArcaneFocus",
                    "Arcane Focus",
                    ResourceType.Mana,
                    15f,
                    8f,
                    0.1f,
                    0f,
                    true,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyArcaneFocus }
                    });

                assets.AIBasic = LoadOrCreate<AIProfile>($"{folders.AI}/AI_Basic.asset");
                ConfigureAIProfile(assets.AIBasic, "AI_Basic", "Basic AI", 12f, 2f, 0.5f);

                assets.UnitPlayer = LoadOrCreate<UnitDefinition>($"{folders.Units}/Unit_Player.asset");
                ConfigureUnit(
                    assets.UnitPlayer,
                    "Unit_Player",
                    "Player",
                    new Object[] { assets.TagPlayer },
                    new StatValueData[]
                    {
                        new StatValueData(assets.MaxHealth, 100f),
                        new StatValueData(assets.HealthRegen, 1f),
                        new StatValueData(assets.MaxMana, 100f),
                        new StatValueData(assets.ManaRegen, 5f)
                    },
                    assets.SkillBasicAttack,
                    new Object[] { assets.SkillFireball, assets.SkillArcaneFocus },
                    null);

                assets.UnitEnemy = LoadOrCreate<UnitDefinition>($"{folders.Units}/Unit_Enemy.asset");
                ConfigureUnit(
                    assets.UnitEnemy,
                    "Unit_Enemy",
                    "Enemy",
                    new Object[] { assets.TagEnemy },
                    new StatValueData[]
                    {
                        new StatValueData(assets.MaxHealth, 80f),
                        new StatValueData(assets.HealthRegen, 0f),
                        new StatValueData(assets.MaxMana, 30f),
                        new StatValueData(assets.ManaRegen, 1f)
                    },
                    assets.SkillBasicAttack,
                    null,
                    assets.AIBasic);

                assets.HUDDefault = LoadOrCreate<HUDConfig>($"{folders.UI}/HUD_Default.asset");
                ConfigureHUD(assets.HUDDefault, "HUD_Default", "Default HUD", 6, 12, true, true, true);

                assets.Database = LoadOrCreate<GameDatabase>($"{folders.Database}/GameDatabase.asset");
                ConfigureDatabase(assets);

                assets.EventHub = LoadOrCreate<CombatEventHub>($"{folders.Runtime}/CombatEventHub.asset");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return assets;
        }
        private static void CreateSampleScene(SampleAssets assets)
        {
            EnsureFolder("Assets", "Scenes");

            var existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath);
            var scene = existingScene != null
                ? EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            RemoveRootObject(scene, "CombatSystems");
            RemoveRootObject(scene, "Sample_Player");
            RemoveRootObject(scene, "Sample_Enemy");

            var combatSystems = new GameObject("CombatSystems");
            var targetingSystem = combatSystems.AddComponent<TargetingSystem>();
            var effectExecutor = combatSystems.AddComponent<EffectExecutor>();
            var projectilePool = combatSystems.AddComponent<ProjectilePool>();

            SetComponentReference(effectExecutor, "targetingSystem", targetingSystem);
            SetComponentReference(effectExecutor, "projectilePool", projectilePool);

            var player = CreateUnitPrimitive("Sample_Player", new Vector3(0f, 0f, 0f));
            var enemy = CreateUnitPrimitive("Sample_Enemy", new Vector3(3f, 0f, 0f));

            ConfigureUnitObject(player, assets.UnitPlayer, assets.EventHub, targetingSystem, effectExecutor, 1, assets.MaxHealth, assets.HealthRegen, assets.MaxMana, assets.ManaRegen);
            ConfigureUnitObject(enemy, assets.UnitEnemy, assets.EventHub, targetingSystem, effectExecutor, 2, assets.MaxHealth, assets.HealthRegen, assets.MaxMana, assets.ManaRegen);

            var enemyAI = enemy.AddComponent<CombatAIController>();
            SetComponentReference(enemyAI, "unitRoot", enemy.GetComponent<UnitRoot>());
            SetComponentReference(enemyAI, "skillUser", enemy.GetComponent<SkillUserComponent>());
            SetComponentReference(enemyAI, "health", enemy.GetComponent<HealthComponent>());
            SetComponentReference(enemyAI, "team", enemy.GetComponent<TeamComponent>());
            SetComponentReference(enemyAI, "targetingSystem", targetingSystem);
            SetComponentReference(enemyAI, "aiProfile", assets.AIBasic);
            SetComponentValue(enemyAI, "useNavMesh", false);
            SetComponentValue(enemyAI, "moveSpeed", 2.5f);

            var driver = player.AddComponent<SampleCombatDriver>();
            SetComponentReference(driver, "skillUser", player.GetComponent<SkillUserComponent>());
            SetComponentReference(driver, "target", enemy.transform);
            SetComponentReference(driver, "primarySkill", assets.SkillFireball);
            SetComponentReference(driver, "secondarySkill", assets.SkillArcaneFocus);
            SetComponentValue(driver, "autoCast", true);
            SetComponentValue(driver, "autoInterval", 2.5f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SampleScenePath);
        }

        private static void RemoveRootObject(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    Object.DestroyImmediate(roots[i]);
                    return;
                }
            }
        }

        private static GameObject CreateUnitPrimitive(string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            return go;
        }

        private static void ConfigureUnitObject(
            GameObject unitObject,
            UnitDefinition definition,
            CombatEventHub eventHub,
            TargetingSystem targetingSystem,
            EffectExecutor effectExecutor,
            int teamId,
            StatDefinition maxHealth,
            StatDefinition healthRegen,
            StatDefinition maxMana,
            StatDefinition manaRegen)
        {
            var unitRoot = unitObject.AddComponent<UnitRoot>();
            var stats = unitObject.AddComponent<StatsComponent>();
            var health = unitObject.AddComponent<HealthComponent>();
            var resource = unitObject.AddComponent<ResourceComponent>();
            var cooldown = unitObject.AddComponent<CooldownComponent>();
            var unitTags = unitObject.AddComponent<UnitTagsComponent>();
            var buffController = unitObject.AddComponent<BuffController>();
            var team = unitObject.AddComponent<TeamComponent>();
            var skillUser = unitObject.AddComponent<SkillUserComponent>();

            SetComponentReference(unitRoot, "unitDefinition", definition);
            SetComponentReference(unitRoot, "eventHub", eventHub);
            SetComponentReference(unitRoot, "stats", stats);
            SetComponentReference(unitRoot, "health", health);
            SetComponentReference(unitRoot, "resource", resource);
            SetComponentReference(unitRoot, "cooldown", cooldown);
            SetComponentReference(unitRoot, "unitTags", unitTags);
            SetComponentReference(unitRoot, "buffController", buffController);

            SetComponentReference(stats, "unitDefinition", definition);
            SetComponentReference(stats, "eventHub", eventHub);
            SetComponentReference(stats, "buffController", buffController);
            SetComponentReference(stats, "unitRoot", unitRoot);
            SetComponentReference(stats, "unitTags", unitTags);

            SetComponentReference(health, "eventHub", eventHub);
            SetComponentReference(health, "stats", stats);
            SetComponentReference(health, "maxHealthStat", maxHealth);
            SetComponentReference(health, "regenStat", healthRegen);

            SetComponentReference(resource, "eventHub", eventHub);
            SetComponentReference(resource, "stats", stats);
            SetComponentValue(resource, "resourceType", ResourceType.Mana);
            SetComponentReference(resource, "maxResourceStat", maxMana);
            SetComponentReference(resource, "regenStat", manaRegen);

            SetComponentReference(cooldown, "eventHub", eventHub);

            SetComponentReference(unitTags, "unitRoot", unitRoot);

            SetComponentReference(buffController, "unitRoot", unitRoot);
            SetComponentReference(buffController, "skillUser", skillUser);
            SetComponentReference(buffController, "effectExecutor", effectExecutor);
            SetComponentReference(buffController, "targetingSystem", targetingSystem);

            SetComponentReference(skillUser, "unitRoot", unitRoot);
            SetComponentReference(skillUser, "eventHub", eventHub);
            SetComponentReference(skillUser, "stats", stats);
            SetComponentReference(skillUser, "health", health);
            SetComponentReference(skillUser, "resource", resource);
            SetComponentReference(skillUser, "cooldown", cooldown);
            SetComponentReference(skillUser, "buffController", buffController);
            SetComponentReference(skillUser, "targetingSystem", targetingSystem);
            SetComponentReference(skillUser, "effectExecutor", effectExecutor);

            SetComponentValue(team, "teamId", teamId);
        }
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetDefinitionBase(SerializedObject so, string id, string displayName)
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
        }

        private static void ConfigureStat(
            StatDefinition stat,
            string id,
            string displayName,
            float defaultValue,
            float minValue,
            float maxValue,
            bool isInteger,
            bool isPercentage)
        {
            var so = new SerializedObject(stat);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("defaultValue").floatValue = defaultValue;
            so.FindProperty("minValue").floatValue = minValue;
            so.FindProperty("maxValue").floatValue = maxValue;
            so.FindProperty("isInteger").boolValue = isInteger;
            so.FindProperty("isPercentage").boolValue = isPercentage;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTag(TagDefinition tag, string id, string displayName)
        {
            var so = new SerializedObject(tag);
            SetDefinitionBase(so, id, displayName);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTargeting(
            TargetingDefinition targeting,
            string id,
            string displayName,
            TargetingMode mode,
            TargetTeam team,
            float range,
            float radius,
            float angle,
            int maxTargets,
            TargetSort sort,
            bool includeSelf,
            Object[] requiredTags,
            Object[] blockedTags)
        {
            var so = new SerializedObject(targeting);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.FindProperty("team").enumValueIndex = (int)team;
            so.FindProperty("range").floatValue = range;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("angle").floatValue = angle;
            so.FindProperty("maxTargets").intValue = maxTargets;
            so.FindProperty("sort").enumValueIndex = (int)sort;
            so.FindProperty("includeSelf").boolValue = includeSelf;
            SetObjectList(so.FindProperty("requiredTags"), requiredTags);
            SetObjectList(so.FindProperty("blockedTags"), blockedTags);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureModifier(
            ModifierDefinition modifier,
            string id,
            string displayName,
            ModifierTargetType targetType,
            StatDefinition stat,
            string parameterId,
            ModifierOperation operation,
            float value,
            ConditionDefinition condition,
            Object[] requiredTags,
            Object[] blockedTags)
        {
            var so = new SerializedObject(modifier);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("target").enumValueIndex = (int)targetType;
            so.FindProperty("stat").objectReferenceValue = stat;
            so.FindProperty("parameterId").stringValue = parameterId ?? string.Empty;
            so.FindProperty("operation").enumValueIndex = (int)operation;
            so.FindProperty("value").floatValue = value;
            so.FindProperty("condition").objectReferenceValue = condition;
            SetObjectList(so.FindProperty("requiredTags"), requiredTags);
            SetObjectList(so.FindProperty("blockedTags"), blockedTags);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectDamage(
            EffectDefinition effect,
            string id,
            string displayName,
            float value,
            DamageType damageType)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.Damage;
            so.FindProperty("value").floatValue = value;
            so.FindProperty("damageType").enumValueIndex = (int)damageType;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectApplyBuff(
            EffectDefinition effect,
            string id,
            string displayName,
            BuffDefinition buff)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.ApplyBuff;
            so.FindProperty("buff").objectReferenceValue = buff;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBuffBurn(BuffDefinition buff, TagDefinition tagFire, EffectDefinition burnTick)
        {
            var so = new SerializedObject(buff);
            SetDefinitionBase(so, "Buff_Burn", "Burn");
            so.FindProperty("isDebuff").boolValue = true;
            so.FindProperty("duration").floatValue = 5f;
            so.FindProperty("tickInterval").floatValue = 1f;
            so.FindProperty("stackingRule").enumValueIndex = (int)BuffStackingRule.Refresh;
            so.FindProperty("maxStacks").intValue = 3;
            SetObjectList(so.FindProperty("tags"), new Object[] { tagFire });
            SetObjectList(so.FindProperty("modifiers"), null);

            var triggers = so.FindProperty("triggers");
            triggers.arraySize = 1;
            var trigger = triggers.GetArrayElementAtIndex(0);
            trigger.FindPropertyRelative("triggerType").enumValueIndex = (int)BuffTriggerType.OnTick;
            trigger.FindPropertyRelative("chance").floatValue = 1f;
            trigger.FindPropertyRelative("condition").objectReferenceValue = null;
            SetObjectList(trigger.FindPropertyRelative("effects"), new Object[] { burnTick });

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBuffArcaneFocus(BuffDefinition buff, TagDefinition tagMagic, Object[] modifiers)
        {
            var so = new SerializedObject(buff);
            SetDefinitionBase(so, "Buff_ArcaneFocus", "Arcane Focus");
            so.FindProperty("isDebuff").boolValue = false;
            so.FindProperty("duration").floatValue = 8f;
            so.FindProperty("tickInterval").floatValue = 0f;
            so.FindProperty("stackingRule").enumValueIndex = (int)BuffStackingRule.Refresh;
            so.FindProperty("maxStacks").intValue = 1;
            SetObjectList(so.FindProperty("tags"), new Object[] { tagMagic });
            SetObjectList(so.FindProperty("modifiers"), modifiers);
            SetObjectList(so.FindProperty("triggers"), null);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct SkillStepData
        {
            public SkillStepTrigger Trigger;
            public float Delay;
            public ConditionDefinition Condition;
            public string AnimationTrigger;
            public Object VfxPrefab;
            public Object Sfx;
            public Object[] Effects;
        }

        private static void ConfigureSkill(
            SkillDefinition skill,
            string id,
            string displayName,
            ResourceType resourceType,
            float resourceCost,
            float cooldown,
            float castTime,
            float channelTime,
            bool canMoveWhileCasting,
            bool canRotateWhileCasting,
            TargetingDefinition targeting,
            Object[] tags,
            SkillStepData step)
        {
            var so = new SerializedObject(skill);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("resourceType").enumValueIndex = (int)resourceType;
            so.FindProperty("resourceCost").floatValue = resourceCost;
            so.FindProperty("cooldown").floatValue = cooldown;
            so.FindProperty("castTime").floatValue = castTime;
            so.FindProperty("channelTime").floatValue = channelTime;
            so.FindProperty("canMoveWhileCasting").boolValue = canMoveWhileCasting;
            so.FindProperty("canRotateWhileCasting").boolValue = canRotateWhileCasting;
            so.FindProperty("targeting").objectReferenceValue = targeting;
            SetObjectList(so.FindProperty("tags"), tags);

            var steps = so.FindProperty("steps");
            steps.arraySize = 1;
            var stepProp = steps.GetArrayElementAtIndex(0);
            stepProp.FindPropertyRelative("trigger").enumValueIndex = (int)step.Trigger;
            stepProp.FindPropertyRelative("delay").floatValue = step.Delay;
            stepProp.FindPropertyRelative("condition").objectReferenceValue = step.Condition;
            stepProp.FindPropertyRelative("animationTrigger").stringValue = step.AnimationTrigger ?? string.Empty;
            stepProp.FindPropertyRelative("vfxPrefab").objectReferenceValue = step.VfxPrefab;
            stepProp.FindPropertyRelative("sfx").objectReferenceValue = step.Sfx;
            SetObjectList(stepProp.FindPropertyRelative("effects"), step.Effects);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct StatValueData
        {
            public StatDefinition Stat;
            public float Value;

            public StatValueData(StatDefinition stat, float value)
            {
                Stat = stat;
                Value = value;
            }
        }

        private static void ConfigureUnit(
            UnitDefinition unit,
            string id,
            string displayName,
            Object[] tags,
            StatValueData[] baseStats,
            SkillDefinition basicAttack,
            Object[] startingSkills,
            AIProfile aiProfile)
        {
            var so = new SerializedObject(unit);
            SetDefinitionBase(so, id, displayName);
            SetObjectList(so.FindProperty("tags"), tags);

            var statsProp = so.FindProperty("baseStats");
            statsProp.arraySize = baseStats != null ? baseStats.Length : 0;
            for (int i = 0; i < statsProp.arraySize; i++)
            {
                var statEntry = statsProp.GetArrayElementAtIndex(i);
                statEntry.FindPropertyRelative("stat").objectReferenceValue = baseStats[i].Stat;
                statEntry.FindPropertyRelative("value").floatValue = baseStats[i].Value;
            }

            so.FindProperty("basicAttack").objectReferenceValue = basicAttack;
            SetObjectList(so.FindProperty("startingSkills"), startingSkills);
            so.FindProperty("aiProfile").objectReferenceValue = aiProfile;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAIProfile(
            AIProfile profile,
            string id,
            string displayName,
            float aggroRange,
            float attackRange,
            float thinkInterval)
        {
            var so = new SerializedObject(profile);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("aggroRange").floatValue = aggroRange;
            so.FindProperty("attackRange").floatValue = attackRange;
            so.FindProperty("thinkInterval").floatValue = thinkInterval;
            SetObjectList(so.FindProperty("skillRules"), null);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureHUD(
            HUDConfig hud,
            string id,
            string displayName,
            int maxSkillSlots,
            int maxBuffSlots,
            bool showCastBar,
            bool showCombatLog,
            bool showFloatingText)
        {
            var so = new SerializedObject(hud);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("maxSkillSlots").intValue = maxSkillSlots;
            so.FindProperty("maxBuffSlots").intValue = maxBuffSlots;
            so.FindProperty("showCastBar").boolValue = showCastBar;
            so.FindProperty("showCombatLog").boolValue = showCombatLog;
            so.FindProperty("showFloatingText").boolValue = showFloatingText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDatabase(SampleAssets assets)
        {
            var so = new SerializedObject(assets.Database);
            SetObjectList(so.FindProperty("stats"), new Object[] { assets.MaxHealth, assets.HealthRegen, assets.MaxMana, assets.ManaRegen });
            SetObjectList(so.FindProperty("tags"), new Object[] { assets.TagPlayer, assets.TagEnemy, assets.TagMagic, assets.TagFire });
            SetObjectList(so.FindProperty("units"), new Object[] { assets.UnitPlayer, assets.UnitEnemy });
            SetObjectList(so.FindProperty("skills"), new Object[] { assets.SkillBasicAttack, assets.SkillFireball, assets.SkillArcaneFocus });
            SetObjectList(so.FindProperty("buffs"), new Object[] { assets.BuffBurn, assets.BuffArcaneFocus });
            SetObjectList(so.FindProperty("effects"), new Object[] { assets.EffectBasicAttack, assets.EffectFireball, assets.EffectApplyBurn, assets.EffectBurnTick, assets.EffectApplyArcaneFocus });
            SetObjectList(so.FindProperty("conditions"), null);
            SetObjectList(so.FindProperty("modifiers"), new Object[] { assets.ModifierSkillCost, assets.ModifierSkillCooldown, assets.ModifierFireBonus, assets.ModifierMaxMana });
            SetObjectList(so.FindProperty("projectiles"), null);
            SetObjectList(so.FindProperty("targetings"), new Object[] { assets.TargetingSingleEnemy, assets.TargetingSelf });
            SetObjectList(so.FindProperty("aiProfiles"), new Object[] { assets.AIBasic });
            SetObjectList(so.FindProperty("hudConfigs"), new Object[] { assets.HUDDefault });
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectList(SerializedProperty property, Object[] items)
        {
            if (property == null)
            {
                return;
            }

            if (items == null || items.Length == 0)
            {
                property.arraySize = 0;
                return;
            }

            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        private static void SetComponentReference(Object component, string propertyName, Object value)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetComponentValue<T>(Object component, string propertyName, T value)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                return;
            }

            if (typeof(T) == typeof(bool))
            {
                prop.boolValue = (bool)(object)value;
            }
            else if (typeof(T) == typeof(int))
            {
                prop.intValue = (int)(object)value;
            }
            else if (typeof(T) == typeof(float))
            {
                prop.floatValue = (float)(object)value;
            }
            else if (typeof(T).IsEnum)
            {
                prop.enumValueIndex = (int)(object)value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
