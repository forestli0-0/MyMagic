using CombatSystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 施法条 UI 组件 - 显示技能施法进度。
    /// 当单位开始施法时显示，施法完成或中断时隐藏。
    /// </summary>
    public class CastBarUI : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("进度填充 Image（需设置为 Filled 类型）")]
        [SerializeField] private Image fill;
        
        [Tooltip("显示技能名称的文本组件")]
        [SerializeField] private Text label;
        
        [Tooltip("用于控制整体显隐的 CanvasGroup（可选）")]
        [SerializeField] private CanvasGroup canvasGroup;

        /// <summary>
        /// 当前阶段开始的时间点
        /// </summary>
        private float phaseStartTime;
        
        /// <summary>
        /// 当前阶段持续时间
        /// </summary>
        private float phaseDuration;
        private float castDuration;
        private float channelDuration;
        private bool isChannelPhase;
        private SkillDefinition currentSkill;
        
        /// <summary>
        /// 施法条是否处于激活状态
        /// </summary>
        private bool isActive;

        private void Awake()
        {
            // 初始化时隐藏施法条
            Hide();
        }

        private void Update()
        {
            // 非激活状态或无持续时间时跳过
            if (!isActive || phaseDuration <= 0f)
            {
                return;
            }

            // 计算施法进度（0~1）
            var progress = Mathf.Clamp01((Time.time - phaseStartTime) / phaseDuration);
            if (fill != null)
            {
                fill.fillAmount = progress;
            }

            // 进度完成后自动隐藏
            if (progress >= 1f)
            {
                if (!isChannelPhase && channelDuration > 0f)
                {
                    StartPhase(true);
                }
                else
                {
                    Hide();
                }
            }
        }

        /// <summary>
        /// 显示施法条并开始进度计时。
        /// </summary>
        /// <param name="skill">正在施放的技能定义</param>
        /// <param name="castTime">施法时间（秒）</param>
        /// <param name="channelTime">引导时间（秒）</param>
        public void Show(SkillDefinition skill, float castTime, float channelTime)
        {
            castDuration = Mathf.Max(0f, castTime);
            channelDuration = Mathf.Max(0f, channelTime);
            currentSkill = skill;

            // 无施法与引导时间则不显示
            if (castDuration <= 0f && channelDuration <= 0f)
            {
                Hide();
                return;
            }

            isActive = true;

            // 设置技能名称标签
            if (label != null)
            {
                label.text = skill != null ? skill.DisplayName : "Casting";
            }

            StartPhase(castDuration <= 0f);

            SetVisible(true);
        }

        /// <summary>
        /// 隐藏施法条并重置状态。
        /// </summary>
        public void Hide()
        {
            isActive = false;
            phaseDuration = 0f;
            castDuration = 0f;
            channelDuration = 0f;
            isChannelPhase = false;
            currentSkill = null;

            if (fill != null)
            {
                fill.fillAmount = 0f;
            }

            SetVisible(false);
        }

        private void StartPhase(bool channelPhase)
        {
            isChannelPhase = channelPhase;
            phaseStartTime = Time.time;
            phaseDuration = channelPhase ? channelDuration : castDuration;

            if (label != null && currentSkill != null)
            {
                label.text = currentSkill.DisplayName;
            }

            if (fill != null)
            {
                fill.fillAmount = 0f;
            }
        }

        /// <summary>
        /// 设置施法条的可见性。
        /// 优先使用 CanvasGroup 控制透明度，否则直接设置 GameObject 激活状态。
        /// </summary>
        /// <param name="visible">是否可见</param>
        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
