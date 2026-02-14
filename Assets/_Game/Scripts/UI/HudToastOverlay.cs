using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    /// <summary>
    /// 轻量 HUD 提示条：队列播放文本提示，并附带淡入淡出位移动画。
    /// </summary>
    [DisallowMultipleComponent]
    public class HudToastOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform container;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private Text messageText;

        [Header("Animation")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private float fadeInDuration = 0.12f;
        [SerializeField] private float holdDuration = 1.05f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private float moveDistance = 20f;

        [Header("Style")]
        [SerializeField] private int maxQueue = 6;
        [SerializeField] private Color infoColor = new Color(0.93f, 0.96f, 1f, 1f);
        [SerializeField] private Color successColor = new Color(0.7f, 1f, 0.75f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.9f, 0.62f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.62f);

        private readonly Queue<ToastRequest> queue = new Queue<ToastRequest>(8);
        private Coroutine playRoutine;
        private Vector2 restingPosition;

        private struct ToastRequest
        {
            public string Message;
            public Color Color;

            public ToastRequest(string message, Color color)
            {
                Message = message;
                Color = color;
            }
        }

        private void Awake()
        {
            EnsureReferences();
            ResetVisual();
        }

        private void OnEnable()
        {
            if (playRoutine == null && queue.Count > 0)
            {
                playRoutine = StartCoroutine(ProcessQueue());
            }
        }

        private void OnDisable()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
        }

        public void ShowInfo(string message)
        {
            Enqueue(message, infoColor);
        }

        public void ShowSuccess(string message)
        {
            Enqueue(message, successColor);
        }

        public void ShowWarning(string message)
        {
            Enqueue(message, warningColor);
        }

        private void Enqueue(string message, Color color)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            EnsureReferences();
            if (queue.Count >= Mathf.Max(1, maxQueue))
            {
                queue.Dequeue();
            }

            queue.Enqueue(new ToastRequest(message, color));
            if (isActiveAndEnabled && playRoutine == null)
            {
                playRoutine = StartCoroutine(ProcessQueue());
            }
        }

        private IEnumerator ProcessQueue()
        {
            while (queue.Count > 0)
            {
                var request = queue.Dequeue();
                yield return PlaySingle(request);
            }

            ResetVisual();
            playRoutine = null;
        }

        private IEnumerator PlaySingle(ToastRequest request)
        {
            if (messageText != null)
            {
                messageText.text = request.Message;
                messageText.color = request.Color;
            }

            var from = restingPosition + Vector2.down * moveDistance;
            var to = restingPosition;
            SetVisual(from, 0f);

            var fadeIn = Mathf.Max(0.02f, fadeInDuration);
            var elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += GetDeltaTime();
                var t = Mathf.Clamp01(elapsed / fadeIn);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                SetVisual(Vector2.LerpUnclamped(from, to, eased), t);
                yield return null;
            }

            SetVisual(to, 1f);

            var hold = Mathf.Max(0f, holdDuration);
            elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += GetDeltaTime();
                yield return null;
            }

            var fadeOut = Mathf.Max(0.02f, fadeOutDuration);
            elapsed = 0f;
            var exitPos = to + Vector2.up * (moveDistance * 0.45f);
            while (elapsed < fadeOut)
            {
                elapsed += GetDeltaTime();
                var t = Mathf.Clamp01(elapsed / fadeOut);
                SetVisual(Vector2.LerpUnclamped(to, exitPos, t), 1f - t);
                yield return null;
            }

            SetVisual(exitPos, 0f);
        }

        private void EnsureReferences()
        {
            if (container == null)
            {
                container = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.raycastTarget = false;
            background.color = backgroundColor;

            if (messageText == null)
            {
                messageText = GetComponentInChildren<Text>(true);
            }

            if (messageText == null)
            {
                var textObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.SetParent(container, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(18f, 8f);
                textRect.offsetMax = new Vector2(-18f, -8f);

                messageText = textObject.GetComponent<Text>();
                messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                messageText.alignment = TextAnchor.MiddleCenter;
                messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
                messageText.verticalOverflow = VerticalWrapMode.Truncate;
                messageText.raycastTarget = false;
                messageText.fontSize = 20;
            }

            if (container != null)
            {
                restingPosition = container.anchoredPosition;
            }
        }

        private void SetVisual(Vector2 position, float alpha)
        {
            if (container != null)
            {
                container.anchoredPosition = position;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void ResetVisual()
        {
            EnsureReferences();
            SetVisual(restingPosition, 0f);
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
