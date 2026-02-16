using CombatSystem.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Gameplay
{
    public class VendorTrigger : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private UIManager uiManager;
        [SerializeField] private VendorScreen vendorScreen;
        [SerializeField] private bool autoOpenOnEnter;
        [SerializeField] private bool allowInteractKeyOpen = true;
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private bool closeOnExit;

        private bool playerInRange;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = true;
            if (autoOpenOnEnter)
            {
                OpenVendor();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = false;
            if (closeOnExit)
            {
                CloseVendor();
            }
        }

        private void Update()
        {
            if (!allowInteractKeyOpen || !playerInRange)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var keyControl = keyboard[interactKey];
            if (keyControl != null && keyControl.wasPressedThisFrame)
            {
                TryOpenVendor();
            }
        }

        public bool TryOpenVendor()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (vendorScreen == null)
            {
                vendorScreen = FindFirstObjectByType<VendorScreen>(FindObjectsInactive.Include);
            }

            if (uiManager == null || vendorScreen == null)
            {
                return false;
            }

            if (uiManager.CurrentScreen == vendorScreen)
            {
                return true;
            }

            uiManager.PushScreen(vendorScreen);
            return true;
        }

        private bool IsPlayer(Component other)
        {
            if (other == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(playerTag))
            {
                return true;
            }

            return other.CompareTag(playerTag);
        }

        private void OpenVendor()
        {
            TryOpenVendor();
        }

        private void CloseVendor()
        {
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (vendorScreen == null)
            {
                vendorScreen = FindFirstObjectByType<VendorScreen>();
            }

            if (uiManager == null || vendorScreen == null)
            {
                return;
            }

            if (uiManager.CurrentScreen == vendorScreen)
            {
                uiManager.PopScreen();
            }
        }
    }
}
