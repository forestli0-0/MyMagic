using System;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 货币组件：管理实体（通常是玩家）的金钱数量。
    /// 提供增加、消费、设置货币的功能，并在数值变化时触发事件。
    /// </summary>
    public class CurrencyComponent : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("游戏开始时的初始货币数量")]
        [SerializeField] private int startingAmount;
        [Tooltip("货币上限，0 表示无上限")]
        [SerializeField] private int maxAmount;
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool initializeOnAwake = true;

        /// <summary>
        /// 当前持有的货币数量（运行时状态）
        /// </summary>
        private int amount;

        /// <summary>
        /// 货币变化事件，参数为 (旧值, 新值)
        /// </summary>
        public event Action<int, int> CurrencyChanged;

        /// <summary>
        /// 获取当前货币数量
        /// </summary>
        public int Amount => amount;

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 初始化货币，将当前数量重置为起始值
        /// </summary>
        public void Initialize()
        {
            amount = 0;
            SetAmount(startingAmount, false);
        }

        /// <summary>
        /// 设置货币数量（会触发变化事件）
        /// </summary>
        /// <param name="value">目标数量</param>
        public void SetAmount(int value)
        {
            SetAmount(value, true);
        }

        /// <summary>
        /// 增加货币
        /// </summary>
        /// <param name="delta">增加的数量，必须为正数</param>
        public void Add(int delta)
        {
            if (delta <= 0)
            {
                return;
            }

            SetAmount(amount + delta, true);
        }

        /// <summary>
        /// 检查是否能负担指定费用
        /// </summary>
        /// <param name="cost">需要花费的金额</param>
        /// <returns>如果当前货币足够则返回 true</returns>
        public bool CanAfford(int cost)
        {
            return cost <= 0 || amount >= cost;
        }

        /// <summary>
        /// 尝试花费指定数量的货币
        /// </summary>
        /// <param name="cost">需要花费的金额</param>
        /// <returns>如果成功扣除返回 true，否则返回 false</returns>
        public bool TrySpend(int cost)
        {
            if (cost <= 0)
            {
                return true;
            }

            if (amount < cost)
            {
                return false;
            }

            SetAmount(amount - cost, true);
            return true;
        }

        /// <summary>
        /// 内部方法：设置货币数量并可选地触发事件
        /// </summary>
        /// <param name="value">目标数量</param>
        /// <param name="notify">是否触发 CurrencyChanged 事件</param>
        private void SetAmount(int value, bool notify)
        {
            // 确保货币不为负数
            var clamped = Mathf.Max(0, value);
            // 如果设置了上限，则限制最大值
            if (maxAmount > 0)
            {
                clamped = Mathf.Min(maxAmount, clamped);
            }

            // 数值未变化则直接返回
            if (clamped == amount)
            {
                return;
            }

            var old = amount;
            amount = clamped;
            if (notify)
            {
                CurrencyChanged?.Invoke(old, amount);
            }
        }
    }
}
