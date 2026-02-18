using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.UI
{
    public enum UIHintDeviceFamily
    {
        KeyboardMouse = 0,
        Gamepad = 1
    }

    public static class UIInputPromptFormatter
    {
        public static UIHintDeviceFamily ResolveCurrentDeviceFamily(UIHintDeviceFamily fallback)
        {
            if (HasKeyboardMouseActivityThisFrame())
            {
                return UIHintDeviceFamily.KeyboardMouse;
            }

            if (HasGamepadActivityThisFrame())
            {
                return UIHintDeviceFamily.Gamepad;
            }

            return fallback;
        }

        public static string Format(string hintTemplate, UIHintDeviceFamily deviceFamily)
        {
            if (string.IsNullOrWhiteSpace(hintTemplate))
            {
                return string.Empty;
            }

            var formatted = hintTemplate;
            formatted = ReplaceToken(formatted, "{CONFIRM}", deviceFamily == UIHintDeviceFamily.Gamepad ? "A" : "鼠标左键/Enter");
            formatted = ReplaceToken(formatted, "{BACK}", deviceFamily == UIHintDeviceFamily.Gamepad ? "B" : "ESC");
            formatted = ReplaceToken(formatted, "{PAUSE}", deviceFamily == UIHintDeviceFamily.Gamepad ? "START" : "ESC");
            formatted = ReplaceToken(formatted, "{MENU_TOGGLE}", deviceFamily == UIHintDeviceFamily.Gamepad ? "View" : "TAB");
            formatted = ReplaceToken(formatted, "{MENU_CLOSE}", deviceFamily == UIHintDeviceFamily.Gamepad ? "B" : "TAB");
            formatted = ReplaceToken(formatted, "{TAB_SWITCH}", deviceFamily == UIHintDeviceFamily.Gamepad ? "LB/RB" : "←/→");
            formatted = ReplaceToken(formatted, "{NAV_VERTICAL}", deviceFamily == UIHintDeviceFamily.Gamepad ? "左摇杆/十字键" : "↑↓");
            formatted = ReplaceToken(formatted, "{MOVE}", deviceFamily == UIHintDeviceFamily.Gamepad ? "左摇杆/点按移动" : "WASD/右键");
            formatted = ReplaceToken(formatted, "{INTERACT}", deviceFamily == UIHintDeviceFamily.Gamepad ? "A" : "E");

            return deviceFamily == UIHintDeviceFamily.Gamepad
                ? ApplyLegacyGamepadCompatibility(formatted)
                : formatted;
        }

        private static string ReplaceToken(string source, string token, string replacement)
        {
            return source.Contains(token)
                ? source.Replace(token, replacement)
                : source;
        }

        private static string ApplyLegacyGamepadCompatibility(string source)
        {
            // 兼容旧文案，避免未迁移页面仍显示键鼠提示。
            var value = source;
            value = value.Replace("鼠标左键 / Enter", "A");
            value = value.Replace("鼠标左键/Enter", "A");
            value = value.Replace("鼠标左键", "A");
            value = value.Replace("Enter", "A");
            value = value.Replace("TAB", "View");
            value = value.Replace("ESC", "B");
            value = value.Replace("←/→", "LB/RB");
            value = value.Replace("↑↓", "左摇杆/十字键");
            value = value.Replace("WASD/右键", "左摇杆/点按移动");
            value = value.Replace("E ", "A ");
            return value;
        }

        private static bool HasKeyboardMouseActivityThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
            {
                return true;
            }

            if (mouse.scroll.ReadValue().sqrMagnitude > 0.001f)
            {
                return true;
            }

            return mouse.delta.ReadValue().sqrMagnitude > 0.001f;
        }

        private static bool HasGamepadActivityThisFrame()
        {
            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return false;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.buttonWest.wasPressedThisFrame ||
                gamepad.buttonNorth.wasPressedThisFrame ||
                gamepad.leftShoulder.wasPressedThisFrame ||
                gamepad.rightShoulder.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame ||
                gamepad.selectButton.wasPressedThisFrame ||
                gamepad.dpad.up.wasPressedThisFrame ||
                gamepad.dpad.down.wasPressedThisFrame ||
                gamepad.dpad.left.wasPressedThisFrame ||
                gamepad.dpad.right.wasPressedThisFrame)
            {
                return true;
            }

            if (gamepad.leftTrigger.ReadValue() > 0.15f || gamepad.rightTrigger.ReadValue() > 0.15f)
            {
                return true;
            }

            return gamepad.leftStick.ReadValue().sqrMagnitude > 0.01f ||
                   gamepad.rightStick.ReadValue().sqrMagnitude > 0.01f;
        }
    }
}
