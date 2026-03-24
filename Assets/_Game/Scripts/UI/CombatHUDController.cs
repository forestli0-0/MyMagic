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
        private const float AuxiliaryBarGap = 6f;
        private const int AuxiliaryBarSearchDepth = 8;

        [Header("References")]
        [Tooltip("战斗事件总线")]
        [SerializeField] private CombatEventHub eventHub;
        
        [Tooltip("HUD 配置数据 (ScriptableObject)")]
        [SerializeField] private HUDConfig hudConfig;
        
        [Tooltip("当前监听的单位目标")]
        [SerializeField] private UnitRoot targetUnit;
        
        [Tooltip("用于 3D 坐标转屏幕坐标的相机")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("当玩家运行时动态生成时，自动重绑检测间隔（秒）")]
        [SerializeField] private float autoRebindInterval = 0.5f;
        [Tooltip("UI 管理器引用，用于感知 HUD 可见性模式（全显/仅技能栏/隐藏）")]
        [SerializeField] private UIManager uiManager;

        [Header("Widgets")]
        [SerializeField] private ValueBarUI healthBar;           // 血条
        [SerializeField] private ValueBarUI resourceBar;         // 蓝条/能量条
        [SerializeField] private ValueBarUI auxiliaryResourceBar; // 附加资源条
        [SerializeField] private SkillBarUI skillBar;             // 技能栏
        [SerializeField] private BuffBarUI buffBar;               // Buff 栏
        [SerializeField] private CastBarUI castBar;               // 施法条
        [SerializeField] private CombatLogUI combatLog;           // 战斗日志
        [SerializeField] private FloatingTextManager floatingText;     // 飘字管理器

        [Header("FX")]
        [SerializeField] private HudToastOverlay hudToastOverlay;
        [SerializeField] private QuestTracker questTracker;
        [SerializeField] private bool enableQuestUpdateToast = true;
        [SerializeField] private bool enableLootPickupToast = true;
        [SerializeField] private bool enableSkillReleaseToast = true;
        [SerializeField] private float questProgressToastCooldown = 0.25f;

        private HealthComponent targetHealth;
        private ResourceComponent targetResource;
        private CooldownComponent targetCooldown;
        private SkillUserComponent targetSkillUser;
        private BuffController targetBuffs;
        private ResourceDefinition activeAuxiliaryResource;

        private bool initialized;
        private float nextAutoRebindTime;
        private bool questEventsSubscribed;
        private bool lootEventsSubscribed;
        private float lastQuestProgressToastTime = -10f;

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

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (Time.unscaledTime < nextAutoRebindTime)
            {
                return;
            }

            if (!NeedsRebind())
            {
                if (!NeedsAuxiliaryRebind())
                {
                    return;
                }
            }

            nextAutoRebindTime = Time.unscaledTime + Mathf.Max(0.1f, autoRebindInterval);
            Unsubscribe();
            EnsureReferences();
            Subscribe();
            RefreshAll();
        }

        /// <summary>
        /// 确保所有核心组件引用已正确获取
        /// </summary>
        private void EnsureReferences()
        {
            if (!PlayerUnitLocator.IsPlayerUnit(targetUnit))
            {
                targetUnit = null;
            }

            if (targetUnit == null)
            {
                targetUnit = PlayerUnitLocator.FindPlayerUnit();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            }

            if (questTracker == null)
            {
                questTracker = QuestTracker.Instance != null
                    ? QuestTracker.Instance
                    : FindFirstObjectByType<QuestTracker>(FindObjectsInactive.Include);
            }

            if (hudToastOverlay == null)
            {
                hudToastOverlay = FindFirstObjectByType<HudToastOverlay>(FindObjectsInactive.Include);
            }

            if (hudToastOverlay == null)
            {
                EnsureHudToastOverlay();
            }

            EnsureAuxiliaryResourceBar();

            targetHealth = null;
            targetResource = null;
            targetCooldown = null;
            targetSkillUser = null;
            targetBuffs = null;

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

        private bool NeedsRebind()
        {
            if (!PlayerUnitLocator.IsPlayerUnit(targetUnit))
            {
                return true;
            }

            if (targetHealth == null || targetResource == null || targetCooldown == null || targetSkillUser == null)
            {
                return true;
            }

            if (worldCamera == null)
            {
                return true;
            }

            return false;
        }

        private bool NeedsAuxiliaryRebind()
        {
            if (hudToastOverlay == null)
            {
                return true;
            }

            if (enableQuestUpdateToast && questTracker == null)
            {
                return true;
            }

            return false;
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
                eventHub.SkillCastInterrupted += HandleSkillCastInterrupted;
            }

            if (targetBuffs != null)
            {
                targetBuffs.BuffsChanged += HandleBuffsChanged;
            }

            SubscribeQuestEvents();
            SubscribeLootEvents();
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
                eventHub.SkillCastInterrupted -= HandleSkillCastInterrupted;
            }

            if (targetBuffs != null)
            {
                targetBuffs.BuffsChanged -= HandleBuffsChanged;
            }

            UnsubscribeQuestEvents();
            UnsubscribeLootEvents();
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
                if (targetResource.PrimaryResource != null)
                {
                    resourceBar.SetFillColor(targetResource.PrimaryResource.HudColor);
                }
            }

            RefreshAuxiliaryResourceBar();

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

            var skillBarOnlyMode = uiManager != null && uiManager.IsHudSkillBarOnlyMode;

            if (castBar != null)
            {
                castBar.gameObject.SetActive(!skillBarOnlyMode && hudConfig.ShowCastBar);
            }

            if (combatLog != null)
            {
                combatLog.gameObject.SetActive(!skillBarOnlyMode && hudConfig.ShowCombatLog);
            }

            if (floatingText != null)
            {
                floatingText.gameObject.SetActive(!skillBarOnlyMode && hudConfig.ShowFloatingText);
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
                if (Mathf.Abs(evt.Delta) >= 1f)
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
                if (evt.Resource == null || evt.Resource == targetResource.PrimaryResource)
                {
                    resourceBar.SetValues(targetResource.Current, targetResource.Max);
                }

                RefreshAuxiliaryResourceBar();
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
                castBar.Show(evt.Skill, evt.CastTime, evt.ChannelTime);
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

            if (evt.Caster == targetUnit && evt.Skill != null)
            {
                if (skillBar != null)
                {
                    skillBar.PlaySkillCastPulse(evt.Skill);
                }

                if (enableSkillReleaseToast && hudToastOverlay != null)
                {
                    hudToastOverlay.ShowInfo($"施放 {evt.Skill.DisplayName}");
                }
            }
        }

        private void HandleSkillCastInterrupted(SkillCastEvent evt)
        {
            if (castBar != null && evt.Caster == targetUnit)
            {
                castBar.Hide();
            }

            if (combatLog != null && hudConfig != null && hudConfig.ShowCombatLog && evt.Skill != null)
            {
                var name = evt.Caster != null ? evt.Caster.name : "Unit";
                combatLog.AddEntry($"{name} 的 {evt.Skill.DisplayName} 被打断");
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

        private void SubscribeQuestEvents()
        {
            if (questEventsSubscribed || questTracker == null)
            {
                return;
            }

            questTracker.QuestAccepted += HandleQuestAccepted;
            questTracker.QuestProgressed += HandleQuestProgressed;
            questTracker.QuestReadyToTurnIn += HandleQuestReadyToTurnIn;
            questTracker.QuestCompleted += HandleQuestCompleted;
            questEventsSubscribed = true;
        }

        private void UnsubscribeQuestEvents()
        {
            if (!questEventsSubscribed || questTracker == null)
            {
                questEventsSubscribed = false;
                return;
            }

            questTracker.QuestAccepted -= HandleQuestAccepted;
            questTracker.QuestProgressed -= HandleQuestProgressed;
            questTracker.QuestReadyToTurnIn -= HandleQuestReadyToTurnIn;
            questTracker.QuestCompleted -= HandleQuestCompleted;
            questEventsSubscribed = false;
        }

        private void SubscribeLootEvents()
        {
            if (lootEventsSubscribed)
            {
                return;
            }

            LootPickup.PickedUp += HandleLootPickedUp;
            lootEventsSubscribed = true;
        }

        private void UnsubscribeLootEvents()
        {
            if (!lootEventsSubscribed)
            {
                return;
            }

            LootPickup.PickedUp -= HandleLootPickedUp;
            lootEventsSubscribed = false;
        }

        private void HandleQuestAccepted(QuestRuntimeState state)
        {
            if (!enableQuestUpdateToast || hudToastOverlay == null)
            {
                return;
            }

            hudToastOverlay.ShowSuccess($"任务已接取：{ResolveQuestName(state)}");
        }

        private void HandleQuestProgressed(QuestRuntimeState state)
        {
            if (!enableQuestUpdateToast || hudToastOverlay == null)
            {
                return;
            }

            if (Time.unscaledTime - lastQuestProgressToastTime < Mathf.Max(0f, questProgressToastCooldown))
            {
                return;
            }

            lastQuestProgressToastTime = Time.unscaledTime;
            hudToastOverlay.ShowInfo($"任务更新：{ResolveQuestName(state)}");
        }

        private void HandleQuestReadyToTurnIn(QuestRuntimeState state)
        {
            if (!enableQuestUpdateToast || hudToastOverlay == null)
            {
                return;
            }

            hudToastOverlay.ShowWarning($"可提交任务：{ResolveQuestName(state)}");
        }

        private void HandleQuestCompleted(QuestRuntimeState state)
        {
            if (!enableQuestUpdateToast || hudToastOverlay == null)
            {
                return;
            }

            hudToastOverlay.ShowSuccess($"任务完成：{ResolveQuestName(state)}");
        }

        private void HandleLootPickedUp(LootPickup.PickupEvent evt)
        {
            if (!enableLootPickupToast || hudToastOverlay == null || evt.Picker == null)
            {
                return;
            }

            var pickerUnit = evt.Picker.GetComponent<UnitRoot>();
            if (!PlayerUnitLocator.IsPlayerUnit(pickerUnit))
            {
                return;
            }

            if (evt.IsCurrency)
            {
                hudToastOverlay.ShowSuccess($"+{evt.Amount} Gold");
                return;
            }

            var label = string.IsNullOrWhiteSpace(evt.Label) ? "物品" : evt.Label;
            var amount = Mathf.Max(1, evt.Amount);
            hudToastOverlay.ShowInfo($"获得 {label} x{amount}");
        }

        private string ResolveQuestName(QuestRuntimeState state)
        {
            if (state == null)
            {
                return "任务";
            }

            var definition = questTracker != null ? questTracker.GetDefinition(state.QuestId) : null;
            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(state.QuestId))
            {
                return state.QuestId;
            }

            return "任务";
        }

        private void EnsureHudToastOverlay()
        {
            Transform parent = null;
            if (UIRoot.Instance != null)
            {
                if (UIRoot.Instance.OverlayCanvas != null)
                {
                    parent = UIRoot.Instance.OverlayCanvas.transform;
                }
                else if (UIRoot.Instance.HudCanvas != null)
                {
                    parent = UIRoot.Instance.HudCanvas.transform;
                }
            }

            if (parent == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                parent = canvas != null ? canvas.transform : transform;
            }

            var root = new GameObject("HudToastOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(HudToastOverlay));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -86f);
            rect.sizeDelta = new Vector2(520f, 58f);

            hudToastOverlay = root.GetComponent<HudToastOverlay>();
        }

        private void EnsureAuxiliaryResourceBar()
        {
            if (auxiliaryResourceBar != null || resourceBar == null)
            {
                return;
            }

            var templateRect = resourceBar.transform as RectTransform;
            if (templateRect == null || templateRect.parent == null)
            {
                return;
            }

            var clone = Instantiate(resourceBar.gameObject, templateRect.parent);
            clone.name = "AuxiliaryResourceBar_Auto";
            var cloneRect = clone.transform as RectTransform;
            if (cloneRect != null)
            {
                cloneRect.SetSiblingIndex(templateRect.GetSiblingIndex() + 1);
                PositionAuxiliaryResourceBar(templateRect, cloneRect);
            }

            auxiliaryResourceBar = clone.GetComponent<ValueBarUI>();
            if (auxiliaryResourceBar != null)
            {
                auxiliaryResourceBar.gameObject.SetActive(false);
            }
        }

        private void PositionAuxiliaryResourceBar(RectTransform templateRect, RectTransform cloneRect)
        {
            if (templateRect == null || cloneRect == null)
            {
                return;
            }

            var layoutRoot = templateRect.parent as RectTransform;
            if (layoutRoot == null)
            {
                cloneRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, -(templateRect.rect.height + AuxiliaryBarGap));
                return;
            }

            var baseRect = TryGetLocalRect(layoutRoot, templateRect);
            if (!baseRect.HasValue)
            {
                cloneRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, -(templateRect.rect.height + AuxiliaryBarGap));
                return;
            }

            var basePosition = templateRect.anchoredPosition;
            var step = Mathf.Max(templateRect.rect.height, cloneRect.rect.height) + AuxiliaryBarGap;
            var bars = layoutRoot.GetComponentsInChildren<ValueBarUI>(true);

            for (int slot = 1; slot <= AuxiliaryBarSearchDepth; slot++)
            {
                for (int directionIndex = 0; directionIndex < 2; directionIndex++)
                {
                    var direction = directionIndex == 0 ? 1f : -1f;
                    var candidatePosition = basePosition + new Vector2(0f, step * slot * direction);
                    var candidateRect = OffsetRect(baseRect.Value, candidatePosition - basePosition);
                    if (IsAuxiliaryBarSlotAvailable(layoutRoot, bars, cloneRect, candidateRect))
                    {
                        cloneRect.anchoredPosition = candidatePosition;
                        return;
                    }
                }
            }

            cloneRect.anchoredPosition = basePosition + new Vector2(0f, step * (AuxiliaryBarSearchDepth + 1));
        }

        private static bool IsAuxiliaryBarSlotAvailable(
            RectTransform layoutRoot,
            ValueBarUI[] bars,
            RectTransform cloneRect,
            Rect candidateRect)
        {
            if (bars == null)
            {
                return true;
            }

            for (int i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                if (bar == null || !bar.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var otherRectTransform = bar.transform as RectTransform;
                if (otherRectTransform == null || otherRectTransform == cloneRect)
                {
                    continue;
                }

                var otherRect = TryGetLocalRect(layoutRoot, otherRectTransform);
                if (!otherRect.HasValue)
                {
                    continue;
                }

                if (candidateRect.Overlaps(ExpandRect(otherRect.Value, AuxiliaryBarGap)))
                {
                    return false;
                }
            }

            return true;
        }

        private static Rect? TryGetLocalRect(RectTransform layoutRoot, RectTransform targetRect)
        {
            if (layoutRoot == null || targetRect == null)
            {
                return null;
            }

            var corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);
            var min = layoutRoot.InverseTransformPoint(corners[0]);
            var max = layoutRoot.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect OffsetRect(Rect rect, Vector2 delta)
        {
            rect.position += delta;
            return rect;
        }

        private static Rect ExpandRect(Rect rect, float padding)
        {
            return Rect.MinMaxRect(
                rect.xMin - padding,
                rect.yMin - padding,
                rect.xMax + padding,
                rect.yMax + padding);
        }

        private void RefreshAuxiliaryResourceBar()
        {
            if (auxiliaryResourceBar == null)
            {
                return;
            }

            if (targetResource == null || !targetResource.TryGetHighestPriorityHudResource(out var view))
            {
                activeAuxiliaryResource = null;
                auxiliaryResourceBar.gameObject.SetActive(false);
                return;
            }

            activeAuxiliaryResource = view.Definition;
            auxiliaryResourceBar.gameObject.SetActive(true);
            auxiliaryResourceBar.SetValues(view.Current, view.Max);
            if (view.Definition != null)
            {
                auxiliaryResourceBar.SetFillColor(view.Definition.HudColor);
            }
        }
    }
}
