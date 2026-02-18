using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public abstract class UIScreenBase : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] protected UIInputMode inputMode = UIInputMode.UI;
        [Header("Navigation")]
        [SerializeField] private Selectable defaultSelectable;

        public UIInputMode InputMode => inputMode;
        public Selectable DefaultSelectable => defaultSelectable;

        private void Awake()
        {
            EnsureCanvasGroup();
        }

        internal void SetVisible(bool visible)
        {
            var group = EnsureCanvasGroup();
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            gameObject.SetActive(visible);
        }

        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnFocus() { }
        public virtual void OnBlur() { }
        public virtual string GetFooterHintText() => string.Empty;
        public virtual bool FocusDefaultSelectable()
        {
            return UIFocusUtility.FocusDefault(defaultSelectable, this);
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }
    }
}
