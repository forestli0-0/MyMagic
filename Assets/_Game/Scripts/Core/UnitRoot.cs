using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 单位根组件，是该 GameObject 的逻辑入口，负责组合各个子组件并协调初始化流程。
    /// </summary>
    public class UnitRoot : MonoBehaviour
    {
        [Header("核心配置")]
        [Tooltip("该单位使用的数据定义")]
        [SerializeField] private UnitDefinition unitDefinition;
        [Tooltip("分配给该单位的全局事件中心")]
        [SerializeField] private CombatEventHub eventHub;
        
        [Header("组件引用")]
        [SerializeField] private StatsComponent stats;
        [SerializeField] private HealthComponent health;
        [SerializeField] private ResourceComponent resource;
        [SerializeField] private CooldownComponent cooldown;
        [SerializeField] private UnitTagsComponent unitTags;
        [SerializeField] private BuffController buffController;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private TeamComponent team;
        
        [Header("选项")]
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool initializeOnAwake = true;

        public UnitDefinition Definition => unitDefinition;
        public CombatEventHub EventHub => eventHub;
        public TeamComponent Team => team;

        private void Reset()
        {
            // 编辑器下自动收集相关的子组件
            stats = GetComponent<StatsComponent>();
            health = GetComponent<HealthComponent>();
            resource = GetComponent<ResourceComponent>();
            cooldown = GetComponent<CooldownComponent>();
            unitTags = GetComponent<UnitTagsComponent>();
            buffController = GetComponent<BuffController>();
            progression = GetComponent<PlayerProgression>();
            team = GetComponent<TeamComponent>();
        }

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize(unitDefinition);
            }
        }

        /// <summary>
        /// 核心初始化入口。
        /// </summary>
        /// <param name="definition">单位配置数据</param>
        public void Initialize(UnitDefinition definition)
        {
            unitDefinition = definition;

            // 1. 挂接事件总线
            ApplyEventHub();

            // 2. 依次驱动各个子组件的初始化
            if (stats != null)
            {
                stats.Initialize(definition);
            }

            if (unitTags != null)
            {
                unitTags.Initialize(definition);
            }

            if (health != null)
            {
                health.Initialize();
            }

            if (resource != null)
            {
                resource.Initialize();
            }
        }

        /// <summary>
        /// 将 EventHub 引用应用至所有的子逻辑组件。
        /// </summary>
        private void ApplyEventHub()
        {
            if (eventHub == null)
            {
                return;
            }

            if (stats != null)
            {
                stats.SetEventHub(eventHub);
            }

            if (health != null)
            {
                health.SetEventHub(eventHub);
            }

            if (resource != null)
            {
                resource.SetEventHub(eventHub);
            }

            if (cooldown != null)
            {
                cooldown.SetEventHub(eventHub);
            }

            if (progression != null)
            {
                progression.SetEventHub(eventHub);
            }

        }
    }
}
