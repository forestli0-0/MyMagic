using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    [DisallowMultipleComponent]
    public class UIHoverVisualController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private bool autoFindUiManager = true;
        [SerializeField] private AudioSource audioSource;

        [Header("Behavior")]
        [SerializeField] private bool applyOnlyInUiMode = true;
        [SerializeField] private bool enableScaleFeedback = true;
        [SerializeField] private float hoverScaleMultiplier = 1.02f;
        [SerializeField] private float pressScaleMultiplier = 0.97f;
        [SerializeField] [Range(0.06f, 0.2f)] private float scaleAnimationDuration = 0.1f;
        [SerializeField] private bool ignoreBackdropSelectables = true;
        [SerializeField] [Range(0.05f, 1f)] private float maxHoverAreaRatio = 0.3f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] [Range(0f, 1f)] private float hoverVolume = 0.16f;
        [SerializeField] [Range(0f, 1f)] private float clickVolume = 0.26f;
        [SerializeField] [Range(0.85f, 1.2f)] private float minPitch = 0.98f;
        [SerializeField] [Range(0.85f, 1.2f)] private float maxPitch = 1.04f;
        [SerializeField] [Range(0f, 0.2f)] private float minHoverInterval = 0.03f;
        [SerializeField] private bool useProceduralFallback = true;

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
        private PointerEventData pointerEventData;

        private Selectable hoveredSelectable;
        private Graphic hoveredGraphic;
        private RectTransform hoveredRect;
        private Color hoveredBaseColor = Color.white;
        private Color hoveredAppliedColor = Color.white;
        private bool hasHoveredAppliedColor;
        private Vector3 hoveredBaseScale = Vector3.one;
        private bool hasHoveredBaseScale;
        private Vector3 scaleVelocity;

        private AudioClip fallbackHoverClip;
        private AudioClip fallbackClickClip;
        private float nextHoverPlayableTime = float.MinValue;

        private void LateUpdate()
        {
            ResolveReferences();
            if (ShouldSuspendHoverFeedback())
            {
                ClearHoverVisuals();
                return;
            }

            var candidate = ResolveHoveredSelectable();
            if (candidate != hoveredSelectable)
            {
                ClearHoverVisuals();
                ApplyHoverTarget(candidate);
                TryPlayHoverSound();
            }

            UpdateHoverVisuals();
        }

        private void OnEnable()
        {
            EnsureAudioSource();
        }

        private void OnDisable()
        {
            ClearHoverVisuals();
        }

        private void OnDestroy()
        {
            if (fallbackHoverClip != null)
            {
                Destroy(fallbackHoverClip);
                fallbackHoverClip = null;
            }

            if (fallbackClickClip != null)
            {
                Destroy(fallbackClickClip);
                fallbackClickClip = null;
            }
        }

        public void SetUIManager(UIManager manager)
        {
            uiManager = manager;
        }

        private void ResolveReferences()
        {
            if (!autoFindUiManager || uiManager != null)
            {
                return;
            }

            uiManager = GetComponentInParent<UIManager>();
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            EnsureAudioSource();
        }

        private bool ShouldSuspendHoverFeedback()
        {
            if (!UnityEngine.Input.mousePresent)
            {
                return true;
            }

            if (!applyOnlyInUiMode)
            {
                return false;
            }

            return uiManager != null && uiManager.CurrentInputMode != UIInputMode.UI;
        }

        private Selectable ResolveHoveredSelectable()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            pointerEventData = new PointerEventData(eventSystem)
            {
                position = UnityEngine.Input.mousePosition
            };

            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            for (int i = 0; i < raycastResults.Count; i++)
            {
                var result = raycastResults[i];
                var selectable = result.gameObject != null
                    ? result.gameObject.GetComponentInParent<Selectable>()
                    : null;
                if (IsHoverCandidate(selectable))
                {
                    return selectable;
                }
            }

            return null;
        }

        private void ApplyHoverTarget(Selectable selectable)
        {
            hoveredSelectable = selectable;
            if (hoveredSelectable == null)
            {
                return;
            }

            hoveredGraphic = hoveredSelectable.targetGraphic as Graphic;
            if (hoveredGraphic == null)
            {
                hoveredGraphic = hoveredSelectable.GetComponent<Graphic>();
            }

            hoveredRect = hoveredGraphic != null
                ? hoveredGraphic.rectTransform
                : hoveredSelectable.transform as RectTransform;
            if (hoveredRect != null)
            {
                hasHoveredBaseScale = true;
                hoveredBaseScale = hoveredRect.localScale;
            }
            else
            {
                hasHoveredBaseScale = false;
                hoveredBaseScale = Vector3.one;
            }

            scaleVelocity = Vector3.zero;

            hoveredBaseColor = ResolveBaseGraphicColor(hoveredSelectable, hoveredGraphic);
            hoveredAppliedColor = hoveredBaseColor;
            hasHoveredAppliedColor = false;
        }

        private void UpdateHoverVisuals()
        {
            if (hoveredSelectable == null)
            {
                return;
            }

            var isPressed = UnityEngine.Input.GetMouseButton(0);
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                TryPlayClickSound();
            }

            if (hoveredGraphic != null)
            {
                // If another system changed color while hovered, rebase to that new color.
                if (hasHoveredAppliedColor && !ApproximatelyColor(hoveredGraphic.color, hoveredAppliedColor))
                {
                    hoveredBaseColor = hoveredGraphic.color;
                }

                var hoverBoost = Mathf.Clamp01(UIStyleKit.InteractionHoverBoost + 0.14f);
                var pressDepth = Mathf.Clamp01(UIStyleKit.InteractionPressDepth + 0.08f);
                var targetColor = isPressed
                    ? UIStyleKit.Shade(hoveredBaseColor, pressDepth)
                    : UIStyleKit.Lift(hoveredBaseColor, hoverBoost);

                hoveredGraphic.color = targetColor;
                hoveredAppliedColor = targetColor;
                hasHoveredAppliedColor = true;
            }

            if (!enableScaleFeedback ||
                !hasHoveredBaseScale ||
                hoveredRect == null ||
                UIFocusUtility.ShouldSuppressScaleFeedback(hoveredSelectable))
            {
                return;
            }

            var scaleFactor = isPressed
                ? Mathf.Clamp(pressScaleMultiplier, 0.85f, 1f)
                : Mathf.Max(1f, hoverScaleMultiplier);
            var targetScale = hoveredBaseScale * scaleFactor;
            hoveredRect.localScale = Vector3.SmoothDamp(
                hoveredRect.localScale,
                targetScale,
                ref scaleVelocity,
                Mathf.Clamp(scaleAnimationDuration, 0.06f, 0.2f),
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }

        private void ClearHoverVisuals()
        {
            if (hoveredGraphic != null)
            {
                // Revert only if current color still matches the color this controller applied.
                if (!hasHoveredAppliedColor || ApproximatelyColor(hoveredGraphic.color, hoveredAppliedColor))
                {
                    hoveredGraphic.color = hoveredBaseColor;
                }
            }

            if (hasHoveredBaseScale && hoveredRect != null)
            {
                hoveredRect.localScale = hoveredBaseScale;
            }

            scaleVelocity = Vector3.zero;
            hoveredSelectable = null;
            hoveredGraphic = null;
            hoveredRect = null;
            hoveredBaseColor = Color.white;
            hoveredAppliedColor = Color.white;
            hasHoveredAppliedColor = false;
            hoveredBaseScale = Vector3.one;
            hasHoveredBaseScale = false;
        }

        private static Color ResolveBaseGraphicColor(Selectable selectable, Graphic graphic)
        {
            if (graphic != null)
            {
                return graphic.color;
            }

            if (selectable != null)
            {
                return selectable.colors.normalColor;
            }

            return Color.white;
        }

        private bool IsHoverCandidate(Selectable selectable)
        {
            if (!UIFocusUtility.IsFocusable(selectable))
            {
                return false;
            }

            if (!ignoreBackdropSelectables)
            {
                return true;
            }

            return !LooksLikeBackdropSelectable(selectable);
        }

        private bool LooksLikeBackdropSelectable(Selectable selectable)
        {
            if (selectable == null)
            {
                return false;
            }

            var name = selectable.gameObject.name;
            if (!string.IsNullOrEmpty(name) &&
                (name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("overlay", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("mask", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            var rect = selectable.transform as RectTransform;
            if (rect == null)
            {
                var graphic = selectable.targetGraphic as Graphic;
                rect = graphic != null ? graphic.rectTransform : null;
            }

            if (rect == null)
            {
                return false;
            }

            var width = Mathf.Abs(rect.rect.width * rect.lossyScale.x);
            var height = Mathf.Abs(rect.rect.height * rect.lossyScale.y);
            var area = width * height;
            if (area <= 1f)
            {
                return false;
            }

            var screenArea = Mathf.Max(1f, (float)(Screen.width * Screen.height));
            var areaRatio = area / screenArea;
            return areaRatio >= Mathf.Clamp01(maxHoverAreaRatio);
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
        }

        private void TryPlayHoverSound()
        {
            if (hoveredSelectable == null)
            {
                return;
            }

            if (Time.unscaledTime < nextHoverPlayableTime)
            {
                return;
            }

            var clip = ResolveHoverClip();
            if (clip == null || audioSource == null || hoverVolume <= 0f)
            {
                return;
            }

            var safeMinPitch = Mathf.Min(minPitch, maxPitch);
            var safeMaxPitch = Mathf.Max(minPitch, maxPitch);
            audioSource.pitch = UnityEngine.Random.Range(safeMinPitch, safeMaxPitch);
            audioSource.PlayOneShot(clip, hoverVolume);
            nextHoverPlayableTime = Time.unscaledTime + Mathf.Max(0f, minHoverInterval);
        }

        private void TryPlayClickSound()
        {
            if (hoveredSelectable == null)
            {
                return;
            }

            var clip = ResolveClickClip();
            if (clip == null || audioSource == null || clickVolume <= 0f)
            {
                return;
            }

            var safeMinPitch = Mathf.Min(minPitch, maxPitch);
            var safeMaxPitch = Mathf.Max(minPitch, maxPitch);
            audioSource.pitch = UnityEngine.Random.Range(safeMinPitch, safeMaxPitch);
            audioSource.PlayOneShot(clip, clickVolume);
        }

        private AudioClip ResolveHoverClip()
        {
            if (hoverClip != null)
            {
                return hoverClip;
            }

            if (!useProceduralFallback)
            {
                return null;
            }

            if (fallbackHoverClip == null)
            {
                fallbackHoverClip = CreateProceduralUiTone("UI_Hover_Fallback", 0.035f, 980f, 0.12f, 7919u);
            }

            return fallbackHoverClip;
        }

        private AudioClip ResolveClickClip()
        {
            if (clickClip != null)
            {
                return clickClip;
            }

            if (!useProceduralFallback)
            {
                return null;
            }

            if (fallbackClickClip == null)
            {
                fallbackClickClip = CreateProceduralUiTone("UI_Click_Fallback", 0.055f, 720f, 0.18f, 3967u);
            }

            return fallbackClickClip;
        }

        private static AudioClip CreateProceduralUiTone(string clipName, float duration, float frequency, float amplitude, uint seed)
        {
            const int sampleRate = 44100;
            var sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * Mathf.Max(0.02f, duration)));
            var samples = new float[sampleCount];

            var rng = new System.Random((int)seed);
            var phase = (float)(rng.NextDouble() * Mathf.PI * 2f);
            var detune = 1f + ((float)rng.NextDouble() - 0.5f) * 0.08f;
            var toneFrequency = Mathf.Max(100f, frequency * detune);

            for (int i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)(sampleCount - 1);
                var envelope = Mathf.Pow(1f - progress, 2.4f);
                var tone = Mathf.Sin((2f * Mathf.PI * toneFrequency * t) + phase);
                var overtone = 0.45f * Mathf.Sin((2f * Mathf.PI * toneFrequency * 2.2f * t) + phase * 0.7f);
                samples[i] = (tone + overtone) * amplitude * envelope;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static bool ApproximatelyColor(Color a, Color b)
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(a.r - b.r) <= epsilon &&
                   Mathf.Abs(a.g - b.g) <= epsilon &&
                   Mathf.Abs(a.b - b.b) <= epsilon &&
                   Mathf.Abs(a.a - b.a) <= epsilon;
        }
    }
}
