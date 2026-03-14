using System.Collections.Generic;
using CombatSystem.AI;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Debugging;
using CombatSystem.Gameplay;
using CombatSystem.Input;
using CombatSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

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
            public string Projectiles;
            public string Skills;
            public string Units;
            public string Progression;
            public string AI;
            public string UI;
            public string Database;
            public string Runtime;
            public string Prefabs;
            public string Conditions;
        }

        private struct SampleAssets
        {
            public StatDefinition MaxHealth;
            public StatDefinition HealthRegen;
            public StatDefinition MaxMana;
            public StatDefinition ManaRegen;
            public StatDefinition MoveSpeed;
            public StatDefinition AttackPower;
            public StatDefinition AbilityPower;
            public StatDefinition AttackSpeed;
            public StatDefinition CritChance;
            public StatDefinition CritMultiplier;
            public StatDefinition Armor;
            public StatDefinition MagicResist;
            public StatDefinition ArmorPenFlat;
            public StatDefinition ArmorPenPercent;
            public StatDefinition MagicPenFlat;
            public StatDefinition MagicPenPercent;
            public StatDefinition AbilityHaste;
            public StatDefinition Lifesteal;
            public StatDefinition Omnivamp;
            public StatDefinition Tenacity;

            public TagDefinition TagPlayer;
            public TagDefinition TagEnemy;
            public TagDefinition TagMagic;
            public TagDefinition TagFire;
            public TagDefinition TagPhysical;
            public TagDefinition TagNature;

            public TargetingDefinition TargetingSingleEnemy;
            public TargetingDefinition TargetingBasicAttack;
            public TargetingDefinition TargetingSelf;
            public TargetingDefinition TargetingConeEnemy;
            public TargetingDefinition TargetingSphereEnemy;
            public TargetingDefinition TargetingChainEnemy;
            public TargetingDefinition TargetingRandomEnemy;
            public TargetingDefinition TargetingLineEnemy;
            public TargetingDefinition TargetingBoxEnemy;
            public TargetingDefinition TargetingAllySingle;
            public TargetingDefinition TargetingPointAny;
            public TargetingDefinition TargetingYasuoSingleEnemy;
            public TargetingDefinition TargetingYasuoQLineEnemy;
            public TargetingDefinition TargetingYasuoQEqSlashEnemy;
            public TargetingDefinition TargetingYasuoRSingleEnemy;
            public TargetingDefinition TargetingYasuoRAirborneCluster;

            public ModifierDefinition ModifierSkillCost;
            public ModifierDefinition ModifierSkillCooldown;
            public ModifierDefinition ModifierFireBonus;
            public ModifierDefinition ModifierMaxMana;
            public ModifierDefinition ModifierResistPhysical;
            public ModifierDefinition ModifierResistMagical;
            public ModifierDefinition ModifierQuickCastTime;
            public ModifierDefinition ModifierQuickChannelTime;
            public ModifierDefinition ModifierEffectDuration;
            public ModifierDefinition ModifierEffectInterval;

            public BuffDefinition BuffBurn;
            public BuffDefinition BuffArcaneFocus;
            public BuffDefinition BuffBleed;
            public BuffDefinition BuffPoison;
            public BuffDefinition BuffStoneSkin;
            public BuffDefinition BuffMagicWard;
            public BuffDefinition BuffQuickCast;
            public BuffDefinition BuffTimeWarp;
            public BuffDefinition BuffGhostStep;
            public BuffDefinition BuffIronShell;
            public BuffDefinition BuffYasuoDashMark;
            public BuffDefinition BuffYasuoEDashState;
            public BuffDefinition BuffYasuoAirborneTag;
            public BuffDefinition BuffYasuoLastBreathState;
            public BuffDefinition BuffYasuoLastBreathSuspend;

            public EffectDefinition EffectBasicAttack;
            public EffectDefinition EffectFireball;
            public EffectDefinition EffectApplyBurn;
            public EffectDefinition EffectBurnTick;
            public EffectDefinition EffectApplyArcaneFocus;
            public EffectDefinition EffectCleaveDamage;
            public EffectDefinition EffectChainDamage;
            public EffectDefinition EffectRandomShotDamage;
            public EffectDefinition EffectExecuteDamage;
            public EffectDefinition EffectHealSmall;
            public EffectDefinition EffectRestoreMana;
            public EffectDefinition EffectDash;
            public EffectDefinition EffectLineDamage;
            public EffectDefinition EffectBoxDamage;
            public EffectDefinition EffectBleedTick;
            public EffectDefinition EffectApplyBleed;
            public EffectDefinition EffectPoisonTick;
            public EffectDefinition EffectApplyPoison;
            public EffectDefinition EffectShockwaveDot;
            public EffectDefinition EffectArcaneBoltHit;
            public EffectDefinition EffectArcaneBoltProjectile;
            public EffectDefinition EffectSummonTotem;
            public EffectDefinition EffectTriggerArcaneFocus;
            public EffectDefinition EffectApplyStoneSkin;
            public EffectDefinition EffectApplyMagicWard;
            public EffectDefinition EffectApplyQuickCast;
            public EffectDefinition EffectApplyTimeWarp;
            public EffectDefinition EffectCombatStateAddUntargetable;
            public EffectDefinition EffectCombatStateRemoveUntargetable;
            public EffectDefinition EffectCombatStateAddInvulnerable;
            public EffectDefinition EffectCombatStateRemoveInvulnerable;
            public EffectDefinition EffectCombatStateGrantSpellShield;
            public EffectDefinition EffectApplyGhostStep;
            public EffectDefinition EffectApplyIronShell;
            public EffectDefinition EffectAmmoBurstDamage;
            public EffectDefinition EffectRecastLungeMove;
            public EffectDefinition EffectReturnBladeDamage;
            public EffectDefinition EffectReturnBladeProjectile;
            public EffectDefinition EffectShardVolleyDamage;
            public EffectDefinition EffectShardVolleyProjectile;
            public EffectDefinition EffectRevealBoltDamage;
            public EffectDefinition EffectWindWallSummon;
            public EffectDefinition EffectYasuoQThrustDamage;
            public EffectDefinition EffectYasuoQTornadoDamage;
            public EffectDefinition EffectYasuoQTornadoApplyAirborneTag;
            public EffectDefinition EffectYasuoQTornadoProjectile;
            public EffectDefinition EffectYasuoEDashMove;
            public EffectDefinition EffectYasuoEDashDamage;
            public EffectDefinition EffectYasuoEApplyDashMark;
            public EffectDefinition EffectYasuoEApplyDashState;
            public EffectDefinition EffectYasuoQEqSlashDamage;
            public EffectDefinition EffectYasuoQEqSlashEmpoweredDamage;
            public EffectDefinition EffectYasuoQEqSlashApplyAirborneTag;
            public EffectDefinition EffectYasuoRDashMove;
            public EffectDefinition EffectYasuoRApplyLastBreathState;
            public EffectDefinition EffectYasuoRApplyTargetSuspend;
            public EffectDefinition EffectYasuoRDamage;
            public EffectDefinition EffectYasuoRRemoveAirborneTag;

            public SkillDefinition SkillBasicAttack;
            public SkillDefinition SkillFireball;
            public SkillDefinition SkillArcaneFocus;
            public SkillDefinition SkillCleave;
            public SkillDefinition SkillChainLightning;
            public SkillDefinition SkillRandomShot;
            public SkillDefinition SkillExecute;
            public SkillDefinition SkillHeal;
            public SkillDefinition SkillDash;
            public SkillDefinition SkillLineStrike;
            public SkillDefinition SkillBoxField;
            public SkillDefinition SkillArcaneBolt;
            public SkillDefinition SkillShockwave;
            public SkillDefinition SkillBleedStrike;
            public SkillDefinition SkillPoisonDart;
            public SkillDefinition SkillStoneSkin;
            public SkillDefinition SkillMagicWard;
            public SkillDefinition SkillQuickCast;
            public SkillDefinition SkillTimeWarp;
            public SkillDefinition SkillManaSurge;
            public SkillDefinition SkillSummonTotem;
            public SkillDefinition SkillTriggerFocus;
            public SkillDefinition SkillGhostStep;
            public SkillDefinition SkillIronShell;
            public SkillDefinition SkillSpellAegis;
            public SkillDefinition SkillAmmoBurst;
            public SkillDefinition SkillRecastLunge;
            public SkillDefinition SkillReturnBlade;
            public SkillDefinition SkillShardVolley;
            public SkillDefinition SkillRevealBolt;
            public SkillDefinition SkillWindWall;
            public SkillDefinition SkillYasuoQ;
            public SkillDefinition SkillYasuoW;
            public SkillDefinition SkillYasuoE;
            public SkillDefinition SkillYasuoR;

            public UnitDefinition UnitPlayer;
            public UnitDefinition UnitEnemy;
            public UnitDefinition UnitEnemyHighHp;
            public UnitDefinition UnitSummonTotem;
            public UnitDefinition UnitPlayerYasuo;

            public AIProfile AIBasic;
            public HUDConfig HUDDefault;
            public ProgressionDefinition ProgressionDefault;

            public ProjectileDefinition ProjectileArcaneBolt;
            public ProjectileDefinition ProjectileReturnBlade;
            public ProjectileDefinition ProjectileShardVolley;
            public ProjectileDefinition ProjectileYasuoQTornado;
            public ConditionDefinition ConditionTargetLowHealth;
            public ConditionDefinition ConditionYasuoQPhase1;
            public ConditionDefinition ConditionYasuoQPhase2;
            public ConditionDefinition ConditionYasuoQPhase3;
            public ConditionDefinition ConditionYasuoQEqPhase1;
            public ConditionDefinition ConditionYasuoQEqPhase2;
            public ConditionDefinition ConditionYasuoQEqPhase3;
            public ConditionDefinition ConditionYasuoETargetNotDashed;
            public ConditionDefinition ConditionYasuoRTargetAirborne;

            public GameObject PrefabArcaneBolt;
            public GameObject PrefabSummonTotem;
            public GameObject PrefabWindWall;

            public GameDatabase Database;
            public CombatEventHub EventHub;
        }

        private static FolderPaths EnsureFolders()
        {
            EnsureFolder("Assets", "_Game");
            var root = EnsureFolder("Assets/_Game", "ScriptableObjects");
            var prefabsRoot = EnsureFolder("Assets/_Game", "Prefabs");
            var samplePrefabs = EnsureFolder(prefabsRoot, "Sample");

            return new FolderPaths
            {
                Root = root,
                Stats = EnsureFolder(root, "Stats"),
                Tags = EnsureFolder(root, "Tags"),
                Targeting = EnsureFolder(root, "Targeting"),
                Modifiers = EnsureFolder(root, "Modifiers"),
                Buffs = EnsureFolder(root, "Buffs"),
                Effects = EnsureFolder(root, "Effects"),
                Projectiles = EnsureFolder(root, "Projectiles"),
                Conditions = EnsureFolder(root, "Conditions"),
                Skills = EnsureFolder(root, "Skills"),
                Units = EnsureFolder(root, "Units"),
                Progression = EnsureFolder(root, "Progression"),
                AI = EnsureFolder(root, "AI"),
                UI = EnsureFolder(root, "UI"),
                Database = EnsureFolder(root, "Database"),
                Runtime = EnsureFolder(root, "Runtime"),
                Prefabs = samplePrefabs
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

            assets.PrefabArcaneBolt = CreateProjectilePrefab($"{folders.Prefabs}/Prefab_ArcaneBolt.prefab");
            assets.PrefabSummonTotem = CreateSummonPrefab($"{folders.Prefabs}/Prefab_SummonTotem.prefab");
            assets.PrefabWindWall = CreateWindWallPrefab($"{folders.Prefabs}/Prefab_WindWall.prefab");

            AssetDatabase.StartAssetEditing();
            try
            {
                assets.MaxHealth = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_MaxHealth.asset");
                ConfigureStat(assets.MaxHealth, "Stat_MaxHealth", "Max Health", 100f, 0f, 9999f, true, false);

                assets.HealthRegen = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_HealthRegen.asset");
                ConfigureStat(assets.HealthRegen, "Stat_HealthRegen", "生命回复", 1f, 0f, 9999f, false, false);

                assets.MaxMana = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_MaxMana.asset");
                ConfigureStat(assets.MaxMana, "Stat_MaxMana", "最大法力", 100f, 0f, 9999f, true, false);

                assets.ManaRegen = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_ManaRegen.asset");
                ConfigureStat(assets.ManaRegen, "Stat_ManaRegen", "法力回复", 5f, 0f, 9999f, false, false);

                assets.MoveSpeed = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_MoveSpeed.asset");
                ConfigureStat(assets.MoveSpeed, "Stat_MoveSpeed", "移动速度", 5f, 0f, 20f, false, false);

                assets.AttackPower = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_AttackPower.asset");
                ConfigureStat(assets.AttackPower, "Stat_AttackPower", "攻击力", 10f, 0f, 9999f, false, false);

                assets.AbilityPower = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_AbilityPower.asset");
                ConfigureStat(assets.AbilityPower, "Stat_AbilityPower", "法术强度", 0f, 0f, 9999f, false, false);

                assets.AttackSpeed = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_AttackSpeed.asset");
                ConfigureStat(assets.AttackSpeed, "Stat_AttackSpeed", "攻速加成", 0f, 0f, 5f, false, true);

                assets.CritChance = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_CritChance.asset");
                ConfigureStat(assets.CritChance, "Stat_CritChance", "暴击率", 0f, 0f, 1f, false, true);

                assets.CritMultiplier = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_CritMultiplier.asset");
                ConfigureStat(assets.CritMultiplier, "Stat_CritMultiplier", "暴击伤害", 2f, 1f, 5f, false, false);

                assets.Armor = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_Armor.asset");
                ConfigureStat(assets.Armor, "Stat_Armor", "护甲", 0f, 0f, 9999f, true, false);

                assets.MagicResist = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_MagicResist.asset");
                ConfigureStat(assets.MagicResist, "Stat_MagicResist", "魔抗", 0f, 0f, 9999f, true, false);

                assets.ArmorPenFlat = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_ArmorPenFlat.asset");
                ConfigureStat(assets.ArmorPenFlat, "Stat_ArmorPenFlat", "护甲穿透(固定)", 0f, 0f, 9999f, true, false);

                assets.ArmorPenPercent = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_ArmorPenPercent.asset");
                ConfigureStat(assets.ArmorPenPercent, "Stat_ArmorPenPercent", "护甲穿透(%)", 0f, 0f, 1f, false, true);

                assets.MagicPenFlat = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_MagicPenFlat.asset");
                ConfigureStat(assets.MagicPenFlat, "Stat_MagicPenFlat", "法术穿透(固定)", 0f, 0f, 9999f, true, false);

                assets.MagicPenPercent = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_MagicPenPercent.asset");
                ConfigureStat(assets.MagicPenPercent, "Stat_MagicPenPercent", "法术穿透(%)", 0f, 0f, 1f, false, true);

                assets.AbilityHaste = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_AbilityHaste.asset");
                ConfigureStat(assets.AbilityHaste, "Stat_AbilityHaste", "技能急速", 0f, 0f, 500f, true, false);

                assets.Lifesteal = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_Lifesteal.asset");
                ConfigureStat(assets.Lifesteal, "Stat_Lifesteal", "生命偷取", 0f, 0f, 1f, false, true);

                assets.Omnivamp = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_Omnivamp.asset");
                ConfigureStat(assets.Omnivamp, "Stat_Omnivamp", "全能吸血", 0f, 0f, 1f, false, true);

                assets.Tenacity = LoadOrCreate<StatDefinition>($"{folders.Stats}/Stat_Tenacity.asset");
                ConfigureStat(assets.Tenacity, "Stat_Tenacity", "韧性", 0f, 0f, 1f, false, true);

                assets.TagPlayer = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Player.asset");
                ConfigureTag(assets.TagPlayer, "Tag_Player", "Player");

                assets.TagEnemy = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Enemy.asset");
                ConfigureTag(assets.TagEnemy, "Tag_Enemy", "Enemy");

                assets.TagMagic = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Magic.asset");
                ConfigureTag(assets.TagMagic, "Tag_Magic", "Magic");

                assets.TagFire = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Fire.asset");
                ConfigureTag(assets.TagFire, "Tag_Fire", "Fire");

                assets.TagPhysical = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Physical.asset");
                ConfigureTag(assets.TagPhysical, "Tag_Physical", "Physical");

                assets.TagNature = LoadOrCreate<TagDefinition>($"{folders.Tags}/Tag_Nature.asset");
                ConfigureTag(assets.TagNature, "Tag_Nature", "Nature");

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

                assets.TargetingBasicAttack = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_BasicAttack.asset");
                ConfigureTargeting(
                    assets.TargetingBasicAttack,
                    "Targeting_BasicAttack",
                    "Basic Attack",
                    TargetingMode.Single,
                    TargetTeam.Enemy,
                    2f,
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

                assets.TargetingConeEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_ConeEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingConeEnemy,
                    "Targeting_ConeEnemy",
                    "Cone Enemy",
                    TargetingMode.Cone,
                    TargetTeam.Enemy,
                    6f,
                    0f,
                    60f,
                    3,
                    TargetSort.Closest,
                    false,
                    null,
                    null);

                assets.TargetingSphereEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_SphereEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingSphereEnemy,
                    "Targeting_SphereEnemy",
                    "Sphere Enemy",
                    TargetingMode.Sphere,
                    TargetTeam.Enemy,
                    0f,
                    4f,
                    0f,
                    6,
                    TargetSort.Closest,
                    false,
                    null,
                    null);

                assets.TargetingChainEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_ChainEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingChainEnemy,
                    "Targeting_ChainEnemy",
                    "Chain Enemy",
                    TargetingMode.Chain,
                    TargetTeam.Enemy,
                    8f,
                    0f,
                    0f,
                    4,
                    TargetSort.Closest,
                    false,
                    null,
                    null);

                assets.TargetingRandomEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_RandomEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingRandomEnemy,
                    "Targeting_RandomEnemy",
                    "Random Enemy",
                    TargetingMode.Random,
                    TargetTeam.Enemy,
                    6f,
                    0f,
                    0f,
                    2,
                    TargetSort.Random,
                    false,
                    null,
                    null);

                assets.TargetingLineEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_LineEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingLineEnemy,
                    "Targeting_LineEnemy",
                    "Line Enemy",
                    TargetingMode.Line,
                    TargetTeam.Enemy,
                    8f,
                    1.2f,
                    0f,
                    6,
                    TargetSort.Closest,
                    false,
                    null,
                    null,
                    TargetingOrigin.Caster,
                    true);

                assets.TargetingBoxEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_BoxEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingBoxEnemy,
                    "Targeting_BoxEnemy",
                    "Box Enemy",
                    TargetingMode.Box,
                    TargetTeam.Enemy,
                    6f,
                    2f,
                    0f,
                    6,
                    TargetSort.Closest,
                    false,
                    null,
                    null,
                    TargetingOrigin.TargetPoint,
                    true);

                assets.TargetingAllySingle = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_AllySingle.asset");
                ConfigureTargeting(
                    assets.TargetingAllySingle,
                    "Targeting_AllySingle",
                    "Single Ally",
                    TargetingMode.Single,
                    TargetTeam.Ally,
                    10f,
                    0f,
                    0f,
                    1,
                    TargetSort.Closest,
                    true,
                    null,
                    null);

                assets.TargetingPointAny = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_PointAny.asset");
                ConfigureTargeting(
                    assets.TargetingPointAny,
                    "Targeting_PointAny",
                    "Point Any",
                    TargetingMode.Sphere,
                    TargetTeam.Any,
                    8f,
                    0.5f,
                    0f,
                    1,
                    TargetSort.None,
                    false,
                    null,
                    null,
                    TargetingOrigin.TargetPoint,
                    true,
                    HitValidationPolicy.None);

                assets.TargetingYasuoSingleEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_YasuoSingleEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingYasuoSingleEnemy,
                    "Targeting_YasuoSingleEnemy",
                    "Yasuo Single Enemy",
                    TargetingMode.Single,
                    TargetTeam.Enemy,
                    5.5f,
                    0f,
                    0f,
                    1,
                    TargetSort.Closest,
                    false,
                    null,
                    null,
                    TargetingOrigin.Caster,
                    false,
                    HitValidationPolicy.AliveOnly,
                    -1,
                    1.5f,
                    true);

                assets.TargetingYasuoQLineEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_YasuoQ_LineEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingYasuoQLineEnemy,
                    "Targeting_YasuoQ_LineEnemy",
                    "Yasuo Q Line Enemy",
                    TargetingMode.Line,
                    TargetTeam.Enemy,
                    6.5f,
                    0.8f,
                    0f,
                    6,
                    TargetSort.Closest,
                    false,
                    null,
                    null,
                    TargetingOrigin.Caster,
                    true,
                    HitValidationPolicy.AliveOnly);

                assets.TargetingYasuoQEqSlashEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_YasuoQ_EQSlashEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingYasuoQEqSlashEnemy,
                    "Targeting_YasuoQ_EQSlashEnemy",
                    "Yasuo EQ Slash Enemy",
                    TargetingMode.Sphere,
                    TargetTeam.Enemy,
                    0f,
                    2.4f,
                    0f,
                    6,
                    TargetSort.Closest,
                    false,
                    null,
                    null,
                    TargetingOrigin.Caster,
                    false,
                    HitValidationPolicy.AliveOnly);

                assets.TargetingYasuoRSingleEnemy = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_YasuoR_SingleEnemy.asset");
                ConfigureTargeting(
                    assets.TargetingYasuoRSingleEnemy,
                    "Targeting_YasuoR_SingleEnemy",
                    "Yasuo R Single Enemy",
                    TargetingMode.Single,
                    TargetTeam.Enemy,
                    12f,
                    0f,
                    0f,
                    1,
                    TargetSort.Closest,
                    false,
                    null,
                    null,
                    TargetingOrigin.Caster,
                    false,
                    HitValidationPolicy.AliveOnly,
                    -1,
                    1.5f,
                    false,
                    true);

                assets.TargetingYasuoRAirborneCluster = LoadOrCreate<TargetingDefinition>($"{folders.Targeting}/Targeting_YasuoR_AirborneCluster.asset");
                ConfigureTargeting(
                    assets.TargetingYasuoRAirborneCluster,
                    "Targeting_YasuoR_AirborneCluster",
                    "Yasuo R Airborne Cluster",
                    TargetingMode.Sphere,
                    TargetTeam.Enemy,
                    0f,
                    2.75f,
                    0f,
                    3,
                    TargetSort.Closest,
                    false,
                    null,
                    null,
                    TargetingOrigin.TargetPoint,
                    false,
                    HitValidationPolicy.AliveOnly);

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

                assets.ModifierResistPhysical = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_Resist_Physical.asset");
                ConfigureModifier(
                    assets.ModifierResistPhysical,
                    "Modifier_Resist_Physical",
                    "Physical Resistance",
                    ModifierTargetType.Effect,
                    null,
                    ModifierParameters.EffectResistancePhysical,
                    ModifierOperation.Add,
                    50f,
                    null,
                    new Object[] { assets.TagPhysical },
                    null,
                    ModifierScope.Target);

                assets.ModifierResistMagical = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_Resist_Magical.asset");
                ConfigureModifier(
                    assets.ModifierResistMagical,
                    "Modifier_Resist_Magical",
                    "Magical Resistance",
                    ModifierTargetType.Effect,
                    null,
                    ModifierParameters.EffectResistanceMagical,
                    ModifierOperation.Add,
                    50f,
                    null,
                    new Object[] { assets.TagMagic },
                    null,
                    ModifierScope.Target);

                assets.ModifierQuickCastTime = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_QuickCast_CastTime.asset");
                ConfigureModifier(
                    assets.ModifierQuickCastTime,
                    "Modifier_QuickCast_CastTime",
                    "Quick Cast Time",
                    ModifierTargetType.Skill,
                    null,
                    ModifierParameters.SkillCastTime,
                    ModifierOperation.Multiply,
                    -0.4f,
                    null,
                    new Object[] { assets.TagMagic },
                    null,
                    ModifierScope.Caster);

                assets.ModifierQuickChannelTime = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_QuickCast_ChannelTime.asset");
                ConfigureModifier(
                    assets.ModifierQuickChannelTime,
                    "Modifier_QuickCast_ChannelTime",
                    "Quick Channel Time",
                    ModifierTargetType.Skill,
                    null,
                    ModifierParameters.SkillChannelTime,
                    ModifierOperation.Multiply,
                    -0.3f,
                    null,
                    new Object[] { assets.TagMagic },
                    null,
                    ModifierScope.Caster);

                assets.ModifierEffectDuration = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_TimeWarp_Duration.asset");
                ConfigureModifier(
                    assets.ModifierEffectDuration,
                    "Modifier_TimeWarp_Duration",
                    "Time Warp Duration",
                    ModifierTargetType.Effect,
                    null,
                    ModifierParameters.EffectDuration,
                    ModifierOperation.Add,
                    2f,
                    null,
                    new Object[] { assets.TagNature },
                    null,
                    ModifierScope.Caster);

                assets.ModifierEffectInterval = LoadOrCreate<ModifierDefinition>($"{folders.Modifiers}/Modifier_TimeWarp_Interval.asset");
                ConfigureModifier(
                    assets.ModifierEffectInterval,
                    "Modifier_TimeWarp_Interval",
                    "Time Warp Interval",
                    ModifierTargetType.Effect,
                    null,
                    ModifierParameters.EffectInterval,
                    ModifierOperation.Multiply,
                    -0.3f,
                    null,
                    new Object[] { assets.TagNature },
                    null,
                    ModifierScope.Caster);

                assets.BuffBurn = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_Burn.asset");
                assets.BuffArcaneFocus = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_ArcaneFocus.asset");
                assets.BuffBleed = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_Bleed.asset");
                assets.BuffPoison = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_Poison.asset");
                assets.BuffStoneSkin = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_StoneSkin.asset");
                assets.BuffMagicWard = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_MagicWard.asset");
                assets.BuffQuickCast = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_QuickCast.asset");
                assets.BuffTimeWarp = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_TimeWarp.asset");
                assets.BuffGhostStep = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_GhostStep.asset");
                assets.BuffIronShell = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_IronShell.asset");
                assets.BuffYasuoDashMark = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_YasuoDashMark.asset");
                assets.BuffYasuoEDashState = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_YasuoEDashState.asset");
                assets.BuffYasuoAirborneTag = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_YasuoAirborneTag.asset");
                assets.BuffYasuoLastBreathState = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_YasuoLastBreathState.asset");
                assets.BuffYasuoLastBreathSuspend = LoadOrCreate<BuffDefinition>($"{folders.Buffs}/Buff_YasuoLastBreathSuspend.asset");

                assets.ProjectileArcaneBolt = LoadOrCreate<ProjectileDefinition>($"{folders.Projectiles}/Projectile_ArcaneBolt.asset");
                assets.ProjectileReturnBlade = LoadOrCreate<ProjectileDefinition>($"{folders.Projectiles}/Projectile_ReturnBlade.asset");
                assets.ProjectileShardVolley = LoadOrCreate<ProjectileDefinition>($"{folders.Projectiles}/Projectile_ShardVolley.asset");
                assets.ProjectileYasuoQTornado = LoadOrCreate<ProjectileDefinition>($"{folders.Projectiles}/Projectile_YasuoQTornado.asset");
                assets.UnitSummonTotem = LoadOrCreate<UnitDefinition>($"{folders.Units}/Unit_SummonTotem.asset");
                assets.UnitPlayerYasuo = LoadOrCreate<UnitDefinition>($"{folders.Units}/Unit_PlayerYasuo.asset");
                assets.ConditionTargetLowHealth = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_TargetLowHealth.asset");
                assets.ConditionYasuoQPhase1 = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_YasuoQ_Phase1.asset");
                assets.ConditionYasuoQPhase2 = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_YasuoQ_Phase2.asset");
                assets.ConditionYasuoQPhase3 = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_YasuoQ_Phase3.asset");
                assets.ConditionYasuoQEqPhase1 = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_YasuoQ_EQ_Phase1.asset");
                assets.ConditionYasuoQEqPhase2 = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_YasuoQ_EQ_Phase2.asset");
                assets.ConditionYasuoQEqPhase3 = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_YasuoQ_EQ_Phase3.asset");
                assets.ConditionYasuoETargetNotDashed = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_YasuoE_TargetNotDashed.asset");
                assets.ConditionYasuoRTargetAirborne = LoadOrCreate<ConditionDefinition>($"{folders.Conditions}/Condition_YasuoR_TargetAirborne.asset");

                assets.EffectBurnTick = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_BurnTickDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectBurnTick,
                    "Effect_BurnTickDamage",
                    "Burn Tick",
                    5f,
                    DamageType.Magical,
                    0f,
                    0f,
                    assets.AbilityPower,
                    0.4f);

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
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier);

                assets.EffectFireball = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_FireballDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectFireball,
                    "Effect_FireballDamage",
                    "Fireball Damage",
                    25f,
                    DamageType.Magical,
                    0f,
                    0f,
                    assets.AbilityPower,
                    1f);

                assets.EffectApplyArcaneFocus = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyArcaneFocus.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyArcaneFocus,
                    "Effect_ApplyArcaneFocus",
                    "Apply Arcane Focus",
                    assets.BuffArcaneFocus);

                assets.EffectCleaveDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_CleaveDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectCleaveDamage,
                    "Effect_CleaveDamage",
                    "Cleave Damage",
                    18f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier);

                assets.EffectChainDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ChainLightningDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectChainDamage,
                    "Effect_ChainLightningDamage",
                    "Chain Lightning Damage",
                    12f,
                    DamageType.Magical,
                    0f,
                    0f,
                    assets.AbilityPower,
                    1f);

                assets.EffectRandomShotDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_RandomShotDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectRandomShotDamage,
                    "Effect_RandomShotDamage",
                    "Random Shot Damage",
                    15f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier);

                assets.EffectExecuteDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ExecuteDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectExecuteDamage,
                    "Effect_ExecuteDamage",
                    "Execute Damage",
                    35f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier);

                assets.EffectHealSmall = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_HealSmall.asset");
                ConfigureEffectHeal(
                    assets.EffectHealSmall,
                    "Effect_HealSmall",
                    "Heal Small",
                    25f);

                assets.EffectRestoreMana = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_RestoreMana.asset");
                ConfigureEffectResource(
                    assets.EffectRestoreMana,
                    "Effect_RestoreMana",
                    "Restore Mana",
                    20f,
                    ResourceType.Mana);

                assets.EffectDash = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_Dash.asset");
                ConfigureEffectMove(
                    assets.EffectDash,
                    "Effect_Dash",
                    "Dash",
                    MoveStyle.Dash,
                    4f,
                    10f);

                assets.EffectLineDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_LineStrikeDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectLineDamage,
                    "Effect_LineStrikeDamage",
                    "Line Strike Damage",
                    16f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier);

                assets.EffectBoxDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_BoxFieldDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectBoxDamage,
                    "Effect_BoxFieldDamage",
                    "Box Field Damage",
                    18f,
                    DamageType.Magical,
                    0f,
                    0f,
                    assets.AbilityPower,
                    1f);

                assets.EffectBleedTick = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_BleedTickDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectBleedTick,
                    "Effect_BleedTickDamage",
                    "Bleed Tick",
                    4f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    0.4f);

                assets.EffectApplyBleed = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyBleed.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyBleed,
                    "Effect_ApplyBleed",
                    "Apply Bleed",
                    assets.BuffBleed);

                assets.EffectPoisonTick = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_PoisonTickDamage.asset");
                ConfigureEffectDamage(
                    assets.EffectPoisonTick,
                    "Effect_PoisonTickDamage",
                    "Poison Tick",
                    3f,
                    DamageType.Magical,
                    0f,
                    0f,
                    assets.AbilityPower,
                    0.4f);

                assets.EffectApplyPoison = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyPoison.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyPoison,
                    "Effect_ApplyPoison",
                    "Apply Poison",
                    assets.BuffPoison);

                assets.EffectShockwaveDot = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ShockwaveDot.asset");
                ConfigureEffectDamage(
                    assets.EffectShockwaveDot,
                    "Effect_ShockwaveDot",
                    "Shockwave DOT",
                    6f,
                    DamageType.Magical,
                    6f,
                    2f,
                    assets.AbilityPower,
                    0.4f);

                assets.EffectArcaneBoltHit = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ArcaneBoltHit.asset");
                ConfigureEffectDamage(
                    assets.EffectArcaneBoltHit,
                    "Effect_ArcaneBoltHit",
                    "Arcane Bolt Hit",
                    16f,
                    DamageType.Magical,
                    0f,
                    0f,
                    assets.AbilityPower,
                    1f);

                assets.EffectArcaneBoltProjectile = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ArcaneBoltProjectile.asset");
                ConfigureEffectProjectile(
                    assets.EffectArcaneBoltProjectile,
                    "Effect_ArcaneBoltProjectile",
                    "Arcane Bolt Projectile",
                    assets.ProjectileArcaneBolt);

                assets.EffectSummonTotem = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_SummonTotem.asset");
                ConfigureEffectSummon(
                    assets.EffectSummonTotem,
                    "Effect_SummonTotem",
                    "Summon Totem",
                    assets.UnitSummonTotem,
                    assets.PrefabSummonTotem);

                assets.EffectApplyStoneSkin = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyStoneSkin.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyStoneSkin,
                    "Effect_ApplyStoneSkin",
                    "Apply Stone Skin",
                    assets.BuffStoneSkin);

                assets.EffectApplyMagicWard = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyMagicWard.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyMagicWard,
                    "Effect_ApplyMagicWard",
                    "Apply Magic Ward",
                    assets.BuffMagicWard);

                assets.EffectApplyQuickCast = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyQuickCast.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyQuickCast,
                    "Effect_ApplyQuickCast",
                    "Apply Quick Cast",
                    assets.BuffQuickCast);

                assets.EffectApplyTimeWarp = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyTimeWarp.asset");
                ConfigureEffectApplyBuff(
                    assets.EffectApplyTimeWarp,
                    "Effect_ApplyTimeWarp",
                    "Apply Time Warp",
                    assets.BuffTimeWarp);

                assets.EffectTriggerArcaneFocus = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_TriggerArcaneFocus.asset");
                assets.EffectCombatStateAddUntargetable = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_CombatState_Add_Untargetable.asset");
                assets.EffectCombatStateRemoveUntargetable = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_CombatState_Remove_Untargetable.asset");
                assets.EffectCombatStateAddInvulnerable = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_CombatState_Add_Invulnerable.asset");
                assets.EffectCombatStateRemoveInvulnerable = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_CombatState_Remove_Invulnerable.asset");
                assets.EffectCombatStateGrantSpellShield = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_CombatState_Grant_SpellShield.asset");
                assets.EffectApplyGhostStep = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyGhostStep.asset");
                assets.EffectApplyIronShell = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ApplyIronShell.asset");
                assets.EffectAmmoBurstDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_AmmoBurstDamage.asset");
                assets.EffectRecastLungeMove = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_RecastLungeMove.asset");
                assets.EffectReturnBladeDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ReturnBladeDamage.asset");
                assets.EffectReturnBladeProjectile = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ReturnBladeProjectile.asset");
                assets.EffectShardVolleyDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ShardVolleyDamage.asset");
                assets.EffectShardVolleyProjectile = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_ShardVolleyProjectile.asset");
                assets.EffectRevealBoltDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_RevealBoltDamage.asset");
                assets.EffectWindWallSummon = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_WindWallSummon.asset");
                assets.EffectYasuoQThrustDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoQ_ThrustDamage.asset");
                assets.EffectYasuoQTornadoDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoQ_TornadoDamage.asset");
                assets.EffectYasuoQTornadoApplyAirborneTag = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoQ_TornadoApplyAirborneTag.asset");
                assets.EffectYasuoQTornadoProjectile = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoQ_TornadoProjectile.asset");
                assets.EffectYasuoEDashMove = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoE_DashMove.asset");
                assets.EffectYasuoEDashDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoE_DashDamage.asset");
                assets.EffectYasuoEApplyDashMark = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoE_ApplyDashMark.asset");
                assets.EffectYasuoEApplyDashState = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoE_ApplyDashState.asset");
                assets.EffectYasuoQEqSlashDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoQ_EQSlashDamage.asset");
                assets.EffectYasuoQEqSlashEmpoweredDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoQ_EQSlashEmpoweredDamage.asset");
                assets.EffectYasuoQEqSlashApplyAirborneTag = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoQ_EQSlashApplyAirborneTag.asset");
                assets.EffectYasuoRDashMove = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoR_DashMove.asset");
                assets.EffectYasuoRApplyLastBreathState = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoR_ApplyLastBreathState.asset");
                assets.EffectYasuoRApplyTargetSuspend = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoR_ApplyTargetSuspend.asset");
                assets.EffectYasuoRDamage = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoR_Damage.asset");
                assets.EffectYasuoRRemoveAirborneTag = LoadOrCreate<EffectDefinition>($"{folders.Effects}/Effect_YasuoR_RemoveAirborneTag.asset");

                ConfigureProjectile(
                    assets.ProjectileArcaneBolt,
                    "Projectile_ArcaneBolt",
                    "Arcane Bolt",
                    assets.PrefabArcaneBolt,
                    12f,
                    3f,
                    0.2f,
                    false,
                    0f,
                    false,
                    1,
                    new Object[] { assets.EffectArcaneBoltHit });

                ConfigureEffectCombatState(
                    assets.EffectCombatStateAddUntargetable,
                    "Effect_CombatState_Add_Untargetable",
                    "Add Untargetable",
                    CombatStateEffectMode.AddFlags,
                    CombatStateFlags.Untargetable);

                ConfigureEffectCombatState(
                    assets.EffectCombatStateRemoveUntargetable,
                    "Effect_CombatState_Remove_Untargetable",
                    "Remove Untargetable",
                    CombatStateEffectMode.RemoveFlags,
                    CombatStateFlags.Untargetable);

                ConfigureEffectCombatState(
                    assets.EffectCombatStateAddInvulnerable,
                    "Effect_CombatState_Add_Invulnerable",
                    "Add Invulnerable",
                    CombatStateEffectMode.AddFlags,
                    CombatStateFlags.Invulnerable);

                ConfigureEffectCombatState(
                    assets.EffectCombatStateRemoveInvulnerable,
                    "Effect_CombatState_Remove_Invulnerable",
                    "Remove Invulnerable",
                    CombatStateEffectMode.RemoveFlags,
                    CombatStateFlags.Invulnerable);

                ConfigureEffectCombatState(
                    assets.EffectCombatStateGrantSpellShield,
                    "Effect_CombatState_Grant_SpellShield",
                    "Grant Spell Shield",
                    CombatStateEffectMode.GrantSpellShield,
                    CombatStateFlags.None,
                    1);

                ConfigureEffectApplyBuff(
                    assets.EffectApplyGhostStep,
                    "Effect_ApplyGhostStep",
                    "Apply Ghost Step",
                    assets.BuffGhostStep);

                ConfigureEffectApplyBuff(
                    assets.EffectApplyIronShell,
                    "Effect_ApplyIronShell",
                    "Apply Iron Shell",
                    assets.BuffIronShell);

                ConfigureEffectDamage(
                    assets.EffectAmmoBurstDamage,
                    "Effect_AmmoBurstDamage",
                    "Ammo Burst Damage",
                    22f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier);

                ConfigureEffectMove(
                    assets.EffectRecastLungeMove,
                    "Effect_RecastLungeMove",
                    "Recast Lunge Move",
                    MoveStyle.Dash,
                    3.5f,
                    14f);

                ConfigureEffectDamage(
                    assets.EffectReturnBladeDamage,
                    "Effect_ReturnBladeDamage",
                    "Return Blade Damage",
                    20f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier);

                ConfigureEffectProjectile(
                    assets.EffectReturnBladeProjectile,
                    "Effect_ReturnBladeProjectile",
                    "Return Blade Projectile",
                    assets.ProjectileReturnBlade);

                ConfigureEffectDamage(
                    assets.EffectShardVolleyDamage,
                    "Effect_ShardVolleyDamage",
                    "Shard Volley Damage",
                    12f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    0.8f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier);

                ConfigureEffectProjectile(
                    assets.EffectShardVolleyProjectile,
                    "Effect_ShardVolleyProjectile",
                    "Shard Volley Projectile",
                    assets.ProjectileShardVolley);

                ConfigureEffectDamage(
                    assets.EffectRevealBoltDamage,
                    "Effect_RevealBoltDamage",
                    "Reveal Bolt Damage",
                    14f,
                    DamageType.Magical,
                    0f,
                    0f,
                    assets.AbilityPower,
                    0.8f,
                    false,
                    null,
                    null,
                    false,
                    true,
                    2f);

                ConfigureEffectSummon(
                    assets.EffectWindWallSummon,
                    "Effect_WindWallSummon",
                    "Wind Wall Summon",
                    null,
                    assets.PrefabWindWall);

                ConfigureEffectDamage(
                    assets.EffectYasuoQThrustDamage,
                    "Effect_YasuoQ_ThrustDamage",
                    "Yasuo Q Thrust Damage",
                    22f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier,
                    true);

                ConfigureEffectDamage(
                    assets.EffectYasuoQTornadoDamage,
                    "Effect_YasuoQ_TornadoDamage",
                    "Yasuo Q Tornado Damage",
                    26f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1.1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier,
                    true);

                ConfigureEffectApplyBuff(
                    assets.EffectYasuoQTornadoApplyAirborneTag,
                    "Effect_YasuoQ_TornadoApplyAirborneTag",
                    "Yasuo Q Tornado Apply Airborne",
                    assets.BuffYasuoAirborneTag);

                ConfigureEffectProjectile(
                    assets.EffectYasuoQTornadoProjectile,
                    "Effect_YasuoQ_TornadoProjectile",
                    "Yasuo Q Tornado Projectile",
                    assets.ProjectileYasuoQTornado);

                ConfigureEffectMove(
                    assets.EffectYasuoEDashMove,
                    "Effect_YasuoE_DashMove",
                    "Yasuo E Dash Move",
                    MoveStyle.Dash,
                    3f,
                    15f,
                    assets.TargetingSelf);

                ConfigureEffectDamage(
                    assets.EffectYasuoEDashDamage,
                    "Effect_YasuoE_DashDamage",
                    "Yasuo E Dash Damage",
                    14f,
                    DamageType.Magical,
                    0f,
                    0f,
                    assets.AttackPower,
                    0.55f,
                    false,
                    null,
                    null,
                    true);

                ConfigureEffectApplyBuff(
                    assets.EffectYasuoEApplyDashMark,
                    "Effect_YasuoE_ApplyDashMark",
                    "Yasuo E Apply Dash Mark",
                    assets.BuffYasuoDashMark);

                ConfigureEffectApplyBuff(
                    assets.EffectYasuoEApplyDashState,
                    "Effect_YasuoE_ApplyDashState",
                    "Yasuo E Apply Dash State",
                    assets.BuffYasuoEDashState,
                    assets.TargetingSelf);

                ConfigureEffectDamage(
                    assets.EffectYasuoQEqSlashDamage,
                    "Effect_YasuoQ_EQSlashDamage",
                    "Yasuo Q EQ Slash Damage",
                    22f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier,
                    true);
                ConfigureEffectExecutionContext(
                    assets.EffectYasuoQEqSlashDamage,
                    null,
                    assets.TargetingYasuoQEqSlashEnemy);

                ConfigureEffectDamage(
                    assets.EffectYasuoQEqSlashEmpoweredDamage,
                    "Effect_YasuoQ_EQSlashEmpoweredDamage",
                    "Yasuo Q EQ Slash Empowered Damage",
                    26f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1.1f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier,
                    true);
                ConfigureEffectExecutionContext(
                    assets.EffectYasuoQEqSlashEmpoweredDamage,
                    null,
                    assets.TargetingYasuoQEqSlashEnemy);

                ConfigureEffectApplyBuff(
                    assets.EffectYasuoQEqSlashApplyAirborneTag,
                    "Effect_YasuoQ_EQSlashApplyAirborneTag",
                    "Yasuo Q EQ Slash Apply Airborne",
                    assets.BuffYasuoAirborneTag,
                    assets.TargetingYasuoQEqSlashEnemy);

                ConfigureEffectMove(
                    assets.EffectYasuoRDashMove,
                    "Effect_YasuoR_DashMove",
                    "Yasuo R Dash Move",
                    MoveStyle.Dash,
                    0f,
                    72f,
                    assets.TargetingSelf,
                    MoveDestinationPolicy.BehindExplicitTarget,
                    MoveCollisionPolicy.IgnoreCharacterControllerCollisions,
                    0.6f);

                ConfigureEffectApplyBuff(
                    assets.EffectYasuoRApplyLastBreathState,
                    "Effect_YasuoR_ApplyLastBreathState",
                    "Yasuo R Apply Last Breath State",
                    assets.BuffYasuoLastBreathState,
                    assets.TargetingSelf);

                ConfigureEffectApplyBuff(
                    assets.EffectYasuoRApplyTargetSuspend,
                    "Effect_YasuoR_ApplyTargetSuspend",
                    "Yasuo R Apply Target Suspend",
                    assets.BuffYasuoLastBreathSuspend);
                ConfigureEffectExecutionContext(
                    assets.EffectYasuoRApplyTargetSuspend,
                    assets.ConditionYasuoRTargetAirborne,
                    assets.TargetingYasuoRAirborneCluster);

                ConfigureEffectDamage(
                    assets.EffectYasuoRDamage,
                    "Effect_YasuoR_Damage",
                    "Yasuo R Damage",
                    36f,
                    DamageType.Physical,
                    0f,
                    0f,
                    assets.AttackPower,
                    1.35f,
                    true,
                    assets.CritChance,
                    assets.CritMultiplier,
                    true);
                ConfigureEffectExecutionContext(
                    assets.EffectYasuoRDamage,
                    assets.ConditionYasuoRTargetAirborne,
                    assets.TargetingYasuoRAirborneCluster);

                ConfigureEffectRemoveBuff(
                    assets.EffectYasuoRRemoveAirborneTag,
                    "Effect_YasuoR_RemoveAirborneTag",
                    "Yasuo R Remove Airborne",
                    assets.BuffYasuoAirborneTag);
                ConfigureEffectExecutionContext(
                    assets.EffectYasuoRRemoveAirborneTag,
                    assets.ConditionYasuoRTargetAirborne,
                    assets.TargetingYasuoRAirborneCluster);

                ConfigureProjectile(
                    assets.ProjectileYasuoQTornado,
                    "Projectile_YasuoQTornado",
                    "Yasuo Q Tornado",
                    assets.PrefabArcaneBolt,
                    18f,
                    3f,
                    0.25f,
                    false,
                    0f,
                    true,
                    99,
                    new Object[] { assets.EffectYasuoQTornadoDamage, assets.EffectYasuoQTornadoApplyAirborneTag });

                ConfigureProjectile(
                    assets.ProjectileReturnBlade,
                    "Projectile_ReturnBlade",
                    "Return Blade",
                    assets.PrefabArcaneBolt,
                    14f,
                    2.5f,
                    0.2f,
                    false,
                    360f,
                    false,
                    1,
                    new Object[] { assets.EffectReturnBladeDamage },
                    ProjectileBehaviorType.Return,
                    1.2f);

                ConfigureProjectile(
                    assets.ProjectileShardVolley,
                    "Projectile_ShardVolley",
                    "Shard Volley",
                    assets.PrefabArcaneBolt,
                    16f,
                    2.5f,
                    0.18f,
                    false,
                    0f,
                    false,
                    1,
                    new Object[] { assets.EffectShardVolleyDamage },
                    ProjectileBehaviorType.Split,
                    1f,
                    3,
                    35f,
                    1);

                ConfigureBuffBurn(assets.BuffBurn, assets.TagFire, assets.EffectBurnTick);
                ConfigureBuffArcaneFocus(
                    assets.BuffArcaneFocus,
                    assets.TagMagic,
                    new Object[] { assets.ModifierSkillCost, assets.ModifierSkillCooldown, assets.ModifierFireBonus, assets.ModifierMaxMana });

                ConfigureCondition(
                    assets.ConditionTargetLowHealth,
                    "Condition_TargetLowHealth",
                    "Target Low Health",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.HealthPercentBelow,
                        Subject = ConditionSubject.Target,
                        Threshold = 0.3f
                    });

                ConfigureCondition(
                    assets.ConditionYasuoQPhase1,
                    "Condition_YasuoQ_Phase1",
                    "Yasuo Q Phase 1",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.SequencePhaseIs,
                        Subject = ConditionSubject.Caster,
                        PhaseThreshold = 1
                    },
                    new ConditionEntryData
                    {
                        Type = ConditionType.NotHasBuff,
                        Subject = ConditionSubject.Caster,
                        Buff = assets.BuffYasuoEDashState
                    });

                ConfigureCondition(
                    assets.ConditionYasuoQPhase2,
                    "Condition_YasuoQ_Phase2",
                    "Yasuo Q Phase 2",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.SequencePhaseIs,
                        Subject = ConditionSubject.Caster,
                        PhaseThreshold = 2
                    },
                    new ConditionEntryData
                    {
                        Type = ConditionType.NotHasBuff,
                        Subject = ConditionSubject.Caster,
                        Buff = assets.BuffYasuoEDashState
                    });

                ConfigureCondition(
                    assets.ConditionYasuoQPhase3,
                    "Condition_YasuoQ_Phase3",
                    "Yasuo Q Phase 3",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.SequencePhaseIs,
                        Subject = ConditionSubject.Caster,
                        PhaseThreshold = 3
                    },
                    new ConditionEntryData
                    {
                        Type = ConditionType.NotHasBuff,
                        Subject = ConditionSubject.Caster,
                        Buff = assets.BuffYasuoEDashState
                    });

                ConfigureCondition(
                    assets.ConditionYasuoQEqPhase1,
                    "Condition_YasuoQ_EQ_Phase1",
                    "Yasuo EQ Phase 1",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.SequencePhaseIs,
                        Subject = ConditionSubject.Caster,
                        PhaseThreshold = 1
                    },
                    new ConditionEntryData
                    {
                        Type = ConditionType.HasBuff,
                        Subject = ConditionSubject.Caster,
                        Buff = assets.BuffYasuoEDashState
                    });

                ConfigureCondition(
                    assets.ConditionYasuoQEqPhase2,
                    "Condition_YasuoQ_EQ_Phase2",
                    "Yasuo EQ Phase 2",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.SequencePhaseIs,
                        Subject = ConditionSubject.Caster,
                        PhaseThreshold = 2
                    },
                    new ConditionEntryData
                    {
                        Type = ConditionType.HasBuff,
                        Subject = ConditionSubject.Caster,
                        Buff = assets.BuffYasuoEDashState
                    });

                ConfigureCondition(
                    assets.ConditionYasuoQEqPhase3,
                    "Condition_YasuoQ_EQ_Phase3",
                    "Yasuo EQ Phase 3",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.SequencePhaseIs,
                        Subject = ConditionSubject.Caster,
                        PhaseThreshold = 3
                    },
                    new ConditionEntryData
                    {
                        Type = ConditionType.HasBuff,
                        Subject = ConditionSubject.Caster,
                        Buff = assets.BuffYasuoEDashState
                    });

                ConfigureCondition(
                    assets.ConditionYasuoETargetNotDashed,
                    "Condition_YasuoE_TargetNotDashed",
                    "Yasuo E Target Not Dashed",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.NotHasBuff,
                        Subject = ConditionSubject.Target,
                        Buff = assets.BuffYasuoDashMark
                    });

                ConfigureCondition(
                    assets.ConditionYasuoRTargetAirborne,
                    "Condition_YasuoR_TargetAirborne",
                    "Yasuo R Target Airborne",
                    ConditionOperator.All,
                    new ConditionEntryData
                    {
                        Type = ConditionType.HasControl,
                        Subject = ConditionSubject.Target,
                        ControlType = ControlType.Knockup
                    });

                ConfigureBuff(
                    assets.BuffBleed,
                    "Buff_Bleed",
                    "Bleed",
                    true,
                    6f,
                    2f,
                    BuffStackingRule.Extend,
                    3,
                    new Object[] { assets.TagPhysical },
                    null,
                    new BuffTriggerData[]
                    {
                        new BuffTriggerData
                        {
                            Trigger = BuffTriggerType.OnTick,
                            Chance = 1f,
                            Condition = null,
                            Effects = new Object[] { assets.EffectBleedTick }
                        }
                    });

                ConfigureBuff(
                    assets.BuffPoison,
                    "Buff_Poison",
                    "Poison",
                    true,
                    4f,
                    1f,
                    BuffStackingRule.Independent,
                    3,
                    new Object[] { assets.TagNature },
                    null,
                    new BuffTriggerData[]
                    {
                        new BuffTriggerData
                        {
                            Trigger = BuffTriggerType.OnTick,
                            Chance = 1f,
                            Condition = null,
                            Effects = new Object[] { assets.EffectPoisonTick }
                        }
                    });

                ConfigureBuff(
                    assets.BuffStoneSkin,
                    "Buff_StoneSkin",
                    "Stone Skin",
                    false,
                    8f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagPhysical },
                    new Object[] { assets.ModifierResistPhysical },
                    null);

                ConfigureBuff(
                    assets.BuffMagicWard,
                    "Buff_MagicWard",
                    "Magic Ward",
                    false,
                    8f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagMagic },
                    new Object[] { assets.ModifierResistMagical },
                    null);

                ConfigureBuff(
                    assets.BuffQuickCast,
                    "Buff_QuickCast",
                    "Quick Cast",
                    false,
                    6f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagMagic },
                    new Object[] { assets.ModifierQuickCastTime, assets.ModifierQuickChannelTime },
                    null);

                ConfigureBuff(
                    assets.BuffTimeWarp,
                    "Buff_TimeWarp",
                    "Time Warp",
                    false,
                    6f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagNature },
                    new Object[] { assets.ModifierEffectDuration, assets.ModifierEffectInterval },
                    null);

                ConfigureBuff(
                    assets.BuffGhostStep,
                    "Buff_GhostStep",
                    "Ghost Step",
                    false,
                    2f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagMagic },
                    null,
                    new BuffTriggerData[]
                    {
                        new BuffTriggerData
                        {
                            Trigger = BuffTriggerType.OnApply,
                            Chance = 1f,
                            Condition = null,
                            Effects = new Object[] { assets.EffectCombatStateAddUntargetable }
                        },
                        new BuffTriggerData
                        {
                            Trigger = BuffTriggerType.OnExpire,
                            Chance = 1f,
                            Condition = null,
                            Effects = new Object[] { assets.EffectCombatStateRemoveUntargetable }
                        }
                    });

                ConfigureBuff(
                    assets.BuffIronShell,
                    "Buff_IronShell",
                    "Iron Shell",
                    false,
                    2.5f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagPhysical },
                    null,
                    new BuffTriggerData[]
                    {
                        new BuffTriggerData
                        {
                            Trigger = BuffTriggerType.OnApply,
                            Chance = 1f,
                            Condition = null,
                            Effects = new Object[] { assets.EffectCombatStateAddInvulnerable }
                        },
                        new BuffTriggerData
                        {
                            Trigger = BuffTriggerType.OnExpire,
                            Chance = 1f,
                            Condition = null,
                            Effects = new Object[] { assets.EffectCombatStateRemoveInvulnerable }
                        }
                    });

                ConfigureBuff(
                    assets.BuffYasuoDashMark,
                    "Buff_YasuoDashMark",
                    "Yasuo Dash Mark",
                    true,
                    7f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagPhysical },
                    null,
                    null);

                ConfigureBuff(
                    assets.BuffYasuoEDashState,
                    "Buff_YasuoEDashState",
                    "Yasuo E Dash State",
                    false,
                    0.32f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    null,
                    null,
                    null,
                    null,
                    null,
                    dispellable: false);

                ConfigureBuff(
                    assets.BuffYasuoAirborneTag,
                    "Buff_YasuoAirborneTag",
                    "Yasuo Airborne Tag",
                    true,
                    1.25f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagPhysical },
                    null,
                    null,
                    new[] { ControlType.Knockup },
                    null);

                ConfigureBuff(
                    assets.BuffYasuoLastBreathState,
                    "Buff_YasuoLastBreathState",
                    "Yasuo Last Breath State",
                    false,
                    0.58f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagPhysical },
                    null,
                    new BuffTriggerData[]
                    {
                        new BuffTriggerData
                        {
                            Trigger = BuffTriggerType.OnApply,
                            Chance = 1f,
                            Condition = null,
                            Effects = new Object[]
                            {
                                assets.EffectCombatStateAddUntargetable,
                                assets.EffectCombatStateAddInvulnerable
                            }
                        },
                        new BuffTriggerData
                        {
                            Trigger = BuffTriggerType.OnExpire,
                            Chance = 1f,
                            Condition = null,
                            Effects = new Object[]
                            {
                                assets.EffectCombatStateRemoveUntargetable,
                                assets.EffectCombatStateRemoveInvulnerable
                            }
                        }
                    },
                    null,
                    null,
                    dispellable: false);

                ConfigureBuff(
                    assets.BuffYasuoLastBreathSuspend,
                    "Buff_YasuoLastBreathSuspend",
                    "Yasuo Last Breath Suspend",
                    true,
                    0.58f,
                    0f,
                    BuffStackingRule.Refresh,
                    1,
                    new Object[] { assets.TagPhysical },
                    null,
                    null,
                    new[] { ControlType.Knockup },
                    null,
                    dispellable: false);

                ConfigureUnit(
                    assets.UnitSummonTotem,
                    "Unit_SummonTotem",
                    "Summon Totem",
                    new Object[] { assets.TagMagic },
                    new StatValueData[]
                    {
                        new StatValueData(assets.MaxHealth, 60f),
                        new StatValueData(assets.HealthRegen, 0f),
                        new StatValueData(assets.MaxMana, 0f),
                        new StatValueData(assets.ManaRegen, 0f)
                    },
                    null,
                    null,
                    null,
                    assets.PrefabSummonTotem);

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
                    assets.TargetingBasicAttack,
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

                ConfigureEffectTriggerSkill(
                    assets.EffectTriggerArcaneFocus,
                    "Effect_TriggerArcaneFocus",
                    "Trigger Arcane Focus",
                    assets.SkillArcaneFocus);

                assets.SkillCleave = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_Cleave.asset");
                ConfigureSkill(
                    assets.SkillCleave,
                    "Skill_Cleave",
                    "Cleave",
                    ResourceType.Mana,
                    5f,
                    3f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingConeEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectCleaveDamage }
                    });

                assets.SkillChainLightning = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_ChainLightning.asset");
                ConfigureSkill(
                    assets.SkillChainLightning,
                    "Skill_ChainLightning",
                    "Chain Lightning",
                    ResourceType.Mana,
                    12f,
                    6f,
                    0.1f,
                    0f,
                    false,
                    true,
                    assets.TargetingChainEnemy,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectChainDamage }
                    });

                assets.SkillRandomShot = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_RandomShot.asset");
                ConfigureSkill(
                    assets.SkillRandomShot,
                    "Skill_RandomShot",
                    "Random Shot",
                    ResourceType.Mana,
                    6f,
                    4f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingRandomEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectRandomShotDamage }
                    });

                assets.SkillExecute = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_Execute.asset");
                ConfigureSkill(
                    assets.SkillExecute,
                    "Skill_Execute",
                    "Execute",
                    ResourceType.Mana,
                    8f,
                    8f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionTargetLowHealth,
                        Effects = new Object[] { assets.EffectExecuteDamage }
                    });

                assets.SkillHeal = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_Heal.asset");
                ConfigureSkill(
                    assets.SkillHeal,
                    "Skill_Heal",
                    "Heal",
                    ResourceType.Mana,
                    10f,
                    4f,
                    0.2f,
                    0f,
                    false,
                    true,
                    assets.TargetingAllySingle,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectHealSmall }
                    });

                assets.SkillDash = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_Dash.asset");
                ConfigureSkill(
                    assets.SkillDash,
                    "Skill_Dash",
                    "Dash",
                    ResourceType.Mana,
                    0f,
                    5f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectDash }
                    });

                assets.SkillLineStrike = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_LineStrike.asset");
                ConfigureSkill(
                    assets.SkillLineStrike,
                    "Skill_LineStrike",
                    "Line Strike",
                    ResourceType.Mana,
                    6f,
                    5f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingYasuoQLineEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectLineDamage }
                    });

                assets.SkillBoxField = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_BoxField.asset");
                ConfigureSkill(
                    assets.SkillBoxField,
                    "Skill_BoxField",
                    "Box Field",
                    ResourceType.Mana,
                    8f,
                    7f,
                    0.1f,
                    0f,
                    false,
                    true,
                    assets.TargetingBoxEnemy,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectBoxDamage }
                    });

                assets.SkillArcaneBolt = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_ArcaneBolt.asset");
                ConfigureSkill(
                    assets.SkillArcaneBolt,
                    "Skill_ArcaneBolt",
                    "Arcane Bolt",
                    ResourceType.Mana,
                    8f,
                    2f,
                    0.1f,
                    0f,
                    false,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectArcaneBoltProjectile }
                    });

                assets.SkillShockwave = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_Shockwave.asset");
                ConfigureSkill(
                    assets.SkillShockwave,
                    "Skill_Shockwave",
                    "Shockwave",
                    ResourceType.Mana,
                    12f,
                    8f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSphereEnemy,
                    new Object[] { assets.TagNature },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectShockwaveDot }
                    });

                assets.SkillBleedStrike = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_BleedStrike.asset");
                ConfigureSkill(
                    assets.SkillBleedStrike,
                    "Skill_BleedStrike",
                    "Bleed Strike",
                    ResourceType.Mana,
                    5f,
                    4f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyBleed }
                    });

                assets.SkillPoisonDart = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_PoisonDart.asset");
                ConfigureSkill(
                    assets.SkillPoisonDart,
                    "Skill_PoisonDart",
                    "Poison Dart",
                    ResourceType.Mana,
                    5f,
                    4f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagNature },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyPoison }
                    });

                assets.SkillStoneSkin = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_StoneSkin.asset");
                ConfigureSkill(
                    assets.SkillStoneSkin,
                    "Skill_StoneSkin",
                    "Stone Skin",
                    ResourceType.Mana,
                    8f,
                    10f,
                    0.1f,
                    0f,
                    false,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyStoneSkin }
                    });

                assets.SkillMagicWard = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_MagicWard.asset");
                ConfigureSkill(
                    assets.SkillMagicWard,
                    "Skill_MagicWard",
                    "Magic Ward",
                    ResourceType.Mana,
                    8f,
                    10f,
                    0.1f,
                    0f,
                    false,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyMagicWard }
                    });

                assets.SkillQuickCast = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_QuickCast.asset");
                ConfigureSkill(
                    assets.SkillQuickCast,
                    "Skill_QuickCast",
                    "Quick Cast",
                    ResourceType.Mana,
                    8f,
                    12f,
                    0.1f,
                    0f,
                    false,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyQuickCast }
                    });

                assets.SkillTimeWarp = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_TimeWarp.asset");
                ConfigureSkill(
                    assets.SkillTimeWarp,
                    "Skill_TimeWarp",
                    "Time Warp",
                    ResourceType.Mana,
                    10f,
                    14f,
                    0.1f,
                    0f,
                    false,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagNature },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyTimeWarp }
                    });

                assets.SkillManaSurge = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_ManaSurge.asset");
                ConfigureSkill(
                    assets.SkillManaSurge,
                    "Skill_ManaSurge",
                    "Mana Surge",
                    ResourceType.Mana,
                    0f,
                    6f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectRestoreMana }
                    });

                assets.SkillSummonTotem = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_SummonTotem.asset");
                ConfigureSkill(
                    assets.SkillSummonTotem,
                    "Skill_SummonTotem",
                    "Summon Totem",
                    ResourceType.Mana,
                    20f,
                    20f,
                    0.3f,
                    0f,
                    false,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectSummonTotem }
                    });

                assets.SkillTriggerFocus = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_TriggerFocus.asset");
                ConfigureSkill(
                    assets.SkillTriggerFocus,
                    "Skill_TriggerFocus",
                    "Trigger Focus",
                    ResourceType.Mana,
                    5f,
                    6f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectTriggerArcaneFocus }
                    });

                assets.SkillGhostStep = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_GhostStep.asset");
                ConfigureSkill(
                    assets.SkillGhostStep,
                    "Skill_GhostStep",
                    "Ghost Step",
                    ResourceType.Mana,
                    8f,
                    8f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyGhostStep }
                    });

                assets.SkillIronShell = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_IronShell.asset");
                ConfigureSkill(
                    assets.SkillIronShell,
                    "Skill_IronShell",
                    "Iron Shell",
                    ResourceType.Mana,
                    10f,
                    10f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectApplyIronShell }
                    });

                assets.SkillSpellAegis = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_SpellAegis.asset");
                ConfigureSkill(
                    assets.SkillSpellAegis,
                    "Skill_SpellAegis",
                    "Spell Aegis",
                    ResourceType.Mana,
                    8f,
                    6f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSelf,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectCombatStateGrantSpellShield }
                    });

                assets.SkillAmmoBurst = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_AmmoBurst.asset");
                ConfigureSkill(
                    assets.SkillAmmoBurst,
                    "Skill_AmmoBurst",
                    "Ammo Burst",
                    ResourceType.Mana,
                    6f,
                    1f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectAmmoBurstDamage }
                    },
                    0f,
                    0f,
                    0f,
                    0f,
                    SkillQueuePolicy.Replace,
                    TargetSnapshotPolicy.AtCastStart,
                    true,
                    2,
                    2,
                    3f);

                assets.SkillRecastLunge = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_RecastLunge.asset");
                ConfigureSkill(
                    assets.SkillRecastLunge,
                    "Skill_RecastLunge",
                    "Recast Lunge",
                    ResourceType.Mana,
                    8f,
                    6f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingPointAny,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectRecastLungeMove }
                    },
                    0f,
                    0f,
                    0f,
                    0f,
                    SkillQueuePolicy.Replace,
                    TargetSnapshotPolicy.AtCastStart,
                    false,
                    0,
                    0,
                    0f,
                    true,
                    1,
                    2.5f,
                    false,
                    true,
                    RecastTargetPolicy.AnyValid);

                assets.SkillReturnBlade = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_ReturnBlade.asset");
                ConfigureSkill(
                    assets.SkillReturnBlade,
                    "Skill_ReturnBlade",
                    "Return Blade",
                    ResourceType.Mana,
                    10f,
                    7f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectReturnBladeProjectile }
                    });

                assets.SkillShardVolley = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_ShardVolley.asset");
                ConfigureSkill(
                    assets.SkillShardVolley,
                    "Skill_ShardVolley",
                    "Shard Volley",
                    ResourceType.Mana,
                    10f,
                    8f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectShardVolleyProjectile }
                    });

                assets.SkillRevealBolt = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_RevealBolt.asset");
                ConfigureSkill(
                    assets.SkillRevealBolt,
                    "Skill_RevealBolt",
                    "Reveal Bolt",
                    ResourceType.Mana,
                    8f,
                    5f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingSingleEnemy,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectRevealBoltDamage }
                    });

                assets.SkillWindWall = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_WindWall.asset");
                ConfigureSkill(
                    assets.SkillWindWall,
                    "Skill_WindWall",
                    "Wind Wall",
                    ResourceType.Mana,
                    12f,
                    12f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingPointAny,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectWindWallSummon }
                    });

                assets.SkillYasuoQ = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_YasuoQ.asset");
                ConfigureSkill(
                    assets.SkillYasuoQ,
                    "Skill_YasuoQ",
                    "Yasuo Q - Steel Tempest",
                    ResourceType.Mana,
                    4f,
                    0f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingLineEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionYasuoQPhase1,
                        Effects = new Object[] { assets.EffectYasuoQThrustDamage }
                    });
                ConfigureSkillSequence(
                    assets.SkillYasuoQ,
                    true,
                    3,
                    6f,
                    SkillSequenceOverflowPolicy.LoopToStart,
                    false,
                    true);
                ConfigureSkillSteps(
                    assets.SkillYasuoQ,
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionYasuoQPhase1,
                        Effects = new Object[] { assets.EffectYasuoQThrustDamage }
                    },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionYasuoQPhase2,
                        Effects = new Object[] { assets.EffectYasuoQThrustDamage }
                    },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionYasuoQPhase3,
                        Effects = new Object[] { assets.EffectYasuoQTornadoProjectile }
                    },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionYasuoQEqPhase1,
                        ExecuteOnce = true,
                        Effects = new Object[] { assets.EffectYasuoQEqSlashDamage }
                    },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionYasuoQEqPhase2,
                        ExecuteOnce = true,
                        Effects = new Object[] { assets.EffectYasuoQEqSlashDamage }
                    },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionYasuoQEqPhase3,
                        ExecuteOnce = true,
                        Effects = new Object[] { assets.EffectYasuoQEqSlashEmpoweredDamage, assets.EffectYasuoQEqSlashApplyAirborneTag }
                    });

                assets.SkillYasuoW = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_YasuoW.asset");
                ConfigureSkill(
                    assets.SkillYasuoW,
                    "Skill_YasuoW",
                    "Yasuo W - Wind Wall",
                    ResourceType.Mana,
                    12f,
                    0f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingPointAny,
                    new Object[] { assets.TagMagic },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectWindWallSummon }
                    });

                assets.SkillYasuoE = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_YasuoE.asset");
                ConfigureSkill(
                    assets.SkillYasuoE,
                    "Skill_YasuoE",
                    "Yasuo E - Sweeping Blade",
                    ResourceType.Mana,
                    5f,
                    0f,
                    0f,
                    0f,
                    true,
                    true,
                    assets.TargetingYasuoSingleEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Condition = assets.ConditionYasuoETargetNotDashed,
                        Effects = new Object[] { assets.EffectYasuoEDashMove, assets.EffectYasuoEDashDamage, assets.EffectYasuoEApplyDashMark, assets.EffectYasuoEApplyDashState }
                    });

                assets.SkillYasuoR = LoadOrCreate<SkillDefinition>($"{folders.Skills}/Skill_YasuoR.asset");
                ConfigureSkill(
                    assets.SkillYasuoR,
                    "Skill_YasuoR",
                    "Yasuo R - Last Breath",
                    ResourceType.Mana,
                    0f,
                    0f,
                    0.26f,
                    0f,
                    false,
                    false,
                    assets.TargetingYasuoRSingleEnemy,
                    new Object[] { assets.TagPhysical },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectYasuoRDashMove, assets.EffectYasuoRApplyLastBreathState, assets.EffectYasuoRApplyTargetSuspend }
                    },
                    0.16f);
                ConfigureSkillCastConstraints(
                    assets.SkillYasuoR,
                    new SkillCastConstraintData
                    {
                        Condition = assets.ConditionYasuoRTargetAirborne,
                        FailReason = SkillCastFailReason.CastConstraintFailed
                    });
                ConfigureSkillSteps(
                    assets.SkillYasuoR,
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastStart,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectYasuoRDashMove, assets.EffectYasuoRApplyLastBreathState, assets.EffectYasuoRApplyTargetSuspend }
                    },
                    new SkillStepData
                    {
                        Trigger = SkillStepTrigger.OnCastComplete,
                        Delay = 0f,
                        Effects = new Object[] { assets.EffectYasuoRDamage, assets.EffectYasuoRRemoveAirborneTag }
                    });

                assets.AIBasic = LoadOrCreate<AIProfile>($"{folders.AI}/AI_Basic.asset");
                ConfigureAIProfile(assets.AIBasic, "AI_Basic", "Basic AI", 12f, 2f, 0.5f);

                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
                var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemy_Default.prefab");
                var enemyBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemy_Boss.prefab");

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
                        new StatValueData(assets.ManaRegen, 5f),
                        new StatValueData(assets.MoveSpeed, 5f)
                    },
                    assets.SkillBasicAttack,
                    new Object[]
                    {
                        assets.SkillYasuoQ,
                        assets.SkillYasuoW,
                        assets.SkillYasuoE,
                        assets.SkillYasuoR
                    },
                    null,
                    playerPrefab);

                ConfigureUnit(
                    assets.UnitPlayerYasuo,
                    "Unit_PlayerYasuo",
                    "Player Yasuo",
                    new Object[] { assets.TagPlayer },
                    new StatValueData[]
                    {
                        new StatValueData(assets.MaxHealth, 110f),
                        new StatValueData(assets.HealthRegen, 1f),
                        new StatValueData(assets.MaxMana, 100f),
                        new StatValueData(assets.ManaRegen, 5f),
                        new StatValueData(assets.MoveSpeed, 5.4f)
                    },
                    assets.SkillBasicAttack,
                    new Object[]
                    {
                        assets.SkillYasuoQ,
                        assets.SkillYasuoW,
                        assets.SkillYasuoE,
                        assets.SkillYasuoR
                    },
                    null,
                    playerPrefab);
                ConfigureUnitVisualProfile(assets.UnitPlayerYasuo, assets.UnitPlayer != null ? assets.UnitPlayer.VisualProfile : null);

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
                        new StatValueData(assets.ManaRegen, 1f),
                        new StatValueData(assets.MoveSpeed, 3.5f)
                    },
                    assets.SkillBasicAttack,
                    null,
                    assets.AIBasic,
                    enemyPrefab);

                assets.UnitEnemyHighHp = LoadOrCreate<UnitDefinition>($"{folders.Units}/Unit_Enemy_high_hp.asset");
                ConfigureUnit(
                    assets.UnitEnemyHighHp,
                    "Unit_Enemy_high_hp",
                    "Enemy Training Dummy",
                    new Object[] { assets.TagEnemy },
                    new StatValueData[]
                    {
                        new StatValueData(assets.MaxHealth, 10000f),
                        new StatValueData(assets.HealthRegen, 2f),
                        new StatValueData(assets.MaxMana, 30f),
                        new StatValueData(assets.ManaRegen, 1f),
                        new StatValueData(assets.MoveSpeed, 2.6f)
                    },
                    assets.SkillBasicAttack,
                    null,
                    assets.AIBasic,
                    enemyBossPrefab);

                assets.HUDDefault = LoadOrCreate<HUDConfig>($"{folders.UI}/HUD_Default.asset");
                ConfigureHUD(assets.HUDDefault, "HUD_Default", "Default HUD", 12, 12, true, true, true);

                assets.ProgressionDefault = LoadOrCreate<ProgressionDefinition>($"{folders.Progression}/Progression_Default.asset");
                ConfigureProgression(assets.ProgressionDefault, "Progression_Default", "Default Progression", 1, 20, 100, 1.2f, 0, 1);

                assets.Database = LoadOrCreate<GameDatabase>($"{folders.Database}/GameDatabase.asset");
                ConfigureDatabase(assets);

                assets.EventHub = LoadOrCreate<CombatEventHub>($"{folders.Runtime}/CombatEventHub.asset");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            PurgeLegacySampleSkills(folders.Skills);
            return assets;
        }

        private static void PurgeLegacySampleSkills(string skillsFolder)
        {
            if (string.IsNullOrWhiteSpace(skillsFolder))
            {
                return;
            }

            var legacySkillNames = new[]
            {
                "Skill_Fireball.asset",
                "Skill_ArcaneFocus.asset",
                "Skill_GhostStep.asset",
                "Skill_IronShell.asset",
                "Skill_SpellAegis.asset",
                "Skill_AmmoBurst.asset",
                "Skill_RecastLunge.asset",
                "Skill_ReturnBlade.asset",
                "Skill_ShardVolley.asset",
                "Skill_RevealBolt.asset",
                "Skill_WindWall.asset"
            };

            for (int i = 0; i < legacySkillNames.Length; i++)
            {
                var path = $"{skillsFolder}/{legacySkillNames[i]}";
                if (AssetDatabase.LoadAssetAtPath<SkillDefinition>(path) == null)
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void CreateSampleScene(SampleAssets assets)
        {
            EnsureFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            RemoveRootObject(scene, "CombatSystems");
            RemoveRootObject(scene, "Sample_Player");
            RemoveRootObject(scene, "Sample_Enemy");
            RemoveRootObject(scene, "Sample_Enemies");
            RemoveRootObject(scene, "HUD");
            RemoveRootObject(scene, "EventSystem");  // 删除旧的事件系统，后面会重新创建
            RemoveRootObject(scene, "Validation_Lane");
            RemoveRootObject(scene, "LevelFlow");
            RemoveRootObjectsWithComponent<LevelFlowController>(scene);

            SetupTopdownCamera();

            var combatSystems = new GameObject("CombatSystems");
            var targetingSystem = combatSystems.AddComponent<TargetingSystem>();
            var effectExecutor = combatSystems.AddComponent<EffectExecutor>();
            var projectilePool = combatSystems.AddComponent<ProjectilePool>();
            combatSystems.AddComponent<HitResolutionSystem>();
            combatSystems.AddComponent<VisionSystem>();

            SetComponentReference(effectExecutor, "targetingSystem", targetingSystem);
            SetComponentReference(effectExecutor, "projectilePool", projectilePool);

            var player = CreateUnitPrimitive("Sample_Player", new Vector3(0f, 0f, 0f));
            player.tag = "Player";
            var samplePlayerDefinition = assets.UnitPlayerYasuo != null ? assets.UnitPlayerYasuo : assets.UnitPlayer;
            ConfigureUnitObject(player, samplePlayerDefinition, assets.EventHub, targetingSystem, effectExecutor, 1, assets.MaxHealth, assets.HealthRegen, assets.MaxMana, assets.ManaRegen);

            var progression = player.AddComponent<PlayerProgression>();
            SetComponentReference(progression, "progression", assets.ProgressionDefault);
            SetComponentReference(progression, "eventHub", assets.EventHub);
            SetComponentValue(progression, "initializeOnAwake", true);

            var xpGrant = player.AddComponent<DebugExperienceGrant>();
            SetComponentReference(xpGrant, "progression", progression);
            SetComponentValue(xpGrant, "grantKey", KeyCode.F5);
            SetComponentValue(xpGrant, "grantAmount", 50);
            SetComponentValue(xpGrant, "requireShift", false);

            var enemyGroup = new GameObject("Sample_TrainingDummies");
            var enemyPositions = BuildTrainingDummyPositions(6, new Vector3(0f, 0f, 11f), 2.2f);
            var sampleDummyDefinition = assets.UnitEnemyHighHp != null ? assets.UnitEnemyHighHp : assets.UnitEnemy;
            for (int i = 0; i < enemyPositions.Length; i++)
            {
                var enemy = CreateUnitPrimitive($"Sample_Dummy_{i + 1}", enemyPositions[i]);
                enemy.transform.SetParent(enemyGroup.transform, true);
                ConfigureUnitObject(enemy, sampleDummyDefinition, assets.EventHub, targetingSystem, effectExecutor, 2, assets.MaxHealth, assets.HealthRegen, assets.MaxMana, assets.ManaRegen);
                ConfigureTrainingDummyObject(enemy);
            }

            var playerMove = player.AddComponent<PlayerMovementDriver>();
            SetComponentReference(playerMove, "movement", player.GetComponent<MovementComponent>());
            SetComponentReference(playerMove, "viewCamera", FindMainCamera());
            SetComponentValue(playerMove, "useCameraYaw", true);

            var indicatorRoot = new GameObject("SkillIndicator");
            indicatorRoot.transform.SetParent(player.transform, false);
            var indicatorLine = indicatorRoot.AddComponent<LineRenderer>();
            var indicator = indicatorRoot.AddComponent<SkillIndicator>();
            SetComponentReference(indicator, "line", indicatorLine);
            SetComponentReference(indicator, "anchor", player.transform);

            var indicatorDriver = player.AddComponent<PlayerSkillIndicatorDriver>();
            SetComponentReference(indicatorDriver, "skillUser", player.GetComponent<SkillUserComponent>());
            SetComponentReference(indicatorDriver, "indicator", indicator);
            SetComponentReference(indicatorDriver, "viewCamera", FindMainCamera());
            SetComponentReference(indicatorDriver, "targetingSystem", targetingSystem);
            SetComponentReference(indicatorDriver, "unitRoot", player.GetComponent<UnitRoot>());
            SetComponentValue(indicatorDriver, "rotateCasterToAim", true);

            var skillBar = BuildUISystemHud(assets, player.GetComponent<UnitRoot>(), projectilePool);
            ConfigureSampleUiEntry();

            CreateValidationLane(assets, targetingSystem, effectExecutor);

            // 确保 EventSystem/InputReader 存在（用于 UI 交互与输入）
            EnsureEventSystem();
            EnsureInputRoot();

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

        private static void RemoveRootObjectsWithComponent<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                {
                    continue;
                }

                if (root.GetComponentInChildren<T>(true) != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void ConfigureSampleUiEntry()
        {
            var uiRoot = Object.FindFirstObjectByType<UIRoot>(FindObjectsInactive.Include);
            if (uiRoot == null)
            {
                return;
            }

            var uiManager = uiRoot.Manager != null
                ? uiRoot.Manager
                : Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            var inGame = Object.FindFirstObjectByType<InGameScreen>(FindObjectsInactive.Include);
            var mainMenu = Object.FindFirstObjectByType<MainMenuScreen>(FindObjectsInactive.Include);

            if (uiManager != null && inGame != null)
            {
                SetComponentReference(uiManager, "initialScreen", inGame);
                SetComponentValue(uiManager, "hideHudOnStart", false);
                SetComponentValue(uiManager, "hideAllScreensOnStart", true);
            }

            if (mainMenu != null)
            {
                // SampleScene 不走章节关卡流，避免 New Game 跳转 Town。
                SetComponentReference(mainMenu, "levelFlow", null);
            }
        }

        private static GameObject CreateUnitPrimitive(string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            return go;
        }

        private static Vector3[] BuildTrainingDummyPositions(int count, Vector3 startPosition, float spacing)
        {
            var result = new Vector3[Mathf.Max(1, count)];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = startPosition + new Vector3((i - (result.Length - 1) * 0.5f) * spacing, 0f, 0f);
            }

            return result;
        }

        private static void ConfigureTrainingDummyObject(GameObject unitObject)
        {
            if (unitObject == null)
            {
                return;
            }

            // 训练木桩不参与物理阻挡，避免冲刺技能撞停。
            var capsule = unitObject.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.isTrigger = true;
            }

            var controller = unitObject.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.detectCollisions = false;
                controller.enableOverlapRecovery = false;
                controller.enabled = false;
            }

            var movement = unitObject.GetComponent<MovementComponent>();
            if (movement != null)
            {
                movement.enabled = false;
            }
        }

        private static void CreateValidationLane(
            SampleAssets assets,
            TargetingSystem targetingSystem,
            EffectExecutor effectExecutor)
        {
            var laneRoot = new GameObject("Validation_Lane");
            var laneDummyDefinition = assets.UnitEnemyHighHp != null ? assets.UnitEnemyHighHp : assets.UnitEnemy;

            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Lane_Strip";
            strip.transform.SetParent(laneRoot.transform, true);
            strip.transform.position = new Vector3(0f, -0.45f, 14f);
            strip.transform.localScale = new Vector3(4f, 0.1f, 18f);

            var enemyDummy = CreateUnitPrimitive("Lane_EnemyDummy", new Vector3(1.2f, 0f, 5f));
            enemyDummy.transform.SetParent(laneRoot.transform, true);
            ConfigureUnitObject(
                enemyDummy,
                laneDummyDefinition,
                assets.EventHub,
                targetingSystem,
                effectExecutor,
                2,
                assets.MaxHealth,
                assets.HealthRegen,
                assets.MaxMana,
                assets.ManaRegen);
            ConfigureTrainingDummyObject(enemyDummy);

            var allyDummy = CreateUnitPrimitive("Lane_AllyDummy", new Vector3(-1.2f, 0f, 5f));
            allyDummy.transform.SetParent(laneRoot.transform, true);
            ConfigureUnitObject(
                allyDummy,
                laneDummyDefinition,
                assets.EventHub,
                targetingSystem,
                effectExecutor,
                2,
                assets.MaxHealth,
                assets.HealthRegen,
                assets.MaxMana,
                assets.ManaRegen);
            ConfigureTrainingDummyObject(allyDummy);

            var wallPoint = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wallPoint.name = "Lane_WindWallPoint";
            wallPoint.transform.SetParent(laneRoot.transform, true);
            wallPoint.transform.position = new Vector3(0f, 0.1f, 16f);
            wallPoint.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
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
            var combatState = unitObject.AddComponent<CombatStateComponent>();
            var visibility = unitObject.AddComponent<VisibilityComponent>();
            var team = unitObject.AddComponent<TeamComponent>();
            var skillUser = unitObject.AddComponent<SkillUserComponent>();
            var characterController = unitObject.AddComponent<CharacterController>();
            var movement = unitObject.AddComponent<MovementComponent>();

            SetComponentReference(unitRoot, "unitDefinition", definition);
            SetComponentReference(unitRoot, "eventHub", eventHub);
            SetComponentReference(unitRoot, "stats", stats);
            SetComponentReference(unitRoot, "health", health);
            SetComponentReference(unitRoot, "resource", resource);
            SetComponentReference(unitRoot, "cooldown", cooldown);
            SetComponentReference(unitRoot, "unitTags", unitTags);
            SetComponentReference(unitRoot, "buffController", buffController);
            SetComponentReference(unitRoot, "team", team);

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
            SetComponentReference(visibility, "ownerTeam", team);
            SetComponentReference(visibility, "combatState", combatState);

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

            SetComponentReference(movement, "controller", characterController);
            SetComponentReference(movement, "skillUser", skillUser);

            SetComponentValue(team, "teamId", teamId);
        }

        /// <summary>
        /// 使用 UI 系统构建 HUD，并返回 HUD 技能栏引用。
        /// </summary>
        private static SkillBarUI BuildUISystemHud(SampleAssets assets, UnitRoot playerUnit, ProjectilePool projectilePool)
        {
            var uiRoot = Object.FindFirstObjectByType<UIRoot>();
            if (uiRoot == null)
            {
                UIRootBuilder.CreateUIRoot();
                uiRoot = Object.FindFirstObjectByType<UIRoot>();
            }

            UIRootBuilder.BuildBasicUI();

            if (uiRoot == null)
            {
                Debug.LogWarning("[CombatSampleGenerator] UIRoot not found after Build Basic UI.");
                return null;
            }

            var hudController = uiRoot.GetComponentInChildren<CombatHUDController>(true);
            if (hudController == null)
            {
                hudController = Object.FindFirstObjectByType<CombatHUDController>();
            }

            if (hudController == null)
            {
                Debug.LogWarning("[CombatSampleGenerator] CombatHUDController not found after Build Basic UI.");
                return null;
            }

            if (playerUnit != null)
            {
                SetComponentReference(hudController, "targetUnit", playerUnit);
            }

            if (assets.EventHub != null)
            {
                SetComponentReference(hudController, "eventHub", assets.EventHub);
            }

            if (assets.HUDDefault != null)
            {
                SetComponentReference(hudController, "hudConfig", assets.HUDDefault);
            }

            var worldCamera = FindMainCamera();
            if (worldCamera != null)
            {
                SetComponentReference(hudController, "worldCamera", worldCamera);
            }

            var debugOverlay = hudController.GetComponentInChildren<CombatDebugOverlay>(true);
            if (debugOverlay != null)
            {
                if (playerUnit != null)
                {
                    SetComponentReference(debugOverlay, "targetUnit", playerUnit);
                }

                if (projectilePool != null)
                {
                    SetComponentReference(debugOverlay, "projectilePool", projectilePool);
                }
            }

            var skillBar = hudController.GetComponentInChildren<SkillBarUI>(true);
            if (skillBar == null)
            {
                Debug.LogWarning("[CombatSampleGenerator] SkillBarUI not found under CombatHUDController.");
            }

            return skillBar;
        }

        /// <summary>
        /// 创建示例 HUD 系统
        /// 包含血条、资源条、施法条、技能栏、Buff 栏、战斗日志、飘字管理器和暂停菜单
        /// </summary>
        /// <param name="assets">示例资源集合</param>
        /// <param name="playerUnit">玩家单位根组件</param>
        private static SkillBarUI CreateSampleHUD(SampleAssets assets, UnitRoot playerUnit, ProjectilePool projectilePool)
        {
            // 创建 HUD Canvas
            var hudCanvas = CreateCanvas("HUD");
            var hudRoot = CreateUIRect("HUDRoot", hudCanvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var healthBar = CreateValueBar(
                "HealthBar",
                hudRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(220f, 20f),
                new Vector2(130f, -20f),
                new Color(0f, 0f, 0f, 0.6f),
                new Color(0.8f, 0.1f, 0.1f, 1f));

            var resourceBar = CreateValueBar(
                "ResourceBar",
                hudRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(220f, 16f),
                new Vector2(130f, -44f),
                new Color(0f, 0f, 0f, 0.6f),
                new Color(0.1f, 0.4f, 0.9f, 1f));

            var castBar = CreateCastBar(hudRoot);

            // 创建技能栏和 Buff 栏
            var skillBar = CreateSkillBar(hudRoot, assets.HUDDefault != null ? assets.HUDDefault.MaxSkillSlots : 6);
            var buffBar = CreateBuffBar(hudRoot, assets.HUDDefault != null ? assets.HUDDefault.MaxBuffSlots : 12);
            // 创建战斗日志和飘字管理器
            var combatLog = CreateCombatLog(hudRoot);
            var floatingText = CreateFloatingText(hudRoot);
            var debugOverlay = CreateDebugOverlay(hudRoot);
            SetComponentReference(debugOverlay, "targetUnit", playerUnit);
            SetComponentReference(debugOverlay, "projectilePool", projectilePool);
            SetComponentReference(debugOverlay, "floatingText", floatingText);
            SetComponentValue(debugOverlay, "visible", true);

            // 配置 HUD 控制器并连接所有 UI 组件
            var hudController = hudCanvas.AddComponent<CombatHUDController>();
            SetComponentReference(hudController, "eventHub", assets.EventHub);
            SetComponentReference(hudController, "hudConfig", assets.HUDDefault);
            SetComponentReference(hudController, "targetUnit", playerUnit);
            SetComponentReference(hudController, "healthBar", healthBar);
            SetComponentReference(hudController, "resourceBar", resourceBar);
            SetComponentReference(hudController, "skillBar", skillBar);
            SetComponentReference(hudController, "buffBar", buffBar);
            SetComponentReference(hudController, "castBar", castBar);
            SetComponentReference(hudController, "combatLog", combatLog);
            SetComponentReference(hudController, "floatingText", floatingText);
            SetComponentReference(hudController, "worldCamera", FindMainCamera());

            // 添加 UIManager 并创建暂停菜单
            var uiManager = hudCanvas.AddComponent<UIManager>();
            var pauseModal = CreatePauseMenu(hudCanvas.transform);
            SetComponentReference(pauseModal, "uiManager", uiManager);

            // 添加暂停热键组件
            var pauseHotkey = hudCanvas.AddComponent<PauseMenuHotkey>();
            SetComponentReference(pauseHotkey, "uiManager", uiManager);
            SetComponentReference(pauseHotkey, "pauseModal", pauseModal);
            SetComponentValue(pauseHotkey, "onlyWhenGameplayScreen", false);

            return skillBar;
        }

        /// <summary>
        /// 创建暂停菜单 Modal
        /// 包含继续、保存、设置、返回主菜单按钮
        /// </summary>
        private static PauseMenuModal CreatePauseMenu(Transform parent)
        {
            // 创建 Modal 根对象
            var modalRect = CreateUIRect("PauseMenuModal", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modalRect.gameObject.SetActive(false); // 默认隐藏
            
            // 暗色背景遮罩
            var background = modalRect.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.7f);
            background.raycastTarget = true;

            // 中央面板
            var panel = CreateUIRect("Panel", modalRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 320f), Vector2.zero);
            var panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.sprite = GetDefaultUISprite();
            panelBg.type = Image.Type.Sliced;
            panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            panelBg.raycastTarget = true;

            // 垂直布局
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // 标题
            var titleRect = CreateUIRect("Title", panel, Vector2.zero, Vector2.zero, new Vector2(380f, 50f), Vector2.zero);
            var titleText = CreateText(titleRect.gameObject, "PAUSED", 30, TextAnchor.MiddleCenter, Color.white);
            var titleLayout = titleRect.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 50f;

            // 创建按钮
            CreatePauseMenuButton(panel, "Resume");
            CreatePauseMenuButton(panel, "Save Game");
            CreatePauseMenuButton(panel, "Settings");
            CreatePauseMenuButton(panel, "Main Menu");

            // 添加 PauseMenuModal 组件
            var modal = modalRect.gameObject.AddComponent<PauseMenuModal>();
            var canvasGroup = modalRect.gameObject.AddComponent<CanvasGroup>();
            
            return modal;
        }

        /// <summary>
        /// 创建暂停菜单按钮
        /// </summary>
        private static Button CreatePauseMenuButton(Transform parent, string label)
        {
            var buttonRect = CreateUIRect($"Button_{label.Replace(" ", "")}", parent, Vector2.zero, Vector2.zero, new Vector2(380f, 48f), Vector2.zero);
            var buttonBg = buttonRect.gameObject.AddComponent<Image>();
            buttonBg.sprite = GetDefaultUISprite();
            buttonBg.type = Image.Type.Sliced;
            buttonBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            buttonBg.raycastTarget = true;

            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonBg;

            var buttonLayout = buttonRect.gameObject.AddComponent<LayoutElement>();
            buttonLayout.preferredHeight = 48f;

            var textRect = CreateUIRect("Label", buttonRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(textRect.gameObject, label, 20, TextAnchor.MiddleCenter, Color.white);

            return button;
        }

        /// <summary>
        /// 创建 UI Canvas
        /// 配置为屏幕空间覆盖模式，使用 1920x1080 参考分辨率
        /// </summary>
        /// <param name="name">Canvas 名称</param>
        /// <returns>创建的 Canvas GameObject</returns>
        private static GameObject CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // 设置为屏幕空间覆盖模式
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 配置 Canvas 缩放模式，使用 1920x1080 作为参考分辨率
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            return go;
        }

        /// <summary>
        /// 创建 UI RectTransform
        /// </summary>
        /// <param name="name">UI 元素名称</param>
        /// <param name="parent">父对象 Transform</param>
        /// <param name="anchorMin">锚点最小值</param>
        /// <param name="anchorMax">锚点最大值</param>
        /// <param name="size">尺寸</param>
        /// <param name="anchoredPos">锚定位置</param>
        /// <returns>创建的 RectTransform</returns>
        private static RectTransform CreateUIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            return rect;
        }

        /// <summary>
        /// 创建数值条 UI（用于血条、资源条、施法条等）
        /// 包含背景、填充条和文本显示
        /// </summary>
        /// <param name="name">数值条名称</param>
        /// <param name="parent">父对象 Transform</param>
        /// <param name="anchorMin">锚点最小值</param>
        /// <param name="anchorMax">锚点最大值</param>
        /// <param name="size">尺寸</param>
        /// <param name="anchoredPos">锚定位置</param>
        /// <param name="backgroundColor">背景颜色</param>
        /// <param name="fillColor">填充条颜色</param>
        /// <returns>创建的 ValueBarUI 组件</returns>
        private static ValueBarUI CreateValueBar(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 anchoredPos,
            Color backgroundColor,
            Color fillColor)
        {
            var root = CreateUIRect(name, parent, anchorMin, anchorMax, size, anchoredPos);
            // 创建背景图片（禁用射线检测以优化性能）
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = GetDefaultUISprite();
            bg.type = Image.Type.Sliced;
            bg.color = backgroundColor;
            bg.raycastTarget = false;

            // 创建填充条（使用水平填充模式）
            var fillRect = CreateUIRect("Fill", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = GetDefaultUISprite();
            fill.color = fillColor;
            fill.type = fill.sprite != null && fill.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            // 创建数值文本显示
            var textRect = CreateUIRect("Text", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = CreateText(textRect.gameObject, string.Empty, 12, TextAnchor.MiddleCenter, Color.white);

            var bar = root.gameObject.AddComponent<ValueBarUI>();
            SetComponentReference(bar, "fill", fill);
            SetComponentReference(bar, "valueText", text);

            return bar;
        }

        /// <summary>
        /// 创建施法条 UI
        /// 包含背景、填充条和技能名称标签
        /// </summary>
        private static CastBarUI CreateCastBar(Transform parent)
        {
            var root = CreateUIRect("CastBar", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300f, 16f), new Vector2(0f, 80f));
            
            // 背景
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = GetDefaultUISprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            // 填充条
            var fillRect = CreateUIRect("Fill", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = GetDefaultUISprite();
            fill.color = new Color(0.9f, 0.7f, 0.2f, 1f);
            fill.type = fill.sprite != null && fill.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            // 技能名称标签
            var labelRect = CreateUIRect("Label", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var label = CreateText(labelRect.gameObject, string.Empty, 12, TextAnchor.MiddleCenter, Color.white);

            // 添加 CastBarUI 组件
            var castBar = root.gameObject.AddComponent<CastBarUI>();
            SetComponentReference(castBar, "fill", fill);
            SetComponentReference(castBar, "label", label);

            return castBar;
        }

        /// <summary>
        /// 创建调试信息面板 UI
        /// </summary>
        private static CombatDebugOverlay CreateDebugOverlay(Transform parent)
        {
            var root = CreateUIRect("DebugOverlay", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(260f, 160f), new Vector2(-10f, -10f));
            root.pivot = new Vector2(1f, 1f);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = GetDefaultUISprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.5f);
            bg.raycastTarget = false;

            var textRect = CreateUIRect("Text", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            textRect.offsetMin = new Vector2(6f, 6f);
            textRect.offsetMax = new Vector2(-6f, -6f);
            var text = CreateText(textRect.gameObject, string.Empty, 12, TextAnchor.UpperLeft, Color.white);

            var overlay = root.gameObject.AddComponent<CombatDebugOverlay>();
            SetComponentReference(overlay, "outputText", text);
            SetComponentReference(overlay, "background", bg);
            return overlay;
        }

        /// <summary>
        /// 创建技能栏 UI
        /// 包含多个技能槽位，每个槽位显示图标、冷却进度和快捷键
        /// </summary>
        /// <param name="parent">父对象 Transform</param>
        /// <param name="slots">技能槽位数量</param>
        /// <returns>创建的 SkillBarUI 组件</returns>
        private static SkillBarUI CreateSkillBar(Transform parent, int slots)
        {
            var root = CreateUIRect("SkillBar", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(320f, 56f), new Vector2(0f, 20f));
            // 配置水平布局组件
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            // 创建技能槽位
            for (int i = 0; i < slots; i++)
            {
                var slot = CreateUIRect($"Slot_{i + 1}", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 48f), Vector2.zero);
                // 槽位背景
                var slotBg = slot.gameObject.AddComponent<Image>();
                slotBg.color = new Color(0f, 0f, 0f, 0.6f);
                slotBg.raycastTarget = false;
                slotBg.sprite = GetDefaultUISprite();
                slotBg.type = Image.Type.Sliced;
                // 技能图标
                var iconRect = CreateUIRect("Icon", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = GetDefaultUISprite();
                icon.raycastTarget = false;

                // 冷却进度遮罩（使用径向填充）
                var cooldownRect = CreateUIRect("Cooldown", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var cooldown = cooldownRect.gameObject.AddComponent<Image>();
                cooldown.sprite = GetDefaultUISprite();
                cooldown.color = new Color(0f, 0f, 0f, 0.6f);
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Radial360;
                cooldown.fillOrigin = 2;
                cooldown.fillAmount = 0f;
                cooldown.raycastTarget = false;

                // 冷却时间文本
                var cooldownTextRect = CreateUIRect("CooldownText", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var cooldownText = CreateText(cooldownTextRect.gameObject, string.Empty, 14, TextAnchor.MiddleCenter, Color.white);

                // 快捷键提示
                var keyRect = CreateUIRect("Key", slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, 16f), new Vector2(12f, -8f));
                var keyText = CreateText(keyRect.gameObject, string.Empty, 10, TextAnchor.MiddleLeft, Color.white);

                var slotUi = slot.gameObject.AddComponent<SkillSlotUI>();
                SetComponentReference(slotUi, "icon", icon);
                SetComponentReference(slotUi, "cooldownFill", cooldown);
                SetComponentReference(slotUi, "cooldownText", cooldownText);
                SetComponentReference(slotUi, "keyText", keyText);
            }

            return root.gameObject.AddComponent<SkillBarUI>();
        }

        /// <summary>
        /// 创建 Buff 栏 UI
        /// 显示 Buff/Debuff 图标、层数和剩余时间
        /// </summary>
        /// <param name="parent">父对象 Transform</param>
        /// <param name="slots">Buff 槽位数量</param>
        /// <returns>创建的 BuffBarUI 组件</returns>
        private static BuffBarUI CreateBuffBar(Transform parent, int slots)
        {
            var root = CreateUIRect("BuffBar", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(320f, 36f), new Vector2(-170f, -20f));
            // 配置水平布局组件（右上角对齐）
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 4f;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            // 创建 Buff 图标槽位
            for (int i = 0; i < slots; i++)
            {
                var iconRoot = CreateUIRect($"Buff_{i + 1}", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), Vector2.zero);
                // Buff 图标
                var icon = iconRoot.gameObject.AddComponent<Image>();
                icon.sprite = GetDefaultUISprite(); // 默认图标，运行时会被 Buff 定义覆盖
                icon.color = new Color(0f, 0f, 0f, 0.6f);
                icon.raycastTarget = false;

                // Buff 层数显示（右下角）
                var stackRect = CreateUIRect("Stacks", iconRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(18f, 14f), new Vector2(-2f, 2f));
                var stackText = CreateText(stackRect.gameObject, string.Empty, 10, TextAnchor.LowerRight, Color.white);

                // Buff 剩余时间显示（左上角）
                var timerRect = CreateUIRect("Timer", iconRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, 14f), new Vector2(2f, -2f));
                var timerText = CreateText(timerRect.gameObject, string.Empty, 10, TextAnchor.UpperLeft, Color.white);

                var iconUi = iconRoot.gameObject.AddComponent<BuffIconUI>();
                SetComponentReference(iconUi, "icon", icon);
                SetComponentReference(iconUi, "stackText", stackText);
                SetComponentReference(iconUi, "timerText", timerText);
            }

            return root.gameObject.AddComponent<BuffBarUI>();
        }

        /// <summary>
        /// 创建战斗日志 UI
        /// 显示战斗事件的文本记录
        /// </summary>
        /// <param name="parent">父对象 Transform</param>
        /// <returns>创建的 CombatLogUI 组件</returns>
        private static CombatLogUI CreateCombatLog(Transform parent)
        {
            // 设置 pivot 为左下角 (0, 0) 以便正确对齐
            var root = CreateUIRect("CombatLog", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(320f, 120f), new Vector2(10f, 10f));
            root.pivot = new Vector2(0f, 0f);
            // 半透明背景
            // 半透明背景
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = GetDefaultUISprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.4f);
            bg.raycastTarget = false;

            // 日志文本区域（带内边距）
            var textRect = CreateUIRect("LogText", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            textRect.offsetMin = new Vector2(6f, 6f);
            textRect.offsetMax = new Vector2(-6f, -6f);
            var text = CreateText(textRect.gameObject, string.Empty, 12, TextAnchor.LowerLeft, Color.white);

            var log = root.gameObject.AddComponent<CombatLogUI>();
            SetComponentReference(log, "logText", text);
            return log;
        }

        /// <summary>
        /// 创建飘字管理器
        /// 用于显示伤害数字、治疗量等战斗反馈
        /// </summary>
        /// <param name="parent">父对象 Transform</param>
        /// <returns>创建的 FloatingTextManager 组件</returns>
        private static FloatingTextManager CreateFloatingText(Transform parent)
        {
            var root = CreateUIRect("FloatingText", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // 创建飘字模板（对象池预制体）
            var template = CreateUIRect("FloatingTextTemplate", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 24f), Vector2.zero);
            var label = CreateText(template.gameObject, "0", 16, TextAnchor.MiddleCenter, Color.white);
            var group = template.gameObject.AddComponent<CanvasGroup>();
            var item = template.gameObject.AddComponent<FloatingTextItem>();
            SetComponentReference(item, "label", label);
            SetComponentReference(item, "canvasGroup", group);
            template.gameObject.SetActive(false); // 默认隐藏模板

            var manager = root.gameObject.AddComponent<FloatingTextManager>();
            SetComponentReference(manager, "itemPrefab", item);
            SetComponentReference(manager, "root", root);
            return manager;
        }

        /// <summary>
        /// 创建 UI 文本组件
        /// </summary>
        /// <param name="go">目标 GameObject</param>
        /// <param name="text">文本内容</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="anchor">对齐方式</param>
        /// <param name="color">文本颜色</param>
        /// <returns>创建的 Text 组件</returns>
        private static Text CreateText(GameObject go, string text, int fontSize, TextAnchor anchor, Color color)
        {
            var uiText = go.AddComponent<Text>();
            uiText.text = text;
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            uiText.alignment = anchor;
            uiText.color = color;
            uiText.raycastTarget = false; // 禁用射线检测优化性能
            return uiText;
        }

        /// <summary>
        /// 查找主相机
        /// 优先使用 MainCamera 标签，降级使用 FindObjectOfType（仅编辑器使用）
        /// </summary>
        /// <returns>找到的主相机，可能为 null</returns>
        /// <summary>
        /// 获取 Unity 内置的默认 UI Sprite
        /// </summary>
        private static Sprite GetDefaultUISprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static Camera FindMainCamera()
        {
            // 优先通过标签查找
            var cameraObject = GameObject.FindGameObjectWithTag("MainCamera");
            if (cameraObject != null)
            {
                return cameraObject.GetComponent<Camera>();
            }

            // 降级方案：查找场景中的第一个 Camera（编辑器工具可接受）
            return Object.FindObjectOfType<Camera>();
        }

        private static Camera SetupTopdownCamera()
        {
            var camera = FindMainCamera();
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }


            var target = Vector3.zero;
            var position = new Vector3(0f, 15f, -12f);
            var cameraTransform = camera.transform;
            cameraTransform.position = position;
            cameraTransform.LookAt(target);
            if (camera.orthographic)
            {
                camera.orthographic = false;
            }

            camera.fieldOfView = 40f;
            if (camera.GetComponent<GameplayCameraController>() == null)
            {
                camera.gameObject.AddComponent<GameplayCameraController>();
            }

            return camera;
        }

        private static GameObject CreateProjectilePrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return prefab;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile_ArcaneBolt";
            var collider = go.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            go.AddComponent<ProjectileController>();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject CreateSummonPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return prefab;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Summon_Totem";
            go.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            go.AddComponent<HealthComponent>();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject CreateWindWallPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return prefab;
            }

            var root = new GameObject("WindWall");
            root.transform.localScale = new Vector3(2.2f, 2f, 0.3f);
            root.AddComponent<TeamComponent>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one;
            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Object.DestroyImmediate(visualCollider);
            }

            var volume = root.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = Vector3.one;
            var interceptor = root.AddComponent<ProjectileInterceptorVolume>();
            SetComponentReference(interceptor, "team", root.GetComponent<TeamComponent>());

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
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
            Object[] blockedTags,
            TargetingOrigin origin = TargetingOrigin.Caster,
            bool allowEmpty = false,
            HitValidationPolicy hitValidation = HitValidationPolicy.AliveOnly,
            int lineOfSightMask = -1,
            float lineOfSightHeight = 1.5f,
            bool requireExplicitTarget = false,
            bool ignoreOptionalExplicitTarget = false)
        {
            var so = new SerializedObject(targeting);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.FindProperty("team").enumValueIndex = (int)team;
            so.FindProperty("origin").enumValueIndex = (int)origin;
            so.FindProperty("range").floatValue = range;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("angle").floatValue = angle;
            so.FindProperty("maxTargets").intValue = maxTargets;
            so.FindProperty("sort").enumValueIndex = (int)sort;
            so.FindProperty("includeSelf").boolValue = includeSelf;
            so.FindProperty("allowEmpty").boolValue = allowEmpty;
            so.FindProperty("requireExplicitTarget").boolValue = requireExplicitTarget;
            so.FindProperty("ignoreOptionalExplicitTarget").boolValue = ignoreOptionalExplicitTarget;
            so.FindProperty("hitValidation").enumValueIndex = (int)hitValidation;
            so.FindProperty("lineOfSightMask").intValue = lineOfSightMask;
            so.FindProperty("lineOfSightHeight").floatValue = lineOfSightHeight;
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
            Object[] blockedTags,
            ModifierScope scope = ModifierScope.Caster)
        {
            var so = new SerializedObject(modifier);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("target").enumValueIndex = (int)targetType;
            so.FindProperty("scope").enumValueIndex = (int)scope;
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
            DamageType damageType,
            float duration = 0f,
            float interval = 0f,
            StatDefinition scalingStat = null,
            float scalingRatio = 0f,
            bool canCrit = false,
            StatDefinition critChanceStat = null,
            StatDefinition critMultiplierStat = null,
            bool triggersOnHit = false,
            bool revealTarget = false,
            float revealDuration = 0f)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.Damage;
            so.FindProperty("value").floatValue = value;
            so.FindProperty("damageType").enumValueIndex = (int)damageType;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("interval").floatValue = interval;
            so.FindProperty("scalingStat").objectReferenceValue = scalingStat;
            so.FindProperty("scalingRatio").floatValue = scalingRatio;
            so.FindProperty("canCrit").boolValue = canCrit;
            so.FindProperty("critChanceStat").objectReferenceValue = critChanceStat;
            so.FindProperty("critMultiplierStat").objectReferenceValue = critMultiplierStat;
            so.FindProperty("triggersOnHit").boolValue = triggersOnHit;
            so.FindProperty("revealTarget").boolValue = revealTarget;
            so.FindProperty("revealDuration").floatValue = revealDuration;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectApplyBuff(
            EffectDefinition effect,
            string id,
            string displayName,
            BuffDefinition buff,
            TargetingDefinition overrideTargeting = null)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.ApplyBuff;
            so.FindProperty("buff").objectReferenceValue = buff;
            so.FindProperty("overrideTargeting").objectReferenceValue = overrideTargeting;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectRemoveBuff(
            EffectDefinition effect,
            string id,
            string displayName,
            BuffDefinition buff)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.RemoveBuff;
            so.FindProperty("buff").objectReferenceValue = buff;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectExecutionContext(
            EffectDefinition effect,
            ConditionDefinition condition = null,
            TargetingDefinition overrideTargeting = null)
        {
            if (effect == null)
            {
                return;
            }

            var so = new SerializedObject(effect);
            so.FindProperty("condition").objectReferenceValue = condition;
            so.FindProperty("overrideTargeting").objectReferenceValue = overrideTargeting;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectCombatState(
            EffectDefinition effect,
            string id,
            string displayName,
            CombatStateEffectMode mode,
            CombatStateFlags flags,
            int spellShieldCharges = 1)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.CombatState;
            so.FindProperty("combatStateMode").enumValueIndex = (int)mode;
            so.FindProperty("combatStateFlags").intValue = (int)flags;
            so.FindProperty("spellShieldCharges").intValue = Mathf.Max(0, spellShieldCharges);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectHeal(EffectDefinition effect, string id, string displayName, float value)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.Heal;
            so.FindProperty("value").floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectResource(EffectDefinition effect, string id, string displayName, float value, ResourceType resourceType)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.Resource;
            so.FindProperty("value").floatValue = value;
            so.FindProperty("resourceType").enumValueIndex = (int)resourceType;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectMove(
            EffectDefinition effect,
            string id,
            string displayName,
            MoveStyle moveStyle,
            float distance,
            float speed,
            TargetingDefinition overrideTargeting = null,
            MoveDestinationPolicy moveDestinationPolicy = MoveDestinationPolicy.Legacy,
            MoveCollisionPolicy moveCollisionPolicy = MoveCollisionPolicy.Default,
            float moveTargetOffset = 0.8f)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.Move;
            so.FindProperty("moveStyle").enumValueIndex = (int)moveStyle;
            var destinationPolicy = so.FindProperty("moveDestinationPolicy");
            if (destinationPolicy != null)
            {
                destinationPolicy.enumValueIndex = (int)moveDestinationPolicy;
            }

            var collisionPolicy = so.FindProperty("moveCollisionPolicy");
            if (collisionPolicy != null)
            {
                collisionPolicy.enumValueIndex = (int)moveCollisionPolicy;
            }

            var targetOffset = so.FindProperty("moveTargetOffset");
            if (targetOffset != null)
            {
                targetOffset.floatValue = moveTargetOffset;
            }

            so.FindProperty("moveDistance").floatValue = distance;
            so.FindProperty("moveSpeed").floatValue = speed;
            so.FindProperty("overrideTargeting").objectReferenceValue = overrideTargeting;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectProjectile(
            EffectDefinition effect,
            string id,
            string displayName,
            ProjectileDefinition projectile)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.Projectile;
            so.FindProperty("projectile").objectReferenceValue = projectile;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectSummon(
            EffectDefinition effect,
            string id,
            string displayName,
            UnitDefinition summonUnit,
            GameObject summonPrefab)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.Summon;
            so.FindProperty("summonUnit").objectReferenceValue = summonUnit;
            so.FindProperty("summonPrefab").objectReferenceValue = summonPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectTriggerSkill(
            EffectDefinition effect,
            string id,
            string displayName,
            SkillDefinition triggeredSkill)
        {
            var so = new SerializedObject(effect);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.TriggerSkill;
            so.FindProperty("triggeredSkill").objectReferenceValue = triggeredSkill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureProjectile(
            ProjectileDefinition projectile,
            string id,
            string displayName,
            GameObject prefab,
            float speed,
            float lifetime,
            float hitRadius,
            bool homing,
            float homingTurnSpeed,
            bool pierce,
            int maxPierce,
            Object[] onHitEffects,
            ProjectileBehaviorType behaviorType = ProjectileBehaviorType.Straight,
            float returnSpeedMultiplier = 1f,
            int splitCount = 0,
            float splitAngle = 35f,
            int maxSplitDepth = 1,
            float orbitRadius = 2.5f,
            float orbitAngularSpeed = 240f,
            float beamLength = 6f,
            bool forceStopOnFirstHit = false)
        {
            var so = new SerializedObject(projectile);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("lifetime").floatValue = lifetime;
            so.FindProperty("hitRadius").floatValue = hitRadius;
            so.FindProperty("behaviorType").enumValueIndex = (int)behaviorType;
            so.FindProperty("homing").boolValue = homing;
            so.FindProperty("homingTurnSpeed").floatValue = homingTurnSpeed;
            so.FindProperty("pierce").boolValue = pierce;
            so.FindProperty("maxPierce").intValue = maxPierce;
            so.FindProperty("returnSpeedMultiplier").floatValue = returnSpeedMultiplier;
            so.FindProperty("splitCount").intValue = splitCount;
            so.FindProperty("splitAngle").floatValue = splitAngle;
            so.FindProperty("maxSplitDepth").intValue = maxSplitDepth;
            so.FindProperty("orbitRadius").floatValue = orbitRadius;
            so.FindProperty("orbitAngularSpeed").floatValue = orbitAngularSpeed;
            so.FindProperty("beamLength").floatValue = beamLength;
            so.FindProperty("forceStopOnFirstHit").boolValue = forceStopOnFirstHit;
            SetObjectList(so.FindProperty("onHitEffects"), onHitEffects);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct ConditionEntryData
        {
            public ConditionType Type;
            public ConditionSubject Subject;
            public float Chance;
            public TagDefinition Tag;
            public BuffDefinition Buff;
            public ControlType ControlType;
            public int StackThreshold;
            public int PhaseThreshold;
            public float Threshold;
        }

        private static void ConfigureCondition(
            ConditionDefinition condition,
            string id,
            string displayName,
            ConditionOperator op,
            params ConditionEntryData[] entries)
        {
            var so = new SerializedObject(condition);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("op").enumValueIndex = (int)op;

            var list = so.FindProperty("entries");
            if (entries == null || entries.Length == 0)
            {
                list.arraySize = 0;
            }
            else
            {
                list.arraySize = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("type").enumValueIndex = (int)entries[i].Type;
                    entry.FindPropertyRelative("subject").enumValueIndex = (int)entries[i].Subject;
                    entry.FindPropertyRelative("chance").floatValue = entries[i].Chance;
                    entry.FindPropertyRelative("tag").objectReferenceValue = entries[i].Tag;
                    entry.FindPropertyRelative("buff").objectReferenceValue = entries[i].Buff;
                    entry.FindPropertyRelative("controlType").enumValueIndex = (int)entries[i].ControlType;
                    entry.FindPropertyRelative("stackThreshold").intValue = entries[i].StackThreshold;
                    entry.FindPropertyRelative("phaseThreshold").intValue = entries[i].PhaseThreshold;
                    entry.FindPropertyRelative("threshold").floatValue = entries[i].Threshold;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct BuffTriggerData
        {
            public BuffTriggerType Trigger;
            public float Chance;
            public ConditionDefinition Condition;
            public Object[] Effects;
        }

        private static void ConfigureBuff(
            BuffDefinition buff,
            string id,
            string displayName,
            bool isDebuff,
            float duration,
            float tickInterval,
            BuffStackingRule stackingRule,
            int maxStacks,
            Object[] tags,
            Object[] modifiers,
            BuffTriggerData[] triggers,
            ControlType[] controlEffects = null,
            ControlType[] controlImmunities = null,
            bool dispellable = true)
        {
            var so = new SerializedObject(buff);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("isDebuff").boolValue = isDebuff;
            so.FindProperty("dispellable").boolValue = dispellable;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("tickInterval").floatValue = tickInterval;
            so.FindProperty("stackingRule").enumValueIndex = (int)stackingRule;
            so.FindProperty("maxStacks").intValue = maxStacks;
            SetObjectList(so.FindProperty("tags"), tags);
            SetObjectList(so.FindProperty("modifiers"), modifiers);

            var triggerList = so.FindProperty("triggers");
            if (triggers == null || triggers.Length == 0)
            {
                triggerList.arraySize = 0;
            }
            else
            {
                triggerList.arraySize = triggers.Length;
                for (int i = 0; i < triggers.Length; i++)
                {
                    var trigger = triggerList.GetArrayElementAtIndex(i);
                    trigger.FindPropertyRelative("triggerType").enumValueIndex = (int)triggers[i].Trigger;
                    trigger.FindPropertyRelative("chance").floatValue = triggers[i].Chance;
                    trigger.FindPropertyRelative("condition").objectReferenceValue = triggers[i].Condition;
                    SetObjectList(trigger.FindPropertyRelative("effects"), triggers[i].Effects);
                }
            }

            SetEnumList(so.FindProperty("controlEffects"), controlEffects);
            SetEnumList(so.FindProperty("controlImmunities"), controlImmunities);

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
            public bool ExecuteOnce;
            public Object[] Effects;
        }

        private struct SkillCastConstraintData
        {
            public ConditionDefinition Condition;
            public SkillCastFailReason FailReason;
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
            SkillStepData step,
            float postCastTime = 0f,
            float gcdDuration = 0f,
            float channelTickInterval = 0f,
            float queueWindow = 0f,
            SkillQueuePolicy queuePolicy = SkillQueuePolicy.Replace,
            TargetSnapshotPolicy targetSnapshotPolicy = TargetSnapshotPolicy.AtCastStart,
            bool ammoEnabled = false,
            int ammoMaxCharges = 0,
            int ammoInitialCharges = 0,
            float ammoRechargeTime = 0f,
            bool recastEnabled = false,
            int recastMaxCount = 0,
            float recastWindow = 0f,
            bool consumesResourceOnRecast = true,
            bool delayCooldownUntilRecastEnds = true,
            RecastTargetPolicy recastTargetPolicy = RecastTargetPolicy.AnyValid)
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
            so.FindProperty("postCastTime").floatValue = postCastTime;
            so.FindProperty("gcdDuration").floatValue = gcdDuration;
            so.FindProperty("channelTickInterval").floatValue = channelTickInterval;
            so.FindProperty("queueWindow").floatValue = queueWindow;
            so.FindProperty("queuePolicy").enumValueIndex = (int)queuePolicy;
            so.FindProperty("targetSnapshotPolicy").enumValueIndex = (int)targetSnapshotPolicy;
            so.FindProperty("targeting").objectReferenceValue = targeting;
            SetObjectList(so.FindProperty("tags"), tags);

            var ammo = so.FindProperty("ammoConfig");
            if (ammo != null)
            {
                var maxCharges = Mathf.Max(0, ammoMaxCharges);
                ammo.FindPropertyRelative("enabled").boolValue = ammoEnabled && maxCharges > 0;
                ammo.FindPropertyRelative("maxCharges").intValue = maxCharges;
                ammo.FindPropertyRelative("initialCharges").intValue = Mathf.Clamp(ammoInitialCharges, 0, maxCharges);
                ammo.FindPropertyRelative("rechargeTime").floatValue = Mathf.Max(0f, ammoRechargeTime);
            }

            var recast = so.FindProperty("recastConfig");
            if (recast != null)
            {
                recast.FindPropertyRelative("enabled").boolValue = recastEnabled && recastMaxCount > 0 && recastWindow > 0f;
                recast.FindPropertyRelative("maxRecasts").intValue = Mathf.Max(0, recastMaxCount);
                recast.FindPropertyRelative("recastWindow").floatValue = Mathf.Max(0f, recastWindow);
                recast.FindPropertyRelative("consumesResourceOnRecast").boolValue = consumesResourceOnRecast;
                recast.FindPropertyRelative("delayCooldownUntilRecastEnds").boolValue = delayCooldownUntilRecastEnds;
                recast.FindPropertyRelative("targetPolicy").enumValueIndex = (int)recastTargetPolicy;
            }

            var steps = so.FindProperty("steps");
            steps.arraySize = 1;
            var stepProp = steps.GetArrayElementAtIndex(0);
            stepProp.FindPropertyRelative("trigger").enumValueIndex = (int)step.Trigger;
            stepProp.FindPropertyRelative("delay").floatValue = step.Delay;
            stepProp.FindPropertyRelative("condition").objectReferenceValue = step.Condition;
            SetObjectList(stepProp.FindPropertyRelative("effects"), step.Effects);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSkillSteps(SkillDefinition skill, params SkillStepData[] steps)
        {
            if (skill == null)
            {
                return;
            }

            var so = new SerializedObject(skill);
            var list = so.FindProperty("steps");
            if (list == null)
            {
                return;
            }

            if (steps == null || steps.Length == 0)
            {
                list.arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            list.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++)
            {
                var stepProp = list.GetArrayElementAtIndex(i);
                stepProp.FindPropertyRelative("trigger").enumValueIndex = (int)steps[i].Trigger;
                stepProp.FindPropertyRelative("delay").floatValue = steps[i].Delay;
                stepProp.FindPropertyRelative("condition").objectReferenceValue = steps[i].Condition;
                var executeOnceProp = stepProp.FindPropertyRelative("executeOnce");
                if (executeOnceProp != null)
                {
                    executeOnceProp.boolValue = steps[i].ExecuteOnce;
                }
                SetObjectList(stepProp.FindPropertyRelative("effects"), steps[i].Effects);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSkillCastConstraints(SkillDefinition skill, params SkillCastConstraintData[] constraints)
        {
            if (skill == null)
            {
                return;
            }

            var so = new SerializedObject(skill);
            var list = so.FindProperty("castConstraints");
            if (list == null)
            {
                return;
            }

            if (constraints == null || constraints.Length == 0)
            {
                list.arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            list.arraySize = constraints.Length;
            for (int i = 0; i < constraints.Length; i++)
            {
                var constraint = list.GetArrayElementAtIndex(i);
                constraint.FindPropertyRelative("condition").objectReferenceValue = constraints[i].Condition;
                constraint.FindPropertyRelative("failReason").enumValueIndex = (int)constraints[i].FailReason;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSkillSequence(
            SkillDefinition skill,
            bool enabled,
            int maxPhases,
            float resetWindow,
            SkillSequenceOverflowPolicy overflowPolicy,
            bool resetOnOtherSkillCast,
            bool advanceOnHit = false)
        {
            if (skill == null)
            {
                return;
            }

            var so = new SerializedObject(skill);
            var sequence = so.FindProperty("sequenceConfig");
            if (sequence == null)
            {
                return;
            }

            var clampedMax = Mathf.Max(1, maxPhases);
            sequence.FindPropertyRelative("enabled").boolValue = enabled && clampedMax > 1;
            sequence.FindPropertyRelative("maxPhases").intValue = clampedMax;
            sequence.FindPropertyRelative("resetWindow").floatValue = Mathf.Max(0f, resetWindow);
            sequence.FindPropertyRelative("overflowPolicy").enumValueIndex = (int)overflowPolicy;
            sequence.FindPropertyRelative("resetOnOtherSkillCast").boolValue = resetOnOtherSkillCast;
            var advanceOnHitProperty = sequence.FindPropertyRelative("advanceOnHit");
            if (advanceOnHitProperty != null)
            {
                advanceOnHitProperty.boolValue = advanceOnHit;
            }
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
            AIProfile aiProfile,
            GameObject prefab = null)
        {
            var so = new SerializedObject(unit);
            SetDefinitionBase(so, id, displayName);
            var prefabProp = so.FindProperty("prefab");
            if (prefabProp != null && prefab != null)
            {
                prefabProp.objectReferenceValue = prefab;
            }
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

        private static void ConfigureUnitVisualProfile(UnitDefinition unit, UnitVisualProfile visualProfile)
        {
            if (unit == null)
            {
                return;
            }

            var so = new SerializedObject(unit);
            so.FindProperty("visualProfile").objectReferenceValue = visualProfile;
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

        private static void ConfigureProgression(
            ProgressionDefinition progression,
            string id,
            string displayName,
            int startLevel,
            int maxLevel,
            int baseXpToNext,
            float xpGrowth,
            int startingAttributePoints,
            int attributePointsPerLevel)
        {
            var so = new SerializedObject(progression);
            SetDefinitionBase(so, id, displayName);
            so.FindProperty("startLevel").intValue = startLevel;
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.FindProperty("baseXpToNext").intValue = baseXpToNext;
            so.FindProperty("xpGrowth").floatValue = xpGrowth;
            so.FindProperty("startingAttributePoints").intValue = startingAttributePoints;
            so.FindProperty("attributePointsPerLevel").intValue = attributePointsPerLevel;

            var xpOverrides = so.FindProperty("xpOverrides");
            if (xpOverrides != null)
            {
                xpOverrides.arraySize = 0;
            }

            var pointOverrides = so.FindProperty("attributePointOverrides");
            if (pointOverrides != null)
            {
                pointOverrides.arraySize = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDatabase(SampleAssets assets)
        {
            var so = new SerializedObject(assets.Database);
            SetObjectList(so.FindProperty("stats"), new Object[]
            {
                assets.MaxHealth,
                assets.HealthRegen,
                assets.MaxMana,
                assets.ManaRegen,
                assets.MoveSpeed,
                assets.AttackPower,
                assets.AbilityPower,
                assets.AttackSpeed,
                assets.CritChance,
                assets.CritMultiplier,
                assets.Armor,
                assets.MagicResist,
                assets.ArmorPenFlat,
                assets.ArmorPenPercent,
                assets.MagicPenFlat,
                assets.MagicPenPercent,
                assets.AbilityHaste,
                assets.Lifesteal,
                assets.Omnivamp,
                assets.Tenacity
            });
            SetObjectList(so.FindProperty("tags"), new Object[] { assets.TagPlayer, assets.TagEnemy, assets.TagMagic, assets.TagFire, assets.TagPhysical, assets.TagNature });
            SetObjectList(so.FindProperty("units"), new Object[] { assets.UnitPlayer, assets.UnitEnemy, assets.UnitEnemyHighHp, assets.UnitSummonTotem, assets.UnitPlayerYasuo });
            SetObjectList(so.FindProperty("progressions"), new Object[] { assets.ProgressionDefault });
            SetObjectList(so.FindProperty("skills"), new Object[]
            {
                assets.SkillBasicAttack,
                assets.SkillCleave,
                assets.SkillChainLightning,
                assets.SkillRandomShot,
                assets.SkillExecute,
                assets.SkillHeal,
                assets.SkillDash,
                assets.SkillLineStrike,
                assets.SkillBoxField,
                assets.SkillArcaneBolt,
                assets.SkillShockwave,
                assets.SkillBleedStrike,
                assets.SkillPoisonDart,
                assets.SkillStoneSkin,
                assets.SkillMagicWard,
                assets.SkillQuickCast,
                assets.SkillTimeWarp,
                assets.SkillManaSurge,
                assets.SkillSummonTotem,
                assets.SkillTriggerFocus,
                assets.SkillYasuoQ,
                assets.SkillYasuoW,
                assets.SkillYasuoE,
                assets.SkillYasuoR
            });
            SetObjectList(so.FindProperty("buffs"), new Object[]
            {
                assets.BuffBurn,
                assets.BuffArcaneFocus,
                assets.BuffBleed,
                assets.BuffPoison,
                assets.BuffStoneSkin,
                assets.BuffMagicWard,
                assets.BuffQuickCast,
                assets.BuffTimeWarp,
                assets.BuffGhostStep,
                assets.BuffIronShell,
                assets.BuffYasuoDashMark,
                assets.BuffYasuoEDashState,
                assets.BuffYasuoAirborneTag,
                assets.BuffYasuoLastBreathState,
                assets.BuffYasuoLastBreathSuspend
            });
            SetObjectList(so.FindProperty("effects"), new Object[]
            {
                assets.EffectBasicAttack,
                assets.EffectFireball,
                assets.EffectApplyBurn,
                assets.EffectBurnTick,
                assets.EffectApplyArcaneFocus,
                assets.EffectCleaveDamage,
                assets.EffectChainDamage,
                assets.EffectRandomShotDamage,
                assets.EffectExecuteDamage,
                assets.EffectHealSmall,
                assets.EffectRestoreMana,
                assets.EffectDash,
                assets.EffectLineDamage,
                assets.EffectBoxDamage,
                assets.EffectBleedTick,
                assets.EffectApplyBleed,
                assets.EffectPoisonTick,
                assets.EffectApplyPoison,
                assets.EffectShockwaveDot,
                assets.EffectArcaneBoltHit,
                assets.EffectArcaneBoltProjectile,
                assets.EffectSummonTotem,
                assets.EffectTriggerArcaneFocus,
                assets.EffectApplyStoneSkin,
                assets.EffectApplyMagicWard,
                assets.EffectApplyQuickCast,
                assets.EffectApplyTimeWarp,
                assets.EffectCombatStateAddUntargetable,
                assets.EffectCombatStateRemoveUntargetable,
                assets.EffectCombatStateAddInvulnerable,
                assets.EffectCombatStateRemoveInvulnerable,
                assets.EffectCombatStateGrantSpellShield,
                assets.EffectApplyGhostStep,
                assets.EffectApplyIronShell,
                assets.EffectAmmoBurstDamage,
                assets.EffectRecastLungeMove,
                assets.EffectReturnBladeDamage,
                assets.EffectReturnBladeProjectile,
                assets.EffectShardVolleyDamage,
                assets.EffectShardVolleyProjectile,
                assets.EffectRevealBoltDamage,
                assets.EffectWindWallSummon,
                assets.EffectYasuoQThrustDamage,
                assets.EffectYasuoQTornadoDamage,
                assets.EffectYasuoQTornadoApplyAirborneTag,
                assets.EffectYasuoQTornadoProjectile,
                assets.EffectYasuoEDashMove,
                assets.EffectYasuoEDashDamage,
                assets.EffectYasuoEApplyDashMark,
                assets.EffectYasuoEApplyDashState,
                assets.EffectYasuoQEqSlashDamage,
                assets.EffectYasuoQEqSlashEmpoweredDamage,
                assets.EffectYasuoQEqSlashApplyAirborneTag,
                assets.EffectYasuoRDashMove,
                assets.EffectYasuoRApplyLastBreathState,
                assets.EffectYasuoRApplyTargetSuspend,
                assets.EffectYasuoRDamage,
                assets.EffectYasuoRRemoveAirborneTag
            });
            SetObjectList(so.FindProperty("conditions"), new Object[]
            {
                assets.ConditionTargetLowHealth,
                assets.ConditionYasuoQPhase1,
                assets.ConditionYasuoQPhase2,
                assets.ConditionYasuoQPhase3,
                assets.ConditionYasuoQEqPhase1,
                assets.ConditionYasuoQEqPhase2,
                assets.ConditionYasuoQEqPhase3,
                assets.ConditionYasuoETargetNotDashed,
                assets.ConditionYasuoRTargetAirborne
            });
            SetObjectList(so.FindProperty("modifiers"), new Object[]
            {
                assets.ModifierSkillCost,
                assets.ModifierSkillCooldown,
                assets.ModifierFireBonus,
                assets.ModifierMaxMana,
                assets.ModifierResistPhysical,
                assets.ModifierResistMagical,
                assets.ModifierQuickCastTime,
                assets.ModifierQuickChannelTime,
                assets.ModifierEffectDuration,
                assets.ModifierEffectInterval
            });
            SetObjectList(so.FindProperty("projectiles"), new Object[] { assets.ProjectileArcaneBolt, assets.ProjectileReturnBlade, assets.ProjectileShardVolley, assets.ProjectileYasuoQTornado });
            SetObjectList(so.FindProperty("targetings"), new Object[]
            {
                assets.TargetingSingleEnemy,
                assets.TargetingBasicAttack,
                assets.TargetingSelf,
                assets.TargetingConeEnemy,
                assets.TargetingSphereEnemy,
                assets.TargetingChainEnemy,
                assets.TargetingRandomEnemy,
                assets.TargetingLineEnemy,
                assets.TargetingBoxEnemy,
                assets.TargetingAllySingle,
                assets.TargetingPointAny,
                assets.TargetingYasuoSingleEnemy,
                assets.TargetingYasuoQLineEnemy,
                assets.TargetingYasuoQEqSlashEnemy,
                assets.TargetingYasuoRSingleEnemy,
                assets.TargetingYasuoRAirborneCluster
            });
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

        private static void SetEnumList<TEnum>(SerializedProperty property, TEnum[] items) where TEnum : struct, System.Enum
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
                property.GetArrayElementAtIndex(i).enumValueIndex = System.Convert.ToInt32(items[i]);
            }
        }

        private static void SetComponentReference(Object component, string propertyName, Object value)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"Property '{propertyName}' not found on {component.GetType().Name}");
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

        /// <summary>
        /// 确保场景中存在 EventSystem
        /// 用于处理 UI 交互事件
        /// </summary>
        private static void EnsureEventSystem()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                var legacy = existing.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    Object.DestroyImmediate(legacy);
                }

                var module = existing.GetComponent<InputSystemUIInputModule>();
                if (module == null)
                {
                    module = existing.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                ConfigureInputSystemUiModule(module);
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ConfigureInputSystemUiModule(eventSystem.GetComponent<InputSystemUIInputModule>());
        }

        private static void EnsureInputRoot()
        {
            if (Object.FindFirstObjectByType<InputReader>() != null)
            {
                return;
            }

            var inputRoot = new GameObject("InputRoot", typeof(InputReader));
            var inputReader = inputRoot.GetComponent<InputReader>();
            var actions = LoadInputActions();
            if (actions != null)
            {
                SetComponentReference(inputReader, "actions", actions);
            }
        }

        private static void ConfigureInputSystemUiModule(InputSystemUIInputModule module)
        {
            if (module == null)
            {
                return;
            }

            var actions = LoadInputActions();
            if (actions == null)
            {
                Debug.LogWarning("[CombatSampleGenerator] CombatInputActions not found. UI input bindings not configured.");
                return;
            }

            var so = new SerializedObject(module);
            SetSerializedReference(so, "actionsAsset", actions);
            SetSerializedReference(so, "point", CreateActionReference(actions, $"{CombatInputIds.UIMap}/{CombatInputIds.UIPoint}"));
            SetSerializedReference(so, "leftClick", CreateActionReference(actions, $"{CombatInputIds.UIMap}/{CombatInputIds.UIClick}"));
            SetSerializedReference(so, "scrollWheel", CreateActionReference(actions, $"{CombatInputIds.UIMap}/{CombatInputIds.UIScroll}"));
            SetSerializedReference(so, "move", CreateActionReference(actions, $"{CombatInputIds.UIMap}/{CombatInputIds.UINavigate}"));
            SetSerializedReference(so, "submit", CreateActionReference(actions, $"{CombatInputIds.UIMap}/{CombatInputIds.UISubmit}"));
            SetSerializedReference(so, "cancel", CreateActionReference(actions, $"{CombatInputIds.UIMap}/{CombatInputIds.UICancel}"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InputActionAsset LoadInputActions()
        {
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Input/CombatInputActions.inputactions");
        }

        private static InputActionReference CreateActionReference(InputActionAsset asset, string actionPath)
        {
            var action = asset != null ? asset.FindAction(actionPath) : null;
            return action != null ? InputActionReference.Create(action) : null;
        }

        private static void SetSerializedReference(SerializedObject serializedObject, string propertyName, Object value)
        {
            var prop = serializedObject.FindProperty(propertyName);
            if (prop == null)
            {
                return;
            }

            prop.objectReferenceValue = value;
        }
    }
}
