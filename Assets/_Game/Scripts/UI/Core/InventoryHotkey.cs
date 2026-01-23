using CombatSystem.Input;
using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// 库存热键控制器，响应 I 键开关库存界面。
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 订阅 InputReader.InventoryPerformed 事件
    /// - 通过 UIManager 切换库存界面
    /// - 支持仅在 Gameplay 模式下响应
    /// </remarks>
    public class InventoryHotkey : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase inventoryScreen;
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
                inputReader.InventoryPerformed += HandleInventoryPerformed;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.InventoryPerformed -= HandleInventoryPerformed;
            }
        }

        private void HandleInventoryPerformed()
        {
            if (uiManager == null || inventoryScreen == null)
            {
                return;
            }

            if (uiManager.CurrentScreen == inventoryScreen)
            {
                uiManager.PopScreen();
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

            uiManager.PushScreen(inventoryScreen);
        }
    }
}
