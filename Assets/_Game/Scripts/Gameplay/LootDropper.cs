using System.Collections.Generic;
using CombatSystem.Data;
using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 掉落生成器：负责在特定事件（如单位死亡）时生成物品和货币掉落。
    /// 根据配置的掉落表进行随机，并在世界中生成可拾取的物品。
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        [Header("Loot")]
        [Tooltip("掉落表配置，定义可能掉落的物品和货币")]
        [SerializeField] private LootTableDefinition lootTable;
        [Tooltip("掉落物预制体，用于在世界中生成可拾取物品")]
        [SerializeField] private LootPickup pickupPrefab;
        [Tooltip("掉落物散布半径，使物品不会堆叠在同一点")]
        [SerializeField] private float scatterRadius = 0.5f;
        [Tooltip("掉落位置原点，为空则使用当前物体位置")]
        [SerializeField] private Transform dropOrigin;

        [Header("Triggers")]
        [Tooltip("是否在死亡时自动触发掉落")]
        [SerializeField] private bool dropOnDeath = true;
        [Tooltip("用于监听死亡事件的生命组件")]
        [SerializeField] private HealthComponent health;

        /// <summary>
        /// 掉落结果缓冲区，避免重复分配内存
        /// </summary>
        private readonly List<LootRollResult> rollBuffer = new List<LootRollResult>(8);

        /// <summary>
        /// 编辑器重置时自动获取 HealthComponent
        /// </summary>
        private void Reset()
        {
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            // 订阅死亡事件
            if (dropOnDeath)
            {
                ResolveHealth();
                if (health != null)
                {
                    health.Died += HandleDied;
                }
            }
        }

        private void OnDisable()
        {
            // 取消订阅，防止内存泄漏
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        /// <summary>
        /// 手动触发掉落逻辑
        /// </summary>
        public void DropLoot()
        {
            if (lootTable == null || pickupPrefab == null)
            {
                return;
            }

            // 执行掉落随机
            if (lootTable.RollDrops(rollBuffer) <= 0)
            {
                return;
            }

            // 确定掉落位置原点
            var origin = dropOrigin != null ? dropOrigin.position : transform.position;

            // 遍历所有掉落结果并生成对应的拾取物
            for (int i = 0; i < rollBuffer.Count; i++)
            {
                var roll = rollBuffer[i];
                // 处理货币掉落
                if (roll.IsCurrency)
                {
                    SpawnPickup(origin, null, roll.Currency);
                    continue;
                }

                // 处理物品掉落
                if (!roll.IsItem)
                {
                    continue;
                }

                SpawnItemDrops(origin, roll);
            }
        }

        /// <summary>
        /// 生成物品掉落，处理堆叠逻辑
        /// </summary>
        private void SpawnItemDrops(Vector3 origin, LootRollResult roll)
        {
            if (roll.Item == null)
            {
                return;
            }

            var remaining = Mathf.Max(1, roll.Stack);
            var maxStack = roll.Item.MaxStack;
            var stackable = roll.Item.IsStackable;

            // 根据最大堆叠数分批生成掉落物
            while (remaining > 0)
            {
                var amount = stackable ? Mathf.Min(remaining, maxStack) : 1;
                var instance = new ItemInstance(roll.Item, amount, roll.Rarity, roll.Affixes);
                SpawnPickup(origin, instance, 0);
                remaining -= amount;
            }
        }

        /// <summary>
        /// 在世界中生成一个可拾取物体
        /// </summary>
        /// <param name="origin">生成位置中心点</param>
        /// <param name="item">物品实例（货币掉落时为null）</param>
        /// <param name="currencyAmount">货币数量（物品掉落时为0）</param>
        private void SpawnPickup(Vector3 origin, ItemInstance item, int currencyAmount)
        {
            // 在XZ平面上随机偏移，形成散布效果
            var offset = Random.insideUnitCircle * scatterRadius;
            var position = origin + new Vector3(offset.x, 0f, offset.y);
            var pickup = Instantiate(pickupPrefab, position, Quaternion.identity);
            if (pickup != null)
            {
                pickup.Initialize(item, currencyAmount);
            }
        }

        /// <summary>
        /// 死亡事件处理器
        /// </summary>
        private void HandleDied(HealthComponent source)
        {
            DropLoot();
        }

        /// <summary>
        /// 确保 health 引用有效
        /// </summary>
        private void ResolveHealth()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }
        }
    }
}
