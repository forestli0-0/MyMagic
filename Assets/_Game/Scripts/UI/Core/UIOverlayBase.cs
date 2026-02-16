using UnityEngine;

namespace CombatSystem.UI
{
    public abstract class UIOverlayBase : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private CanvasGroup canvasGroup;

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

        public virtual void OnShow() { }
        public virtual void OnHide() { }

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
