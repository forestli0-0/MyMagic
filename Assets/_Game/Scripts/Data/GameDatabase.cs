using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 战斗系统核心数据库，负责统一管理和运行时索引所有 ScriptableObject 配置。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Database/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        [Header("基础配置")]
        [SerializeField] private List<StatDefinition> stats = new List<StatDefinition>();
        [SerializeField] private List<TagDefinition> tags = new List<TagDefinition>();
        
        [Header("单位与技能")]
        [SerializeField] private List<UnitDefinition> units = new List<UnitDefinition>();
        [SerializeField] private List<SkillDefinition> skills = new List<SkillDefinition>();

        [Header("关卡与流程")]
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();

        [Header("成长与进度")]
        [SerializeField] private List<ProgressionDefinition> progressions = new List<ProgressionDefinition>();

        [Header("物品与掉落")]
        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();
        [SerializeField] private List<AffixDefinition> affixes = new List<AffixDefinition>();
        
        [Header("战斗逻辑")]
        [SerializeField] private List<BuffDefinition> buffs = new List<BuffDefinition>();
        [SerializeField] private List<EffectDefinition> effects = new List<EffectDefinition>();
        [SerializeField] private List<ConditionDefinition> conditions = new List<ConditionDefinition>();
        [SerializeField] private List<ModifierDefinition> modifiers = new List<ModifierDefinition>();
        
        [Header("机械与表现")]
        [SerializeField] private List<ProjectileDefinition> projectiles = new List<ProjectileDefinition>();
        [SerializeField] private List<TargetingDefinition> targetings = new List<TargetingDefinition>();
        
        [Header("其他")]
        [SerializeField] private List<AIProfile> aiProfiles = new List<AIProfile>();
        [SerializeField] private List<HUDConfig> hudConfigs = new List<HUDConfig>();

        // 运行时字典索引，用于快速查找
        private Dictionary<string, StatDefinition> statsById;
        private Dictionary<string, TagDefinition> tagsById;
        private Dictionary<string, UnitDefinition> unitsById;
        private Dictionary<string, SkillDefinition> skillsById;
        private Dictionary<string, LevelDefinition> levelsById;
        private Dictionary<string, ProgressionDefinition> progressionsById;
        private Dictionary<string, ItemDefinition> itemsById;
        private Dictionary<string, AffixDefinition> affixesById;
        private Dictionary<string, BuffDefinition> buffsById;
        private Dictionary<string, EffectDefinition> effectsById;
        private Dictionary<string, ConditionDefinition> conditionsById;
        private Dictionary<string, ModifierDefinition> modifiersById;
        private Dictionary<string, ProjectileDefinition> projectilesById;
        private Dictionary<string, TargetingDefinition> targetingsById;
        private Dictionary<string, AIProfile> aiProfilesById;
        private Dictionary<string, HUDConfig> hudConfigsById;

        public IReadOnlyList<StatDefinition> Stats => stats;
        public IReadOnlyList<TagDefinition> Tags => tags;
        public IReadOnlyList<UnitDefinition> Units => units;
        public IReadOnlyList<SkillDefinition> Skills => skills;
        public IReadOnlyList<LevelDefinition> Levels => levels;
        public IReadOnlyList<ProgressionDefinition> Progressions => progressions;
        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyList<AffixDefinition> Affixes => affixes;
        public IReadOnlyList<BuffDefinition> Buffs => buffs;
        public IReadOnlyList<EffectDefinition> Effects => effects;
        public IReadOnlyList<ConditionDefinition> Conditions => conditions;
        public IReadOnlyList<ModifierDefinition> Modifiers => modifiers;
        public IReadOnlyList<ProjectileDefinition> Projectiles => projectiles;
        public IReadOnlyList<TargetingDefinition> Targetings => targetings;
        public IReadOnlyList<AIProfile> AIProfiles => aiProfiles;
        public IReadOnlyList<HUDConfig> HUDConfigs => hudConfigs;

        private void OnEnable()
        {
            BuildIndexes();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            BuildIndexes();
        }
#endif

        /// <summary>
        /// 构建所有配置的快速查找字典。
        /// </summary>
        public void BuildIndexes()
        {
            statsById = BuildIndex(stats);
            tagsById = BuildIndex(tags);
            unitsById = BuildIndex(units);
            skillsById = BuildIndex(skills);
            levelsById = BuildIndex(levels);
            progressionsById = BuildIndex(progressions);
            itemsById = BuildIndex(items);
            affixesById = BuildIndex(affixes);
            buffsById = BuildIndex(buffs);
            effectsById = BuildIndex(effects);
            conditionsById = BuildIndex(conditions);
            modifiersById = BuildIndex(modifiers);
            projectilesById = BuildIndex(projectiles);
            targetingsById = BuildIndex(targetings);
            aiProfilesById = BuildIndex(aiProfiles);
            hudConfigsById = BuildIndex(hudConfigs);
        }

        public StatDefinition GetStat(string id)
        {
            EnsureIndexes();
            return GetById(statsById, id);
        }

        public TagDefinition GetTag(string id)
        {
            EnsureIndexes();
            return GetById(tagsById, id);
        }

        public UnitDefinition GetUnit(string id)
        {
            EnsureIndexes();
            return GetById(unitsById, id);
        }

        public SkillDefinition GetSkill(string id)
        {
            EnsureIndexes();
            return GetById(skillsById, id);
        }

        public LevelDefinition GetLevel(string id)
        {
            EnsureIndexes();
            return GetById(levelsById, id);
        }

        public ProgressionDefinition GetProgression(string id)
        {
            EnsureIndexes();
            return GetById(progressionsById, id);
        }

        public ItemDefinition GetItem(string id)
        {
            EnsureIndexes();
            return GetById(itemsById, id);
        }

        public AffixDefinition GetAffix(string id)
        {
            EnsureIndexes();
            return GetById(affixesById, id);
        }

        public BuffDefinition GetBuff(string id)
        {
            EnsureIndexes();
            return GetById(buffsById, id);
        }

        public EffectDefinition GetEffect(string id)
        {
            EnsureIndexes();
            return GetById(effectsById, id);
        }

        public ConditionDefinition GetCondition(string id)
        {
            EnsureIndexes();
            return GetById(conditionsById, id);
        }

        public ModifierDefinition GetModifier(string id)
        {
            EnsureIndexes();
            return GetById(modifiersById, id);
        }

        public ProjectileDefinition GetProjectile(string id)
        {
            EnsureIndexes();
            return GetById(projectilesById, id);
        }

        public TargetingDefinition GetTargeting(string id)
        {
            EnsureIndexes();
            return GetById(targetingsById, id);
        }

        public AIProfile GetAIProfile(string id)
        {
            EnsureIndexes();
            return GetById(aiProfilesById, id);
        }

        public HUDConfig GetHUDConfig(string id)
        {
            EnsureIndexes();
            return GetById(hudConfigsById, id);
        }

        private void EnsureIndexes()
        {
            if (statsById == null)
            {
                BuildIndexes();
            }
        }

        private static Dictionary<string, T> BuildIndex<T>(List<T> list) where T : DefinitionBase
        {
            var dict = new Dictionary<string, T>(list.Count, StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null)
                {
                    continue;
                }

                var key = item.Id;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                dict[key] = item;
            }

            return dict;
        }

        private static T GetById<T>(Dictionary<string, T> dict, string id) where T : DefinitionBase
        {
            if (dict == null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (dict.TryGetValue(id, out var value))
            {
                return value;
            }

            return null;
        }
    }
}
