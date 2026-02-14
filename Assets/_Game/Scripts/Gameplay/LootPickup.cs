using System;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 可拾取物：代表世界中一个可被玩家拾取的物品或货币。
    /// 支持触发器自动拾取和手动拾取两种模式。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LootPickup : MonoBehaviour
    {
        public readonly struct PickupEvent
        {
            public readonly GameObject Picker;
            public readonly bool IsCurrency;
            public readonly int Amount;
            public readonly string Label;

            public PickupEvent(GameObject picker, bool isCurrency, int amount, string label)
            {
                Picker = picker;
                IsCurrency = isCurrency;
                Amount = Mathf.Max(0, amount);
                Label = label;
            }
        }

        public static event Action<PickupEvent> PickedUp;

        [Header("Settings")]
        [Tooltip("玩家标签，用于识别拾取者")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("是否在玩家接触时自动拾取")]
        [SerializeField] private bool autoPickup = true;

        /// <summary>物品实例（货币掉落时为null）</summary>
        private ItemInstance item;
        /// <summary>货币数量（物品掉落时为0）</summary>
        private int currency;
        /// <summary>是否已完成初始化</summary>
        private bool initialized;

        /// <summary>获取物品实例</summary>
        public ItemInstance Item => item;
        /// <summary>获取货币数量</summary>
        public int Currency => currency;

        /// <summary>
        /// 初始化拾取物内容
        /// </summary>
        /// <param name="instance">物品实例</param>
        /// <param name="currencyAmount">货币数量</param>
        public void Initialize(ItemInstance instance, int currencyAmount)
        {
            item = instance;
            currency = Mathf.Max(0, currencyAmount);
            initialized = true;
        }

        /// <summary>
        /// 触发器进入事件：实现自动拾取功能
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!autoPickup)
            {
                return;
            }

            // 验证是否为玩家
            if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
            {
                return;
            }

            TryPickup(other.gameObject);
        }

        /// <summary>
        /// 尝试拾取该物品
        /// </summary>
        /// <param name="picker">拾取者 GameObject</param>
        /// <returns>拾取成功返回 true，否则返回 false</returns>
        public bool TryPickup(GameObject picker)
        {
            if (!initialized || picker == null)
            {
                return false;
            }

            // 处理货币拾取
            if (currency > 0)
            {
                var wallet = picker.GetComponent<CurrencyComponent>();
                if (wallet == null)
                {
                    return false;
                }

                var amount = currency;
                wallet.Add(currency);
                PickedUp?.Invoke(new PickupEvent(picker, true, amount, "Gold"));
                Consume();
                return true;
            }

            // 处理物品拾取
            if (item != null)
            {
                var inventory = picker.GetComponent<InventoryComponent>();
                if (inventory == null)
                {
                    return false;
                }

                // 尝试添加到背包，如果失败则保留在地上
                if (!inventory.TryAddItem(item))
                {
                    return false;
                }

                if (QuestTracker.Instance != null)
                {
                    QuestTracker.Instance.NotifyItemCollected(item.Definition, item.Stack);
                }

                var itemLabel = ResolveItemLabel(item);
                var amount = item.Stack;
                PickedUp?.Invoke(new PickupEvent(picker, false, amount, itemLabel));
                Consume();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 消耗该拾取物：清空数据并销毁 GameObject
        /// </summary>
        private void Consume()
        {
            item = null;
            currency = 0;
            initialized = false;
            Destroy(gameObject);
        }

        private static string ResolveItemLabel(ItemInstance instance)
        {
            if (instance == null || instance.Definition == null)
            {
                return "Item";
            }

            if (!string.IsNullOrWhiteSpace(instance.Definition.DisplayName))
            {
                return instance.Definition.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(instance.Definition.Id))
            {
                return instance.Definition.Id;
            }

            return "Item";
        }
    }
}
