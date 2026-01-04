using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// 战斗 HUD 控制器 - 负责协调所有 UI 组件并响应战斗事件
    /// </summary>
    public class CombatHUDController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("战斗事件总线")]
        [SerializeField] private CombatEventHub eventHub;
        
        [Tooltip("HUD 配置数据 (ScriptableObject)")]
        [SerializeField] private HUDConfig hudConfig;
        
        [Tooltip("当前监听的单位目标")]
        [SerializeField] private UnitRoot targetUnit;
        
        [Tooltip("用于 3D 坐标转屏幕坐标的相机")]
        [SerializeField] private Camera worldCamera;

        [Header("Widgets")]
        [SerializeField] private ValueBarUI healthBar;           // 血条
        [SerializeField] private ValueBarUI resourceBar;         // 蓝条/能量条
        [SerializeField] private SkillBarUI skillBar;             // 技能栏
        [SerializeField] private BuffBarUI buffBar;               // Buff 栏
        [SerializeField] private CastBarUI castBar;               // 施法条
        [SerializeField] private CombatLogUI combatLog;           // 战斗日志
        [SerializeField] private FloatingTextManager floatingText;     // 飘字管理器

        private HealthComponent targetHealth;
        private ResourceComponent targetResource;
        private CooldownComponent targetCooldown;
        private SkillUserComponent targetSkillUser;
        private BuffController targetBuffs;

        private bool initialized;

        private void Start()
        {
            if (!initialized)
            {
                EnsureReferences();
                Subscribe();
                RefreshAll();
                initialized = true;
            }
        }

        private void OnEnable()
        {
            if (initialized)
            {
                EnsureReferences();
                Subscribe();
                RefreshAll();
            }
        }

        private void OnDisable()
        {
            Unsubscribe(); // 隐藏时取消监听，防止内存泄漏或无效回调
        }

        /// <summary>
        /// 确保所有核心组件引用已正确获取
        /// </summary>
        private void EnsureReferences()
        {
            if (targetUnit == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    targetUnit = playerObj.GetComponent<UnitRoot>();
                }
            }
            
            if (targetUnit == null)
            {
                targetUnit = FindObjectOfType<UnitRoot>();
                if (targetUnit != null)
                {
                    Debug.LogWarning("[CombatHUDController] 未找到 Player 标签对象，使用 FindObjectOfType 降级查找", this);
                }
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (targetUnit != null)
            {
                // 获取目标单位的基础战斗组件
                targetHealth = targetUnit.GetComponent<HealthComponent>();
                targetResource = targetUnit.GetComponent<ResourceComponent>();
                targetCooldown = targetUnit.GetComponent<CooldownComponent>();
                targetSkillUser = targetUnit.GetComponent<SkillUserComponent>();
                targetBuffs = targetUnit.GetComponent<BuffController>();
            }

            // 如果没有分配总线，则从目标单位获取
            if (eventHub == null && targetUnit != null)
            {
                eventHub = targetUnit.EventHub;
            }
        }

        /// <summary>
        /// 订阅相关的战斗事件
        /// </summary>
        private void Subscribe()
        {
            if (eventHub != null)
            {
                eventHub.HealthChanged += HandleHealthChanged;
                eventHub.ResourceChanged += HandleResourceChanged;
                eventHub.CooldownChanged += HandleCooldownChanged;
                eventHub.SkillCastStarted += HandleSkillCastStarted;
                eventHub.SkillCastCompleted += HandleSkillCastCompleted;
            }

            if (targetBuffs != null)
            {
                targetBuffs.BuffsChanged += HandleBuffsChanged;
            }
        }

        private void Unsubscribe()
        {
            if (eventHub != null)
            {
                eventHub.HealthChanged -= HandleHealthChanged;
                eventHub.ResourceChanged -= HandleResourceChanged;
                eventHub.CooldownChanged -= HandleCooldownChanged;
                eventHub.SkillCastStarted -= HandleSkillCastStarted;
                eventHub.SkillCastCompleted -= HandleSkillCastCompleted;
            }

            if (targetBuffs != null)
            {
                targetBuffs.BuffsChanged -= HandleBuffsChanged;
            }
        }

        /// <summary>
        /// 全量刷新 UI 显示（通常用于初始化或重新绑定目标时）
        /// </summary>
        private void RefreshAll()
        {
            // 从配置中获取槽位数量，默认值为常规设定
            var maxSkillSlots = hudConfig != null ? hudConfig.MaxSkillSlots : 6;
            var maxBuffSlots = hudConfig != null ? hudConfig.MaxBuffSlots : 12;

            if (healthBar != null && targetHealth != null)
            {
                healthBar.SetValues(targetHealth.Current, targetHealth.Max);
            }

            if (resourceBar != null && targetResource != null)
            {
                resourceBar.SetValues(targetResource.Current, targetResource.Max);
            }

            if (skillBar != null && targetSkillUser != null)
            {
                // 绑定技能栏与冷却系统
                skillBar.Bind(targetSkillUser, targetCooldown, maxSkillSlots);
            }

            if (buffBar != null && targetBuffs != null)
            {
                // 绑定 Buff 显示
                buffBar.Bind(targetBuffs, maxBuffSlots);
            }

            if (castBar != null)
            {
                castBar.Hide(); // 初始默认隐藏施法条
            }

            if (floatingText != null)
            {
                floatingText.SetCamera(worldCamera);
            }

            ApplyConfigVisibility(); // 应用显示/隐藏配置
        }

        /// <summary>
        /// 根据配置强制刷新各个组件的显隐状态
        /// </summary>
        private void ApplyConfigVisibility()
        {
            if (hudConfig == null)
            {
                return;
            }

            if (castBar != null)
            {
                castBar.gameObject.SetActive(hudConfig.ShowCastBar);
            }

            if (combatLog != null)
            {
                combatLog.gameObject.SetActive(hudConfig.ShowCombatLog);
            }

            if (floatingText != null)
            {
                floatingText.gameObject.SetActive(hudConfig.ShowFloatingText);
            }
        }

        // --- 回调处理程序 ---

        private void HandleHealthChanged(HealthChangedEvent evt)
        {
            // 更新血条显示
            if (targetHealth != null && evt.Source == targetHealth && healthBar != null)
            {
                healthBar.SetValues(evt.NewValue, targetHealth.Max);
            }

            // 处理飘字逻辑
            if (floatingText != null && hudConfig != null && hudConfig.ShowFloatingText && evt.Source != null)
            {
                if (!Mathf.Approximately(evt.Delta, 0f))
                {
                    // 在目标位置生成伤害或治疗飘字
                    floatingText.Spawn(evt.Source.transform.position, evt.Delta);
                }
            }
        }

        private void HandleResourceChanged(ResourceChangedEvent evt)
        {
            if (targetResource != null && evt.Source == targetResource && resourceBar != null)
            {
                resourceBar.SetValues(evt.NewValue, targetResource.Max);
            }
        }

        private void HandleCooldownChanged(CooldownChangedEvent evt)
        {
            if (targetCooldown != null && evt.Source == targetCooldown && skillBar != null)
            {
                skillBar.HandleCooldownChanged(evt);
            }
        }

        private void HandleSkillCastStarted(SkillCastEvent evt)
        {
            // 如果监听目标开始施法，显示施法条
            if (castBar != null && evt.Caster == targetUnit && (hudConfig == null || hudConfig.ShowCastBar))
            {
                castBar.Show(evt.Skill, evt.CastTime);
            }

            // 记录到战斗日志
            if (combatLog != null && hudConfig != null && hudConfig.ShowCombatLog && evt.Skill != null)
            {
                var name = evt.Caster != null ? evt.Caster.name : "Unit";
                combatLog.AddEntry($"{name} 正在施放 {evt.Skill.DisplayName}");
            }
        }

        private void HandleSkillCastCompleted(SkillCastEvent evt)
        {
            if (castBar != null && evt.Caster == targetUnit)
            {
                castBar.Hide();
            }
        }

        private void HandleBuffsChanged()
        {
            if (buffBar != null && targetBuffs != null)
            {
                var maxBuffSlots = hudConfig != null ? hudConfig.MaxBuffSlots : 12;
                buffBar.Refresh(maxBuffSlots);
            }
        }
    }
}
