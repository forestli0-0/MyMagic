using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// 全局 Toast 入口：统一弹出提示，不依赖具体页面实现。
    /// </summary>
    public static class UIToast
    {
        public static void Info(string message)
        {
            var overlay = ResolveOverlay();
            if (overlay != null)
            {
                overlay.ShowInfo(message);
            }
        }

        public static void Success(string message)
        {
            var overlay = ResolveOverlay();
            if (overlay != null)
            {
                overlay.ShowSuccess(message);
            }
        }

        public static void Warning(string message)
        {
            var overlay = ResolveOverlay();
            if (overlay != null)
            {
                overlay.ShowWarning(message);
            }
        }

        private static HudToastOverlay ResolveOverlay()
        {
            var overlay = Object.FindFirstObjectByType<HudToastOverlay>(FindObjectsInactive.Include);
            if (overlay != null)
            {
                return overlay;
            }

            var root = UIRoot.Instance != null
                ? UIRoot.Instance
                : Object.FindFirstObjectByType<UIRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                return null;
            }

            var parent = root.OverlayCanvas != null
                ? root.OverlayCanvas.transform
                : (root.HudCanvas != null ? root.HudCanvas.transform : null);
            if (parent == null)
            {
                return null;
            }

            var go = new GameObject("GlobalToastOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(HudToastOverlay));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -86f);
            rect.sizeDelta = new Vector2(560f, 58f);

            return go.GetComponent<HudToastOverlay>();
        }
    }
}
