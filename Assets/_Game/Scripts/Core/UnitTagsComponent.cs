using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 单位标签组件，用于管理单位运行时的标签集合。
    /// </summary>
    /// <remarks>
    /// 标签系统是战斗系统中实现条件筛选和特殊交互的核心机制。
    /// 例如：
    /// - 目标筛选时可以要求目标必须具备某些标签（RequiredTags）
    /// - 或者排除具备某些标签的目标（BlockedTags）
    /// - Buff 和技能可以基于标签进行条件触发
    /// </remarks>
    public class UnitTagsComponent : MonoBehaviour
    {
        [Tooltip("单位根组件引用")]
        [SerializeField] private UnitRoot unitRoot;
        
        [Tooltip("是否从 UnitDefinition 中加载初始标签")]
        [SerializeField] private bool useUnitDefinitionTags = true;
        
        [Tooltip("当前单位拥有的标签列表")]
        [SerializeField] private List<TagDefinition> tags = new List<TagDefinition>();

        /// <summary>
        /// 获取当前单位的标签只读列表。
        /// </summary>
        public IReadOnlyList<TagDefinition> Tags => tags;

        private void Reset()
        {
            // 编辑器下自动查找 UnitRoot 组件
            unitRoot = GetComponent<UnitRoot>();
        }

        private void Awake()
        {
            if (useUnitDefinitionTags && unitRoot != null)
            {
                Initialize(unitRoot.Definition);
            }
        }

        /// <summary>
        /// 根据单位定义初始化标签列表。
        /// </summary>
        /// <param name="definition">单位配置定义</param>
        public void Initialize(UnitDefinition definition)
        {
            if (!useUnitDefinitionTags || definition == null)
            {
                return;
            }

            tags.Clear();
            var defTags = definition.Tags;
            for (int i = 0; i < defTags.Count; i++)
            {
                var tag = defTags[i];
                if (tag != null && !tags.Contains(tag))
                {
                    tags.Add(tag);
                }
            }
        }

        /// <summary>
        /// 检查单位是否拥有指定标签。
        /// </summary>
        /// <param name="tag">要检查的标签定义</param>
        /// <returns>若拥有该标签则返回 true</returns>
        public bool HasTag(TagDefinition tag)
        {
            return tag != null && tags.Contains(tag);
        }
    }
}
