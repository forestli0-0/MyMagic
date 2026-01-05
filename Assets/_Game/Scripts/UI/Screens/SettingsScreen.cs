using UnityEngine;

namespace CombatSystem.UI
{
    public class SettingsScreen : UIScreenBase
    {
        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase backScreen;

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        public void Back()
        {
            if (uiManager != null && backScreen != null)
            {
                uiManager.ShowScreen(backScreen, true);
            }
        }
    }
}
