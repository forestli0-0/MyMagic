using UnityEngine;

namespace CombatSystem.UI
{
    public abstract class UIOverlayBase : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private CanvasGroup canvasGroup;

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

        public virtual void OnShow() { }
        public virtual void OnHide() { }
    }
}
