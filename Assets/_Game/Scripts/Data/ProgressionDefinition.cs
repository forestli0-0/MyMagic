using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// Progression configuration for levels, experience curve, and attribute points.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Progression/Progression Definition", fileName = "Progression_")]
    public class ProgressionDefinition : DefinitionBase
    {
        [Header("Level Range")]
        [SerializeField] private int startLevel = 1;
        [SerializeField] private int maxLevel = 20;

        [Header("Experience")]
        [SerializeField] private int baseXpToNext = 100;
        [SerializeField] private float xpGrowth = 1.2f;
        [SerializeField] private List<int> xpOverrides = new List<int>();

        [Header("Attribute Points")]
        [SerializeField] private int startingAttributePoints = 0;
        [SerializeField] private int attributePointsPerLevel = 1;
        [SerializeField] private List<int> attributePointOverrides = new List<int>();

        public int StartLevel => startLevel;
        public int MaxLevel => maxLevel;
        public int StartingAttributePoints => startingAttributePoints;

        public int GetXpToNext(int level)
        {
            if (level < startLevel || level >= maxLevel)
            {
                return 0;
            }

            var index = level - startLevel;
            if (index >= 0 && index < xpOverrides.Count && xpOverrides[index] > 0)
            {
                return xpOverrides[index];
            }

            var scaled = baseXpToNext * Mathf.Pow(Mathf.Max(1f, xpGrowth), index);
            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

        public int GetAttributePointsForLevel(int level)
        {
            if (level <= startLevel || level > maxLevel)
            {
                return 0;
            }

            var index = level - startLevel - 1;
            if (index >= 0 && index < attributePointOverrides.Count && attributePointOverrides[index] > 0)
            {
                return attributePointOverrides[index];
            }

            return Mathf.Max(0, attributePointsPerLevel);
        }

        public int GetTotalXpForLevel(int level)
        {
            var targetLevel = Mathf.Clamp(level, startLevel, maxLevel);
            var total = 0;
            for (var lv = startLevel; lv < targetLevel; lv++)
            {
                total += GetXpToNext(lv);
            }

            return total;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            startLevel = Mathf.Max(1, startLevel);
            maxLevel = Mathf.Max(startLevel, maxLevel);
            baseXpToNext = Mathf.Max(0, baseXpToNext);
            xpGrowth = Mathf.Max(1f, xpGrowth);
            startingAttributePoints = Mathf.Max(0, startingAttributePoints);
            attributePointsPerLevel = Mathf.Max(0, attributePointsPerLevel);
        }
#endif
    }
}
