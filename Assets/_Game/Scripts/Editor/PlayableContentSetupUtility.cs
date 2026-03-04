using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.EditorTools
{
    /// <summary>
    /// 将测试向的物品/掉落/商店配置整理为可体验版本。
    /// </summary>
    public static class PlayableContentSetupUtility
    {
        private const string DatabasePath = "Assets/_Game/ScriptableObjects/Database/GameDatabase.asset";
        private const string VendorPath = "Assets/_Game/ScriptableObjects/Vendors/Vendor_Default.asset";
        private const string LootTablePath = "Assets/_Game/ScriptableObjects/Loot/LootTable_Default.asset";
        private const string QuestSidePath = "Assets/_Game/ScriptableObjects/Quests/Quest_Side_FieldSupplies.asset";
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player.prefab";
        private const string PlayerUnitPath = "Assets/_Game/ScriptableObjects/Units/Unit_Player.asset";
        private const string ModifiersFolder = "Assets/_Game/ScriptableObjects/Modifiers/Playable";

        private const string ItemWeaponPath = "Assets/_Game/ScriptableObjects/Items/Item_Weapon_Test.asset";
        private const string ItemHeadbandPath = "Assets/_Game/ScriptableObjects/Items/Item_Headband_Test.asset";
        private const string ItemClothesPath = "Assets/_Game/ScriptableObjects/Items/Item_Clothes_Test.asset";
        private const string ItemShoesPath = "Assets/_Game/ScriptableObjects/Items/Item_Shoes_Test.asset";
        private const string ItemAccessoryPath = "Assets/_Game/ScriptableObjects/Items/Item_Accessory_Test.asset";
        private const string ItemPotionPath = "Assets/_Game/ScriptableObjects/Items/Item_Potion_Test.asset";
        private const string ItemSkillFireballPath = "Assets/_Game/ScriptableObjects/Items/Item_Skillbook_Fireball.asset";
        private const string ItemSkillLockOnBoltPath = "Assets/_Game/ScriptableObjects/Items/Item_Skillbook_LockOnBolt.asset";
        private const string ItemSkillDashPath = "Assets/_Game/ScriptableObjects/Items/Item_Skillbook_Dash.asset";
        private const string ItemSkillArcaneFocusPath = "Assets/_Game/ScriptableObjects/Items/Item_Skillbook_ArcaneFocus.asset";

        private const string SkillFireballPath = "Assets/_Game/ScriptableObjects/Skills/Skill_YasuoQ.asset";
        private const string SkillLockOnBoltPath = "Assets/_Game/ScriptableObjects/Skills/Skill_LockOnBolt.asset";
        private const string SkillDashPath = "Assets/_Game/ScriptableObjects/Skills/Skill_Dash.asset";
        private const string SkillArcaneFocusPath = "Assets/_Game/ScriptableObjects/Skills/Skill_YasuoW.asset";

        [MenuItem("Combat/Content/Setup Playable Item Experience")]
        public static void SetupPlayableItemExperience()
        {
            EnsureFolder("Assets/_Game/ScriptableObjects/Modifiers", "Playable");

            var attackPower = FindStatById("Stat_AttackPower");
            var attackSpeed = FindStatById("Stat_AttackSpeed");
            var maxMana = FindStatById("Stat_MaxMana");
            var manaRegen = FindStatById("Stat_ManaRegen");
            var maxHealth = FindStatById("Stat_MaxHealth");
            var armor = FindStatById("Stat_Armor");
            var moveSpeed = FindStatById("Stat_MoveSpeed");
            var healthRegen = FindStatById("Stat_HealthRegen");
            var abilityPower = FindStatById("Stat_AbilityPower");
            var abilityHaste = FindStatById("Stat_AbilityHaste");

            var modifierWeaponAtk = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_StarterBlade_AttackPower.asset",
                "Modifier_Item_StarterBlade_AttackPower",
                "Starter Blade Attack Power",
                attackPower,
                8f);
            var modifierWeaponAspd = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_StarterBlade_AttackSpeed.asset",
                "Modifier_Item_StarterBlade_AttackSpeed",
                "Starter Blade Attack Speed",
                attackSpeed,
                0.15f);
            var modifierHeadMana = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_AdeptBand_MaxMana.asset",
                "Modifier_Item_AdeptBand_MaxMana",
                "Adept Band Max Mana",
                maxMana,
                35f);
            var modifierHeadRegen = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_AdeptBand_ManaRegen.asset",
                "Modifier_Item_AdeptBand_ManaRegen",
                "Adept Band Mana Regen",
                manaRegen,
                1.2f);
            var modifierClothesHealth = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_HunterCoat_MaxHealth.asset",
                "Modifier_Item_HunterCoat_MaxHealth",
                "Hunter Coat Max Health",
                maxHealth,
                60f);
            var modifierClothesArmor = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_HunterCoat_Armor.asset",
                "Modifier_Item_HunterCoat_Armor",
                "Hunter Coat Armor",
                armor,
                6f);
            var modifierShoesMove = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_PathfinderBoots_MoveSpeed.asset",
                "Modifier_Item_PathfinderBoots_MoveSpeed",
                "Pathfinder Boots Move Speed",
                moveSpeed,
                0.55f);
            var modifierShoesRegen = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_PathfinderBoots_HealthRegen.asset",
                "Modifier_Item_PathfinderBoots_HealthRegen",
                "Pathfinder Boots Health Regen",
                healthRegen,
                0.9f);
            var modifierAccessoryAp = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_RuneCharm_AbilityPower.asset",
                "Modifier_Item_RuneCharm_AbilityPower",
                "Rune Charm Ability Power",
                abilityPower,
                12f);
            var modifierAccessoryHaste = CreateOrUpdateStatModifier(
                $"{ModifiersFolder}/Modifier_Item_RuneCharm_AbilityHaste.asset",
                "Modifier_Item_RuneCharm_AbilityHaste",
                "Rune Charm Ability Haste",
                abilityHaste,
                8f);

            var skillFireball = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillFireballPath);
            var skillLockOnBolt = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillLockOnBoltPath);
            var skillDash = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillDashPath);
            var skillArcaneFocus = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillArcaneFocusPath);

            var weapon = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemWeaponPath);
            var headband = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemHeadbandPath);
            var clothes = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemClothesPath);
            var shoes = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemShoesPath);
            var accessory = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemAccessoryPath);
            var potion = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPotionPath);
            var skillbookFireball = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemSkillFireballPath);
            var skillbookLockOnBolt = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemSkillLockOnBoltPath);
            var skillbookDash = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemSkillDashPath);
            var skillbookArcaneFocus = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemSkillArcaneFocusPath);

            ConfigureItem(
                weapon,
                "新兵短剑",
                "近战基础武器，提升攻击力与攻速。",
                ItemRarity.Common,
                ItemSlot.Weapon,
                true,
                ItemCategory.Weapon,
                70,
                null,
                new[] { modifierWeaponAtk, modifierWeaponAspd });

            ConfigureItem(
                headband,
                "学徒头环",
                "施法入门装备，提升最大法力与回蓝。",
                ItemRarity.Common,
                ItemSlot.Headband,
                true,
                ItemCategory.Armor,
                64,
                null,
                new[] { modifierHeadMana, modifierHeadRegen });

            ConfigureItem(
                clothes,
                "猎手护衣",
                "野外生存护甲，提升生命与护甲。",
                ItemRarity.Common,
                ItemSlot.Clothes,
                true,
                ItemCategory.Armor,
                78,
                null,
                new[] { modifierClothesHealth, modifierClothesArmor });

            ConfigureItem(
                shoes,
                "巡路短靴",
                "提升移速并提供少量生命恢复。",
                ItemRarity.Common,
                ItemSlot.Shoes,
                true,
                ItemCategory.Armor,
                66,
                null,
                new[] { modifierShoesMove, modifierShoesRegen });

            ConfigureItem(
                accessory,
                "符文挂饰",
                "法术强化饰品，提升法强与技能急速。",
                ItemRarity.Magic,
                ItemSlot.Accessory,
                true,
                ItemCategory.Accessory,
                96,
                null,
                new[] { modifierAccessoryAp, modifierAccessoryHaste });

            ConfigureItem(
                potion,
                "简易恢复药剂",
                "基础消耗品：可在背包中使用（恢复效果由背包逻辑处理）。",
                ItemRarity.Common,
                ItemSlot.None,
                false,
                ItemCategory.Consumable,
                20,
                null,
                null,
                true,
                20);

            ConfigureItem(
                skillbookFireball,
                "技能书：火球术",
                "学习主动技能【火球术】并装配到技能栏。",
                ItemRarity.Magic,
                ItemSlot.None,
                false,
                ItemCategory.Skill,
                120,
                skillFireball,
                null);

            ConfigureItem(
                skillbookLockOnBolt,
                "技能书：锁定飞弹",
                "学习锁定技能【锁定飞弹】。必须选中敌人才能释放。",
                ItemRarity.Magic,
                ItemSlot.None,
                false,
                ItemCategory.Skill,
                135,
                skillLockOnBolt,
                null);

            ConfigureItem(
                skillbookDash,
                "技能书：冲刺",
                "学习位移技能【冲刺】并装配到技能栏。",
                ItemRarity.Common,
                ItemSlot.None,
                false,
                ItemCategory.Skill,
                90,
                skillDash,
                null);

            ConfigureItem(
                skillbookArcaneFocus,
                "技能符文：奥术专注",
                "学习强化技能【奥术专注】并装配到技能栏。",
                ItemRarity.Magic,
                ItemSlot.None,
                false,
                ItemCategory.Skill,
                110,
                skillArcaneFocus,
                null);

            ConfigureVendor(
                AssetDatabase.LoadAssetAtPath<VendorDefinition>(VendorPath),
                potion,
                weapon,
                headband,
                clothes,
                shoes,
                accessory,
                skillbookFireball,
                skillbookLockOnBolt,
                skillbookDash,
                skillbookArcaneFocus);

            ConfigureLootTable(
                AssetDatabase.LoadAssetAtPath<LootTableDefinition>(LootTablePath),
                potion,
                weapon,
                headband,
                clothes,
                shoes,
                accessory,
                skillbookFireball,
                skillbookLockOnBolt,
                skillbookDash,
                skillbookArcaneFocus);

            ConfigureSideQuestReward(
                AssetDatabase.LoadAssetAtPath<QuestDefinition>(QuestSidePath),
                potion,
                skillbookDash);

            ConfigurePlayerStartingItems(
                weapon,
                headband,
                clothes,
                shoes,
                accessory,
                potion,
                skillbookFireball,
                skillbookLockOnBolt,
                skillbookDash);

            ConfigurePlayerBaseStats(
                AssetDatabase.LoadAssetAtPath<UnitDefinition>(PlayerUnitPath),
                attackPower,
                attackSpeed,
                armor,
                abilityPower,
                abilityHaste);

            EnsureDatabaseReferences(
                AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath),
                modifierWeaponAtk,
                modifierWeaponAspd,
                modifierHeadMana,
                modifierHeadRegen,
                modifierClothesHealth,
                modifierClothesArmor,
                modifierShoesMove,
                modifierShoesRegen,
                modifierAccessoryAp,
                modifierAccessoryHaste);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayableContent] Starter item experience setup complete.");
        }

        /// <summary>
        /// 批处理入口，支持 CLI: -executeMethod CombatSystem.EditorTools.PlayableContentSetupUtility.SetupPlayableItemExperienceBatch
        /// </summary>
        public static void SetupPlayableItemExperienceBatch()
        {
            SetupPlayableItemExperience();
        }

        private static void ConfigureItem(
            ItemDefinition item,
            string displayName,
            string description,
            ItemRarity rarity,
            ItemSlot slot,
            bool allowAffixes,
            ItemCategory category,
            int basePrice,
            SkillDefinition linkedSkill,
            IReadOnlyList<ModifierDefinition> baseModifiers,
            bool stackable = false,
            int maxStack = 1)
        {
            if (item == null)
            {
                return;
            }

            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = displayName ?? string.Empty;
            so.FindProperty("description").stringValue = description ?? string.Empty;
            so.FindProperty("rarity").enumValueIndex = (int)rarity;
            so.FindProperty("slot").enumValueIndex = (int)slot;
            so.FindProperty("allowAffixes").boolValue = allowAffixes;
            so.FindProperty("itemCategory").enumValueIndex = (int)category;
            so.FindProperty("basePrice").intValue = Mathf.Max(1, basePrice);
            so.FindProperty("buyPriceOverride").intValue = -1;
            so.FindProperty("sellPriceOverride").intValue = -1;
            so.FindProperty("canBuy").boolValue = true;
            so.FindProperty("canSell").boolValue = true;
            so.FindProperty("itemLevel").intValue = 1;
            so.FindProperty("stackable").boolValue = stackable;
            so.FindProperty("maxStack").intValue = Mathf.Max(1, maxStack);
            so.FindProperty("linkedSkill").objectReferenceValue = linkedSkill;

            var modifiers = so.FindProperty("baseModifiers");
            SetObjectList(modifiers, baseModifiers);

            var equipBuffs = so.FindProperty("equipBuffs");
            equipBuffs.ClearArray();

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void ConfigureVendor(
            VendorDefinition vendor,
            ItemDefinition potion,
            ItemDefinition weapon,
            ItemDefinition headband,
            ItemDefinition clothes,
            ItemDefinition shoes,
            ItemDefinition accessory,
            ItemDefinition skillbookFireball,
            ItemDefinition skillbookLockOnBolt,
            ItemDefinition skillbookDash,
            ItemDefinition skillbookArcaneFocus)
        {
            if (vendor == null)
            {
                return;
            }

            var so = new SerializedObject(vendor);
            so.FindProperty("displayName").stringValue = "旅行补给商";
            so.FindProperty("buyPriceMultiplier").floatValue = 1f;
            so.FindProperty("sellPriceMultiplier").floatValue = 0.55f;
            so.FindProperty("allowBuyBack").boolValue = true;
            so.FindProperty("maxBuyBackEntries").intValue = 16;
            so.FindProperty("refreshMode").enumValueIndex = (int)VendorRefreshMode.Never;
            so.FindProperty("restockOnOpen").boolValue = false;

            var items = so.FindProperty("items");
            items.ClearArray();
            AddVendorItem(items, potion, true, 20);
            AddVendorItem(items, weapon, true, 5);
            AddVendorItem(items, headband, true, 5);
            AddVendorItem(items, clothes, true, 5);
            AddVendorItem(items, shoes, true, 5);
            AddVendorItem(items, accessory, true, 4);
            AddVendorItem(items, skillbookFireball, false, 1);
            AddVendorItem(items, skillbookLockOnBolt, false, 1);
            AddVendorItem(items, skillbookDash, false, 1);
            AddVendorItem(items, skillbookArcaneFocus, false, 1);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(vendor);
        }

        private static void AddVendorItem(SerializedProperty items, ItemDefinition item, bool infiniteStock, int stock)
        {
            if (items == null || item == null)
            {
                return;
            }

            items.arraySize += 1;
            var entry = items.GetArrayElementAtIndex(items.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("priceOverride").intValue = -1;
            entry.FindPropertyRelative("stock").intValue = Mathf.Max(1, stock);
            entry.FindPropertyRelative("infiniteStock").boolValue = infiniteStock;
        }

        private static void ConfigureLootTable(
            LootTableDefinition lootTable,
            ItemDefinition potion,
            ItemDefinition weapon,
            ItemDefinition headband,
            ItemDefinition clothes,
            ItemDefinition shoes,
            ItemDefinition accessory,
            ItemDefinition skillbookFireball,
            ItemDefinition skillbookLockOnBolt,
            ItemDefinition skillbookDash,
            ItemDefinition skillbookArcaneFocus)
        {
            if (lootTable == null)
            {
                return;
            }

            var so = new SerializedObject(lootTable);
            so.FindProperty("displayName").stringValue = "Act1 基础掉落表";
            so.FindProperty("minRolls").intValue = 1;
            so.FindProperty("maxRolls").intValue = 2;

            var entries = so.FindProperty("entries");
            entries.ClearArray();

            AddCurrencyLootEntry(entries, 45, 8, 24);
            AddItemLootEntry(entries, potion, 30, 1, 2);
            AddItemLootEntry(entries, weapon, 7, 1, 1);
            AddItemLootEntry(entries, headband, 7, 1, 1);
            AddItemLootEntry(entries, clothes, 7, 1, 1);
            AddItemLootEntry(entries, shoes, 7, 1, 1);
            AddItemLootEntry(entries, accessory, 5, 1, 1);
            AddItemLootEntry(entries, skillbookFireball, 3, 1, 1);
            AddItemLootEntry(entries, skillbookLockOnBolt, 3, 1, 1);
            AddItemLootEntry(entries, skillbookDash, 3, 1, 1);
            AddItemLootEntry(entries, skillbookArcaneFocus, 3, 1, 1);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lootTable);
        }

        private static void AddCurrencyLootEntry(SerializedProperty entries, int weight, int minCurrency, int maxCurrency)
        {
            if (entries == null)
            {
                return;
            }

            entries.arraySize += 1;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("type").enumValueIndex = (int)LootEntryType.Currency;
            entry.FindPropertyRelative("weight").intValue = Mathf.Max(1, weight);
            entry.FindPropertyRelative("item").objectReferenceValue = null;
            entry.FindPropertyRelative("minStack").intValue = 0;
            entry.FindPropertyRelative("maxStack").intValue = 0;
            entry.FindPropertyRelative("overrideRarity").boolValue = false;
            entry.FindPropertyRelative("rarity").enumValueIndex = (int)ItemRarity.Common;
            entry.FindPropertyRelative("rollAffixes").boolValue = false;
            entry.FindPropertyRelative("minAffixes").intValue = 0;
            entry.FindPropertyRelative("maxAffixes").intValue = 0;
            entry.FindPropertyRelative("affixPool").ClearArray();
            entry.FindPropertyRelative("minCurrency").intValue = Mathf.Max(1, minCurrency);
            entry.FindPropertyRelative("maxCurrency").intValue = Mathf.Max(minCurrency, maxCurrency);
        }

        private static void AddItemLootEntry(SerializedProperty entries, ItemDefinition item, int weight, int minStack, int maxStack)
        {
            if (entries == null || item == null)
            {
                return;
            }

            entries.arraySize += 1;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("type").enumValueIndex = (int)LootEntryType.Item;
            entry.FindPropertyRelative("weight").intValue = Mathf.Max(1, weight);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("minStack").intValue = Mathf.Max(1, minStack);
            entry.FindPropertyRelative("maxStack").intValue = Mathf.Max(minStack, maxStack);
            entry.FindPropertyRelative("overrideRarity").boolValue = false;
            entry.FindPropertyRelative("rarity").enumValueIndex = (int)item.Rarity;
            entry.FindPropertyRelative("rollAffixes").boolValue = false;
            entry.FindPropertyRelative("minAffixes").intValue = 0;
            entry.FindPropertyRelative("maxAffixes").intValue = 0;
            entry.FindPropertyRelative("affixPool").ClearArray();
            entry.FindPropertyRelative("minCurrency").intValue = 0;
            entry.FindPropertyRelative("maxCurrency").intValue = 0;
        }

        private static void ConfigureSideQuestReward(QuestDefinition sideQuest, ItemDefinition potion, ItemDefinition skillbookDash)
        {
            if (sideQuest == null)
            {
                return;
            }

            var so = new SerializedObject(sideQuest);
            var reward = so.FindProperty("reward");
            if (reward == null)
            {
                return;
            }

            reward.FindPropertyRelative("currency").intValue = 35;
            reward.FindPropertyRelative("experience").intValue = 60;

            var items = reward.FindPropertyRelative("items");
            items.ClearArray();
            AddQuestRewardItem(items, potion, 2);
            AddQuestRewardItem(items, skillbookDash, 1);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sideQuest);
        }

        private static void AddQuestRewardItem(SerializedProperty items, ItemDefinition item, int stack)
        {
            if (items == null || item == null)
            {
                return;
            }

            items.arraySize += 1;
            var entry = items.GetArrayElementAtIndex(items.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("stack").intValue = Mathf.Max(1, stack);
        }

        private static void ConfigurePlayerStartingItems(
            ItemDefinition weapon,
            ItemDefinition headband,
            ItemDefinition clothes,
            ItemDefinition shoes,
            ItemDefinition accessory,
            ItemDefinition potion,
            ItemDefinition skillbookFireball,
            ItemDefinition skillbookLockOnBolt,
            ItemDefinition skillbookDash)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var inventory = prefabRoot != null ? prefabRoot.GetComponent<InventoryComponent>() : null;
                if (inventory == null)
                {
                    return;
                }

                var so = new SerializedObject(inventory);
                var startingItems = so.FindProperty("startingItems");
                if (startingItems == null)
                {
                    return;
                }

                startingItems.ClearArray();
                AddStartingItem(startingItems, weapon, 1);
                AddStartingItem(startingItems, headband, 1);
                AddStartingItem(startingItems, clothes, 1);
                AddStartingItem(startingItems, shoes, 1);
                AddStartingItem(startingItems, accessory, 1);
                AddStartingItem(startingItems, potion, 3);
                AddStartingItem(startingItems, skillbookFireball, 1);
                AddStartingItem(startingItems, skillbookLockOnBolt, 1);
                AddStartingItem(startingItems, skillbookDash, 1);

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(inventory);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void AddStartingItem(SerializedProperty startingItems, ItemDefinition definition, int stack)
        {
            if (startingItems == null || definition == null)
            {
                return;
            }

            startingItems.arraySize += 1;
            var entry = startingItems.GetArrayElementAtIndex(startingItems.arraySize - 1);
            entry.FindPropertyRelative("definition").objectReferenceValue = definition;
            entry.FindPropertyRelative("stack").intValue = Mathf.Max(1, stack);
            entry.FindPropertyRelative("rarity").enumValueIndex = (int)definition.Rarity;
            entry.FindPropertyRelative("affixes").ClearArray();
        }

        private static void ConfigurePlayerBaseStats(
            UnitDefinition playerUnit,
            StatDefinition attackPower,
            StatDefinition attackSpeed,
            StatDefinition armor,
            StatDefinition abilityPower,
            StatDefinition abilityHaste)
        {
            if (playerUnit == null)
            {
                return;
            }

            var so = new SerializedObject(playerUnit);
            var baseStats = so.FindProperty("baseStats");
            if (baseStats == null)
            {
                return;
            }

            EnsureBaseStat(baseStats, attackPower, 10f);
            EnsureBaseStat(baseStats, attackSpeed, 0f);
            EnsureBaseStat(baseStats, armor, 0f);
            EnsureBaseStat(baseStats, abilityPower, 0f);
            EnsureBaseStat(baseStats, abilityHaste, 0f);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerUnit);
        }

        private static void EnsureBaseStat(SerializedProperty baseStats, StatDefinition stat, float value)
        {
            if (baseStats == null || stat == null)
            {
                return;
            }

            for (int i = 0; i < baseStats.arraySize; i++)
            {
                var entry = baseStats.GetArrayElementAtIndex(i);
                var existingStat = entry.FindPropertyRelative("stat").objectReferenceValue as StatDefinition;
                if (existingStat == stat)
                {
                    return;
                }
            }

            baseStats.arraySize += 1;
            var newEntry = baseStats.GetArrayElementAtIndex(baseStats.arraySize - 1);
            newEntry.FindPropertyRelative("stat").objectReferenceValue = stat;
            newEntry.FindPropertyRelative("value").floatValue = value;
        }

        private static void EnsureDatabaseReferences(GameDatabase database, params ModifierDefinition[] modifiers)
        {
            if (database == null)
            {
                return;
            }

            var so = new SerializedObject(database);
            EnsureObjectInList(so.FindProperty("modifiers"), modifiers);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void EnsureObjectInList(SerializedProperty list, IReadOnlyList<ModifierDefinition> values)
        {
            if (list == null || values == null)
            {
                return;
            }

            var existing = new HashSet<Object>();
            for (var i = 0; i < list.arraySize; i++)
            {
                var value = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null)
                {
                    existing.Add(value);
                }
            }

            for (var i = 0; i < values.Count; i++)
            {
                var modifier = values[i];
                if (modifier == null || existing.Contains(modifier))
                {
                    continue;
                }

                list.arraySize += 1;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = modifier;
            }
        }

        private static void SetObjectList(SerializedProperty list, IReadOnlyList<ModifierDefinition> values)
        {
            if (list == null)
            {
                return;
            }

            list.ClearArray();
            if (values == null)
            {
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == null)
                {
                    continue;
                }

                list.arraySize += 1;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = values[i];
            }
        }

        private static ModifierDefinition CreateOrUpdateStatModifier(
            string assetPath,
            string id,
            string displayName,
            StatDefinition stat,
            float value)
        {
            if (stat == null)
            {
                return null;
            }

            var modifier = AssetDatabase.LoadAssetAtPath<ModifierDefinition>(assetPath);
            if (modifier == null)
            {
                modifier = ScriptableObject.CreateInstance<ModifierDefinition>();
                AssetDatabase.CreateAsset(modifier, assetPath);
            }

            var so = new SerializedObject(modifier);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("target").enumValueIndex = (int)ModifierTargetType.Stat;
            so.FindProperty("scope").enumValueIndex = (int)ModifierScope.Caster;
            so.FindProperty("stat").objectReferenceValue = stat;
            so.FindProperty("parameterId").stringValue = string.Empty;
            so.FindProperty("operation").enumValueIndex = (int)ModifierOperation.Add;
            so.FindProperty("value").floatValue = value;
            so.FindProperty("condition").objectReferenceValue = null;
            so.FindProperty("requiredTags").ClearArray();
            so.FindProperty("blockedTags").ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(modifier);
            return modifier;
        }

        private static StatDefinition FindStatById(string id)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database != null)
            {
                database.BuildIndexes();
                var fromDatabase = database.GetStat(id);
                if (fromDatabase != null)
                {
                    return fromDatabase;
                }
            }

            var guids = AssetDatabase.FindAssets($"t:StatDefinition {id}", new[] { "Assets/_Game/ScriptableObjects/Stats" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var stat = AssetDatabase.LoadAssetAtPath<StatDefinition>(path);
                if (stat != null && stat.Id == id)
                {
                    return stat;
                }
            }

            return null;
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
    }
}
