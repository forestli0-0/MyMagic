using System;
using System.Collections.Generic;
using CombatSystem.Debugging;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CombatSystem.UI
{
    public class UIManager : MonoBehaviour
    {
        private static readonly string[] SkillBarOnlyHideNameHints =
        {
            "CombatLog",
            "DebugOverlay",
            "FloatingText",
            "QuestTracker",
            "ProgressionHUD",
            "HudToast",
            "CastBar",
            "BuffBar",
            "ValueBar",
            "PlayerHealthBar",
            "PlayerResourceBar"
        };

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
        private readonly HashSet<GameObject> hudTemporarilyHiddenLookup = new HashSet<GameObject>();
        private readonly HashSet<GameObject> hudMaskCandidates = new HashSet<GameObject>(64);
        private readonly HashSet<Transform> hudMaskProtectedTransforms = new HashSet<Transform>(64);
        private readonly HashSet<Transform> hudMaskProtectedPath = new HashSet<Transform>(128);

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
            EnforceHudSkillBarOnlyMaskIfNeeded();
            TryRecoverUiFocus();
        }

        private void LateUpdate()
        {
            EnforceHudSkillBarOnlyMaskIfNeeded();
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

                if (mode == HudVisibilityMode.SkillBarOnly)
                {
                    if (!hudActive)
                    {
                        hudRoot.gameObject.SetActive(true);
                    }

                    // 同模式重复调用时做增量压制，避免先恢复再隐藏导致闪烁。
                    ApplySkillBarOnlyMask();
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

            var keepTransforms = CollectSkillBarProtectedTransforms(hudMaskProtectedTransforms);
            var candidates = hudMaskCandidates;
            candidates.Clear();

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
            CollectHudHideCandidatesByHierarchy(candidates, keepTransforms);
            CollectHudHideCandidatesByName(candidates, keepTransforms);

            foreach (var go in candidates)
            {
                if (go == null)
                {
                    continue;
                }

                if (hudTemporarilyHiddenLookup.Add(go))
                {
                    hudTemporarilyHiddenObjects.Add(go);
                }

                if (go.activeSelf)
                {
                    go.SetActive(false);
                }
            }
        }

        private void CollectHudHideCandidatesByName(HashSet<GameObject> candidates, HashSet<Transform> protectedTransforms)
        {
            if (hudRoot == null || SkillBarOnlyHideNameHints == null || SkillBarOnlyHideNameHints.Length == 0)
            {
                return;
            }

            var nodes = hudRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || node == hudRoot || IsProtectedTransform(node, protectedTransforms))
                {
                    continue;
                }

                var name = node.name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                for (int j = 0; j < SkillBarOnlyHideNameHints.Length; j++)
                {
                    var hint = SkillBarOnlyHideNameHints[j];
                    if (string.IsNullOrWhiteSpace(hint))
                    {
                        continue;
                    }

                    if (name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    candidates.Add(node.gameObject);
                    break;
                }
            }
        }

        private void CollectHudHideCandidatesByHierarchy(HashSet<GameObject> candidates, HashSet<Transform> protectedTransforms)
        {
            if (hudRoot == null)
            {
                return;
            }

            BuildProtectedPathSet(protectedTransforms, hudMaskProtectedPath);
            CollectHudHideCandidatesRecursive(hudRoot, candidates, hudMaskProtectedPath);
        }

        private static void BuildProtectedPathSet(HashSet<Transform> protectedTransforms, HashSet<Transform> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            if (protectedTransforms == null || protectedTransforms.Count == 0)
            {
                return;
            }

            foreach (var leaf in protectedTransforms)
            {
                for (var cursor = leaf; cursor != null; cursor = cursor.parent)
                {
                    if (!output.Add(cursor))
                    {
                        continue;
                    }
                }
            }
        }

        private static void CollectHudHideCandidatesRecursive(Transform root, HashSet<GameObject> candidates, HashSet<Transform> protectedPath)
        {
            if (root == null)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (protectedPath != null && protectedPath.Contains(child))
                {
                    CollectHudHideCandidatesRecursive(child, candidates, protectedPath);
                    continue;
                }

                candidates.Add(child.gameObject);
            }
        }

        private HashSet<Transform> CollectSkillBarProtectedTransforms(HashSet<Transform> output)
        {
            if (output == null)
            {
                output = new HashSet<Transform>(64);
            }

            output.Clear();
            if (hudRoot == null)
            {
                return output;
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
                        output.Add(node);
                    }
                }
            }

            return output;
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
            hudTemporarilyHiddenLookup.Clear();
        }

        private void EnforceHudSkillBarOnlyMaskIfNeeded()
        {
            if (hudVisibilityMode != HudVisibilityMode.SkillBarOnly || hudRoot == null || !hudRoot.gameObject.activeInHierarchy)
            {
                return;
            }

            if (IsSkillBarOnlyMaskIntact())
            {
                return;
            }

            // 仅补充压制，不先恢复，避免日志/面板出现可见闪烁。
            ApplySkillBarOnlyMask();
        }

        private bool IsSkillBarOnlyMaskIntact()
        {
            if (hudTemporarilyHiddenObjects == null || hudTemporarilyHiddenObjects.Count == 0)
            {
                return false;
            }

            var hasValidTarget = false;
            var removedNull = false;
            for (int i = hudTemporarilyHiddenObjects.Count - 1; i >= 0; i--)
            {
                var go = hudTemporarilyHiddenObjects[i];
                if (go == null)
                {
                    hudTemporarilyHiddenObjects.RemoveAt(i);
                    removedNull = true;
                    continue;
                }

                hasValidTarget = true;
                if (go.activeSelf)
                {
                    return false;
                }
            }

            if (removedNull)
            {
                RebuildHudHiddenLookup();
            }

            return hasValidTarget;
        }

        private void RebuildHudHiddenLookup()
        {
            hudTemporarilyHiddenLookup.Clear();
            for (int i = 0; i < hudTemporarilyHiddenObjects.Count; i++)
            {
                var go = hudTemporarilyHiddenObjects[i];
                if (go != null)
                {
                    hudTemporarilyHiddenLookup.Add(go);
                }
            }
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
