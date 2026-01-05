using UnityEngine;

namespace CombatSystem.UI
{
    public abstract class UIScreenBase : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] protected UIInputMode inputMode = UIInputMode.UI;

        public UIInputMode InputMode => inputMode;

        internal void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            gameObject.SetActive(visible);
        }

        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnFocus() { }
        public virtual void OnBlur() { }
    }
}
