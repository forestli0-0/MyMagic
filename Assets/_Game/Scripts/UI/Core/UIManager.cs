using System;
using UnityEngine;

namespace CombatSystem.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Defaults")]
        [SerializeField] private UIScreenBase initialScreen;
        [SerializeField] private bool hideAllScreensOnStart = true;
        [SerializeField] private bool hideHudOnStart = true;

        [Header("Roots")]
        [SerializeField] private Transform screensRoot;
        [SerializeField] private Transform hudRoot;
        [SerializeField] private Transform modalRoot;
        [SerializeField] private Transform overlayRoot;

        private readonly UIStack<UIScreenBase> screenStack = new UIStack<UIScreenBase>(4);
        private readonly UIStack<UIModalBase> modalStack = new UIStack<UIModalBase>(4);

        private UIRoot root;
        private UIInputMode inputMode = UIInputMode.Gameplay;
        private int pauseCount;
        private float cachedTimeScale = 1f;
        private bool initialized;

        public event Action<UIInputMode> InputModeChanged;

        public UIScreenBase CurrentScreen => screenStack.Peek();
        public UIModalBase CurrentModal => modalStack.Peek();
        public int ModalCount => modalStack.Count;
        public UIInputMode CurrentInputMode => inputMode;

        private void Awake()
        {
            if (!initialized)
            {
                var owner = GetComponentInParent<UIRoot>();
                Initialize(owner);
            }
        }

        public void Initialize(UIRoot uiRoot)
        {
            if (initialized && root != null)
            {
                return;
            }

            if (uiRoot != null)
            {
                root = uiRoot;
            }

            if (root == null)
            {
                return;
            }

            initialized = true;

            if (screensRoot == null && root != null && root.ScreensCanvas != null)
            {
                screensRoot = root.ScreensCanvas.transform;
            }

            if (hudRoot == null && root != null && root.HudCanvas != null)
            {
                hudRoot = root.HudCanvas.transform;
            }

            if (modalRoot == null && root != null && root.ModalCanvas != null)
            {
                modalRoot = root.ModalCanvas.transform;
            }

            if (overlayRoot == null && root != null && root.OverlayCanvas != null)
            {
                overlayRoot = root.OverlayCanvas.transform;
            }

            if (hideAllScreensOnStart)
            {
                HideAllScreens();
            }

            if (hideHudOnStart)
            {
                SetHudVisible(false);
            }

            if (initialScreen != null)
            {
                ShowScreen(initialScreen, true);
            }
        }

        public void ShowScreen(UIScreenBase screen, bool clearStack)
        {
            if (screen == null)
            {
                return;
            }

            EnsureParent(screensRoot, screen.transform);

            if (clearStack)
            {
                ClearScreens();
            }
            else
            {
                HideCurrentScreen();
            }

            screenStack.Push(screen);
            screen.SetVisible(true);
            screen.OnEnter();
            screen.OnFocus();
            UpdateInputModeAfterModalChange();
        }

        public void PushScreen(UIScreenBase screen)
        {
            ShowScreen(screen, false);
        }

        public void PopScreen()
        {
            if (screenStack.Count <= 1)
            {
                return;
            }

            var current = screenStack.Pop();
            if (current != null)
            {
                current.OnExit();
                current.SetVisible(false);
            }

            var next = screenStack.Peek();
            if (next != null)
            {
                next.SetVisible(true);
                next.OnFocus();
            }

            UpdateInputModeAfterModalChange();
        }

        public void PushModal(UIModalBase modal)
        {
            if (modal == null)
            {
                return;
            }

            EnsureParent(modalRoot, modal.transform);

            var current = modalStack.Peek();
            if (current != null)
            {
                current.OnBlur();
                current.SetVisible(false);
            }

            modalStack.Push(modal);
            modal.CloseRequested += HandleModalCloseRequested;
            modal.SetVisible(true);
            modal.OnEnter();
            modal.OnFocus();
            TrackPause(modal, true);
            SetInputMode(UIInputMode.UI);
        }

        public void PopModal()
        {
            CloseModal(modalStack.Peek());
        }

        public void CloseModal(UIModalBase modal)
        {
            if (modal == null)
            {
                return;
            }

            var wasTop = modalStack.Peek() == modal;
            if (!modalStack.Remove(modal))
            {
                return;
            }

            modal.CloseRequested -= HandleModalCloseRequested;
            modal.OnExit();
            modal.SetVisible(false);
            TrackPause(modal, false);

            if (wasTop)
            {
                var next = modalStack.Peek();
                if (next != null)
                {
                    next.SetVisible(true);
                    next.OnFocus();
                }
            }

            UpdateInputModeAfterModalChange();
        }

        public void CloseAllModals()
        {
            while (modalStack.Count > 0)
            {
                CloseModal(modalStack.Peek());
            }
        }

        public void SetHudVisible(bool visible)
        {
            if (hudRoot != null)
            {
                hudRoot.gameObject.SetActive(visible);
            }
        }

        public void ShowOverlay(UIOverlayBase overlay)
        {
            if (overlay == null)
            {
                return;
            }

            EnsureParent(overlayRoot, overlay.transform);
            overlay.SetVisible(true);
            overlay.OnShow();
        }

        public void HideOverlay(UIOverlayBase overlay)
        {
            if (overlay == null)
            {
                return;
            }

            overlay.OnHide();
            overlay.SetVisible(false);
        }

        private void HandleModalCloseRequested(UIModalBase modal)
        {
            CloseModal(modal);
        }

        private void UpdateInputModeAfterModalChange()
        {
            if (modalStack.Count > 0)
            {
                SetInputMode(UIInputMode.UI);
                return;
            }

            var screen = screenStack.Peek();
            if (screen != null)
            {
                SetInputMode(screen.InputMode);
            }
        }

        private void SetInputMode(UIInputMode mode)
        {
            if (inputMode == mode)
            {
                return;
            }

            inputMode = mode;
            InputModeChanged?.Invoke(mode);
        }

        private void TrackPause(UIModalBase modal, bool isOpening)
        {
            if (modal == null || !modal.PauseGameplay)
            {
                return;
            }

            pauseCount = Mathf.Max(0, pauseCount + (isOpening ? 1 : -1));
            ApplyPauseState();
        }

        private void ApplyPauseState()
        {
            if (pauseCount > 0)
            {
                if (!Mathf.Approximately(Time.timeScale, 0f))
                {
                    cachedTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }

                return;
            }

            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = Mathf.Max(0f, cachedTimeScale);
            }
        }

        private void HideAllScreens()
        {
            if (screensRoot == null)
            {
                return;
            }

            for (var i = 0; i < screensRoot.childCount; i++)
            {
                var screen = screensRoot.GetChild(i).GetComponent<UIScreenBase>();
                if (screen != null)
                {
                    screen.SetVisible(false);
                }
            }
        }

        private void ClearScreens()
        {
            while (screenStack.Count > 0)
            {
                var oldScreen = screenStack.Pop();
                if (oldScreen != null)
                {
                    oldScreen.OnExit();
                    oldScreen.SetVisible(false);
                }
            }
        }

        private void HideCurrentScreen()
        {
            var current = screenStack.Peek();
            if (current != null)
            {
                current.OnBlur();
                current.SetVisible(false);
            }
        }

        private static void EnsureParent(Transform root, Transform child)
        {
            if (root == null || child == null)
            {
                return;
            }

            if (child.parent != root)
            {
                child.SetParent(root, false);
            }
        }
    }
}
