using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    public class Day5QuestSystemsTests
    {
        [Test]
        public void QuestTracker_AcceptProgressTurnIn_Works()
        {
            var databaseType = GetRuntimeType("CombatSystem.Data.GameDatabase");
            var questType = GetRuntimeType("CombatSystem.Data.QuestDefinition");
            var trackerType = GetRuntimeType("CombatSystem.Gameplay.QuestTracker");
            var inventoryType = GetRuntimeType("CombatSystem.Gameplay.InventoryComponent");
            var currencyType = GetRuntimeType("CombatSystem.Gameplay.CurrencyComponent");
            var statusType = GetRuntimeType("CombatSystem.Gameplay.QuestStatus");

            if (databaseType == null || questType == null || trackerType == null || inventoryType == null || currencyType == null || statusType == null)
            {
                Assert.Ignore("Day5 runtime types not found.");
                return;
            }

            var database = ScriptableObject.CreateInstance(databaseType);
            var quest = BuildQuestDefinition("Quest_Test_Trade", true, 50, 1);
            var questList = CreateList(questType, quest);
            SetPrivateField(database, "quests", questList);
            CallMethod(database, "BuildIndexes");

            var player = new GameObject("QuestPlayer");
            var inventory = player.AddComponent(inventoryType);
            var currency = player.AddComponent(currencyType);
            CallMethod(inventory, "Initialize");
            CallMethod(currency, "Initialize");

            var trackerGo = new GameObject("QuestTracker_Test");
            var tracker = trackerGo.AddComponent(trackerType);
            SetPrivateField(tracker, "database", database);
            SetPrivateField(tracker, "autoFindPlayer", false);
            SetPrivateField(tracker, "playerInventory", inventory);
            SetPrivateField(tracker, "playerCurrency", currency);
            SetPrivateField(tracker, "playerProgression", null);

            var questId = GetStringProperty(quest, "Id");
            Assert.IsTrue(CallBoolMethod(tracker, "AcceptQuest", questId));
            var state = CallMethod(tracker, "GetState", questId);
            Assert.NotNull(state);
            Assert.AreEqual(Enum.Parse(statusType, "InProgress"), GetPropertyValue(state, "Status"));

            Assert.IsTrue(CallBoolMethod(tracker, "TryAdvanceObjective", questId, "objective_0", 1));
            state = CallMethod(tracker, "GetState", questId);
            Assert.NotNull(state);
            Assert.AreEqual(Enum.Parse(statusType, "ReadyToTurnIn"), GetPropertyValue(state, "Status"));

            Assert.AreEqual(0, GetIntProperty(currency, "Amount"));
            Assert.IsTrue(CallBoolMethod(tracker, "TryTurnInQuest", questId));
            Assert.AreEqual(50, GetIntProperty(currency, "Amount"));
            Assert.AreEqual(Enum.Parse(statusType, "Completed"), GetPropertyValue(CallMethod(tracker, "GetState", questId), "Status"));

            UnityEngine.Object.DestroyImmediate(trackerGo);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(quest);
            UnityEngine.Object.DestroyImmediate(database);
        }

        [Test]
        public void QuestTracker_CaptureApplySaveData_RestoresProgress()
        {
            var databaseType = GetRuntimeType("CombatSystem.Data.GameDatabase");
            var questType = GetRuntimeType("CombatSystem.Data.QuestDefinition");
            var trackerType = GetRuntimeType("CombatSystem.Gameplay.QuestTracker");
            var statusType = GetRuntimeType("CombatSystem.Gameplay.QuestStatus");
            if (databaseType == null || questType == null || trackerType == null || statusType == null)
            {
                Assert.Ignore("Day5 runtime types not found.");
                return;
            }

            var database = ScriptableObject.CreateInstance(databaseType);
            var quest = BuildQuestDefinition("Quest_Test_Save", true, 0, 2);
            var questList = CreateList(questType, quest);
            SetPrivateField(database, "quests", questList);
            CallMethod(database, "BuildIndexes");

            var questId = GetStringProperty(quest, "Id");

            var trackerGoA = new GameObject("QuestTracker_Save_A");
            var trackerA = trackerGoA.AddComponent(trackerType);
            SetPrivateField(trackerA, "database", database);
            SetPrivateField(trackerA, "autoFindPlayer", false);
            Assert.IsTrue(CallBoolMethod(trackerA, "AcceptQuest", questId));
            Assert.IsTrue(CallBoolMethod(trackerA, "TryAdvanceObjective", questId, "objective_0", 1));
            Assert.AreEqual(Enum.Parse(statusType, "InProgress"), GetPropertyValue(CallMethod(trackerA, "GetState", questId), "Status"));

            var saveData = CallMethod(trackerA, "CaptureSaveData");
            Assert.NotNull(saveData);
            var savedStates = GetPropertyValue(saveData, "quests") as Array;
            Assert.NotNull(savedStates);
            Assert.AreEqual(1, savedStates.Length);
            Assert.AreEqual(questId, GetPropertyValue(saveData, "trackedQuestId"));

            UnityEngine.Object.DestroyImmediate(trackerGoA);

            var trackerGoB = new GameObject("QuestTracker_Save_B");
            var trackerB = trackerGoB.AddComponent(trackerType);
            SetPrivateField(trackerB, "database", database);
            SetPrivateField(trackerB, "autoFindPlayer", false);
            CallMethod(trackerB, "ApplySaveData", saveData);

            var loaded = CallMethod(trackerB, "GetState", questId);
            Assert.NotNull(loaded);
            Assert.AreEqual(Enum.Parse(statusType, "InProgress"), GetPropertyValue(loaded, "Status"));
            Assert.AreEqual(1, (int)CallMethod(loaded, "GetObjectiveProgress", 0));
            Assert.AreEqual(0, (int)CallMethod(loaded, "GetObjectiveProgress", 1));
            Assert.AreEqual(questId, GetStringProperty(trackerB, "TrackedQuestId"));

            UnityEngine.Object.DestroyImmediate(trackerGoB);
            UnityEngine.Object.DestroyImmediate(quest);
            UnityEngine.Object.DestroyImmediate(database);
        }

        [Test]
        public void QuestTracker_CollectObjective_AdvancesOnCurrencyLootPickup()
        {
            var databaseType = GetRuntimeType("CombatSystem.Data.GameDatabase");
            var questType = GetRuntimeType("CombatSystem.Data.QuestDefinition");
            var trackerType = GetRuntimeType("CombatSystem.Gameplay.QuestTracker");
            var inventoryType = GetRuntimeType("CombatSystem.Gameplay.InventoryComponent");
            var currencyType = GetRuntimeType("CombatSystem.Gameplay.CurrencyComponent");
            var lootPickupType = GetRuntimeType("CombatSystem.Gameplay.LootPickup");
            var statusType = GetRuntimeType("CombatSystem.Gameplay.QuestStatus");

            if (databaseType == null || questType == null || trackerType == null || inventoryType == null || currencyType == null || lootPickupType == null || statusType == null)
            {
                Assert.Ignore("Day5 loot/quest runtime types not found.");
                return;
            }

            var database = ScriptableObject.CreateInstance(databaseType);
            var quest = BuildQuestDefinition("Quest_Test_Collect", false, 0, 1, "CollectItem");
            var questList = CreateList(questType, quest);
            SetPrivateField(database, "quests", questList);
            CallMethod(database, "BuildIndexes");

            var player = new GameObject("QuestPlayer_Collect");
            var inventory = player.AddComponent(inventoryType);
            var currency = player.AddComponent(currencyType);
            CallMethod(inventory, "Initialize");
            CallMethod(currency, "Initialize");

            var trackerGo = new GameObject("QuestTracker_Collect");
            var tracker = trackerGo.AddComponent(trackerType);
            SetPrivateField(tracker, "database", database);
            SetPrivateField(tracker, "autoFindPlayer", false);
            SetPrivateField(tracker, "playerInventory", inventory);
            SetPrivateField(tracker, "playerCurrency", currency);
            SetPrivateField(tracker, "playerProgression", null);

            var questId = GetStringProperty(quest, "Id");
            Assert.IsTrue(CallBoolMethod(tracker, "AcceptQuest", questId));

            var pickupGo = new GameObject("LootPickup_Collect");
            pickupGo.AddComponent<SphereCollider>().isTrigger = true;
            var pickup = pickupGo.AddComponent(lootPickupType);
            CallMethod(pickup, "Initialize", null, 10);
            Assert.IsTrue(CallBoolMethod(pickup, "TryPickup", player));

            var state = CallMethod(tracker, "GetState", questId);
            Assert.NotNull(state);
            Assert.AreEqual(1, (int)CallMethod(state, "GetObjectiveProgress", 0));
            Assert.AreEqual(Enum.Parse(statusType, "Completed"), GetPropertyValue(state, "Status"));

            UnityEngine.Object.DestroyImmediate(trackerGo);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(quest);
            UnityEngine.Object.DestroyImmediate(database);
        }

        private static ScriptableObject BuildQuestDefinition(
            string questId,
            bool requireTurnIn,
            int rewardCurrency,
            int requiredObjectiveCount,
            string objectiveTypeName = "Trigger",
            string targetId = "")
        {
            var questType = GetRuntimeType("CombatSystem.Data.QuestDefinition");
            var objectiveType = GetRuntimeType("CombatSystem.Data.QuestObjectiveDefinition");
            var objectiveEnumType = GetRuntimeType("CombatSystem.Data.QuestObjectiveType");
            var rewardType = GetRuntimeType("CombatSystem.Data.QuestRewardDefinition");
            var rewardItemEntryType = GetRuntimeType("CombatSystem.Data.QuestRewardItemEntry");

            Assert.NotNull(questType, "QuestDefinition type not found.");
            Assert.NotNull(objectiveType, "QuestObjectiveDefinition type not found.");
            Assert.NotNull(objectiveEnumType, "QuestObjectiveType enum not found.");
            Assert.NotNull(rewardType, "QuestRewardDefinition type not found.");
            Assert.NotNull(rewardItemEntryType, "QuestRewardItemEntry type not found.");

            var quest = ScriptableObject.CreateInstance(questType);
            SetPrivateField(quest, "id", questId);
            SetPrivateField(quest, "displayName", questId);
            SetPrivateField(quest, "summary", "test");
            SetPrivateField(quest, "requireTurnIn", requireTurnIn);
            SetPrivateField(quest, "autoTrackOnAccept", true);

            var objectives = Activator.CreateInstance(typeof(List<>).MakeGenericType(objectiveType));
            var addObjectiveMethod = objectives.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < requiredObjectiveCount; i++)
            {
                var objective = Activator.CreateInstance(objectiveType);
                SetPrivateField(objective, "objectiveId", $"objective_{i}");
                SetPrivateField(objective, "description", $"Objective {i}");
                SetPrivateField(objective, "objectiveType", Enum.Parse(objectiveEnumType, objectiveTypeName));
                SetPrivateField(objective, "targetId", targetId ?? string.Empty);
                SetPrivateField(objective, "requiredAmount", 1);
                SetPrivateField(objective, "optional", false);
                SetPrivateField(objective, "hiddenUntilProgress", false);
                addObjectiveMethod?.Invoke(objectives, new[] { objective });
            }

            SetPrivateField(quest, "objectives", objectives);

            var reward = Activator.CreateInstance(rewardType);
            SetPrivateField(reward, "currency", rewardCurrency);
            SetPrivateField(reward, "experience", 0);
            var rewardItems = Activator.CreateInstance(typeof(List<>).MakeGenericType(rewardItemEntryType));
            SetPrivateField(reward, "items", rewardItems);
            SetPrivateField(quest, "reward", reward);

            return quest;
        }

        private static Type GetRuntimeType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            return type ?? Type.GetType(typeName);
        }

        private static object CreateList(Type elementType, object firstItem = null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType);
            if (firstItem != null)
            {
                var addMethod = listType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
                addMethod?.Invoke(list, new[] { firstItem });
            }

            return list;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            Assert.NotNull(target);
            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            while (field == null && type.BaseType != null)
            {
                type = type.BaseType;
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object GetPropertyValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            var type = target.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                return property.GetValue(target);
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(target) : null;
        }

        private static string GetStringProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value as string;
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value is int intValue ? intValue : 0;
        }

        private static object CallMethod(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var safeArgs = args ?? Array.Empty<object>();
            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != safeArgs.Length)
                {
                    continue;
                }

                var compatible = true;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (!IsArgumentCompatible(parameters[p].ParameterType, safeArgs[p]))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                {
                    continue;
                }

                return method.Invoke(target, safeArgs);
            }

            return null;
        }

        private static bool IsArgumentCompatible(Type parameterType, object arg)
        {
            if (parameterType == null)
            {
                return false;
            }

            if (arg == null)
            {
                return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;
            }

            var argType = arg.GetType();
            return parameterType.IsAssignableFrom(argType);
        }

        private static bool CallBoolMethod(object target, string methodName, params object[] args)
        {
            var result = CallMethod(target, methodName, args);
            return result is bool boolValue && boolValue;
        }
    }
}
