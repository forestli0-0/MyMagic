using UnityEngine;

namespace CombatSystem.UI
{
    [CreateAssetMenu(fileName = "UIThemeConfig", menuName = "Combat/UI/Theme Config")]
    public class UIThemeConfig : ScriptableObject
    {
        [Header("Assets")]
        [SerializeField] private Font defaultFont;
        [SerializeField] private Sprite defaultSprite;

        [Header("Gameplay Menu Palette")]
        [SerializeField] private Color gameplayOverlayColor = new Color(0.03f, 0.04f, 0.06f, 0.68f);
        [SerializeField] private Color gameplayHeaderColor = new Color(0.05f, 0.08f, 0.12f, 0.92f);
        [SerializeField] private Color gameplayPanelColor = new Color(0.09f, 0.12f, 0.18f, 0.94f);
        [SerializeField] private Color gameplayPanelAltColor = new Color(0.12f, 0.15f, 0.2f, 0.94f);
        [SerializeField] private Color tabActiveColor = new Color(0.26f, 0.38f, 0.56f, 1f);
        [SerializeField] private Color tabInactiveColor = new Color(0.2f, 0.22f, 0.26f, 1f);
        [SerializeField] private Color tabActiveTextColor = new Color(0.97f, 0.98f, 1f, 1f);
        [SerializeField] private Color tabInactiveTextColor = new Color(0.85f, 0.87f, 0.9f, 1f);

        [Header("Footer Hint")]
        [SerializeField] private Color footerHintBackgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.92f);
        [SerializeField] private Color footerHintTextColor = new Color(0.74f, 0.79f, 0.86f, 1f);

        [Header("Interaction States")]
        [SerializeField] private float interactionHoverBoost = 0.1f;
        [SerializeField] private float interactionPressDepth = 0.18f;
        [SerializeField] private float interactionSelectedBoost = 0.08f;
        [SerializeField] private float interactionDisabledDepth = 0.52f;
        [SerializeField] private float interactionFadeDuration = 0.08f;
        [SerializeField] private float interactionHoverScale = 1.02f;
        [SerializeField] private float interactionPressScale = 0.97f;
        [SerializeField] private float interactionHoverColorSmoothing = 22f;
        [SerializeField] private float interactionHoverScaleSmoothing = 18f;
        [SerializeField] private float interactionClickPulseScale = 1.045f;
        [SerializeField] private float interactionClickPulseDuration = 0.11f;

        [Header("Focus Highlight")]
        [SerializeField] private Color focusOutlineColor = new Color(0.62f, 0.78f, 1f, 0.95f);
        [SerializeField] private float focusOutlineWidth = 1.8f;
        [SerializeField] private float focusScaleMultiplier = 1.02f;

        public Font DefaultFont => defaultFont;
        public Sprite DefaultSprite => defaultSprite;

        public Color GameplayOverlayColor => gameplayOverlayColor;
        public Color GameplayHeaderColor => gameplayHeaderColor;
        public Color GameplayPanelColor => gameplayPanelColor;
        public Color GameplayPanelAltColor => gameplayPanelAltColor;
        public Color TabActiveColor => tabActiveColor;
        public Color TabInactiveColor => tabInactiveColor;
        public Color TabActiveTextColor => tabActiveTextColor;
        public Color TabInactiveTextColor => tabInactiveTextColor;

        public Color FooterHintBackgroundColor => footerHintBackgroundColor;
        public Color FooterHintTextColor => footerHintTextColor;

        public float InteractionHoverBoost => interactionHoverBoost;
        public float InteractionPressDepth => interactionPressDepth;
        public float InteractionSelectedBoost => interactionSelectedBoost;
        public float InteractionDisabledDepth => interactionDisabledDepth;
        public float InteractionFadeDuration => interactionFadeDuration;
        public float InteractionHoverScale => interactionHoverScale;
        public float InteractionPressScale => interactionPressScale;
        public float InteractionHoverColorSmoothing => interactionHoverColorSmoothing;
        public float InteractionHoverScaleSmoothing => interactionHoverScaleSmoothing;
        public float InteractionClickPulseScale => interactionClickPulseScale;
        public float InteractionClickPulseDuration => interactionClickPulseDuration;

        public Color FocusOutlineColor => focusOutlineColor;
        public float FocusOutlineWidth => focusOutlineWidth;
        public float FocusScaleMultiplier => focusScaleMultiplier;

        public void EnsureFallbackAssets(Font fallbackFont, Sprite fallbackSprite)
        {
            if (defaultFont == null)
            {
                defaultFont = fallbackFont;
            }

            if (defaultSprite == null)
            {
                defaultSprite = fallbackSprite;
            }
        }
    }
}
