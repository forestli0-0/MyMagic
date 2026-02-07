using CombatSystem.UI;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    public class VendorTrigger : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private UIManager uiManager;
        [SerializeField] private VendorScreen vendorScreen;
        [SerializeField] private bool closeOnExit;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            OpenVendor();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!closeOnExit || !IsPlayer(other))
            {
                return;
            }

            CloseVendor();
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
                return;
            }

            uiManager.PushScreen(vendorScreen);
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
