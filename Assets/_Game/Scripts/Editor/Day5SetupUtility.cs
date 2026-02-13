using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using CombatSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CombatSystem.EditorTools
{
    public static class Day5SetupUtility
    {
        private const string QuestFolderPath = "Assets/_Game/ScriptableObjects/Quests";
        private const string MainQuestPath = "Assets/_Game/ScriptableObjects/Quests/Quest_Main_FirstTrade.asset";
        private const string SideQuestPath = "Assets/_Game/ScriptableObjects/Quests/Quest_Side_FieldSupplies.asset";
        private const string GameDatabasePath = "Assets/_Game/ScriptableObjects/Database/GameDatabase.asset";

        [MenuItem("Combat/Day5/Setup Quest Assets")]
        public static void SetupQuestAssets()
        {
            EnsureFolder(QuestFolderPath);

            var items = FindItemDefinitions();
            var rewardItem = items.Count > 0 ? items[0] : null;

            var mainQuest = CreateOrUpdateMainQuest();
            var sideQuest = CreateOrUpdateSideQuest(rewardItem);
            UpdateGameDatabase(mainQuest, sideQuest);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = mainQuest != null ? mainQuest : sideQuest;
            Debug.Log("[Day5] Quest assets setup complete.");
        }

        [MenuItem("Combat/Day5/Setup Quest Runtime (Current Scene)")]
        public static void SetupQuestRuntime()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Day5] No active scene.");
                return;
            }

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabasePath);
            if (database == null)
            {
                Debug.LogWarning("[Day5] GameDatabase not found at " + GameDatabasePath);
                return;
            }

            var mainQuest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(MainQuestPath);
            if (mainQuest == null)
            {
                Debug.LogWarning("[Day5] Main quest asset not found. Run Setup Quest Assets first.");
                return;
            }

            var tracker = EnsureQuestTracker(database);
            var hud = EnsureQuestHud(tracker, database);
            EnsureVendorQuestGiver(mainQuest, tracker);

            if (hud != null)
            {
                Selection.activeObject = hud.gameObject;
            }
            else if (tracker != null)
            {
                Selection.activeObject = tracker.gameObject;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Day5] Quest runtime setup complete for current scene.");
        }

        private static QuestDefinition CreateOrUpdateMainQuest()
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(MainQuestPath);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(quest, MainQuestPath);
            }

            var serialized = new SerializedObject(quest);
            serialized.FindProperty("id").stringValue = "Quest_Main_FirstTrade";
            serialized.FindProperty("displayName").stringValue = "第一次交易";
            serialized.FindProperty("category").enumValueIndex = (int)QuestCategory.Main;
            serialized.FindProperty("summary").stringValue = "与商人交谈并完成第一次购买。";
            serialized.FindProperty("requireTurnIn").boolValue = true;
            serialized.FindProperty("autoTrackOnAccept").boolValue = true;
            serialized.FindProperty("nextQuestId").stringValue = "Quest_Side_FieldSupplies";

            var objectives = serialized.FindProperty("objectives");
            objectives.ClearArray();
            objectives.arraySize = 2;

            ConfigureObjective(
                objectives.GetArrayElementAtIndex(0),
                "talk_vendor",
                "与商人交谈",
                QuestObjectiveType.TalkToNpc,
                "vendor_talk",
                1,
                false,
                false);

            ConfigureObjective(
                objectives.GetArrayElementAtIndex(1),
                "buy_any_item",
                "从商店购买 1 件物品",
                QuestObjectiveType.BuyFromVendor,
                string.Empty,
                1,
                false,
                false);

            var reward = serialized.FindProperty("reward");
            reward.FindPropertyRelative("currency").intValue = 40;
            reward.FindPropertyRelative("experience").intValue = 30;
            reward.FindPropertyRelative("items").ClearArray();

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(quest);
            return quest;
        }

        private static QuestDefinition CreateOrUpdateSideQuest(ItemDefinition rewardItem)
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(SideQuestPath);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(quest, SideQuestPath);
            }

            var serialized = new SerializedObject(quest);
            serialized.FindProperty("id").stringValue = "Quest_Side_FieldSupplies";
            serialized.FindProperty("displayName").stringValue = "补给收集";
            serialized.FindProperty("category").enumValueIndex = (int)QuestCategory.Side;
            serialized.FindProperty("summary").stringValue = "在野外拾取战利品补给。";
            serialized.FindProperty("requireTurnIn").boolValue = false;
            serialized.FindProperty("autoTrackOnAccept").boolValue = false;
            serialized.FindProperty("nextQuestId").stringValue = string.Empty;

            var objectives = serialized.FindProperty("objectives");
            objectives.ClearArray();
            objectives.arraySize = 1;

            ConfigureObjective(
                objectives.GetArrayElementAtIndex(0),
                "collect_supplies",
                "拾取任意战利品 3 次",
                QuestObjectiveType.CollectItem,
                string.Empty,
                3,
                false,
                false);

            var reward = serialized.FindProperty("reward");
            reward.FindPropertyRelative("currency").intValue = 25;
            reward.FindPropertyRelative("experience").intValue = 45;
            var rewardItems = reward.FindPropertyRelative("items");
            rewardItems.ClearArray();
            if (rewardItem != null)
            {
                rewardItems.arraySize = 1;
                rewardItems.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = rewardItem;
                rewardItems.GetArrayElementAtIndex(0).FindPropertyRelative("stack").intValue = 1;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(quest);
            return quest;
        }

        private static void ConfigureObjective(
            SerializedProperty objective,
            string objectiveId,
            string description,
            QuestObjectiveType type,
            string targetId,
            int requiredAmount,
            bool optional,
            bool hiddenUntilProgress)
        {
            if (objective == null)
            {
                return;
            }

            objective.FindPropertyRelative("objectiveId").stringValue = objectiveId;
            objective.FindPropertyRelative("description").stringValue = description;
            objective.FindPropertyRelative("objectiveType").enumValueIndex = (int)type;
            objective.FindPropertyRelative("targetId").stringValue = targetId;
            objective.FindPropertyRelative("requiredAmount").intValue = Mathf.Max(1, requiredAmount);
            objective.FindPropertyRelative("optional").boolValue = optional;
            objective.FindPropertyRelative("hiddenUntilProgress").boolValue = hiddenUntilProgress;
        }

        private static void UpdateGameDatabase(QuestDefinition mainQuest, QuestDefinition sideQuest)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabasePath);
            if (database == null)
            {
                Debug.LogWarning("[Day5] GameDatabase not found at " + GameDatabasePath);
                return;
            }

            var serialized = new SerializedObject(database);
            var quests = serialized.FindProperty("quests");
            AddToList(quests, mainQuest);
            AddToList(quests, sideQuest);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
        }

        private static QuestTracker EnsureQuestTracker(GameDatabase database)
        {
            var tracker = Object.FindFirstObjectByType<QuestTracker>();
            if (tracker == null)
            {
                var root = new GameObject("QuestTracker");
                tracker = root.AddComponent<QuestTracker>();
            }

            var serialized = new SerializedObject(tracker);
            serialized.FindProperty("database").objectReferenceValue = database;
            serialized.FindProperty("autoFindPlayer").boolValue = true;
            serialized.FindProperty("discoverOnSceneLoad").boolValue = true;
            serialized.FindProperty("dontDestroyOnLoad").boolValue = true;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(tracker);
            return tracker;
        }

        private static QuestTrackerHUD EnsureQuestHud(QuestTracker tracker, GameDatabase database)
        {
            var existing = Object.FindFirstObjectByType<QuestTrackerHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                var serializedExisting = new SerializedObject(existing);
                serializedExisting.FindProperty("questTracker").objectReferenceValue = tracker;
                serializedExisting.FindProperty("database").objectReferenceValue = database;
                serializedExisting.ApplyModifiedProperties();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var uiRoot = Object.FindFirstObjectByType<UIRoot>();
            if (uiRoot == null || uiRoot.HudCanvas == null)
            {
                Debug.LogWarning("[Day5] UIRoot/HUD canvas not found. Quest HUD was not created.");
                return null;
            }

            var panel = new GameObject("QuestHUD", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(uiRoot.HudCanvas.transform, false);
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);
            panelRect.sizeDelta = new Vector2(420f, 190f);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.04f, 0.06f, 0.1f, 0.72f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var title = CreateHudText("Title", panel.transform, "Quest", 20, FontStyle.Bold);
            var objectives = CreateHudText("Objectives", panel.transform, "No active quest.", 15, FontStyle.Normal);
            objectives.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectives.verticalOverflow = VerticalWrapMode.Overflow;
            objectives.GetComponent<LayoutElement>().flexibleHeight = 1f;
            var status = CreateHudText("Status", panel.transform, string.Empty, 14, FontStyle.Bold);
            var reward = CreateHudText("Reward", panel.transform, string.Empty, 13, FontStyle.Normal);

            var hud = panel.AddComponent<QuestTrackerHUD>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("questTracker").objectReferenceValue = tracker;
            serialized.FindProperty("database").objectReferenceValue = database;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("objectivesText").objectReferenceValue = objectives;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("rewardText").objectReferenceValue = reward;
            serialized.FindProperty("hideWhenNoActiveQuest").boolValue = true;
            serialized.FindProperty("emptyTitle").stringValue = "Quest";
            serialized.FindProperty("emptyContent").stringValue = "No active quest.";
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(hud);
            return hud;
        }

        private static void EnsureVendorQuestGiver(QuestDefinition mainQuest, QuestTracker tracker)
        {
            if (mainQuest == null || tracker == null)
            {
                return;
            }

            var target = GameObject.Find("VendorNPC");
            if (target == null)
            {
                var vendorTrigger = Object.FindFirstObjectByType<VendorTrigger>();
                if (vendorTrigger != null)
                {
                    target = vendorTrigger.gameObject;
                }
            }

            if (target == null)
            {
                return;
            }

            var collider = target.GetComponent<Collider>();
            if (collider == null)
            {
                collider = target.AddComponent<SphereCollider>();
                collider.isTrigger = true;
            }

            var giver = target.GetComponent<QuestGiverTrigger>();
            if (giver == null)
            {
                giver = target.AddComponent<QuestGiverTrigger>();
            }

            var serialized = new SerializedObject(giver);
            var uiManager = Object.FindFirstObjectByType<UIManager>();
            var questModal = Object.FindFirstObjectByType<QuestGiverModal>(FindObjectsInactive.Include);
            serialized.FindProperty("quest").objectReferenceValue = mainQuest;
            serialized.FindProperty("questTracker").objectReferenceValue = tracker;
            serialized.FindProperty("autoAcceptOnEnter").boolValue = false;
            serialized.FindProperty("autoTurnInWhenReady").boolValue = true;
            serialized.FindProperty("allowInteractKey").boolValue = true;
            serialized.FindProperty("useDialogUi").boolValue = true;
            serialized.FindProperty("closeDialogOnExit").boolValue = true;
            serialized.FindProperty("uiManager").objectReferenceValue = uiManager;
            serialized.FindProperty("questModal").objectReferenceValue = questModal;
            serialized.FindProperty("advanceObjectiveOnInteract").boolValue = true;
            serialized.FindProperty("objectiveType").enumValueIndex = (int)QuestObjectiveType.TalkToNpc;
            serialized.FindProperty("objectiveTargetId").stringValue = "vendor_talk";
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(giver);
        }

        private static Text CreateHudText(string name, Transform parent, string content, int fontSize, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = fontSize + 8f;
            return text;
        }

        private static List<ItemDefinition> FindItemDefinitions()
        {
            var items = new List<ItemDefinition>();
            var guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game/ScriptableObjects/Items" });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            items.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return items;
        }

        private static void AddToList(SerializedProperty list, Object value)
        {
            if (list == null || value == null)
            {
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                {
                    return;
                }
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
