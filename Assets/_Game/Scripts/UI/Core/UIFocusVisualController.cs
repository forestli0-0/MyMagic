using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    [DisallowMultipleComponent]
    public class UIFocusVisualController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private bool autoFindUiManager = true;

        [Header("Behavior")]
        [SerializeField] private bool applyOnlyInUiMode = true;
        [SerializeField] private bool useOutlineHighlight = true;
        [SerializeField] private bool useScaleHighlight = true;

        private Selectable activeSelectable;
        private RectTransform activeRectTransform;
        private Vector3 activeOriginalScale = Vector3.one;
        private bool hasOriginalScale;

        private Outline activeOutline;
        private bool createdOutline;
        private bool cachedOutlineEnabled;
        private Color cachedOutlineColor;
        private Vector2 cachedOutlineDistance;
        private bool cachedUseGraphicAlpha;

        private void OnEnable()
        {
            UIThemeRuntime.ThemeChanged += HandleThemeChanged;
            ResolveReferences();
            RefreshActiveSelectable(true);
        }

        private void OnDisable()
        {
            UIThemeRuntime.ThemeChanged -= HandleThemeChanged;
            ClearFocusVisuals();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            RefreshActiveSelectable(false);
        }

        public void SetUIManager(UIManager manager)
        {
            uiManager = manager;
        }

        private void ResolveReferences()
        {
            if (!autoFindUiManager || uiManager != null)
            {
                return;
            }

            uiManager = GetComponentInParent<UIManager>();
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        private void RefreshActiveSelectable(bool force)
        {
            if (applyOnlyInUiMode && uiManager != null && uiManager.CurrentInputMode != UIInputMode.UI)
            {
                if (force || activeSelectable != null)
                {
                    ClearFocusVisuals();
                }

                return;
            }

            var candidate = ResolveCurrentSelectable();
            if (!force && candidate == activeSelectable)
            {
                return;
            }

            if (candidate == activeSelectable)
            {
                ApplyThemeToCurrentOutline();
                return;
            }

            ClearFocusVisuals();
            if (candidate != null)
            {
                ApplyFocusVisuals(candidate);
            }
        }

        private Selectable ResolveCurrentSelectable()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            var selectedGo = eventSystem.currentSelectedGameObject;
            if (selectedGo == null)
            {
                return null;
            }

            var selectable = selectedGo.GetComponent<Selectable>();
            if (selectable == null)
            {
                selectable = selectedGo.GetComponentInParent<Selectable>();
            }

            if (!UIFocusUtility.IsFocusable(selectable))
            {
                return null;
            }

            return selectable;
        }

        private void ApplyFocusVisuals(Selectable selectable)
        {
            activeSelectable = selectable;
            if (activeSelectable == null)
            {
                return;
            }

            var graphic = activeSelectable.targetGraphic as Graphic;
            if (graphic == null)
            {
                graphic = activeSelectable.GetComponent<Graphic>();
            }

            activeRectTransform = graphic != null ? graphic.rectTransform : activeSelectable.transform as RectTransform;
            var allowScaleHighlight = useScaleHighlight &&
                                      activeRectTransform != null &&
                                      !UIFocusUtility.ShouldSuppressScaleFeedback(activeSelectable);
            if (allowScaleHighlight)
            {
                activeOriginalScale = activeRectTransform.localScale;
                hasOriginalScale = true;
                activeRectTransform.localScale = activeOriginalScale * UIStyleKit.FocusScaleMultiplier;
            }

            if (!useOutlineHighlight || graphic == null)
            {
                return;
            }

            activeOutline = graphic.GetComponent<Outline>();
            if (activeOutline != null)
            {
                createdOutline = false;
                cachedOutlineEnabled = activeOutline.enabled;
                cachedOutlineColor = activeOutline.effectColor;
                cachedOutlineDistance = activeOutline.effectDistance;
                cachedUseGraphicAlpha = activeOutline.useGraphicAlpha;
            }
            else
            {
                activeOutline = graphic.gameObject.AddComponent<Outline>();
                createdOutline = true;
            }

            if (activeOutline != null)
            {
                activeOutline.enabled = true;
                activeOutline.effectColor = UIStyleKit.FocusOutlineColor;
                var width = UIStyleKit.FocusOutlineWidth;
                activeOutline.effectDistance = new Vector2(width, -width);
                activeOutline.useGraphicAlpha = true;
            }
        }

        private void ClearFocusVisuals()
        {
            if (hasOriginalScale && activeRectTransform != null)
            {
                activeRectTransform.localScale = activeOriginalScale;
            }

            hasOriginalScale = false;
            activeRectTransform = null;

            if (activeOutline != null)
            {
                if (createdOutline)
                {
                    Destroy(activeOutline);
                }
                else
                {
                    activeOutline.enabled = cachedOutlineEnabled;
                    activeOutline.effectColor = cachedOutlineColor;
                    activeOutline.effectDistance = cachedOutlineDistance;
                    activeOutline.useGraphicAlpha = cachedUseGraphicAlpha;
                }
            }

            activeOutline = null;
            createdOutline = false;
            activeSelectable = null;
        }

        private void HandleThemeChanged(UIThemeConfig theme)
        {
            ApplyThemeToCurrentOutline();
            if (activeRectTransform != null && hasOriginalScale)
            {
                activeRectTransform.localScale = activeOriginalScale * UIStyleKit.FocusScaleMultiplier;
            }
        }

        private void ApplyThemeToCurrentOutline()
        {
            if (activeOutline == null)
            {
                return;
            }

            activeOutline.effectColor = UIStyleKit.FocusOutlineColor;
            var width = UIStyleKit.FocusOutlineWidth;
            activeOutline.effectDistance = new Vector2(width, -width);
        }
    }
}
