using CombatSystem.Input;
using UnityEngine;

namespace CombatSystem.UI
{
    public class PauseMenuHotkey : MonoBehaviour
    {
        private static int suppressOpenUntilFrame = -1;

        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIModalBase pauseModal;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private bool autoFindInputReader = true;
        [SerializeField] private bool onlyWhenGameplayScreen = true;
        [SerializeField] private float suppressAfterUiPauseSeconds = 0.12f;
        [SerializeField] private float suppressAfterInputModeSwitchSeconds = 0.12f;

        private float lastGameplayModeEnterTime = float.NegativeInfinity;

        private void Reset()
        {
            if (uiManager == null)
            {
                uiManager = GetComponentInParent<UIManager>();
            }

            if (autoFindInputReader && inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }
        }

        private void OnEnable()
        {
            if (uiManager == null)
            {
                uiManager = GetComponentInParent<UIManager>();
                if (uiManager == null)
                {
                    uiManager = FindFirstObjectByType<UIManager>();
                }
            }

            if (autoFindInputReader && inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }

            if (uiManager != null)
            {
                uiManager.InputModeChanged -= HandleInputModeChanged;
                uiManager.InputModeChanged += HandleInputModeChanged;
                if (uiManager.CurrentInputMode == UIInputMode.Gameplay)
                {
                    lastGameplayModeEnterTime = Time.unscaledTime;
                }
            }

            if (inputReader != null)
            {
                inputReader.PausePerformed += HandlePausePerformed;
            }
        }

        private void OnDisable()
        {
            if (uiManager != null)
            {
                uiManager.InputModeChanged -= HandleInputModeChanged;
            }

            if (inputReader != null)
            {
                inputReader.PausePerformed -= HandlePausePerformed;
            }
        }

        private void HandleInputModeChanged(UIInputMode mode)
        {
            if (mode == UIInputMode.Gameplay)
            {
                lastGameplayModeEnterTime = Time.unscaledTime;
            }
        }

        private void HandlePausePerformed()
        {
            if (uiManager == null || pauseModal == null)
            {
                return;
            }

            if (Time.frameCount <= suppressOpenUntilFrame)
            {
                return;
            }

            if (uiManager.CurrentModal == pauseModal)
            {
                uiManager.CloseModal(pauseModal);
                return;
            }

            if (uiManager.ModalCount > 0)
            {
                return;
            }

            if (uiManager.CurrentInputMode != UIInputMode.Gameplay)
            {
                return;
            }

            if (Time.unscaledTime - lastGameplayModeEnterTime <= suppressAfterInputModeSwitchSeconds)
            {
                return;
            }

            if (inputReader != null && Time.unscaledTime - inputReader.LastUiPauseTime <= suppressAfterUiPauseSeconds)
            {
                return;
            }

            // 若按下 ESC 时输入模式已经是 UI（例如正在 Vendor/Inventory/Journal 界面），
            // 则不应顺手打开全局暂停菜单。
            if (inputReader != null && inputReader.LastPauseInputMode != UIInputMode.Gameplay)
            {
                return;
            }

            if (onlyWhenGameplayScreen && uiManager.CurrentScreen != null && uiManager.CurrentScreen.InputMode != UIInputMode.Gameplay)
            {
                return;
            }

            uiManager.PushModal(pauseModal);
        }

        public static void SuppressOpenForFrames(int frameCount = 2)
        {
            var frames = Mathf.Max(1, frameCount);
            suppressOpenUntilFrame = Mathf.Max(suppressOpenUntilFrame, Time.frameCount + frames);
        }
    }
}
