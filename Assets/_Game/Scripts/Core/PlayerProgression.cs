using System;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// Runtime progression state and leveling logic.
    /// </summary>
    public class PlayerProgression : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private ProgressionDefinition progression;
        [SerializeField] private CombatEventHub eventHub;

        [Header("State")]
        [SerializeField] private int level = 1;
        [SerializeField] private int currentExperience;
        [SerializeField] private int unspentAttributePoints;
        [SerializeField] private bool initializeOnAwake = true;

        public event Action<ExperienceChangedEvent> ExperienceChanged;
        public event Action<LevelChangedEvent> LevelChanged;
        public event Action<AttributePointsChangedEvent> AttributePointsChanged;

        public ProgressionDefinition Definition => progression;
        public int Level => level;
        public int CurrentExperience => currentExperience;
        public int UnspentAttributePoints => unspentAttributePoints;
        public int MaxLevel => progression != null ? progression.MaxLevel : level;
        public int ExperienceToNextLevel => progression != null ? progression.GetXpToNext(level) : 0;
        public float ExperienceNormalized => ExperienceToNextLevel > 0 ? (float)currentExperience / ExperienceToNextLevel : 0f;

        private void Awake()
        {
            if (initializeOnAwake && progression != null)
            {
                Initialize(progression);
            }
        }

        public void SetEventHub(CombatEventHub hub)
        {
            eventHub = hub;
        }

        public void Initialize(ProgressionDefinition definition)
        {
            progression = definition;
            if (progression == null)
            {
                level = Mathf.Max(1, level);
                currentExperience = Mathf.Max(0, currentExperience);
                unspentAttributePoints = Mathf.Max(0, unspentAttributePoints);
                return;
            }

            level = Mathf.Clamp(progression.StartLevel, progression.StartLevel, progression.MaxLevel);
            currentExperience = 0;
            unspentAttributePoints = progression.StartingAttributePoints;
        }

        public void ApplyState(int newLevel, int experience, int attributePoints)
        {
            ApplyState(newLevel, experience, attributePoints, false);
        }

        public void ApplyState(int newLevel, int experience, int attributePoints, bool raiseEvents)
        {
            var oldLevel = level;
            var oldExperience = currentExperience;
            var oldPoints = unspentAttributePoints;

            if (progression != null)
            {
                level = Mathf.Clamp(newLevel, progression.StartLevel, progression.MaxLevel);
            }
            else
            {
                level = Mathf.Max(1, newLevel);
            }

            currentExperience = Mathf.Max(0, experience);
            unspentAttributePoints = Mathf.Max(0, attributePoints);

            if (!raiseEvents)
            {
                return;
            }

            if (oldLevel != level)
            {
                RaiseLevelChanged(oldLevel, level);
            }

            if (oldExperience != currentExperience)
            {
                RaiseExperienceChanged(oldExperience, currentExperience);
            }

            if (oldPoints != unspentAttributePoints)
            {
                RaiseAttributePointsChanged(oldPoints, unspentAttributePoints);
            }
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            var oldExperience = currentExperience;
            var oldLevel = level;
            currentExperience = Mathf.Max(0, currentExperience + amount);

            if (progression != null)
            {
                var maxLevel = progression.MaxLevel;
                while (level < maxLevel)
                {
                    var xpToNext = progression.GetXpToNext(level);
                    if (xpToNext <= 0 || currentExperience < xpToNext)
                    {
                        break;
                    }

                    currentExperience -= xpToNext;
                    var previousLevel = level;
                    level = Mathf.Min(level + 1, maxLevel);

                    var gainedPoints = progression.GetAttributePointsForLevel(level);
                    if (gainedPoints > 0)
                    {
                        AddAttributePointsInternal(gainedPoints);
                    }

                    RaiseLevelChanged(previousLevel, level);
                }

                if (level >= maxLevel)
                {
                    currentExperience = 0;
                }
            }

            if (oldExperience != currentExperience || oldLevel != level)
            {
                RaiseExperienceChanged(oldExperience, currentExperience);
            }
        }

        public bool SpendAttributePoints(int amount)
        {
            if (amount <= 0 || unspentAttributePoints < amount)
            {
                return false;
            }

            var oldValue = unspentAttributePoints;
            unspentAttributePoints -= amount;
            RaiseAttributePointsChanged(oldValue, unspentAttributePoints);
            return true;
        }

        public void GrantAttributePoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            AddAttributePointsInternal(amount);
        }

        private void AddAttributePointsInternal(int amount)
        {
            var oldValue = unspentAttributePoints;
            unspentAttributePoints += amount;
            RaiseAttributePointsChanged(oldValue, unspentAttributePoints);
        }

        private void RaiseExperienceChanged(int oldValue, int newValue)
        {
            var evt = new ExperienceChangedEvent(this, oldValue, newValue, level, ExperienceToNextLevel);
            ExperienceChanged?.Invoke(evt);
            eventHub?.RaiseExperienceChanged(evt);
        }

        private void RaiseLevelChanged(int oldLevel, int newLevel)
        {
            var evt = new LevelChangedEvent(this, oldLevel, newLevel);
            LevelChanged?.Invoke(evt);
            eventHub?.RaiseLevelChanged(evt);
        }

        private void RaiseAttributePointsChanged(int oldValue, int newValue)
        {
            var evt = new AttributePointsChangedEvent(this, oldValue, newValue);
            AttributePointsChanged?.Invoke(evt);
            eventHub?.RaiseAttributePointsChanged(evt);
        }
    }
}
