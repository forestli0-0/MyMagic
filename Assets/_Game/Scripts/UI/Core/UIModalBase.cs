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
