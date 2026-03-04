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

        [Header("Charge FX")]
        [SerializeField] private bool autoCreateChargeFrame = true;
        [SerializeField] private float chargeFrameThickness = 2f;
        [SerializeField] private Color chargeFrameColor = new Color(1f, 0.84f, 0.24f, 0.95f);
        [SerializeField] private RectTransform chargeFrameRoot;
        [SerializeField] private Image chargeEdgeTop;
        [SerializeField] private Image chargeEdgeRight;
        [SerializeField] private Image chargeEdgeBottom;
        [SerializeField] private Image chargeEdgeLeft;

        [Header("Runtime State FX")]
        [SerializeField] private bool autoCreateRuntimeStateFrame = true;
        [SerializeField] private float runtimeStateFrameThickness = 2f;
        [SerializeField] private Color runtimeStateFrameColor = new Color(0.35f, 0.85f, 1f, 0.95f);
        [SerializeField] private RectTransform runtimeStateFrameRoot;
        [SerializeField] private Image runtimeStateEdgeTop;
        [SerializeField] private Image runtimeStateEdgeRight;
        [SerializeField] private Image runtimeStateEdgeBottom;
        [SerializeField] private Image runtimeStateEdgeLeft;

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
        private bool chargeVisualVisible;
        private bool runtimeStateVisualVisible;
        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private Color baseIconColor = Color.white;
        private Color baseSlotColor = Color.white;
        private Sprite baseSkillIcon;
        private Coroutine castPulseRoutine;
        private const float CooldownEndThreshold = 0.02f;
        private const float FillUpdateEpsilon = 0.001f;
        private const float ChargeUpdateEpsilon = 0.002f;
        private const float RuntimeStateUpdateEpsilon = 0.002f;
        private float lastChargeRatio = -1f;
        private float lastRuntimeStateRatio = -1f;

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

            EnsureChargeFrameVisual();
            SetChargeProgress(0f, false, true);
            EnsureRuntimeStateFrameVisual();
            SetRuntimeStateProgress(0f, false, true);
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
            baseSkillIcon = skillDef != null ? skillDef.Icon : null;
            ApplyIconSprite(baseSkillIcon);

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
                SetChargeProgress(0f, false, true);
                SetRuntimeStateProgress(0f, false, true);
            }

            if (slotBackground != null)
            {
                slotBackground.color = skillDef != null ? occupiedBackgroundColor : emptyBackgroundColor;
            }

            if (skillDef == null)
            {
                SetChargeProgress(0f, false, true);
                SetRuntimeStateProgress(0f, false, true);
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

        public void SetChargeProgress(float normalizedRatio, bool charging)
        {
            SetChargeProgress(normalizedRatio, charging, false);
        }

        /// <summary>
        /// 更新技能运行时状态表现（图标覆盖 + 连段窗口进度边）。
        /// </summary>
        public void NotifyRuntimeState(Sprite iconOverride, float normalizedRatio, bool active)
        {
            if (skill == null)
            {
                ApplyIconSprite(null);
                SetRuntimeStateProgress(0f, false, false);
                return;
            }

            var displayIcon = iconOverride != null ? iconOverride : baseSkillIcon;
            ApplyIconSprite(displayIcon);
            SetRuntimeStateProgress(normalizedRatio, active, false);
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

        private void SetChargeProgress(float normalizedRatio, bool charging, bool force)
        {
            if (skill == null || !charging || !skill.SupportsCharge)
            {
                HideChargeVisual(force);
                return;
            }

            EnsureChargeFrameVisual();
            if (chargeFrameRoot == null
                || chargeEdgeTop == null
                || chargeEdgeRight == null
                || chargeEdgeBottom == null
                || chargeEdgeLeft == null)
            {
                return;
            }

            var ratio = Mathf.Clamp01(normalizedRatio);
            if (!force && chargeVisualVisible && Mathf.Abs(ratio - lastChargeRatio) <= ChargeUpdateEpsilon)
            {
                return;
            }

            chargeVisualVisible = true;
            chargeFrameRoot.gameObject.SetActive(true);
            UpdateChargeEdges(ratio);
            lastChargeRatio = ratio;
        }

        private void SetRuntimeStateProgress(float normalizedRatio, bool active, bool force)
        {
            if (skill == null || !active)
            {
                HideRuntimeStateVisual(force);
                return;
            }

            EnsureRuntimeStateFrameVisual();
            if (runtimeStateFrameRoot == null
                || runtimeStateEdgeTop == null
                || runtimeStateEdgeRight == null
                || runtimeStateEdgeBottom == null
                || runtimeStateEdgeLeft == null)
            {
                return;
            }

            var ratio = Mathf.Clamp01(normalizedRatio);
            if (!force && runtimeStateVisualVisible && Mathf.Abs(ratio - lastRuntimeStateRatio) <= RuntimeStateUpdateEpsilon)
            {
                return;
            }

            runtimeStateVisualVisible = true;
            runtimeStateFrameRoot.gameObject.SetActive(true);
            UpdateRuntimeStateEdges(ratio);
            lastRuntimeStateRatio = ratio;
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

        private void ApplyIconSprite(Sprite sprite)
        {
            if (icon == null)
            {
                return;
            }

            if (icon.sprite != sprite)
            {
                icon.sprite = sprite;
            }

            icon.enabled = skill != null && icon.sprite != null;
        }

        private void EnsureChargeFrameVisual()
        {
            if (chargeFrameRoot == null && autoCreateChargeFrame)
            {
                chargeFrameRoot = CreateChargeFrameRoot();
            }

            if (chargeFrameRoot == null)
            {
                return;
            }

            if (chargeEdgeTop == null)
            {
                chargeEdgeTop = CreateChargeEdge("Top", new Vector2(0f, 1f), new Vector2(0f, 1f));
            }

            if (chargeEdgeRight == null)
            {
                chargeEdgeRight = CreateChargeEdge("Right", new Vector2(1f, 1f), new Vector2(1f, 1f));
            }

            if (chargeEdgeBottom == null)
            {
                chargeEdgeBottom = CreateChargeEdge("Bottom", new Vector2(1f, 0f), new Vector2(1f, 0f));
            }

            if (chargeEdgeLeft == null)
            {
                chargeEdgeLeft = CreateChargeEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 0f));
            }

            ApplyChargeEdgeStyle(chargeEdgeTop);
            ApplyChargeEdgeStyle(chargeEdgeRight);
            ApplyChargeEdgeStyle(chargeEdgeBottom);
            ApplyChargeEdgeStyle(chargeEdgeLeft);
            chargeFrameRoot.SetAsLastSibling();
        }

        private RectTransform CreateChargeFrameRoot()
        {
            var root = new GameObject("ChargeFrame", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.localScale = Vector3.one;
            rootRect.localRotation = Quaternion.identity;
            rootRect.SetAsLastSibling();
            return rootRect;
        }

        private Image CreateChargeEdge(string name, Vector2 anchor, Vector2 pivot)
        {
            if (chargeFrameRoot == null)
            {
                return null;
            }

            var edgeObject = new GameObject($"ChargeEdge_{name}", typeof(RectTransform), typeof(Image));
            var edgeRect = edgeObject.GetComponent<RectTransform>();
            edgeRect.SetParent(chargeFrameRoot, false);
            edgeRect.anchorMin = anchor;
            edgeRect.anchorMax = anchor;
            edgeRect.pivot = pivot;
            edgeRect.anchoredPosition = Vector2.zero;
            edgeRect.localScale = Vector3.one;
            edgeRect.localRotation = Quaternion.identity;

            var image = edgeObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private void ApplyChargeEdgeStyle(Image edge)
        {
            if (edge == null)
            {
                return;
            }

            edge.color = chargeFrameColor;
            edge.raycastTarget = false;
        }

        private void UpdateChargeEdges(float ratio)
        {
            if (chargeFrameRoot == null)
            {
                return;
            }

            var width = Mathf.Max(1f, chargeFrameRoot.rect.width);
            var height = Mathf.Max(1f, chargeFrameRoot.rect.height);
            var thickness = Mathf.Max(1f, chargeFrameThickness);
            var perimeterProgress = Mathf.Clamp01(ratio) * 4f;

            var topProgress = Mathf.Clamp01(perimeterProgress);
            var rightProgress = Mathf.Clamp01(perimeterProgress - 1f);
            var bottomProgress = Mathf.Clamp01(perimeterProgress - 2f);
            var leftProgress = Mathf.Clamp01(perimeterProgress - 3f);

            UpdateChargeEdgeRect(chargeEdgeTop, width * topProgress, thickness, topProgress > 0f);
            UpdateChargeEdgeRect(chargeEdgeRight, thickness, height * rightProgress, rightProgress > 0f);
            UpdateChargeEdgeRect(chargeEdgeBottom, width * bottomProgress, thickness, bottomProgress > 0f);
            UpdateChargeEdgeRect(chargeEdgeLeft, thickness, height * leftProgress, leftProgress > 0f);
        }

        private static void UpdateChargeEdgeRect(Image edge, float width, float height, bool visible)
        {
            if (edge == null)
            {
                return;
            }

            var rect = edge.rectTransform;
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
            }

            edge.enabled = visible;
        }

        private void HideChargeVisual(bool force)
        {
            if (!force && !chargeVisualVisible)
            {
                return;
            }

            chargeVisualVisible = false;
            lastChargeRatio = -1f;
            if (chargeFrameRoot != null)
            {
                chargeFrameRoot.gameObject.SetActive(false);
            }
        }

        private void EnsureRuntimeStateFrameVisual()
        {
            if (runtimeStateFrameRoot == null && autoCreateRuntimeStateFrame)
            {
                runtimeStateFrameRoot = CreateRuntimeStateFrameRoot();
            }

            if (runtimeStateFrameRoot == null)
            {
                return;
            }

            if (runtimeStateEdgeTop == null)
            {
                runtimeStateEdgeTop = CreateRuntimeStateEdge("Top", new Vector2(0f, 1f), new Vector2(0f, 1f));
            }

            if (runtimeStateEdgeRight == null)
            {
                runtimeStateEdgeRight = CreateRuntimeStateEdge("Right", new Vector2(1f, 1f), new Vector2(1f, 1f));
            }

            if (runtimeStateEdgeBottom == null)
            {
                runtimeStateEdgeBottom = CreateRuntimeStateEdge("Bottom", new Vector2(1f, 0f), new Vector2(1f, 0f));
            }

            if (runtimeStateEdgeLeft == null)
            {
                runtimeStateEdgeLeft = CreateRuntimeStateEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 0f));
            }

            ApplyRuntimeStateEdgeStyle(runtimeStateEdgeTop);
            ApplyRuntimeStateEdgeStyle(runtimeStateEdgeRight);
            ApplyRuntimeStateEdgeStyle(runtimeStateEdgeBottom);
            ApplyRuntimeStateEdgeStyle(runtimeStateEdgeLeft);
            runtimeStateFrameRoot.SetAsLastSibling();
        }

        private RectTransform CreateRuntimeStateFrameRoot()
        {
            var root = new GameObject("RuntimeStateFrame", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.localScale = Vector3.one;
            rootRect.localRotation = Quaternion.identity;
            rootRect.SetAsLastSibling();
            return rootRect;
        }

        private Image CreateRuntimeStateEdge(string name, Vector2 anchor, Vector2 pivot)
        {
            if (runtimeStateFrameRoot == null)
            {
                return null;
            }

            var edgeObject = new GameObject($"RuntimeStateEdge_{name}", typeof(RectTransform), typeof(Image));
            var edgeRect = edgeObject.GetComponent<RectTransform>();
            edgeRect.SetParent(runtimeStateFrameRoot, false);
            edgeRect.anchorMin = anchor;
            edgeRect.anchorMax = anchor;
            edgeRect.pivot = pivot;
            edgeRect.anchoredPosition = Vector2.zero;
            edgeRect.localScale = Vector3.one;
            edgeRect.localRotation = Quaternion.identity;

            var image = edgeObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private void ApplyRuntimeStateEdgeStyle(Image edge)
        {
            if (edge == null)
            {
                return;
            }

            edge.color = runtimeStateFrameColor;
            edge.raycastTarget = false;
        }

        private void UpdateRuntimeStateEdges(float ratio)
        {
            if (runtimeStateFrameRoot == null)
            {
                return;
            }

            var width = Mathf.Max(1f, runtimeStateFrameRoot.rect.width);
            var height = Mathf.Max(1f, runtimeStateFrameRoot.rect.height);
            var thickness = Mathf.Max(1f, runtimeStateFrameThickness);
            var perimeterProgress = Mathf.Clamp01(ratio) * 4f;

            var topProgress = Mathf.Clamp01(perimeterProgress);
            var rightProgress = Mathf.Clamp01(perimeterProgress - 1f);
            var bottomProgress = Mathf.Clamp01(perimeterProgress - 2f);
            var leftProgress = Mathf.Clamp01(perimeterProgress - 3f);

            UpdateChargeEdgeRect(runtimeStateEdgeTop, width * topProgress, thickness, topProgress > 0f);
            UpdateChargeEdgeRect(runtimeStateEdgeRight, thickness, height * rightProgress, rightProgress > 0f);
            UpdateChargeEdgeRect(runtimeStateEdgeBottom, width * bottomProgress, thickness, bottomProgress > 0f);
            UpdateChargeEdgeRect(runtimeStateEdgeLeft, thickness, height * leftProgress, leftProgress > 0f);
        }

        private void HideRuntimeStateVisual(bool force)
        {
            if (!force && !runtimeStateVisualVisible)
            {
                return;
            }

            runtimeStateVisualVisible = false;
            lastRuntimeStateRatio = -1f;
            if (runtimeStateFrameRoot != null)
            {
                runtimeStateFrameRoot.gameObject.SetActive(false);
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
