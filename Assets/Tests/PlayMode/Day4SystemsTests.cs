using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    public class Day4SystemsTests
    {
        [Test]
        public void CurrencyComponent_AddSpend_Works()
        {
            var currencyType = GetRuntimeType("CombatSystem.Gameplay.CurrencyComponent");
            if (currencyType == null)
            {
                Assert.Ignore("CurrencyComponent not found.");
                return;
            }

            var go = new GameObject("CurrencyTest");
            var currency = go.AddComponent(currencyType);

            CallMethod(currency, "Add", 50);
            Assert.AreEqual(50, GetIntProperty(currency, "Amount"));

            Assert.IsTrue(CallBoolMethod(currency, "TrySpend", 20));
            Assert.AreEqual(30, GetIntProperty(currency, "Amount"));

            Assert.IsFalse(CallBoolMethod(currency, "TrySpend", 40));
            Assert.AreEqual(30, GetIntProperty(currency, "Amount"));

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void LootTable_RollsCurrencyEntry()
        {
            var tableType = GetRuntimeType("CombatSystem.Data.LootTableDefinition");
            var entryType = GetRuntimeType("CombatSystem.Data.LootEntry");
            var entryTypeEnum = GetRuntimeType("CombatSystem.Data.LootEntryType");
            var resultType = GetRuntimeType("CombatSystem.Data.LootRollResult");
            if (tableType == null || entryType == null || entryTypeEnum == null || resultType == null)
            {
                Assert.Ignore("Loot table types not found.");
                return;
            }

            var table = ScriptableObject.CreateInstance(tableType);
            SetPrivateField(table, "minRolls", 1);
            SetPrivateField(table, "maxRolls", 1);

            var entry = System.Activator.CreateInstance(entryType);
            var currencyEnum = System.Enum.Parse(entryTypeEnum, "Currency");
            SetPrivateField(entry, "type", currencyEnum);
            SetPrivateField(entry, "weight", 1);
            SetPrivateField(entry, "minCurrency", 5);
            SetPrivateField(entry, "maxCurrency", 5);

            var entryList = CreateList(entryType, entry);
            SetPrivateField(table, "entries", entryList);

            var results = CreateList(resultType);
            var count = (int)CallMethod(table, "RollDrops", results);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, GetListCount(results));
            var first = GetListItem(results, 0);
            Assert.IsTrue(GetBoolProperty(first, "IsCurrency"));
            Assert.AreEqual(5, GetIntProperty(first, "Currency"));

            UnityEngine.Object.DestroyImmediate(table);
        }

        [Test]
        public void VendorService_BuySell_Works()
        {
            var inventoryType = GetRuntimeType("CombatSystem.Gameplay.InventoryComponent");
            var currencyType = GetRuntimeType("CombatSystem.Gameplay.CurrencyComponent");
            var vendorServiceType = GetRuntimeType("CombatSystem.Gameplay.VendorService");
            if (inventoryType == null || currencyType == null || vendorServiceType == null)
            {
                Assert.Ignore("VendorService dependencies not found.");
                return;
            }

            var item = CreateItemDefinition(20);
            var vendor = CreateVendorDefinition(item, 5, 1f, 0.5f);
            if (vendor == null)
            {
                UnityEngine.Object.DestroyImmediate(item);
                return;
            }

            var player = new GameObject("VendorPlayer");
            var inventory = player.AddComponent(inventoryType);
            var currency = player.AddComponent(currencyType);
            CallMethod(currency, "Add", 100);

            var vendorGo = new GameObject("VendorService");
            var service = vendorGo.AddComponent(vendorServiceType);
            SetPrivateField(service, "autoFindPlayer", false);
            SetPrivateField(service, "playerInventory", inventory);
            SetPrivateField(service, "playerCurrency", currency);
            InvokeSetVendor(service, vendor);

            Assert.IsTrue(CallBoolMethod(service, "TryBuy", 0, 1));
            Assert.AreEqual(80, GetIntProperty(currency, "Amount"));

            object purchased = null;
            var itemsList = GetPropertyValue(inventory, "Items");
            for (int i = 0; i < GetListCount(itemsList); i++)
            {
                var entry = GetListItem(itemsList, i);
                if (entry != null)
                {
                    purchased = entry;
                    break;
                }
            }

            Assert.IsNotNull(purchased);
            Assert.AreEqual(item, GetPropertyValue(purchased, "Definition"));

            Assert.IsTrue(CallBoolMethod(service, "TrySell", purchased, 1));
            Assert.AreEqual(90, GetIntProperty(currency, "Amount"));

            var stillHasItem = false;
            itemsList = GetPropertyValue(inventory, "Items");
            for (int i = 0; i < GetListCount(itemsList); i++)
            {
                if (GetListItem(itemsList, i) != null)
                {
                    stillHasItem = true;
                    break;
                }
            }

            Assert.IsFalse(stillHasItem);

            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(vendorGo);
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(vendor);
        }

        [Test]
        public void VendorService_SellBlocked_WhenItemCannotBeSold()
        {
            var inventoryType = GetRuntimeType("CombatSystem.Gameplay.InventoryComponent");
            var currencyType = GetRuntimeType("CombatSystem.Gameplay.CurrencyComponent");
            var vendorServiceType = GetRuntimeType("CombatSystem.Gameplay.VendorService");
            if (inventoryType == null || currencyType == null || vendorServiceType == null)
            {
                Assert.Ignore("VendorService dependencies not found.");
                return;
            }

            var item = CreateItemDefinition(20);
            SetPrivateField(item, "canSell", false);
            SetPrivateField(item, "economyVersion", 1);
            var vendor = CreateVendorDefinition(item, 5, 1f, 0.5f);
            if (vendor == null)
            {
                UnityEngine.Object.DestroyImmediate(item);
                return;
            }

            var player = new GameObject("VendorPlayer_NoSell");
            var inventory = player.AddComponent(inventoryType);
            var currency = player.AddComponent(currencyType);
            CallMethod(currency, "Add", 100);

            var vendorGo = new GameObject("VendorService_NoSell");
            var service = vendorGo.AddComponent(vendorServiceType);
            SetPrivateField(service, "autoFindPlayer", false);
            SetPrivateField(service, "playerInventory", inventory);
            SetPrivateField(service, "playerCurrency", currency);
            InvokeSetVendor(service, vendor);

            Assert.IsTrue(CallBoolMethod(service, "TryBuy", 0, 1));
            Assert.AreEqual(80, GetIntProperty(currency, "Amount"));

            var purchased = FindFirstInventoryItem(inventory);
            Assert.IsNotNull(purchased);

            Assert.IsFalse(CallBoolMethod(service, "TrySell", purchased, 1));
            Assert.AreEqual(80, GetIntProperty(currency, "Amount"));
            Assert.IsNotNull(FindFirstInventoryItem(inventory));

            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(vendorGo);
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(vendor);
        }

        [Test]
        public void VendorService_UsesItemPriceOverrides()
        {
            var inventoryType = GetRuntimeType("CombatSystem.Gameplay.InventoryComponent");
            var currencyType = GetRuntimeType("CombatSystem.Gameplay.CurrencyComponent");
            var vendorServiceType = GetRuntimeType("CombatSystem.Gameplay.VendorService");
            if (inventoryType == null || currencyType == null || vendorServiceType == null)
            {
                Assert.Ignore("VendorService dependencies not found.");
                return;
            }

            var item = CreateItemDefinition(20);
            SetPrivateField(item, "buyPriceOverride", 40);
            SetPrivateField(item, "sellPriceOverride", 30);
            SetPrivateField(item, "economyVersion", 1);

            var vendor = CreateVendorDefinition(item, 5, 1f, 0.5f);
            if (vendor == null)
            {
                UnityEngine.Object.DestroyImmediate(item);
                return;
            }

            var player = new GameObject("VendorPlayer_Override");
            var inventory = player.AddComponent(inventoryType);
            var currency = player.AddComponent(currencyType);
            CallMethod(currency, "Add", 100);

            var vendorGo = new GameObject("VendorService_Override");
            var service = vendorGo.AddComponent(vendorServiceType);
            SetPrivateField(service, "autoFindPlayer", false);
            SetPrivateField(service, "playerInventory", inventory);
            SetPrivateField(service, "playerCurrency", currency);
            InvokeSetVendor(service, vendor);

            Assert.IsTrue(CallBoolMethod(service, "TryBuy", 0, 1));
            Assert.AreEqual(60, GetIntProperty(currency, "Amount"));

            var purchased = FindFirstInventoryItem(inventory);
            Assert.IsNotNull(purchased);

            Assert.IsTrue(CallBoolMethod(service, "TrySell", purchased, 1));
            Assert.AreEqual(75, GetIntProperty(currency, "Amount"));

            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(vendorGo);
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(vendor);
        }

        private static ScriptableObject CreateItemDefinition(int basePrice)
        {
            var itemType = GetRuntimeType("CombatSystem.Data.ItemDefinition");
            if (itemType == null)
            {
                Assert.Ignore("ItemDefinition not found.");
                return null;
            }

            var slotEnum = GetRuntimeType("CombatSystem.Data.ItemSlot");
            var slotNone = slotEnum != null ? System.Enum.Parse(slotEnum, "None") : null;

            var item = ScriptableObject.CreateInstance(itemType);
            SetPrivateField(item, "economyVersion", 1);
            SetPrivateField(item, "basePrice", basePrice);
            SetPrivateField(item, "buyPriceOverride", -1);
            SetPrivateField(item, "sellPriceOverride", -1);
            SetPrivateField(item, "canBuy", true);
            SetPrivateField(item, "canSell", true);
            SetPrivateField(item, "stackable", true);
            SetPrivateField(item, "maxStack", 99);
            SetPrivateField(item, "allowAffixes", false);
            if (slotNone != null)
            {
                SetPrivateField(item, "slot", slotNone);
            }
            return item;
        }

        private static ScriptableObject CreateVendorDefinition(ScriptableObject item, int stock, float buyMultiplier, float sellMultiplier)
        {
            var vendorType = GetRuntimeType("CombatSystem.Data.VendorDefinition");
            var entryType = GetRuntimeType("CombatSystem.Data.VendorItemEntry");
            if (vendorType == null || entryType == null)
            {
                Assert.Ignore("VendorDefinition types not found. Ensure scripts are imported.");
                return null;
            }

            var vendor = ScriptableObject.CreateInstance(vendorType);
            SetPrivateField(vendor, "vendorRuntimeVersion", 1);
            SetPrivateField(vendor, "buyPriceMultiplier", buyMultiplier);
            SetPrivateField(vendor, "sellPriceMultiplier", sellMultiplier);

            var entry = Activator.CreateInstance(entryType);
            SetPrivateField(entry, "item", item);
            SetPrivateField(entry, "priceOverride", -1);
            SetPrivateField(entry, "stock", stock);
            SetPrivateField(entry, "infiniteStock", false);

            var listType = typeof(List<>).MakeGenericType(entryType);
            var list = Activator.CreateInstance(listType);
            listType.GetMethod("Add")?.Invoke(list, new[] { entry });
            SetPrivateField(vendor, "items", list);
            return vendor;
        }

        private static object FindFirstInventoryItem(Component inventory)
        {
            if (inventory == null)
            {
                return null;
            }

            var itemsList = GetPropertyValue(inventory, "Items");
            for (int i = 0; i < GetListCount(itemsList); i++)
            {
                var entry = GetListItem(itemsList, i);
                if (entry != null)
                {
                    return entry;
                }
            }

            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
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

        private static void InvokeSetVendor(Component service, ScriptableObject vendor)
        {
            if (service == null || vendor == null)
            {
                return;
            }

            var method = service.GetType().GetMethod("SetVendor", BindingFlags.Instance | BindingFlags.Public);
            method?.Invoke(service, new object[] { vendor });
        }

        private static Type GetRuntimeType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            return type ?? Type.GetType(typeName);
        }

        private static object CreateList(Type elementType, object firstItem = null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = System.Activator.CreateInstance(listType);
            if (firstItem != null)
            {
                listType.GetMethod("Add")?.Invoke(list, new[] { firstItem });
            }

            return list;
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                return property.GetValue(target);
            }

            // Some runtime data types (e.g. readonly structs) expose fields instead of properties.
            var field = type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(target);
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value is int intValue ? intValue : 0;
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value is bool boolValue && boolValue;
        }

        private static int GetListCount(object list)
        {
            if (list is System.Collections.ICollection collection)
            {
                return collection.Count;
            }

            return 0;
        }

        private static object GetListItem(object list, int index)
        {
            if (list is System.Collections.IList typedList)
            {
                return typedList[index];
            }

            return null;
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
