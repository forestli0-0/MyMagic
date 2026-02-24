using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// 飘字管理器 - 负责管理和复用浮动伤害/治疗数字。
    /// 采用对象池模式避免频繁的实例化和销毁开销。
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        [Header("预制体与容器")]
        [Tooltip("飘字项目预制体")]
        [SerializeField] private FloatingTextItem itemPrefab;
        
        [Tooltip("飘字的 UI 根节点（RectTransform）")]
        [SerializeField] private RectTransform root;
        
        [Tooltip("用于世界坐标转屏幕坐标的相机")]
        [SerializeField] private Camera worldCamera;
        
        [Header("对象池配置")]
        [Tooltip("初始预热的对象池大小")]
        [SerializeField] private int initialPoolSize = 8;

        /// <summary>
        /// 当前正在显示的飘字项目列表
        /// </summary>
        private readonly List<FloatingTextItem> active = new List<FloatingTextItem>(16);
        private readonly HashSet<FloatingTextItem> activeLookup = new HashSet<FloatingTextItem>();
        
        /// <summary>
        /// 可复用的飘字项目对象池（栈结构）
        /// </summary>
        private readonly Stack<FloatingTextItem> pool = new Stack<FloatingTextItem>(16);
        private readonly HashSet<FloatingTextItem> poolLookup = new HashSet<FloatingTextItem>();
        private int totalCreated;

        public int ActiveCount => active.Count;
        public int PooledCount => pool.Count;
        public int TotalCreated => totalCreated;

        private void Awake()
        {
            // 如果未手动指定根节点，使用自身 Transform
            if (root == null)
            {
                root = transform as RectTransform;
            }

            // 如果未指定预制体，尝试从子节点获取
            if (itemPrefab == null)
            {
                itemPrefab = GetComponentInChildren<FloatingTextItem>(true);
            }

            EnsureTemplateHidden();

            // 预热对象池
            Prewarm();
        }

        private void OnEnable()
        {
            EnsureTemplateHidden();
            ReclaimUnexpectedChildren();
        }

        private void OnDisable()
        {
            // HUD 被整体隐藏时强制回收，避免恢复后出现脏飘字。
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i];
                if (item == null)
                {
                    continue;
                }

                item.gameObject.SetActive(false);
                PushToPool(item);
            }

            active.Clear();
            activeLookup.Clear();
            EnsureTemplateHidden();
        }

        private void Update()
        {
            EnsureTemplateHidden();

            if (active.Count == 0)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            
            // 倒序遍历，方便安全地移除已完成的项目
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i];
                if (item == null)
                {
                    RemoveActiveAt(i, item);
                    continue;
                }

                // 更新飘字状态，如果生命周期结束则回收
                if (!item.Tick(deltaTime))
                {
                    Release(item);
                    RemoveActiveAt(i, item);
                }
            }
        }

        /// <summary>
        /// 设置用于坐标转换的相机。
        /// </summary>
        /// <param name="camera">世界相机引用</param>
        public void SetCamera(Camera camera)
        {
            worldCamera = camera;
        }

        /// <summary>
        /// 在指定世界位置生成一个飘字。
        /// </summary>
        /// <param name="worldPosition">世界空间位置</param>
        /// <param name="value">数值（负数为伤害，正数为治疗）</param>
        public void Spawn(Vector3 worldPosition, float value)
        {
            if (itemPrefab == null || root == null)
            {
                return;
            }

            // 过滤近似 0 的数值，避免显示无意义的 "0" 飘字。
            if (Mathf.Abs(value) < 0.5f)
            {
                return;
            }

            // 从对象池获取或新建实例
            var item = AcquireItem();
            if (item == null)
            {
                return;
            }

            item.gameObject.SetActive(true);
            item.Activate(worldPosition, value, worldCamera, root);
            active.Add(item);
            activeLookup.Add(item);
        }

        /// <summary>
        /// 预热对象池：预先创建指定数量的飘字项目。
        /// 避免运行时首次生成时的实例化开销。
        /// </summary>
        private void Prewarm()
        {
            if (itemPrefab == null)
            {
                return;
            }

            for (int i = pool.Count; i < initialPoolSize; i++)
            {
                var item = Instantiate(itemPrefab, root);
                item.gameObject.SetActive(false);
                PushToPool(item);
                totalCreated++;
            }
        }

        /// <summary>
        /// 将飘字项目回收到对象池。
        /// </summary>
        /// <param name="item">要回收的飘字项目</param>
        private void Release(FloatingTextItem item)
        {
            item.gameObject.SetActive(false);
            PushToPool(item);
        }

        private void EnsureTemplateHidden()
        {
            if (itemPrefab == null)
            {
                return;
            }

            itemPrefab.HideAsTemplate();
            if (itemPrefab.gameObject.activeSelf)
            {
                itemPrefab.gameObject.SetActive(false);
            }
        }

        private void ReclaimUnexpectedChildren()
        {
            if (root == null)
            {
                return;
            }

            var allItems = root.GetComponentsInChildren<FloatingTextItem>(true);
            for (int i = 0; i < allItems.Length; i++)
            {
                var item = allItems[i];
                if (item == null || item == itemPrefab)
                {
                    continue;
                }

                var isActiveTracked = activeLookup.Contains(item);
                if (!isActiveTracked && item.gameObject.activeSelf)
                {
                    item.gameObject.SetActive(false);
                }

                if (!isActiveTracked && !poolLookup.Contains(item))
                {
                    PushToPool(item);
                }
            }
        }

        private FloatingTextItem AcquireItem()
        {
            while (pool.Count > 0)
            {
                var pooled = pool.Pop();
                poolLookup.Remove(pooled);
                if (pooled != null)
                {
                    return pooled;
                }
            }

            if (itemPrefab == null || root == null)
            {
                return null;
            }

            totalCreated++;
            return Instantiate(itemPrefab, root);
        }

        private void PushToPool(FloatingTextItem item)
        {
            if (item == null)
            {
                return;
            }

            if (!poolLookup.Add(item))
            {
                return;
            }

            pool.Push(item);
        }

        private void RemoveActiveAt(int index, FloatingTextItem item)
        {
            if (index >= 0 && index < active.Count)
            {
                active.RemoveAt(index);
            }

            activeLookup.Remove(item);
        }
    }
}
