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
        
        /// <summary>
        /// 可复用的飘字项目对象池（栈结构）
        /// </summary>
        private readonly Stack<FloatingTextItem> pool = new Stack<FloatingTextItem>(16);

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
                if (itemPrefab != null)
                {
                    // 将模板对象隐藏，作为预制体使用
                    itemPrefab.gameObject.SetActive(false);
                }
            }

            // 预热对象池
            Prewarm();
        }

        private void Update()
        {
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
                    active.RemoveAt(i);
                    continue;
                }

                // 更新飘字状态，如果生命周期结束则回收
                if (!item.Tick(deltaTime))
                {
                    Release(item);
                    active.RemoveAt(i);
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

            // 从对象池获取或新建实例
            var item = pool.Count > 0 ? pool.Pop() : Instantiate(itemPrefab, root);
            item.gameObject.SetActive(true);
            item.Activate(worldPosition, value, worldCamera, root);
            active.Add(item);
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
                pool.Push(item);
            }
        }

        /// <summary>
        /// 将飘字项目回收到对象池。
        /// </summary>
        /// <param name="item">要回收的飘字项目</param>
        private void Release(FloatingTextItem item)
        {
            item.gameObject.SetActive(false);
            pool.Push(item);
        }
    }
}
