using System;
using System.Collections.Generic;
using CombatSystem.Debugging;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CombatSystem.UI
{
    public class UIManager : MonoBehaviour
    {
        private enum HudVisibilityMode
        {
            Hidden = 0,
            Full = 1,
            SkillBarOnly = 2
        }

        [Header("Defaults")]
        [SerializeField] private UIScreenBase initialScreen;
        [SerializeField] private bool hideAllScreensOnStart = true;
        [SerializeField] private bool hideHudOnStart = true;
        [SerializeField] private bool pauseGameplayOnUiScreens = true;
        [Header("Navigation")]
        [SerializeField] private bool recoverFocusInUiMode = true;
        [Min(0.05f)]
        [SerializeField] private float focusRecoveryInterval = 0.12f;

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
        private float screenCachedTimeScale = 1f;
        private bool pausedByScreen;
        private bool initialized;
        private float nextFocusRecoveryTime;
        private HudVisibilityMode hudVisibilityMode = HudVisibilityMode.Hidden;
        private readonly List<GameObject> hudTemporarilyHiddenObjects = new List<GameObject>(24);

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

        private void Update()
        {
            TryRecoverUiFocus();
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
                // 切换主屏：清空历史栈，避免返回到旧页面
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
            if (modalStack.Count == 0 && screen.InputMode == UIInputMode.UI && screen.FocusDefaultSelectable())
            {
                ScheduleNextFocusRecovery();
            }

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
                if (modalStack.Count == 0 && next.InputMode == UIInputMode.UI && next.FocusDefaultSelectable())
                {
                    ScheduleNextFocusRecovery();
                }
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
            if (modal.FocusDefaultSelectable())
            {
                ScheduleNextFocusRecovery();
            }

            // Modal 计数式暂停，支持多层弹窗叠加
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
                    if (next.FocusDefaultSelectable())
                    {
                        ScheduleNextFocusRecovery();
                    }
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
            ApplyHudVisibilityMode(visible ? HudVisibilityMode.Full : HudVisibilityMode.Hidden);
        }

        public void SetHudSkillBarOnlyVisible(bool visible)
        {
            ApplyHudVisibilityMode(visible ? HudVisibilityMode.SkillBarOnly : HudVisibilityMode.Hidden);
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
                // 有弹窗时强制 UI 输入模式
                SetInputMode(UIInputMode.UI);
                UpdateScreenPauseState();
                if (FocusTopUiDefault())
                {
                    ScheduleNextFocusRecovery();
                }

                return;
            }

            var screen = screenStack.Peek();
            if (screen != null)
            {
                SetInputMode(screen.InputMode);
            }

            UpdateScreenPauseState();
            if (inputMode == UIInputMode.UI && FocusTopUiDefault())
            {
                ScheduleNextFocusRecovery();
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

            // 叠加计数，确保多弹窗关闭时正确恢复
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

        private void UpdateScreenPauseState()
        {
            if (!pauseGameplayOnUiScreens)
            {
                return;
            }

            if (modalStack.Count > 0)
            {
                return;
            }

            var screen = screenStack.Peek();
            var shouldPause = screen != null && screen.InputMode == UIInputMode.UI;

            if (shouldPause)
            {
                if (!pausedByScreen)
                {
                    // 屏幕级暂停与弹窗暂停分开缓存，避免互相覆盖
                    screenCachedTimeScale = Time.timeScale;
                    if (!Mathf.Approximately(Time.timeScale, 0f))
                    {
                        Time.timeScale = 0f;
                    }

                    pausedByScreen = true;
                }

                return;
            }

            if (pausedByScreen)
            {
                Time.timeScale = Mathf.Max(0f, screenCachedTimeScale);
                pausedByScreen = false;
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

        private void ApplyHudVisibilityMode(HudVisibilityMode mode)
        {
            if (hudRoot == null)
            {
                hudVisibilityMode = mode;
                return;
            }

            var hudActive = hudRoot.gameObject.activeSelf;
            var hasTemporaryMask = hudTemporarilyHiddenObjects.Count > 0;
            if (hudVisibilityMode == mode)
            {
                if (mode == HudVisibilityMode.Hidden && !hudActive && !hasTemporaryMask)
                {
                    return;
                }

                if (mode == HudVisibilityMode.Full && hudActive && !hasTemporaryMask)
                {
                    return;
                }

                if (mode == HudVisibilityMode.SkillBarOnly && hudActive && hasTemporaryMask)
                {
                    return;
                }
            }

            RestoreHudTemporarilyHiddenObjects();
            hudVisibilityMode = mode;

            switch (mode)
            {
                case HudVisibilityMode.Hidden:
                    hudRoot.gameObject.SetActive(false);
                    return;
                case HudVisibilityMode.Full:
                    hudRoot.gameObject.SetActive(true);
                    return;
                case HudVisibilityMode.SkillBarOnly:
                    hudRoot.gameObject.SetActive(true);
                    ApplySkillBarOnlyMask();
                    return;
                default:
                    hudRoot.gameObject.SetActive(true);
                    return;
            }
        }

        private void ApplySkillBarOnlyMask()
        {
            if (hudRoot == null)
            {
                return;
            }

            var keepTransforms = CollectSkillBarProtectedTransforms();
            var candidates = new HashSet<GameObject>();

            CollectHudHideCandidates<ValueBarUI>(candidates, keepTransforms);
            CollectHudHideCandidates<BuffBarUI>(candidates, keepTransforms);
            CollectHudHideCandidates<CastBarUI>(candidates, keepTransforms);
            CollectHudHideCandidates<CombatLogUI>(candidates, keepTransforms);
            CollectHudHideCandidates<FloatingTextManager>(candidates, keepTransforms);
            CollectHudHideCandidates<CombatDebugOverlay>(candidates, keepTransforms);
            CollectHudHideCandidates<ProgressionHUDController>(candidates, keepTransforms);
            CollectHudHideCandidates<UnitHealthBarManager>(candidates, keepTransforms);
            CollectHudHideCandidates<QuestTrackerHUD>(candidates, keepTransforms);
            CollectHudHideCandidates<HudToastOverlay>(candidates, keepTransforms);

            foreach (var go in candidates)
            {
                if (go == null || !go.activeSelf)
                {
                    continue;
                }

                hudTemporarilyHiddenObjects.Add(go);
                go.SetActive(false);
            }
        }

        private HashSet<Transform> CollectSkillBarProtectedTransforms()
        {
            var protectedTransforms = new HashSet<Transform>();
            if (hudRoot == null)
            {
                return protectedTransforms;
            }

            var skillBars = hudRoot.GetComponentsInChildren<SkillBarUI>(true);
            for (int i = 0; i < skillBars.Length; i++)
            {
                var bar = skillBars[i];
                if (bar == null)
                {
                    continue;
                }

                var barTransforms = bar.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < barTransforms.Length; j++)
                {
                    var node = barTransforms[j];
                    if (node != null)
                    {
                        protectedTransforms.Add(node);
                    }
                }
            }

            return protectedTransforms;
        }

        private void CollectHudHideCandidates<T>(HashSet<GameObject> candidates, HashSet<Transform> protectedTransforms) where T : Component
        {
            if (hudRoot == null)
            {
                return;
            }

            var components = hudRoot.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var target = component.transform;
                if (IsProtectedTransform(target, protectedTransforms))
                {
                    continue;
                }

                candidates.Add(component.gameObject);
            }
        }

        private static bool IsProtectedTransform(Transform target, HashSet<Transform> protectedTransforms)
        {
            if (target == null || protectedTransforms == null || protectedTransforms.Count == 0)
            {
                return false;
            }

            for (var cursor = target; cursor != null; cursor = cursor.parent)
            {
                if (protectedTransforms.Contains(cursor))
                {
                    return true;
                }
            }

            return false;
        }

        private void RestoreHudTemporarilyHiddenObjects()
        {
            if (hudTemporarilyHiddenObjects.Count == 0)
            {
                return;
            }

            for (int i = 0; i < hudTemporarilyHiddenObjects.Count; i++)
            {
                var go = hudTemporarilyHiddenObjects[i];
                if (go != null)
                {
                    go.SetActive(true);
                }
            }

            hudTemporarilyHiddenObjects.Clear();
        }

        private void TryRecoverUiFocus()
        {
            if (!recoverFocusInUiMode || inputMode != UIInputMode.UI)
            {
                return;
            }

            if (Time.unscaledTime < nextFocusRecoveryTime)
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject != null)
            {
                return;
            }

            FocusTopUiDefault();
            ScheduleNextFocusRecovery();
        }

        private bool FocusTopUiDefault()
        {
            var modal = modalStack.Peek();
            if (modal != null)
            {
                return modal.FocusDefaultSelectable();
            }

            var screen = screenStack.Peek();
            if (screen == null || screen.InputMode != UIInputMode.UI)
            {
                return false;
            }

            return screen.FocusDefaultSelectable();
        }

        private void ScheduleNextFocusRecovery()
        {
            nextFocusRecoveryTime = Time.unscaledTime + Mathf.Max(0.05f, focusRecoveryInterval);
        }
    }
}
