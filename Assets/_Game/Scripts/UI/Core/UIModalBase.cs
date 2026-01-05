using System;
using UnityEngine;

namespace CombatSystem.UI
{
    public abstract class UIModalBase : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool closeOnBackground = true;
        [SerializeField] private bool pauseGameplay = true;

        public bool CloseOnBackground => closeOnBackground;
        public bool PauseGameplay => pauseGameplay;

        public event Action<UIModalBase> CloseRequested;

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

        public void RequestClose()
        {
            CloseRequested?.Invoke(this);
        }

        public void HandleBackgroundClick()
        {
            if (closeOnBackground)
            {
                RequestClose();
            }
        }

        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnFocus() { }
        public virtual void OnBlur() { }
    }
}
