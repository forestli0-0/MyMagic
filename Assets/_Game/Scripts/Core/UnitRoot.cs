using CombatSystem.Data;
using CombatSystem.Gameplay;
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
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private PassiveController passiveController;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private TeamComponent team;
        
        [Header("选项")]
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool initializeOnAwake = true;
        [Tooltip("若单位缺少 UnitVisualPresenter，运行时自动补齐，保证视觉配置链路可用。")]
        [SerializeField] private bool ensureVisualPresenterOnAwake = true;

        public UnitDefinition Definition => unitDefinition;
        public CombatEventHub EventHub => eventHub;
        public TeamComponent Team
        {
            get
            {
                if (team == null)
                {
                    team = GetComponent<TeamComponent>();
                }

                return team;
            }
        }

        private void Reset()
        {
            // 编辑器下自动收集相关的子组件
            stats = GetComponent<StatsComponent>();
            health = GetComponent<HealthComponent>();
            resource = GetComponent<ResourceComponent>();
            cooldown = GetComponent<CooldownComponent>();
            unitTags = GetComponent<UnitTagsComponent>();
            buffController = GetComponent<BuffController>();
            skillUser = GetComponent<SkillUserComponent>();
            passiveController = GetComponent<PassiveController>();
            progression = GetComponent<PlayerProgression>();
            team = GetComponent<TeamComponent>();
        }

        private void Awake()
        {
            CacheComponentsIfMissing();
            EnsureVisualPresenterIfMissing();
            EnsurePassiveControllerIfMissing();

            if (initializeOnAwake)
            {
                Initialize(unitDefinition);
            }
        }

        private void CacheComponentsIfMissing()
        {
            if (stats == null)
            {
                stats = GetComponent<StatsComponent>();
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (resource == null)
            {
                resource = GetComponent<ResourceComponent>();
            }

            if (cooldown == null)
            {
                cooldown = GetComponent<CooldownComponent>();
            }

            if (unitTags == null)
            {
                unitTags = GetComponent<UnitTagsComponent>();
            }

            if (buffController == null)
            {
                buffController = GetComponent<BuffController>();
            }

            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            if (passiveController == null)
            {
                passiveController = GetComponent<PassiveController>();
            }

            if (progression == null)
            {
                progression = GetComponent<PlayerProgression>();
            }

            if (team == null)
            {
                team = GetComponent<TeamComponent>();
            }
        }

        private void EnsureVisualPresenterIfMissing()
        {
            if (!ensureVisualPresenterOnAwake)
            {
                return;
            }

            if (GetComponent<UnitVisualPresenter>() != null)
            {
                return;
            }

            gameObject.AddComponent<UnitVisualPresenter>();
        }

        private void EnsurePassiveControllerIfMissing()
        {
            if (GetComponent<PassiveController>() == null)
            {
                passiveController = gameObject.AddComponent<PassiveController>();
                return;
            }

            if (passiveController == null)
            {
                passiveController = GetComponent<PassiveController>();
            }
        }

        /// <summary>
        /// 核心初始化入口。
        /// </summary>
        /// <param name="definition">单位配置数据</param>
        public void Initialize(UnitDefinition definition)
        {
            CacheComponentsIfMissing();
            EnsurePassiveControllerIfMissing();
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

            if (skillUser != null)
            {
                skillUser.Initialize(definition);
            }

            if (passiveController != null)
            {
                passiveController.Initialize(definition);
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
