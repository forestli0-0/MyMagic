using UnityEngine;

namespace CombatSystem.UI
{
    public class UIRoot : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private Canvas screensCanvas;
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Canvas modalCanvas;
        [SerializeField] private Canvas overlayCanvas;

        [Header("Settings")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Manager")]
        [SerializeField] private UIManager uiManager;

        private static UIRoot instance;

        public static UIRoot Instance => instance;
        public Canvas ScreensCanvas => screensCanvas;
        public Canvas HudCanvas => hudCanvas;
        public Canvas ModalCanvas => modalCanvas;
        public Canvas OverlayCanvas => overlayCanvas;
        public UIManager Manager => uiManager;
        public static bool IsGameplayInputAllowed()
        {
            if (instance == null || instance.uiManager == null)
            {
                return true;
            }

            return instance.uiManager.CurrentInputMode == UIInputMode.Gameplay;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (uiManager == null)
            {
                uiManager = GetComponentInChildren<UIManager>();
                if (uiManager == null)
                {
                    uiManager = gameObject.AddComponent<UIManager>();
                }
            }

            uiManager.Initialize(this);
        }
    }
}
