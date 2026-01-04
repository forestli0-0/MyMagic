using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 投射物对象池，负责投射物实例的复用以避免频繁的实例化和销毁。
    /// </summary>
    /// <remarks>
    /// 性能优化要点：
    /// - 每种 Prefab 独立维护一个对象池
    /// - 使用 Stack 实现 LIFO（后进先出），利用 CPU 缓存
    /// - 投射物生命周期结束后调用 Release 归还池中
    /// </remarks>
    public class ProjectilePool : MonoBehaviour
    {
        // 按 Prefab 分组的对象池，Key 为 Prefab，Value 为可复用的投射物栈
        private readonly Dictionary<GameObject, Stack<ProjectileController>> pools = new Dictionary<GameObject, Stack<ProjectileController>>(8);
        private int activeCount;
        private int totalCreated;

        public int ActiveCount => activeCount;
        public int TotalCreated => totalCreated;

        public int PooledCount
        {
            get
            {
                var count = 0;
                foreach (var stack in pools.Values)
                {
                    count += stack.Count;
                }

                return count;
            }
        }

        /// <summary>
        /// 从对象池获取或创建一个投射物实例。
        /// </summary>
        /// <param name="definition">投射物配置定义</param>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成朝向</param>
        /// <returns>可用的投射物控制器，若配置无效则返回 null</returns>
        public ProjectileController Spawn(ProjectileDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || definition.Prefab == null)
            {
                return null;
            }

            // 获取或创建该 Prefab 对应的对象池
            if (!pools.TryGetValue(definition.Prefab, out var stack))
            {
                stack = new Stack<ProjectileController>(8);
                pools[definition.Prefab] = stack;
            }

            ProjectileController instance;
            if (stack.Count > 0)
            {
                // 池中有可复用实例，直接取出
                instance = stack.Pop();
            }
            else
            {
                // 池为空，创建新实例
                instance = CreateInstance(definition.Prefab);
                totalCreated++;
            }

            // 设置位置和朝向，激活对象
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            // 告知投射物其所属的池和 Prefab（用于归还时定位）
            instance.SetPool(this, definition.Prefab);
            activeCount++;
            return instance;
        }

        /// <summary>
        /// 将投射物归还到对象池。
        /// </summary>
        /// <param name="prefab">投射物的原始 Prefab</param>
        /// <param name="instance">要归还的投射物实例</param>
        public void Release(GameObject prefab, ProjectileController instance)
        {
            if (prefab == null || instance == null)
            {
                return;
            }

            // 确保该 Prefab 有对应的池
            if (!pools.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<ProjectileController>(8);
                pools[prefab] = stack;
            }

            // 禁用对象并放回池中
            instance.gameObject.SetActive(false);
            stack.Push(instance);
            if (activeCount > 0)
            {
                activeCount--;
            }
        }

        /// <summary>
        /// 创建新的投射物实例。
        /// </summary>
        /// <param name="prefab">投射物 Prefab</param>
        /// <returns>新创建的投射物控制器</returns>
        private ProjectileController CreateInstance(GameObject prefab)
        {
            // 作为子对象生成，便于层级管理
            var go = Instantiate(prefab, transform);
            var controller = go.GetComponent<ProjectileController>();
            
            // 如果 Prefab 没有 ProjectileController，自动添加
            if (controller == null)
            {
                controller = go.AddComponent<ProjectileController>();
            }

            return controller;
        }
    }
}
