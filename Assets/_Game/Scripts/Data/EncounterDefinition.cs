using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 遭遇配置定义，描述本次战斗应生成的敌人波次与精英规则。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Encounters/Encounter Definition", fileName = "Encounter_")]
    public class EncounterDefinition : DefinitionBase
    {
        [SerializeField] private List<EncounterWaveDefinition> waves = new List<EncounterWaveDefinition>();
        [SerializeField] private List<EnemyAffixDefinition> globalEliteAffixes = new List<EnemyAffixDefinition>();
        [SerializeField] private float spawnRadius = 8f;
        [SerializeField] private int randomSeed = 0;

        public IReadOnlyList<EncounterWaveDefinition> Waves => waves;
        public IReadOnlyList<EnemyAffixDefinition> GlobalEliteAffixes => globalEliteAffixes;
        public float SpawnRadius => Mathf.Max(0.5f, spawnRadius);
        public int RandomSeed => randomSeed;
    }

    [System.Serializable]
    public class EncounterWaveDefinition
    {
        [SerializeField] private string waveId = "wave";
        [SerializeField] private UnitDefinition unit;
        [SerializeField] private GameObject prefabOverride;
        [SerializeField] private int minCount = 1;
        [SerializeField] private int maxCount = 1;
        [SerializeField] private int guaranteedEliteCount = 0;
        [Range(0f, 1f)]
        [SerializeField] private float eliteChance;
        [SerializeField] private List<EnemyAffixDefinition> eliteAffixes = new List<EnemyAffixDefinition>();

        public string WaveId => waveId;
        public UnitDefinition Unit => unit;
        public GameObject PrefabOverride => prefabOverride;
        public int MinCount => Mathf.Max(0, minCount);
        public int MaxCount => Mathf.Max(MinCount, maxCount);
        public int GuaranteedEliteCount => Mathf.Max(0, guaranteedEliteCount);
        public float EliteChance => Mathf.Clamp01(eliteChance);
        public IReadOnlyList<EnemyAffixDefinition> EliteAffixes => eliteAffixes;
    }
}
