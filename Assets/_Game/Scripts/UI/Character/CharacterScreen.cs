using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 角色信息界面：展示角色成长、战斗属性与当前构筑信息。
    /// </summary>
    public class CharacterScreen : UIScreenBase
    {
        private const string StatMaxHealthId = "Stat_MaxHealth";
        private const string StatAttackPowerId = "Stat_AttackPower";
        private const string StatArmorId = "Stat_Armor";
        private const string StatMoveSpeedId = "Stat_MoveSpeed";

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UnitRoot playerUnit;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private HealthComponent health;
        [SerializeField] private ResourceComponent resource;
        [SerializeField] private StatsComponent stats;
        [SerializeField] private MovementComponent movement;
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private EquipmentComponent equipment;
        [SerializeField] private CurrencyComponent currency;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool autoFindPlayer = true;

        [Header("Widgets")]
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text shieldText;
        [SerializeField] private Text resourceText;
        [SerializeField] private Text attributePointsText;
        [SerializeField] private Text currencyText;
        [SerializeField] private Text moveSpeedText;
        [SerializeField] private Text armorText;
        [SerializeField] private Text magicResistText;
        [SerializeField] private Text tenacityText;
        [SerializeField] private Text attackPowerText;
        [SerializeField] private Text attackSpeedText;
        [SerializeField] private Text abilityHasteText;
        [SerializeField] private Text lifestealText;
        [SerializeField] private Text omnivampText;
        [SerializeField] private Text armorPenFlatText;
        [SerializeField] private Text armorPenPercentText;
        [SerializeField] private Text magicPenFlatText;
        [SerializeField] private Text magicPenPercentText;
        [SerializeField] private Text inventoryText;
        [SerializeField] private Text equipmentText;
        [SerializeField] private Text skillCountText;
        [SerializeField] private Text basicAttackText;

        [Header("Attribute Allocation")]
        [SerializeField] private StatDefinition maxHealthAllocationStat;
        [SerializeField] private StatDefinition attackPowerAllocationStat;
        [SerializeField] private StatDefinition armorAllocationStat;
        [SerializeField] private StatDefinition moveSpeedAllocationStat;
        [SerializeField] private float maxHealthPerPoint = 20f;
        [SerializeField] private float attackPowerPerPoint = 2f;
        [SerializeField] private float armorPerPoint = 2f;
        [SerializeField] private float moveSpeedPerPoint = 0.2f;
        [SerializeField] private Text maxHealthAllocationText;
        [SerializeField] private Text attackPowerAllocationText;
        [SerializeField] private Text armorAllocationText;
        [SerializeField] private Text moveSpeedAllocationText;
        [SerializeField] private Text allocationFeedbackText;
        [SerializeField] private Button maxHealthAllocateButton;
        [SerializeField] private Button attackPowerAllocateButton;
        [SerializeField] private Button armorAllocateButton;
        [SerializeField] private Button moveSpeedAllocateButton;
        [SerializeField] private Color allocationSuccessColor = new Color(0.72f, 0.9f, 0.66f, 1f);
        [SerializeField] private Color allocationErrorColor = new Color(0.95f, 0.62f, 0.62f, 1f);
        [SerializeField] private int allocatedMaxHealthPoints;
        [SerializeField] private int allocatedAttackPowerPoints;
        [SerializeField] private int allocatedArmorPoints;
        [SerializeField] private int allocatedMoveSpeedPoints;

        private bool subscribed;
        private string allocationFeedback = string.Empty;
        private bool allocationFeedbackIsError;
        private int appliedMaxHealthPoints;
        private int appliedAttackPowerPoints;
        private int appliedArmorPoints;
        private int appliedMoveSpeedPoints;
        private int allocationAppliedStatsInstanceId;

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
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

            if (playerUnit != null)
            {
                BindPlayerReferences(playerUnit.gameObject);
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
                    BindPlayerReferences(player);
                }
            }

            if (playerUnit == null)
            {
                playerUnit = FindFirstObjectByType<UnitRoot>();
            }

            if (playerUnit != null)
            {
                BindPlayerReferences(playerUnit.gameObject);
            }

            if (progression == null)
            {
                progression = FindFirstObjectByType<PlayerProgression>();
            }

            if (health == null)
            {
                health = FindFirstObjectByType<HealthComponent>();
            }

            if (resource == null)
            {
                resource = FindFirstObjectByType<ResourceComponent>();
            }

            if (stats == null)
            {
                stats = FindFirstObjectByType<StatsComponent>();
            }

            if (movement == null)
            {
                movement = FindFirstObjectByType<MovementComponent>();
            }

            if (skillUser == null)
            {
                skillUser = FindFirstObjectByType<SkillUserComponent>();
            }

            if (inventory == null)
            {
                inventory = FindFirstObjectByType<InventoryComponent>();
            }

            if (equipment == null)
            {
                equipment = FindFirstObjectByType<EquipmentComponent>();
            }

            if (currency == null)
            {
                currency = FindFirstObjectByType<CurrencyComponent>();
            }

            ResolveAllocationStatReferences();
            SyncAllocationToCurrentStats();
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

            if (stats != null)
            {
                stats.StatChanged += HandleStatChanged;
            }

            if (inventory != null)
            {
                inventory.InventoryChanged += HandleInventoryChanged;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged += HandleEquipmentChanged;
            }

            if (currency != null)
            {
                currency.CurrencyChanged += HandleCurrencyChanged;
            }

            if (skillUser != null)
            {
                skillUser.SkillsChanged += HandleSkillsChanged;
            }

            if (maxHealthAllocateButton != null)
            {
                maxHealthAllocateButton.onClick.AddListener(AllocateMaxHealth);
            }

            if (attackPowerAllocateButton != null)
            {
                attackPowerAllocateButton.onClick.AddListener(AllocateAttackPower);
            }

            if (armorAllocateButton != null)
            {
                armorAllocateButton.onClick.AddListener(AllocateArmor);
            }

            if (moveSpeedAllocateButton != null)
            {
                moveSpeedAllocateButton.onClick.AddListener(AllocateMoveSpeed);
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

            if (stats != null)
            {
                stats.StatChanged -= HandleStatChanged;
            }

            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged -= HandleEquipmentChanged;
            }

            if (currency != null)
            {
                currency.CurrencyChanged -= HandleCurrencyChanged;
            }

            if (skillUser != null)
            {
                skillUser.SkillsChanged -= HandleSkillsChanged;
            }

            if (maxHealthAllocateButton != null)
            {
                maxHealthAllocateButton.onClick.RemoveListener(AllocateMaxHealth);
            }

            if (attackPowerAllocateButton != null)
            {
                attackPowerAllocateButton.onClick.RemoveListener(AllocateAttackPower);
            }

            if (armorAllocateButton != null)
            {
                armorAllocateButton.onClick.RemoveListener(AllocateArmor);
            }

            if (moveSpeedAllocateButton != null)
            {
                moveSpeedAllocateButton.onClick.RemoveListener(AllocateMoveSpeed);
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

        private void HandleStatChanged(StatChangedEvent evt)
        {
            Refresh();
        }

        private void HandleInventoryChanged()
        {
            Refresh();
        }

        private void HandleEquipmentChanged()
        {
            Refresh();
        }

        private void HandleCurrencyChanged(int oldValue, int newValue)
        {
            Refresh();
        }

        private void HandleSkillsChanged()
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
                    if (progression.ExperienceToNextLevel > 0)
                    {
                        var normalized = Mathf.Clamp01(progression.ExperienceNormalized) * 100f;
                        levelText.text = $"等级: {progression.Level}  经验: {progression.CurrentExperience}/{progression.ExperienceToNextLevel} ({normalized:0}%)";
                    }
                    else
                    {
                        levelText.text = $"等级: {progression.Level}  经验: 已满级";
                    }
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

            if (shieldText != null)
            {
                if (health != null)
                {
                    shieldText.text = $"护盾: {Mathf.RoundToInt(health.Shield)}";
                }
                else
                {
                    shieldText.text = "护盾: -";
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

            if (currencyText != null)
            {
                currencyText.text = currency != null ? $"金币: {currency.Amount}" : "金币: -";
            }

            if (moveSpeedText != null)
            {
                var speed = movement != null ? movement.MoveSpeed : GetStatValue(CombatStatIds.MoveSpeed, -1f);
                moveSpeedText.text = speed >= 0f ? $"移动速度: {FormatNumber(speed)}" : "移动速度: -";
            }

            if (armorText != null)
            {
                armorText.text = $"护甲: {FormatNumber(GetStatValue(CombatStatIds.Armor))}";
            }

            if (magicResistText != null)
            {
                magicResistText.text = $"魔抗: {FormatNumber(GetStatValue(CombatStatIds.MagicResist))}";
            }

            if (tenacityText != null)
            {
                tenacityText.text = $"韧性: {FormatPercent(GetStatValue(CombatStatIds.Tenacity))}";
            }

            if (attackPowerText != null)
            {
                attackPowerText.text = $"攻击力: {FormatNumber(GetStatValue(StatAttackPowerId))}";
            }

            if (attackSpeedText != null)
            {
                attackSpeedText.text = $"攻速加成: {FormatPercent(GetStatValue(CombatStatIds.AttackSpeed))}";
            }

            if (abilityHasteText != null)
            {
                abilityHasteText.text = $"技能急速: {FormatNumber(GetStatValue(CombatStatIds.AbilityHaste))}";
            }

            if (lifestealText != null)
            {
                lifestealText.text = $"生命偷取: {FormatPercent(GetStatValue(CombatStatIds.Lifesteal))}";
            }

            if (omnivampText != null)
            {
                omnivampText.text = $"全能吸血: {FormatPercent(GetStatValue(CombatStatIds.Omnivamp))}";
            }

            if (armorPenFlatText != null)
            {
                armorPenFlatText.text = $"护甲穿透(固定): {FormatNumber(GetStatValue(CombatStatIds.ArmorPenFlat))}";
            }

            if (armorPenPercentText != null)
            {
                armorPenPercentText.text = $"护甲穿透(%): {FormatPercent(GetStatValue(CombatStatIds.ArmorPenPercent))}";
            }

            if (magicPenFlatText != null)
            {
                magicPenFlatText.text = $"法穿(固定): {FormatNumber(GetStatValue(CombatStatIds.MagicPenFlat))}";
            }

            if (magicPenPercentText != null)
            {
                magicPenPercentText.text = $"法穿(%): {FormatPercent(GetStatValue(CombatStatIds.MagicPenPercent))}";
            }

            if (inventoryText != null)
            {
                inventoryText.text = BuildInventorySummary();
            }

            if (equipmentText != null)
            {
                equipmentText.text = BuildEquipmentSummary();
            }

            if (skillCountText != null)
            {
                var count = skillUser != null && skillUser.Skills != null ? skillUser.Skills.Count : 0;
                skillCountText.text = skillUser != null ? $"技能总数: {count}" : "技能总数: -";
            }

            if (basicAttackText != null)
            {
                if (skillUser != null && skillUser.BasicAttack != null)
                {
                    basicAttackText.text = $"普攻技能: {ResolveDisplayName(skillUser.BasicAttack.DisplayName, skillUser.BasicAttack.name)}";
                }
                else
                {
                    basicAttackText.text = "普攻技能: -";
                }
            }

            RefreshAllocationSection();
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

        private void BindPlayerReferences(GameObject player)
        {
            if (player == null)
            {
                return;
            }

            if (playerUnit == null)
            {
                playerUnit = player.GetComponent<UnitRoot>();
            }

            if (progression == null)
            {
                progression = player.GetComponent<PlayerProgression>();
            }

            if (health == null)
            {
                health = player.GetComponent<HealthComponent>();
            }

            if (resource == null)
            {
                resource = player.GetComponent<ResourceComponent>();
            }

            if (stats == null)
            {
                stats = player.GetComponent<StatsComponent>();
            }

            if (movement == null)
            {
                movement = player.GetComponent<MovementComponent>();
            }

            if (skillUser == null)
            {
                skillUser = player.GetComponent<SkillUserComponent>();
            }

            if (inventory == null)
            {
                inventory = player.GetComponent<InventoryComponent>();
            }

            if (equipment == null)
            {
                equipment = player.GetComponent<EquipmentComponent>();
            }

            if (currency == null)
            {
                currency = player.GetComponent<CurrencyComponent>();
            }
        }

        private float GetStatValue(string statId, float fallback = 0f)
        {
            if (stats == null || string.IsNullOrEmpty(statId))
            {
                return fallback;
            }

            return stats.GetValueById(statId, fallback);
        }

        private string BuildInventorySummary()
        {
            if (inventory == null)
            {
                return "背包: -";
            }

            var occupiedSlots = 0;
            var itemCount = 0;
            var items = inventory.Items;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null || item.Definition == null)
                    {
                        continue;
                    }

                    occupiedSlots++;
                    itemCount += Mathf.Max(1, item.Stack);
                }
            }

            return $"背包: {occupiedSlots}/{inventory.Capacity} 格  物品数: {itemCount}";
        }

        private string BuildEquipmentSummary()
        {
            if (equipment == null || equipment.Slots == null)
            {
                return "装备槽: -";
            }

            var equippedCount = 0;
            var slots = equipment.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.Item != null)
                {
                    equippedCount++;
                }
            }

            return $"装备槽: {equippedCount}/{slots.Count}";
        }

        private static string ResolveDisplayName(string displayName, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return string.IsNullOrWhiteSpace(fallback) ? "-" : fallback;
        }

        private static string FormatNumber(float value)
        {
            if (Mathf.Approximately(value, Mathf.Round(value)))
            {
                return Mathf.RoundToInt(value).ToString();
            }

            return value.ToString("0.0");
        }

        private static string FormatPercent(float value)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return "0%";
            }

            var percent = Mathf.Abs(value) <= 2.5f ? value * 100f : value;
            return $"{percent:0.#}%";
        }

        private void RefreshAllocationSection()
        {
            var hasStats = stats != null;
            var unspent = progression != null ? progression.UnspentAttributePoints : 0;
            var canSpend = hasStats && progression != null && unspent > 0;

            if (maxHealthAllocationText != null)
            {
                maxHealthAllocationText.text = BuildAllocationLine(
                    "体质",
                    maxHealthPerPoint,
                    "生命",
                    GetStatValue(StatMaxHealthId),
                    "最大生命",
                    allocatedMaxHealthPoints);
            }

            if (attackPowerAllocationText != null)
            {
                attackPowerAllocationText.text = BuildAllocationLine(
                    "力量",
                    attackPowerPerPoint,
                    "攻击",
                    GetStatValue(StatAttackPowerId),
                    "攻击力",
                    allocatedAttackPowerPoints);
            }

            if (armorAllocationText != null)
            {
                armorAllocationText.text = BuildAllocationLine(
                    "防御",
                    armorPerPoint,
                    "护甲",
                    GetStatValue(StatArmorId),
                    "护甲",
                    allocatedArmorPoints);
            }

            if (moveSpeedAllocationText != null)
            {
                moveSpeedAllocationText.text = BuildAllocationLine(
                    "迅捷",
                    moveSpeedPerPoint,
                    "移速",
                    GetStatValue(StatMoveSpeedId),
                    "移动速度",
                    allocatedMoveSpeedPoints);
            }

            if (allocationFeedbackText != null)
            {
                if (string.IsNullOrWhiteSpace(allocationFeedback))
                {
                    allocationFeedbackText.text = progression != null
                        ? $"可用属性点: {unspent}"
                        : "未找到成长组件。";
                    allocationFeedbackText.color = new Color(0.86f, 0.9f, 0.95f, 1f);
                }
                else
                {
                    allocationFeedbackText.text = allocationFeedback;
                    allocationFeedbackText.color = allocationFeedbackIsError ? allocationErrorColor : allocationSuccessColor;
                }
            }

            SetAllocationButtonState(maxHealthAllocateButton, canSpend && ResolveAllocationStat(maxHealthAllocationStat, StatMaxHealthId) != null);
            SetAllocationButtonState(attackPowerAllocateButton, canSpend && ResolveAllocationStat(attackPowerAllocationStat, StatAttackPowerId) != null);
            SetAllocationButtonState(armorAllocateButton, canSpend && ResolveAllocationStat(armorAllocationStat, StatArmorId) != null);
            SetAllocationButtonState(moveSpeedAllocateButton, canSpend && ResolveAllocationStat(moveSpeedAllocationStat, StatMoveSpeedId) != null);
        }

        private void AllocateMaxHealth()
        {
            TryAllocateStat(
                ref maxHealthAllocationStat,
                StatMaxHealthId,
                maxHealthPerPoint,
                "体质",
                ref allocatedMaxHealthPoints,
                ref appliedMaxHealthPoints);
        }

        private void AllocateAttackPower()
        {
            TryAllocateStat(
                ref attackPowerAllocationStat,
                StatAttackPowerId,
                attackPowerPerPoint,
                "力量",
                ref allocatedAttackPowerPoints,
                ref appliedAttackPowerPoints);
        }

        private void AllocateArmor()
        {
            TryAllocateStat(
                ref armorAllocationStat,
                StatArmorId,
                armorPerPoint,
                "防御",
                ref allocatedArmorPoints,
                ref appliedArmorPoints);
        }

        private void AllocateMoveSpeed()
        {
            TryAllocateStat(
                ref moveSpeedAllocationStat,
                StatMoveSpeedId,
                moveSpeedPerPoint,
                "迅捷",
                ref allocatedMoveSpeedPoints,
                ref appliedMoveSpeedPoints);
        }

        private void TryAllocateStat(
            ref StatDefinition statDefinition,
            string statId,
            float deltaPerPoint,
            string label,
            ref int allocatedPoints,
            ref int appliedPoints)
        {
            EnsureReferences();
            statDefinition = ResolveAllocationStat(statDefinition, statId);

            if (progression == null)
            {
                SetAllocationFeedback("未找到成长组件，无法加点。", true);
                Refresh();
                return;
            }

            if (stats == null)
            {
                SetAllocationFeedback("未找到属性组件，无法加点。", true);
                Refresh();
                return;
            }

            if (statDefinition == null)
            {
                SetAllocationFeedback($"属性未配置：{label}", true);
                Refresh();
                return;
            }

            if (deltaPerPoint <= 0f)
            {
                SetAllocationFeedback($"加点配置无效：{label}", true);
                Refresh();
                return;
            }

            if (!progression.SpendAttributePoints(1))
            {
                SetAllocationFeedback("属性点不足。", true);
                Refresh();
                return;
            }

            stats.ModifyValue(statDefinition, deltaPerPoint);
            allocatedPoints = Mathf.Max(0, allocatedPoints + 1);
            appliedPoints = Mathf.Max(0, appliedPoints + 1);
            SetAllocationFeedback($"{label} +{FormatNumber(deltaPerPoint)}（已投入 {allocatedPoints}）", false);
            Refresh();
        }

        private void ResolveAllocationStatReferences()
        {
            maxHealthAllocationStat = ResolveAllocationStat(maxHealthAllocationStat, StatMaxHealthId);
            attackPowerAllocationStat = ResolveAllocationStat(attackPowerAllocationStat, StatAttackPowerId);
            armorAllocationStat = ResolveAllocationStat(armorAllocationStat, StatArmorId);
            moveSpeedAllocationStat = ResolveAllocationStat(moveSpeedAllocationStat, StatMoveSpeedId);
        }

        private StatDefinition ResolveAllocationStat(StatDefinition configured, string statId)
        {
            if (configured != null)
            {
                return configured;
            }

            if (playerUnit != null && playerUnit.Definition != null && playerUnit.Definition.BaseStats != null)
            {
                var baseStats = playerUnit.Definition.BaseStats;
                for (int i = 0; i < baseStats.Count; i++)
                {
                    var stat = baseStats[i].stat;
                    if (stat != null && string.Equals(stat.Id, statId, System.StringComparison.Ordinal))
                    {
                        return stat;
                    }
                }
            }

            var allStats = Resources.FindObjectsOfTypeAll<StatDefinition>();
            if (allStats == null)
            {
                return null;
            }

            for (int i = 0; i < allStats.Length; i++)
            {
                var stat = allStats[i];
                if (stat == null)
                {
                    continue;
                }

                if (string.Equals(stat.Id, statId, System.StringComparison.Ordinal))
                {
                    return stat;
                }
            }

            return null;
        }

        private static void SetAllocationButtonState(Button button, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = enabled;
        }

        private static string BuildAllocationLine(string label, float perPoint, string suffix, float currentValue, string currentLabel, int allocatedPoints)
        {
            return $"{label}  +{FormatNumber(perPoint)} {suffix} / 点  |  {currentLabel}: {FormatNumber(currentValue)}  |  已投入: {Mathf.Max(0, allocatedPoints)}";
        }

        private void SetAllocationFeedback(string message, bool isError)
        {
            allocationFeedback = message ?? string.Empty;
            allocationFeedbackIsError = isError;
        }

        public void GetAllocationPoints(out int maxHealthPoints, out int attackPowerPoints, out int armorPoints, out int moveSpeedPoints)
        {
            maxHealthPoints = Mathf.Max(0, allocatedMaxHealthPoints);
            attackPowerPoints = Mathf.Max(0, allocatedAttackPowerPoints);
            armorPoints = Mathf.Max(0, allocatedArmorPoints);
            moveSpeedPoints = Mathf.Max(0, allocatedMoveSpeedPoints);
        }

        public void SetAllocationPoints(int maxHealthPoints, int attackPowerPoints, int armorPoints, int moveSpeedPoints)
        {
            allocatedMaxHealthPoints = Mathf.Max(0, maxHealthPoints);
            allocatedAttackPowerPoints = Mathf.Max(0, attackPowerPoints);
            allocatedArmorPoints = Mathf.Max(0, armorPoints);
            allocatedMoveSpeedPoints = Mathf.Max(0, moveSpeedPoints);
            allocationFeedback = string.Empty;
            EnsureReferences();
            SyncAllocationToCurrentStats();
            Refresh();
        }

        private void SyncAllocationToCurrentStats()
        {
            if (stats == null)
            {
                return;
            }

            var currentStatsInstanceId = stats.GetInstanceID();
            if (currentStatsInstanceId != allocationAppliedStatsInstanceId)
            {
                allocationAppliedStatsInstanceId = currentStatsInstanceId;
                appliedMaxHealthPoints = 0;
                appliedAttackPowerPoints = 0;
                appliedArmorPoints = 0;
                appliedMoveSpeedPoints = 0;
            }

            ApplyAllocationDifference(ref appliedMaxHealthPoints, allocatedMaxHealthPoints, ref maxHealthAllocationStat, StatMaxHealthId, maxHealthPerPoint);
            ApplyAllocationDifference(ref appliedAttackPowerPoints, allocatedAttackPowerPoints, ref attackPowerAllocationStat, StatAttackPowerId, attackPowerPerPoint);
            ApplyAllocationDifference(ref appliedArmorPoints, allocatedArmorPoints, ref armorAllocationStat, StatArmorId, armorPerPoint);
            ApplyAllocationDifference(ref appliedMoveSpeedPoints, allocatedMoveSpeedPoints, ref moveSpeedAllocationStat, StatMoveSpeedId, moveSpeedPerPoint);
        }

        private void ApplyAllocationDifference(
            ref int appliedPoints,
            int targetPoints,
            ref StatDefinition statDefinition,
            string statId,
            float deltaPerPoint)
        {
            if (stats == null || deltaPerPoint <= 0f)
            {
                return;
            }

            statDefinition = ResolveAllocationStat(statDefinition, statId);
            if (statDefinition == null)
            {
                return;
            }

            var safeTargetPoints = Mathf.Max(0, targetPoints);
            var deltaPoints = safeTargetPoints - Mathf.Max(0, appliedPoints);
            if (deltaPoints == 0)
            {
                return;
            }

            stats.ModifyValue(statDefinition, deltaPoints * deltaPerPoint);
            appliedPoints = safeTargetPoints;
        }

        public override string GetFooterHintText()
        {
            return "TAB 关闭菜单    ESC 返回游戏    ←/→ 切换页签    鼠标左键 分配属性";
        }
    }
}
