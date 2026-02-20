using System.Collections.Generic;
using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 命中闪烁反馈：监听伤害事件并对目标渲染器做短时高亮。
    /// </summary>
    public class HitFlashReceiver : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("References")]
        [SerializeField] private CombatEventHub eventHub;
        [SerializeField] private HealthComponent targetHealth;
        [SerializeField] private Renderer[] targetRenderers;

        [Header("Flash")]
        [SerializeField] private Color flashColor = new Color(1f, 0.95f, 0.85f, 1f);
        [SerializeField] [Range(0f, 1f)] private float flashStrength = 0.6f;
        [SerializeField] private float flashDuration = 0.09f;
        [SerializeField] private bool onlyWhenHealthDamage = true;

        private MaterialPropertyBlock block;
        private RendererState[] rendererStates = System.Array.Empty<RendererState>();
        private float flashRemaining;
        private bool subscribed;

        private void Awake()
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            EnsureReferences();
            BuildRendererStateCache();
            ApplyFlash(0f);
        }

        private void OnEnable()
        {
            RefreshRendererCache();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            flashRemaining = 0f;
            ApplyFlash(0f);
        }

        private void Update()
        {
            if (flashRemaining <= 0f || rendererStates.Length == 0)
            {
                return;
            }

            flashRemaining -= Time.deltaTime;
            var normalized = flashDuration > 0f ? Mathf.Clamp01(flashRemaining / flashDuration) : 0f;
            ApplyFlash(normalized);
            if (flashRemaining <= 0f)
            {
                ApplyFlash(0f);
            }
        }

        private void EnsureReferences()
        {
            if (targetHealth == null)
            {
                targetHealth = GetComponent<HealthComponent>();
            }

            if (eventHub == null)
            {
                var unit = GetComponent<UnitRoot>();
                if (unit != null && unit.EventHub != null)
                {
                    eventHub = unit.EventHub;
                }
                else if (targetHealth != null)
                {
                    var parentUnit = targetHealth.GetComponent<UnitRoot>();
                    if (parentUnit != null)
                    {
                        eventHub = parentUnit.EventHub;
                    }
                }
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void Subscribe()
        {
            if (subscribed || eventHub == null)
            {
                return;
            }

            eventHub.DamageApplied += HandleDamageApplied;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || eventHub == null)
            {
                return;
            }

            eventHub.DamageApplied -= HandleDamageApplied;
            subscribed = false;
        }

        private void HandleDamageApplied(DamageAppliedEvent evt)
        {
            if (targetHealth == null || evt.Target != targetHealth)
            {
                return;
            }

            if (onlyWhenHealthDamage && evt.AppliedDamage <= 0f)
            {
                return;
            }

            flashRemaining = Mathf.Max(flashRemaining, flashDuration);
            ApplyFlash(1f);
        }

        private void BuildRendererStateCache()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                rendererStates = System.Array.Empty<RendererState>();
                return;
            }

            var states = new List<RendererState>(targetRenderers.Length);
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var material = renderer.sharedMaterial;
                if (material == null)
                {
                    continue;
                }

                var colorPropertyId = -1;
                if (material.HasProperty(BaseColorId))
                {
                    colorPropertyId = BaseColorId;
                }
                else if (material.HasProperty(ColorId))
                {
                    colorPropertyId = ColorId;
                }

                if (colorPropertyId < 0)
                {
                    continue;
                }

                var baseColor = material.GetColor(colorPropertyId);
                states.Add(new RendererState(renderer, colorPropertyId, baseColor));
            }

            rendererStates = states.ToArray();
        }

        /// <summary>
        /// 重新解析目标渲染器并重建闪烁缓存。
        /// 供运行时替换模型后主动刷新调用。
        /// </summary>
        public void RefreshRendererCache()
        {
            EnsureReferences();
            BuildRendererStateCache();
            ApplyFlash(0f);
        }

        private void ApplyFlash(float normalized)
        {
            if (rendererStates.Length == 0)
            {
                return;
            }

            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            var strength = Mathf.Clamp01(normalized) * Mathf.Clamp01(flashStrength);
            for (int i = 0; i < rendererStates.Length; i++)
            {
                var state = rendererStates[i];
                if (state.Renderer == null || state.ColorPropertyId < 0)
                {
                    continue;
                }

                var color = Color.Lerp(state.BaseColor, flashColor, strength);
                state.Renderer.GetPropertyBlock(block);
                block.SetColor(state.ColorPropertyId, color);
                state.Renderer.SetPropertyBlock(block);
                block.Clear();
            }
        }

        private readonly struct RendererState
        {
            public readonly Renderer Renderer;
            public readonly int ColorPropertyId;
            public readonly Color BaseColor;

            public RendererState(Renderer renderer, int colorPropertyId, Color baseColor)
            {
                Renderer = renderer;
                ColorPropertyId = colorPropertyId;
                BaseColor = baseColor;
            }
        }
    }
}
