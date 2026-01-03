using System.Collections.Generic;

namespace CombatSystem.Core
{
    /// <summary>
    /// 泛型列表对象池，用于复用 List&lt;T&gt; 实例以避免频繁的内存分配。
    /// </summary>
    /// <remarks>
    /// 在战斗系统的热路径（如 Update、目标筛选）中，频繁创建和销毁 List 会产生 GC 压力。
    /// 此对象池通过复用列表实例来实现"零 GC"目标。
    /// 
    /// 使用模式：
    /// <code>
    /// var list = SimpleListPool&lt;CombatTarget&gt;.Get();
    /// try
    /// {
    ///     // 使用 list 进行操作
    /// }
    /// finally
    /// {
    ///     SimpleListPool&lt;CombatTarget&gt;.Release(list);
    /// }
    /// </code>
    /// 
    /// [设计说明] 当前实现没有池容量上限。
    /// 在极端情况下（如战斗结束后大量列表被归还），池可能持有过多实例。
    /// 若需控制内存占用，可添加 MaxPoolSize 限制：
    /// <code>
    /// private const int MaxPoolSize = 64;
    /// if (Pool.Count >= MaxPoolSize) return;
    /// </code>
    /// </remarks>
    /// <typeparam name="T">列表元素类型</typeparam>
    public static class SimpleListPool<T>
    {
        // 内部池化栈，预分配 32 个槽位
        private static readonly Stack<List<T>> Pool = new Stack<List<T>>(32);

        /// <summary>
        /// 从池中获取一个可用的列表实例。
        /// </summary>
        /// <param name="capacity">期望的最小容量，若池中列表容量不足会自动扩容</param>
        /// <returns>一个空的、可复用的列表实例</returns>
        public static List<T> Get(int capacity = 0)
        {
            if (Pool.Count > 0)
            {
                var list = Pool.Pop();
                // 如果需要更大的容量，提前扩容避免后续多次自动扩容
                if (capacity > list.Capacity)
                {
                    list.Capacity = capacity;
                }
                return list;
            }

            // 池为空时创建新实例
            return capacity > 0 ? new List<T>(capacity) : new List<T>();
        }

        /// <summary>
        /// 将列表归还到池中以供后续复用。
        /// </summary>
        /// <remarks>
        /// 归还时会自动清空列表内容，但保留其内部数组容量。
        /// 调用方在 Release 后不应继续使用该列表。
        /// </remarks>
        /// <param name="list">要归还的列表实例</param>
        public static void Release(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            list.Clear();
            Pool.Push(list);
        }
    }
}
