using CombatSystem.Input;
using UnityEngine;

namespace CombatSystem.UI
{
    public class PauseMenuHotkey : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIModalBase pauseModal;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private bool autoFindInputReader = true;
        [SerializeField] private bool onlyWhenGameplayScreen = true;

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
            if (autoFindInputReader && inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }

            if (inputReader != null)
            {
                inputReader.PausePerformed += HandlePausePerformed;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.PausePerformed -= HandlePausePerformed;
            }
        }

        private void HandlePausePerformed()
        {
            if (uiManager == null || pauseModal == null)
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

            if (onlyWhenGameplayScreen && uiManager.CurrentScreen != null && uiManager.CurrentScreen.InputMode != UIInputMode.Gameplay)
            {
                return;
            }

            uiManager.PushModal(pauseModal);
        }
    }
}
