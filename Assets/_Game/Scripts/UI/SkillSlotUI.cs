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

        [Tooltip("槽位底板（为空时自动尝试获取本物体上的 Image）")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Color occupiedBackgroundColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color emptyBackgroundColor = new Color(1f, 1f, 1f, 0.55f);

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
        private float lastCooldownFillAmount = -1f;
        private bool cooldownVisualVisible;
        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private Color baseIconColor = Color.white;
        private Color baseSlotColor = Color.white;
        private Coroutine castPulseRoutine;
        private const float CooldownEndThreshold = 0.02f;
        private const float FillUpdateEpsilon = 0.001f;

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

            EnsureCooldownFillConfigured();

            if (icon != null)
            {
                baseIconColor = icon.color;
            }

            if (slotBackground == null)
            {
                slotBackground = GetComponent<Image>();
            }

            if (slotBackground != null)
            {
                baseSlotColor = slotBackground.color;
                if (occupiedBackgroundColor.a <= 0f)
                {
                    occupiedBackgroundColor = baseSlotColor;
                }
            }
        }

        /// <summary>
        /// 绑定技能定义并初始化槽位显示。
        /// </summary>
        /// <param name="skillDef">要绑定的技能定义，null 表示清空槽位</param>
        /// <param name="keyLabel">显示的快捷键标签</param>
        public void BindSkill(SkillDefinition skillDef, string keyLabel)
        {
            var previousSkill = skill;
            var skillChanged = previousSkill != skillDef;
            skill = skillDef;
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
                keyText.enabled = !string.IsNullOrEmpty(keyLabel);
            }

            // 仅在技能变更时重置冷却，避免 HUD 重绑时出现闪烁/进度条跳变。
            if (skillChanged)
            {
                cooldownDuration = 0f;
                ResetCooldownVisuals(true);
            }

            if (slotBackground != null)
            {
                slotBackground.color = skillDef != null ? occupiedBackgroundColor : emptyBackgroundColor;
            }
        }

        public void SetSlotVisible(bool visible)
        {
            if (gameObject.activeSelf == visible)
            {
                return;
            }

            gameObject.SetActive(visible);
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
                ResetCooldownVisuals();
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
            RefreshCooldown(remaining);
        }

        /// <summary>
        /// 直接使用剩余冷却时间刷新（由 SkillBarUI 在批量轮询时调用，避免重复查询）。
        /// </summary>
        /// <param name="remaining">剩余冷却时间</param>
        public void RefreshCooldown(float remaining)
        {
            if (skill == null)
            {
                return;
            }

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

            EnsureCooldownFillConfigured();

            // 冷却结束：隐藏所有冷却元素
            if (remaining <= CooldownEndThreshold)
            {
                ResetCooldownVisuals();
                return;
            }

            // 计算填充比例（剩余时间 / 总时间）
            if (cooldownDuration <= CooldownEndThreshold)
            {
                // UI 中途绑定时事件可能已经错过，这里兜底用当前剩余值初始化总时长。
                cooldownDuration = remaining;
            }

            var duration = cooldownDuration > 0f ? cooldownDuration : remaining;
            var fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 1f;
            SetCooldownVisualVisible(true);

            // 更新冷却遮罩
            if (cooldownFill != null)
            {
                if (Mathf.Abs(fillAmount - lastCooldownFillAmount) > FillUpdateEpsilon)
                {
                    cooldownFill.fillAmount = fillAmount;
                    lastCooldownFillAmount = fillAmount;
                }
            }

            // 更新冷却倒计时文本（仅当秒数变化时更新，减少文本重绘）
            if (cooldownText != null)
            {
                var seconds = Mathf.Max(1, Mathf.CeilToInt(remaining));
                if (seconds != lastCooldownSeconds)
                {
                    cooldownText.text = seconds.ToString();
                    lastCooldownSeconds = seconds;
                }
            }
        }

        private void SetCooldownVisualVisible(bool visible, bool force = false)
        {
            if (!force && cooldownVisualVisible == visible)
            {
                return;
            }

            cooldownVisualVisible = visible;
            if (cooldownFill != null)
            {
                cooldownFill.enabled = visible;
            }

            if (cooldownText != null)
            {
                cooldownText.enabled = visible;
            }
        }

        private void ResetCooldownVisuals(bool force = false)
        {
            if (cooldownFill != null)
            {
                if (force || cooldownFill.fillAmount > 0f)
                {
                    cooldownFill.fillAmount = 0f;
                }
            }

            if (cooldownText != null)
            {
                if (force || !string.IsNullOrEmpty(cooldownText.text))
                {
                    cooldownText.text = string.Empty;
                }
            }

            SetCooldownVisualVisible(false, force);
            lastCooldownSeconds = -1;
            lastCooldownFillAmount = -1f;
        }

        private void EnsureCooldownFillConfigured()
        {
            if (cooldownFill == null)
            {
                return;
            }

            // 主题替换/动态构建后可能被改回 Simple，导致 fillAmount 无效。
            if (cooldownFill.type != Image.Type.Filled)
            {
                cooldownFill.type = Image.Type.Filled;
            }

            if (cooldownFill.fillMethod != Image.FillMethod.Radial360)
            {
                cooldownFill.fillMethod = Image.FillMethod.Radial360;
            }

            // 视觉上从上方向逆时针收缩，接近常见 ARPG/MOBA 反馈。
            cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            cooldownFill.fillClockwise = false;
            cooldownFill.fillCenter = true;
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
