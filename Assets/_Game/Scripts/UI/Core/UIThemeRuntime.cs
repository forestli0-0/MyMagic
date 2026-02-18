using System;

namespace CombatSystem.UI
{
    public static class UIThemeRuntime
    {
        private static UIThemeConfig activeTheme;

        public static event Action<UIThemeConfig> ThemeChanged;

        public static UIThemeConfig ActiveTheme => activeTheme;

        public static void SetActiveTheme(UIThemeConfig theme)
        {
            if (activeTheme == theme)
            {
                return;
            }

            activeTheme = theme;
            ThemeChanged?.Invoke(activeTheme);
        }
    }
}
