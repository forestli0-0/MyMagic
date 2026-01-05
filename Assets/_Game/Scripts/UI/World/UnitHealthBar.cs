using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class UnitHealthBar : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private ValueBarUI valueBar;
        [SerializeField] private Text nameText;

        private RectTransform rectTransform;
        private HealthComponent health;
        private UnitRoot unit;
        private UnitHealthBarAnchor anchor;
        private Vector3 fallbackOffset;
        private bool pinned;
        private float hideAt;
        private bool visible;

        public HealthComponent Health => health;
        public bool IsPinned => pinned;
        public bool IsVisible => visible;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
        }

        public void Bind(HealthComponent healthComponent, UnitRoot unitRoot, Vector3 defaultOffset)
        {
            health = healthComponent;
            unit = unitRoot;
            anchor = null;
            fallbackOffset = defaultOffset;

            if (health != null)
            {
                anchor = health.GetComponent<UnitHealthBarAnchor>();
            }

            if (anchor == null && unit != null)
            {
                anchor = unit.GetComponent<UnitHealthBarAnchor>();
            }

            var displayName = ResolveDisplayName();
            if (nameText != null)
            {
                nameText.text = displayName;
            }

            Refresh();
        }

        public void Configure(ValueBarUI bar, Text label)
        {
            valueBar = bar;
            nameText = label;
        }

        public void Refresh()
        {
            if (valueBar != null && health != null)
            {
                valueBar.SetValues(health.Current, health.Max);
            }
        }

        public void SetPinned(bool state)
        {
            pinned = state;
        }

        public void ShowForDuration(float duration, float now)
        {
            hideAt = duration > 0f ? now + duration : now;
            SetVisible(true);
        }

        public void ShowIndefinite()
        {
            hideAt = float.PositiveInfinity;
            SetVisible(true);
        }

        public bool ShouldHide(float now)
        {
            return !pinned && now >= hideAt;
        }

        public void SetVisible(bool state)
        {
            if (visible == state)
            {
                return;
            }

            visible = state;
            gameObject.SetActive(state);
        }

        public void SetScreenPosition(Vector2 anchoredPosition)
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = anchoredPosition;
            }
        }

        public Vector3 GetWorldPosition()
        {
            var targetTransform = health != null ? health.transform : null;
            if (targetTransform == null)
            {
                return Vector3.zero;
            }

            if (anchor != null)
            {
                return targetTransform.position + anchor.WorldOffset;
            }

            return targetTransform.position + fallbackOffset;
        }

        private string ResolveDisplayName()
        {
            if (unit != null && unit.Definition != null && !string.IsNullOrWhiteSpace(unit.Definition.DisplayName))
            {
                return unit.Definition.DisplayName;
            }

            return health != null ? health.name : "Unit";
        }
    }
}
