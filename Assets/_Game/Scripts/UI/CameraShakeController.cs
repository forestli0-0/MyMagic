using UnityEngine;

namespace CombatSystem.UI
{
    /// <summary>
    /// 轻量相机抖动控制器，使用 trauma 模型实现可叠加反馈。
    /// </summary>
    public class CameraShakeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform targetTransform;

        [Header("Shake")]
        [SerializeField] private bool enforceMinimumPreset = true;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private float traumaDecay = 1.45f;
        [SerializeField] private float noiseFrequency = 24f;
        [SerializeField] private float maxOffset = 0.17f;
        [SerializeField] private float maxRoll = 1.15f;

        private Vector3 originLocalPosition;
        private Quaternion originLocalRotation;
        private float trauma;
        private float noiseSeedX;
        private float noiseSeedY;
        private float noiseSeedR;
        private bool hasOrigin;

        private void Awake()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            ApplyMinimumPresetIfNeeded();
            CaptureOrigin();
            InitializeNoiseSeeds();
        }

        private void OnEnable()
        {
            CaptureOrigin();
        }

        private void OnDisable()
        {
            RestoreTransform();
        }

        /// <summary>
        /// 叠加抖动强度（0~1）。
        /// </summary>
        public void Shake(float amount)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
        }

        private void LateUpdate()
        {
            if (targetTransform == null)
            {
                return;
            }

            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            if (trauma <= 0f)
            {
                CaptureOrigin();
                return;
            }

            var currentTime = (useUnscaledTime ? Time.unscaledTime : Time.time) * noiseFrequency;
            var shakeWeight = trauma;
            var offset = new Vector3(
                (Mathf.PerlinNoise(noiseSeedX, currentTime) - 0.5f) * 2f,
                (Mathf.PerlinNoise(noiseSeedY, currentTime) - 0.5f) * 2f,
                0f) * (maxOffset * shakeWeight);
            var roll = (Mathf.PerlinNoise(noiseSeedR, currentTime) - 0.5f) * 2f * (maxRoll * shakeWeight);

            targetTransform.localPosition = originLocalPosition + offset;
            targetTransform.localRotation = originLocalRotation * Quaternion.Euler(0f, 0f, roll);

            trauma = Mathf.Max(0f, trauma - traumaDecay * deltaTime);
            if (trauma <= 0f)
            {
                RestoreTransform();
            }
        }

        private void CaptureOrigin()
        {
            if (targetTransform == null)
            {
                return;
            }

            originLocalPosition = targetTransform.localPosition;
            originLocalRotation = targetTransform.localRotation;
            hasOrigin = true;
        }

        private void RestoreTransform()
        {
            if (!hasOrigin || targetTransform == null)
            {
                return;
            }

            targetTransform.localPosition = originLocalPosition;
            targetTransform.localRotation = originLocalRotation;
        }

        private void InitializeNoiseSeeds()
        {
            noiseSeedX = Random.Range(1f, 999f);
            noiseSeedY = Random.Range(1001f, 1999f);
            noiseSeedR = Random.Range(2001f, 2999f);
        }

        private void ApplyMinimumPresetIfNeeded()
        {
            if (!enforceMinimumPreset)
            {
                return;
            }

            // 限制在可感知且不晃眼的区间，避免场景里遗留参数过强。
            traumaDecay = Mathf.Clamp(traumaDecay, 0.9f, 1.7f);
            noiseFrequency = Mathf.Clamp(noiseFrequency, 16f, 30f);
            maxOffset = Mathf.Clamp(maxOffset, 0.1f, 0.2f);
            maxRoll = Mathf.Clamp(maxRoll, 0.7f, 1.5f);
        }
    }
}
