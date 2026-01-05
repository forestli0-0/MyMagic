using UnityEngine;

namespace CombatSystem.UI
{
    public class PauseMenuHotkey : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIModalBase pauseModal;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
        [SerializeField] private bool onlyWhenGameplayScreen = true;

        private void Reset()
        {
            if (uiManager == null)
            {
                uiManager = GetComponentInParent<UIManager>();
            }
        }

        private void Update()
        {
            if (toggleKey == KeyCode.None || uiManager == null || pauseModal == null)
            {
                return;
            }

            if (!Input.GetKeyDown(toggleKey))
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
