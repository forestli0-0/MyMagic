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
        private static readonly Color DefaultGameplayOverlayColor = new Color(0.03f, 0.04f, 0.06f, 0.68f);
        private static readonly Color DefaultGameplayHeaderColor = new Color(0.05f, 0.08f, 0.12f, 0.92f);
        private static readonly Color DefaultGameplayPanelColor = new Color(0.09f, 0.12f, 0.18f, 0.94f);
        private static readonly Color DefaultGameplayPanelAltColor = new Color(0.12f, 0.15f, 0.2f, 0.94f);
        private static readonly Color DefaultTabActiveColor = new Color(0.26f, 0.38f, 0.56f, 1f);
        private static readonly Color DefaultTabInactiveColor = new Color(0.2f, 0.22f, 0.26f, 1f);
        private static readonly Color DefaultTabActiveTextColor = new Color(0.97f, 0.98f, 1f, 1f);
        private static readonly Color DefaultTabInactiveTextColor = new Color(0.85f, 0.87f, 0.9f, 1f);
        private static readonly Color DefaultFooterHintBackgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.92f);
        private static readonly Color DefaultFooterHintTextColor = new Color(0.74f, 0.79f, 0.86f, 1f);
        private static Font fallbackFont;

        public static Color GameplayOverlayColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.GameplayOverlayColor
            : DefaultGameplayOverlayColor;

        public static Color GameplayHeaderColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.GameplayHeaderColor
            : DefaultGameplayHeaderColor;

        public static Color GameplayPanelColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.GameplayPanelColor
            : DefaultGameplayPanelColor;

        public static Color GameplayPanelAltColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.GameplayPanelAltColor
            : DefaultGameplayPanelAltColor;

        public static Color TabActiveColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.TabActiveColor
            : DefaultTabActiveColor;

        public static Color TabInactiveColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.TabInactiveColor
            : DefaultTabInactiveColor;

        public static Color TabActiveTextColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.TabActiveTextColor
            : DefaultTabActiveTextColor;

        public static Color TabInactiveTextColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.TabInactiveTextColor
            : DefaultTabInactiveTextColor;

        public static Color FooterHintBackgroundColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.FooterHintBackgroundColor
            : DefaultFooterHintBackgroundColor;

        public static Color FooterHintTextColor => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.FooterHintTextColor
            : DefaultFooterHintTextColor;

        public static Font ThemeFont
        {
            get
            {
                if (UIThemeRuntime.ActiveTheme != null && UIThemeRuntime.ActiveTheme.DefaultFont != null)
                {
                    return UIThemeRuntime.ActiveTheme.DefaultFont;
                }

                if (fallbackFont == null)
                {
                    fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return fallbackFont;
            }
        }

        public static Sprite ThemeSprite => UIThemeRuntime.ActiveTheme != null
            ? UIThemeRuntime.ActiveTheme.DefaultSprite
            : null;

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
