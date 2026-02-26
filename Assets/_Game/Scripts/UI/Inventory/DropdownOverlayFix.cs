using System;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// Keeps runtime dropdown list above gameplay UI layers.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Dropdown))]
    public class DropdownOverlayFix : MonoBehaviour
    {
        [SerializeField] private int overlayOrderOffset = 120;
        [SerializeField] private bool moveListToRootCanvas = true;
        [SerializeField] private Color itemTextColor = new Color(0.95f, 0.97f, 1f, 1f);

        private Dropdown dropdown;
        private Canvas rootCanvas;
        private Font fallbackFont;

        private void Awake()
        {
            dropdown = GetComponent<Dropdown>();
            ResolveRootCanvas();
            ResolveFallbackFont();
        }

        private void OnEnable()
        {
            ResolveRootCanvas();
            ResolveFallbackFont();
        }

        private void LateUpdate()
        {
            EnsureOpenListOnTop();
        }

        private void ResolveRootCanvas()
        {
            if (rootCanvas != null)
            {
                return;
            }

            var currentCanvas = GetComponentInParent<Canvas>();
            if (currentCanvas != null)
            {
                rootCanvas = currentCanvas.rootCanvas;
            }
        }

        private void EnsureOpenListOnTop()
        {
            if (dropdown == null)
            {
                return;
            }

            var listRect = dropdown.transform.Find("Dropdown List") as RectTransform;
            if (listRect == null)
            {
                return;
            }

            ResolveRootCanvas();
            if (moveListToRootCanvas && rootCanvas != null && listRect.parent != rootCanvas.transform)
            {
                listRect.SetParent(rootCanvas.transform, true);
            }

            listRect.SetAsLastSibling();

            var listCanvas = listRect.GetComponent<Canvas>();
            if (listCanvas == null)
            {
                listCanvas = listRect.gameObject.AddComponent<Canvas>();
            }

            listCanvas.overrideSorting = true;
            if (rootCanvas != null)
            {
                listCanvas.sortingLayerID = rootCanvas.sortingLayerID;
                listCanvas.sortingOrder = rootCanvas.sortingOrder + Mathf.Max(10, overlayOrderOffset);
            }
            else
            {
                listCanvas.sortingOrder = Mathf.Max(1000, listCanvas.sortingOrder);
            }

            if (listRect.GetComponent<GraphicRaycaster>() == null)
            {
                listRect.gameObject.AddComponent<GraphicRaycaster>();
            }

            PatchRuntimeOptionLabels(listRect);
        }

        private void ResolveFallbackFont()
        {
            if (fallbackFont != null)
            {
                return;
            }

            if (dropdown != null && dropdown.captionText != null && dropdown.captionText.font != null)
            {
                fallbackFont = dropdown.captionText.font;
                return;
            }

            fallbackFont = ResolveBuiltinFallbackFont();
        }

        private static Font ResolveBuiltinFallbackFont()
        {
            try
            {
                var legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacyFont != null)
                {
                    return legacyFont;
                }
            }
            catch (Exception)
            {
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void PatchRuntimeOptionLabels(RectTransform listRect)
        {
            if (dropdown == null || listRect == null || dropdown.options == null)
            {
                return;
            }

            var content = listRect.Find("Viewport/Content") as RectTransform;
            if (content == null)
            {
                return;
            }

            var optionCount = dropdown.options.Count;
            if (optionCount <= 0)
            {
                return;
            }

            var itemCount = content.childCount;
            for (int i = 0; i < itemCount; i++)
            {
                var item = content.GetChild(i) as RectTransform;
                if (item == null)
                {
                    continue;
                }

                var label = ResolveOrCreateItemLabel(item);
                if (label == null)
                {
                    continue;
                }

                var optionIndex = Mathf.Clamp(i, 0, optionCount - 1);
                var optionData = dropdown.options[optionIndex];
                label.text = optionData != null ? optionData.text : string.Empty;
                label.color = itemTextColor;
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.raycastTarget = false;
                label.fontSize = Mathf.Max(14, label.fontSize);

                if (label.font == null)
                {
                    ResolveFallbackFont();
                    label.font = fallbackFont;
                }

                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(18f, 0f);
                labelRect.offsetMax = new Vector2(-18f, 0f);

                var itemImage = item.GetComponent<Image>();
                if (itemImage != null && itemImage.color.a < 0.45f)
                {
                    itemImage.color = new Color(0.2f, 0.23f, 0.3f, 0.9f);
                }
            }
        }

        private static Text ResolveOrCreateItemLabel(RectTransform item)
        {
            if (item == null)
            {
                return null;
            }

            var labelTransform = item.Find("Item Label");
            if (labelTransform != null)
            {
                return labelTransform.GetComponent<Text>();
            }

            var existingText = item.GetComponentInChildren<Text>(true);
            if (existingText != null)
            {
                return existingText;
            }

            var labelGo = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(item, false);
            return labelGo.GetComponent<Text>();
        }
    }
}
