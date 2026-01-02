using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 所有配置定义的抽象基类，基于 ScriptableObject。
    /// </summary>
    public abstract class DefinitionBase : ScriptableObject
    {
        [Tooltip("唯一标识符，用于在数据库中索引")]
        [SerializeField] private string id;
        
        [Tooltip("显示的显示名称（支持多语言）")]
        [SerializeField] private string displayName;

        public string Id => id;
        public string DisplayName => displayName;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // 如果 ID 为空，则默认使用文件名
            if (string.IsNullOrWhiteSpace(id))
            {
                id = name;
            }
        }
#endif
    }
}
