using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// UI 视觉与交互状态的公共样式工具。
    /// 统一管理常用色板和 Selectable 五态颜色。
    /// </summary>
    public static class UIStyleKit
    {
        public static readonly Color GameplayOverlayColor = new Color(0.03f, 0.04f, 0.06f, 0.68f);
        public static readonly Color GameplayHeaderColor = new Color(0.05f, 0.08f, 0.12f, 0.92f);
        public static readonly Color GameplayPanelColor = new Color(0.09f, 0.12f, 0.18f, 0.94f);
        public static readonly Color GameplayPanelAltColor = new Color(0.12f, 0.15f, 0.2f, 0.94f);
        public static readonly Color TabActiveColor = new Color(0.26f, 0.38f, 0.56f, 1f);
        public static readonly Color TabInactiveColor = new Color(0.2f, 0.22f, 0.26f, 1f);
        public static readonly Color TabActiveTextColor = new Color(0.97f, 0.98f, 1f, 1f);
        public static readonly Color TabInactiveTextColor = new Color(0.85f, 0.87f, 0.9f, 1f);

        public static void ApplyButtonStateColors(
            Button button,
            Color normalColor,
            float hoverBoost = 0.1f,
            float pressDepth = 0.16f,
            float disabledDepth = 0.5f,
            float fadeDuration = 0.08f)
        {
            ApplySelectableStateColors(button, normalColor, hoverBoost, pressDepth, disabledDepth, fadeDuration);
        }

        public static void ApplySelectableStateColors(
            Selectable selectable,
            Color normalColor,
            float hoverBoost = 0.1f,
            float pressDepth = 0.16f,
            float disabledDepth = 0.5f,
            float fadeDuration = 0.08f)
        {
            if (selectable == null)
            {
                return;
            }

            var colors = selectable.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Lift(normalColor, hoverBoost);
            colors.pressedColor = Shade(normalColor, pressDepth);
            colors.selectedColor = Lift(normalColor, hoverBoost * 0.65f);
            colors.disabledColor = Muted(normalColor, disabledDepth);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = Mathf.Max(0f, fadeDuration);
            selectable.colors = colors;
        }

        public static Color Lift(Color color, float amount)
        {
            return Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
        }

        public static Color Shade(Color color, float amount)
        {
            return Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
        }

        public static Color Muted(Color color, float amount)
        {
            var shaded = Shade(color, Mathf.Clamp01(amount));
            shaded.a = Mathf.Clamp01(color.a * 0.78f);
            return shaded;
        }
    }
}
