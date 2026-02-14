using System.Collections;
using CombatSystem.Data;
using CombatSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 单个技能槽位 UI 组件 - 显示技能图标、冷却遮罩、冷却倒计时和快捷键。
    /// 由 SkillBarUI 管理和更新。
    /// </summary>
    public class SkillSlotUI : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("技能图标 Image 组件")]
        [SerializeField] private Image icon;
        
        [Tooltip("冷却遮罩 Image（需设置为 Filled 类型，推荐 Radial 360）")]
        [SerializeField] private Image cooldownFill;
        
        [Tooltip("显示冷却剩余秒数的文本组件")]
        [SerializeField] private Text cooldownText;
        
        [Tooltip("显示快捷键的文本组件")]
        [SerializeField] private Text keyText;

        [Header("Cast FX")]
        [SerializeField] private bool enableCastPulse = true;
        [SerializeField] private float castPulseScale = 1.12f;
        [SerializeField] private float castPulseDuration = 0.14f;
        [SerializeField] private bool pulseUseUnscaledTime = true;
        [SerializeField] private Color castPulseTint = new Color(1f, 0.96f, 0.7f, 1f);

        /// <summary>
        /// 当前绑定的技能定义
        /// </summary>
        private SkillDefinition skill;
        
        /// <summary>
        /// 缓存的冷却总持续时间（用于计算填充比例）
        /// </summary>
        private float cooldownDuration;
        
        /// <summary>
        /// 上一次显示的冷却秒数（用于减少文本更新频率）
        /// </summary>
        private int lastCooldownSeconds = -1;
        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private Color baseIconColor = Color.white;
        private Coroutine castPulseRoutine;

        /// <summary>
        /// 当前绑定的技能定义（只读）
        /// </summary>
        public SkillDefinition Skill => skill;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
            }

            if (icon != null)
            {
                baseIconColor = icon.color;
            }
        }

        /// <summary>
        /// 绑定技能定义并初始化槽位显示。
        /// </summary>
        /// <param name="skillDef">要绑定的技能定义，null 表示清空槽位</param>
        /// <param name="keyLabel">显示的快捷键标签</param>
        public void BindSkill(SkillDefinition skillDef, string keyLabel)
        {
            skill = skillDef;
            cooldownDuration = 0f;
            lastCooldownSeconds = -1;
            ResetCastPulseVisual();

            // 设置技能图标
            if (icon != null)
            {
                icon.sprite = skillDef != null ? skillDef.Icon : null;
                icon.enabled = skillDef != null && icon.sprite != null;
            }

            // 设置快捷键文本
            if (keyText != null)
            {
                keyText.text = string.IsNullOrEmpty(keyLabel) ? string.Empty : keyLabel;
            }

            // 重置冷却显示
            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = 0f;
                cooldownFill.enabled = false;
            }

            if (cooldownText != null)
            {
                cooldownText.text = string.Empty;
                cooldownText.enabled = false;
            }

            // 无技能时隐藏整个槽位
            gameObject.SetActive(skillDef != null);
        }

        public void PlayCastPulse()
        {
            if (!enableCastPulse || !isActiveAndEnabled)
            {
                return;
            }

            if (castPulseRoutine != null)
            {
                StopCoroutine(castPulseRoutine);
                castPulseRoutine = null;
            }

            castPulseRoutine = StartCoroutine(PlayCastPulseRoutine());
        }

        /// <summary>
        /// 接收冷却变化通知并更新显示。
        /// 由 SkillBarUI 在收到冷却事件时调用。
        /// </summary>
        /// <param name="remaining">剩余冷却时间</param>
        /// <param name="duration">冷却总持续时间</param>
        /// <param name="isCoolingDown">是否正在冷却中</param>
        public void NotifyCooldown(float remaining, float duration, bool isCoolingDown)
        {
            if (!isCoolingDown)
            {
                // 冷却结束，重置状态
                cooldownDuration = 0f;
                lastCooldownSeconds = -1;
                return;
            }

            // 缓存冷却总时长（仅在有效时更新）
            if (duration > 0f)
            {
                cooldownDuration = duration;
            }

            UpdateCooldownVisuals(remaining);
        }

        /// <summary>
        /// 从 CooldownComponent 主动刷新冷却状态。
        /// 由 SkillBarUI 在 Update 中调用以实现平滑的进度条。
        /// </summary>
        /// <param name="cooldown">冷却组件引用</param>
        public void RefreshCooldown(CooldownComponent cooldown)
        {
            if (skill == null || cooldown == null)
            {
                return;
            }

            var remaining = cooldown.GetRemaining(skill);
            UpdateCooldownVisuals(remaining);
        }

        /// <summary>
        /// 更新冷却相关的视觉元素（填充遮罩和倒计时文本）。
        /// </summary>
        /// <param name="remaining">剩余冷却时间</param>
        private void UpdateCooldownVisuals(float remaining)
        {
            if (cooldownFill == null && cooldownText == null)
            {
                return;
            }

            // 冷却结束：隐藏所有冷却元素
            if (remaining <= 0f)
            {
                if (cooldownFill != null)
                {
                    cooldownFill.fillAmount = 0f;
                    cooldownFill.enabled = false;
                }

                if (cooldownText != null)
                {
                    cooldownText.text = string.Empty;
                    cooldownText.enabled = false;
                }

                lastCooldownSeconds = -1;
                return;
            }

            // 计算填充比例（剩余时间 / 总时间）
            var duration = cooldownDuration > 0f ? cooldownDuration : remaining;
            var fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 1f;

            // 更新冷却遮罩
            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = fillAmount;
                cooldownFill.enabled = true;
            }

            // 更新冷却倒计时文本（仅当秒数变化时更新，减少文本重绘）
            if (cooldownText != null)
            {
                var seconds = Mathf.CeilToInt(remaining);
                if (seconds != lastCooldownSeconds)
                {
                    cooldownText.text = seconds.ToString();
                    lastCooldownSeconds = seconds;
                }

                cooldownText.enabled = true;
            }
        }

        private IEnumerator PlayCastPulseRoutine()
        {
            var duration = Mathf.Max(0.06f, castPulseDuration);
            var half = duration * 0.5f;
            var elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += GetPulseDeltaTime();
                var t = Mathf.Clamp01(elapsed / half);
                ApplyPulseVisual(t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += GetPulseDeltaTime();
                var t = 1f - Mathf.Clamp01(elapsed / half);
                ApplyPulseVisual(t);
                yield return null;
            }

            ResetCastPulseVisual();
            castPulseRoutine = null;
        }

        private void ApplyPulseVisual(float intensity)
        {
            if (rectTransform != null)
            {
                var targetScale = baseScale * Mathf.Max(1f, castPulseScale);
                rectTransform.localScale = Vector3.LerpUnclamped(baseScale, targetScale, intensity);
            }

            if (icon != null)
            {
                icon.color = Color.Lerp(baseIconColor, castPulseTint, intensity);
            }
        }

        private void ResetCastPulseVisual()
        {
            if (rectTransform != null)
            {
                rectTransform.localScale = baseScale;
            }

            if (icon != null)
            {
                icon.color = baseIconColor;
            }
        }

        private float GetPulseDeltaTime()
        {
            return pulseUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
