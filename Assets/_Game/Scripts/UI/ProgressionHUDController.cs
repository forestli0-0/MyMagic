using CombatSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 角色成长 HUD 控制器，负责显示等级、经验条和属性点信息。
    /// </summary>
    /// <remarks>
    /// 该组件通过 CombatEventHub 或直接订阅 PlayerProgression 事件来响应成长数据变化。
    /// 优先使用 EventHub 以保持事件驱动架构的一致性。
    /// </remarks>
    public class ProgressionHUDController : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("全局事件中心，用于监听成长相关事件")]
        [SerializeField] private CombatEventHub eventHub;
        [Tooltip("玩家成长组件引用")]
        [SerializeField] private PlayerProgression progression;
        [Tooltip("经验条 UI 组件")]
        [SerializeField] private ValueBarUI experienceBar;
        [Tooltip("等级文本显示")]
        [SerializeField] private Text levelText;
        [Tooltip("属性点文本显示")]
        [SerializeField] private Text pointsText;
        [Tooltip("当玩家运行时动态生成时，自动重绑检测间隔（秒）")]
        [SerializeField] private float autoRebindInterval = 0.5f;

        [Header("显示格式")]
        [Tooltip("等级显示格式，{0} 会被替换为等级数值")]
        [SerializeField] private string levelFormat = "Lv {0}";
        [Tooltip("属性点显示格式，{0} 会被替换为可用属性点数")]
        [SerializeField] private string pointsFormat = "Points: {0}";

        /// <summary>
        /// 订阅标志，记录是否通过 EventHub 订阅事件。
        /// </summary>
        private bool subscribedToHub;
        private float nextAutoRebindTime;

        private void OnEnable()
        {
            EnsureReferences();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextAutoRebindTime)
            {
                return;
            }

            if (!NeedsRebind())
            {
                return;
            }

            nextAutoRebindTime = Time.unscaledTime + Mathf.Max(0.1f, autoRebindInterval);
            Unsubscribe();
            EnsureReferences();
            Subscribe();
            Refresh();
        }

        /// <summary>
        /// 确保组件引用有效，如果未在 Inspector 中赋值则尝试自动查找。
        /// </summary>
        private void EnsureReferences()
        {
            if (!IsPlayerProgression(progression))
            {
                progression = null;
            }

            if (progression == null)
            {
                var playerUnit = PlayerUnitLocator.FindPlayerUnit();
                if (playerUnit != null)
                {
                    progression = playerUnit.GetComponent<PlayerProgression>();
                }
            }

            if (eventHub == null)
            {
                var playerUnit = PlayerUnitLocator.FindPlayerUnit();
                var root = progression != null ? progression.GetComponent<UnitRoot>() : playerUnit;
                if (!PlayerUnitLocator.IsPlayerUnit(root))
                {
                    root = playerUnit;
                }

                if (root != null)
                {
                    eventHub = root.EventHub;
                }
            }
        }

        private bool NeedsRebind()
        {
            if (!IsPlayerProgression(progression))
            {
                return true;
            }

            if (eventHub == null)
            {
                return true;
            }

            return false;
        }

        private bool IsPlayerProgression(PlayerProgression value)
        {
            if (value == null)
            {
                return false;
            }

            var unit = value.GetComponent<UnitRoot>();
            return PlayerUnitLocator.IsPlayerUnit(unit);
        }

        /// <summary>
        /// 订阅成长相关事件。
        /// </summary>
        /// <remarks>
        /// 优先订阅 EventHub，若不可用则直接订阅 PlayerProgression 组件事件。
        /// </remarks>
        private void Subscribe()
        {
            if (eventHub != null)
            {
                eventHub.ExperienceChanged += HandleExperienceChanged;
                eventHub.LevelChanged += HandleLevelChanged;
                eventHub.AttributePointsChanged += HandleAttributePointsChanged;
                subscribedToHub = true;
                return;
            }

            if (progression != null)
            {
                progression.ExperienceChanged += HandleExperienceChanged;
                progression.LevelChanged += HandleLevelChanged;
                progression.AttributePointsChanged += HandleAttributePointsChanged;
            }
        }

        /// <summary>
        /// 取消订阅成长相关事件。
        /// </summary>
        private void Unsubscribe()
        {
            if (subscribedToHub && eventHub != null)
            {
                eventHub.ExperienceChanged -= HandleExperienceChanged;
                eventHub.LevelChanged -= HandleLevelChanged;
                eventHub.AttributePointsChanged -= HandleAttributePointsChanged;
                subscribedToHub = false;
                return;
            }

            if (progression != null)
            {
                progression.ExperienceChanged -= HandleExperienceChanged;
                progression.LevelChanged -= HandleLevelChanged;
                progression.AttributePointsChanged -= HandleAttributePointsChanged;
            }
        }

        /// <summary>
        /// 处理经验变化事件。
        /// </summary>
        private void HandleExperienceChanged(ExperienceChangedEvent evt)
        {
            if (!BindIfMatches(evt.Source))
            {
                return;
            }

            UpdateExperience(evt.NewValue, evt.XpToNext);
        }

        /// <summary>
        /// 处理等级变化事件。
        /// </summary>
        private void HandleLevelChanged(LevelChangedEvent evt)
        {
            if (!BindIfMatches(evt.Source))
            {
                return;
            }

            UpdateLevel(evt.NewLevel);
        }

        /// <summary>
        /// 处理属性点变化事件。
        /// </summary>
        private void HandleAttributePointsChanged(AttributePointsChangedEvent evt)
        {
            if (!BindIfMatches(evt.Source))
            {
                return;
            }

            UpdatePoints(evt.NewValue);
        }

        /// <summary>
        /// 绑定事件来源，确保只响应目标 PlayerProgression 的事件。
        /// </summary>
        /// <param name="source">事件来源的 PlayerProgression 组件</param>
        /// <returns>是否应处理该事件</returns>
        private bool BindIfMatches(PlayerProgression source)
        {
            if (source == null)
            {
                return false;
            }

            if (progression == null)
            {
                progression = source;
                return true;
            }

            return progression == source;
        }

        /// <summary>
        /// 刷新所有 UI 显示。
        /// </summary>
        private void Refresh()
        {
            if (progression == null)
            {
                UpdateExperience(0, 0);
                UpdateLevel(0);
                UpdatePoints(0);
                return;
            }

            UpdateExperience(progression.CurrentExperience, progression.ExperienceToNextLevel);
            UpdateLevel(progression.Level);
            UpdatePoints(progression.UnspentAttributePoints);
        }

        /// <summary>
        /// 更新经验条显示。
        /// </summary>
        /// <param name="current">当前经验值</param>
        /// <param name="toNext">升级所需经验</param>
        private void UpdateExperience(int current, int toNext)
        {
            if (experienceBar == null)
            {
                return;
            }

            if (toNext <= 0)
            {
                experienceBar.SetValues(1f, 1f);
                return;
            }

            experienceBar.SetValues(current, toNext);
        }

        /// <summary>
        /// 更新等级文本显示。
        /// </summary>
        /// <param name="value">当前等级</param>
        private void UpdateLevel(int value)
        {
            if (levelText == null)
            {
                return;
            }

            levelText.text = string.Format(levelFormat, Mathf.Max(0, value));
        }

        /// <summary>
        /// 更新属性点文本显示。
        /// </summary>
        /// <param name="value">可用属性点数</param>
        private void UpdatePoints(int value)
        {
            if (pointsText == null)
            {
                return;
            }

            pointsText.text = string.Format(pointsFormat, Mathf.Max(0, value));
        }
    }
}
