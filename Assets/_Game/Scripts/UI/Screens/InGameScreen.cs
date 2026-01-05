using UnityEngine;

namespace CombatSystem.UI
{
    public class InGameScreen : UIScreenBase
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;

        private void Reset()
        {
            inputMode = UIInputMode.Gameplay;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        public override void OnEnter()
        {
            if (uiManager != null)
            {
                uiManager.SetHudVisible(true);
            }
        }

        public override void OnExit()
        {
            if (uiManager != null)
            {
                uiManager.SetHudVisible(false);
            }
        }
    }
}
