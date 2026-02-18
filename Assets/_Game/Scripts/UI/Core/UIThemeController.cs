using UnityEngine;

namespace CombatSystem.UI
{
    [DisallowMultipleComponent]
    public class UIThemeController : MonoBehaviour
    {
        [SerializeField] private UIThemeConfig theme;
        [SerializeField] private bool applyOnEnable = true;

        public UIThemeConfig Theme => theme;

        private void Awake()
        {
            if (applyOnEnable)
            {
                ApplyTheme();
            }
        }

        private void OnEnable()
        {
            if (applyOnEnable)
            {
                ApplyTheme();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && applyOnEnable)
            {
                ApplyTheme();
            }
        }
#endif

        public void SetTheme(UIThemeConfig newTheme, bool applyNow = true)
        {
            theme = newTheme;
            if (applyNow)
            {
                ApplyTheme();
            }
        }

        [ContextMenu("UI/Apply Theme")]
        public void ApplyTheme()
        {
            UIThemeRuntime.SetActiveTheme(theme);
        }
    }
}
