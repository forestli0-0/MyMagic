using CombatSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 角色信息界面：展示等级、生命、资源与属性点。
    /// </summary>
    public class CharacterScreen : UIScreenBase
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UnitRoot playerUnit;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private HealthComponent health;
        [SerializeField] private ResourceComponent resource;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool autoFindPlayer = true;

        [Header("Widgets")]
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text resourceText;
        [SerializeField] private Text attributePointsText;

        private bool subscribed;

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        public override void OnEnter()
        {
            EnsureReferences();
            Subscribe();
            Refresh();
            if (uiManager != null)
            {
                uiManager.SetHudVisible(false);
            }
        }

        public override void OnExit()
        {
            Unsubscribe();
            if (uiManager != null)
            {
                uiManager.SetHudVisible(true);
            }
        }

        public override void OnFocus()
        {
            Refresh();
        }

        private void EnsureReferences()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (!autoFindPlayer)
            {
                return;
            }

            if (playerUnit == null && !string.IsNullOrWhiteSpace(playerTag))
            {
                var player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    playerUnit = player.GetComponent<UnitRoot>();
                    progression = player.GetComponent<PlayerProgression>();
                    health = player.GetComponent<HealthComponent>();
                    resource = player.GetComponent<ResourceComponent>();
                }
            }

            if (playerUnit == null)
            {
                playerUnit = FindFirstObjectByType<UnitRoot>();
            }

            if (progression == null)
            {
                progression = playerUnit != null ? playerUnit.GetComponent<PlayerProgression>() : FindFirstObjectByType<PlayerProgression>();
            }

            if (health == null)
            {
                health = playerUnit != null ? playerUnit.GetComponent<HealthComponent>() : FindFirstObjectByType<HealthComponent>();
            }

            if (resource == null)
            {
                resource = playerUnit != null ? playerUnit.GetComponent<ResourceComponent>() : FindFirstObjectByType<ResourceComponent>();
            }
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (progression != null)
            {
                progression.ExperienceChanged += HandleExperienceChanged;
                progression.LevelChanged += HandleLevelChanged;
                progression.AttributePointsChanged += HandleAttributePointsChanged;
            }

            if (health != null)
            {
                health.HealthChanged += HandleHealthChanged;
            }

            if (resource != null)
            {
                resource.ResourceChanged += HandleResourceChanged;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (progression != null)
            {
                progression.ExperienceChanged -= HandleExperienceChanged;
                progression.LevelChanged -= HandleLevelChanged;
                progression.AttributePointsChanged -= HandleAttributePointsChanged;
            }

            if (health != null)
            {
                health.HealthChanged -= HandleHealthChanged;
            }

            if (resource != null)
            {
                resource.ResourceChanged -= HandleResourceChanged;
            }

            subscribed = false;
        }

        private void HandleExperienceChanged(ExperienceChangedEvent evt)
        {
            Refresh();
        }

        private void HandleLevelChanged(LevelChangedEvent evt)
        {
            Refresh();
        }

        private void HandleAttributePointsChanged(AttributePointsChangedEvent evt)
        {
            Refresh();
        }

        private void HandleHealthChanged(HealthChangedEvent evt)
        {
            Refresh();
        }

        private void HandleResourceChanged(ResourceChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            EnsureReferences();

            if (nameText != null)
            {
                nameText.text = ResolvePlayerName();
            }

            if (levelText != null)
            {
                if (progression != null)
                {
                    levelText.text = $"等级: {progression.Level}  (XP {progression.CurrentExperience}/{progression.ExperienceToNextLevel})";
                }
                else
                {
                    levelText.text = "等级: -";
                }
            }

            if (healthText != null)
            {
                if (health != null)
                {
                    healthText.text = $"生命: {Mathf.RoundToInt(health.Current)}/{Mathf.RoundToInt(health.Max)}";
                }
                else
                {
                    healthText.text = "生命: -";
                }
            }

            if (resourceText != null)
            {
                if (resource != null)
                {
                    resourceText.text = $"{resource.ResourceType}: {Mathf.RoundToInt(resource.Current)}/{Mathf.RoundToInt(resource.Max)}";
                }
                else
                {
                    resourceText.text = "资源: -";
                }
            }

            if (attributePointsText != null)
            {
                if (progression != null)
                {
                    attributePointsText.text = $"未分配属性点: {progression.UnspentAttributePoints}";
                }
                else
                {
                    attributePointsText.text = "未分配属性点: -";
                }
            }
        }

        private string ResolvePlayerName()
        {
            if (playerUnit != null && playerUnit.Definition != null)
            {
                var displayName = playerUnit.Definition.DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }

            if (playerUnit != null)
            {
                return playerUnit.name;
            }

            return "Player";
        }
    }
}
