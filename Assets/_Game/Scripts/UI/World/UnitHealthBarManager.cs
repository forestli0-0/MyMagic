using System.Collections.Generic;
using CombatSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class UnitHealthBarManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatEventHub eventHub;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform barsRoot;
        [SerializeField] private UnitHealthBar barTemplate;

        [Header("Display")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private Vector2 screenOffset = new Vector2(0f, 24f);
        [SerializeField] private float showDuration = 5f;
        [SerializeField] private bool showPlayer = false;
        [SerializeField] private string playerTag = "Player";

        private readonly Dictionary<HealthComponent, UnitHealthBar> bars = new Dictionary<HealthComponent, UnitHealthBar>(32);
        private readonly List<HealthComponent> releaseQueue = new List<HealthComponent>(8);
        private readonly List<UnitHealthBar> pool = new List<UnitHealthBar>(16);

        private HealthComponent currentTarget;
        private bool templateBuilt;
        private static Sprite fallbackSprite;

        private void Awake()
        {
            if (barsRoot == null)
            {
                barsRoot = transform as RectTransform;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            ResolveEventHub();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (bars.Count == 0)
            {
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    return;
                }
            }

            var now = Time.unscaledTime;

            foreach (var pair in bars)
            {
                var bar = pair.Value;
                if (bar == null)
                {
                    releaseQueue.Add(pair.Key);
                    continue;
                }

                if (bar.ShouldHide(now))
                {
                    releaseQueue.Add(pair.Key);
                    continue;
                }

                UpdateBarPosition(bar);
            }

            if (releaseQueue.Count > 0)
            {
                for (var i = 0; i < releaseQueue.Count; i++)
                {
                    if (bars.TryGetValue(releaseQueue[i], out var bar))
                    {
                        ReleaseBar(releaseQueue[i], bar);
                    }
                }

                releaseQueue.Clear();
            }
        }

        private void ResolveEventHub()
        {
            if (eventHub != null)
            {
                return;
            }

            var unit = FindFirstObjectByType<UnitRoot>();
            if (unit != null)
            {
                eventHub = unit.EventHub;
            }
        }

        private void Subscribe()
        {
            if (eventHub == null)
            {
                return;
            }

            eventHub.HealthChanged += HandleHealthChanged;
            eventHub.UnitDied += HandleUnitDied;
            eventHub.TargetChanged += HandleTargetChanged;
            eventHub.TargetCleared += HandleTargetCleared;
        }

        private void Unsubscribe()
        {
            if (eventHub == null)
            {
                return;
            }

            eventHub.HealthChanged -= HandleHealthChanged;
            eventHub.UnitDied -= HandleUnitDied;
            eventHub.TargetChanged -= HandleTargetChanged;
            eventHub.TargetCleared -= HandleTargetCleared;
        }

        private void HandleHealthChanged(HealthChangedEvent evt)
        {
            if (evt.Source == null || !CanShowFor(evt.Source))
            {
                return;
            }

            var bar = GetOrCreateBar(evt.Source);
            if (bar == null)
            {
                return;
            }

            bar.Refresh();
            if (evt.Source == currentTarget)
            {
                bar.SetPinned(true);
                bar.ShowIndefinite();
            }
            else if (evt.Delta < 0f)
            {
                bar.ShowForDuration(showDuration, Time.unscaledTime);
            }
        }

        private void HandleUnitDied(HealthComponent source)
        {
            if (source == null)
            {
                return;
            }

            if (currentTarget == source)
            {
                currentTarget = null;
            }

            if (bars.TryGetValue(source, out var bar))
            {
                ReleaseBar(source, bar);
            }
        }

        private void HandleTargetChanged(HealthComponent target)
        {
            if (target == null || !CanShowFor(target))
            {
                HandleTargetCleared();
                return;
            }

            if (currentTarget == target)
            {
                return;
            }

            if (currentTarget != null && bars.TryGetValue(currentTarget, out var previous))
            {
                previous.SetPinned(false);
                previous.ShowForDuration(showDuration, Time.unscaledTime);
            }

            currentTarget = target;

            var bar = GetOrCreateBar(target);
            if (bar != null)
            {
                bar.Refresh();
                bar.SetPinned(true);
                bar.ShowIndefinite();
            }
        }

        private void HandleTargetCleared()
        {
            if (currentTarget == null)
            {
                return;
            }

            if (bars.TryGetValue(currentTarget, out var bar))
            {
                bar.SetPinned(false);
                bar.ShowForDuration(showDuration, Time.unscaledTime);
            }

            currentTarget = null;
        }

        /// <summary>
        /// 获取或创建指定单位的血条实例。
        /// 如果已存在对应血条则直接返回，否则从对象池获取或新建。
        /// </summary>
        /// <param name="health">目标单位的生命组件</param>
        /// <returns>血条实例，如果无法创建则返回 null</returns>
        private UnitHealthBar GetOrCreateBar(HealthComponent health)
        {
            if (health == null)
            {
                return null;
            }

            if (bars.TryGetValue(health, out var existing))
            {
                return existing;
            }

            EnsureTemplate();
            if (barTemplate == null)
            {
                return null;
            }

            var instance = GetFromPool();
            if (instance == null)
            {
                return null;
            }

            var unitRoot = health.GetComponent<UnitRoot>();
            instance.Bind(health, unitRoot, worldOffset);
            bars.Add(health, instance);
            return instance;
        }

        /// <summary>
        /// 从对象池获取一个可用的血条实例。
        /// 如果池为空则会创建新实例。
        /// </summary>
        /// <returns>可用的血条实例</returns>
        private UnitHealthBar GetFromPool()
        {
            UnitHealthBar bar = null;
            if (pool.Count > 0)
            {
                var index = pool.Count - 1;
                bar = pool[index];
                pool.RemoveAt(index);
            }

            if (bar == null)
            {
                bar = Instantiate(barTemplate, barsRoot != null ? barsRoot : transform);
            }

            bar.gameObject.SetActive(true);
            bar.SetPinned(false);
            return bar;
        }

        /// <summary>
        /// 释放血条实例回对象池。
        /// 从活跃字典中移除并隐藏血条，然后放回对象池以供复用。
        /// </summary>
        /// <param name="health">关联的生命组件</param>
        /// <param name="bar">要释放的血条实例</param>
        private void ReleaseBar(HealthComponent health, UnitHealthBar bar)
        {
            if (health != null)
            {
                bars.Remove(health);
            }

            if (bar == null)
            {
                return;
            }

            bar.SetPinned(false);
            bar.SetVisible(false);
            pool.Add(bar);
        }

        /// <summary>
        /// 更新血条的屏幕位置。
        /// 将世界坐标转换为屏幕坐标，并应用偏移量。
        /// </summary>
        /// <param name="bar">要更新位置的血条</param>
        private void UpdateBarPosition(UnitHealthBar bar)
        {
            if (barsRoot == null || bar == null)
            {
                return;
            }

            var worldPos = bar.GetWorldPosition();
            var screenPoint = worldCamera.WorldToScreenPoint(worldPos);

            if (screenPoint.z <= 0f)
            {
                bar.SetVisible(false);
                return;
            }

            if (!bar.IsVisible)
            {
                bar.SetVisible(true);
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(barsRoot, screenPoint + (Vector3)screenOffset, null, out var localPoint))
            {
                bar.SetScreenPosition(localPoint);
            }
        }

        /// <summary>
        /// 检查是否应该为指定单位显示血条。
        /// 根据配置判断是否排除玩家单位。
        /// </summary>
        /// <param name="health">目标生命组件</param>
        /// <returns>如果应该显示血条则返回 true</returns>
        private bool CanShowFor(HealthComponent health)
        {
            if (health == null)
            {
                return false;
            }

            if (!showPlayer && !string.IsNullOrEmpty(playerTag) && health.CompareTag(playerTag))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 确保血条模板已初始化。
        /// 如果未配置模板，将在运行时动态创建一个默认模板。
        /// </summary>
        private void EnsureTemplate()
        {
            if (templateBuilt)
            {
                return;
            }

            templateBuilt = true;

            if (barTemplate != null)
            {
                return;
            }

            if (barsRoot == null)
            {
                barsRoot = transform as RectTransform;
            }

            var templateObject = new GameObject("UnitHealthBarTemplate", typeof(RectTransform));
            templateObject.transform.SetParent(barsRoot, false);
            var rect = (RectTransform)templateObject.transform;
            rect.sizeDelta = new Vector2(140f, 18f);

            var sprite = GetDefaultSprite();
            var background = templateObject.AddComponent<Image>();
            background.sprite = sprite;
            background.color = new Color(0f, 0f, 0f, 0.6f);
            background.raycastTarget = false;

            var fillRect = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            fillRect.SetParent(templateObject.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImage = fillRect.GetComponent<Image>();
            fillImage.sprite = sprite;
            fillImage.color = new Color(0.85f, 0.2f, 0.2f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;

            var valueTextRect = new GameObject("ValueText", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            valueTextRect.SetParent(templateObject.transform, false);
            valueTextRect.anchorMin = Vector2.zero;
            valueTextRect.anchorMax = Vector2.one;
            valueTextRect.offsetMin = Vector2.zero;
            valueTextRect.offsetMax = Vector2.zero;

            var valueText = valueTextRect.GetComponent<Text>();
            valueText.text = string.Empty;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.fontSize = 12;
            valueText.color = Color.white;
            valueText.raycastTarget = false;
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var nameRect = new GameObject("NameText", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            nameRect.SetParent(templateObject.transform, false);
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 6f);
            nameRect.sizeDelta = new Vector2(0f, 18f);

            var nameText = nameRect.GetComponent<Text>();
            nameText.text = string.Empty;
            nameText.alignment = TextAnchor.LowerCenter;
            nameText.fontSize = 12;
            nameText.color = Color.white;
            nameText.raycastTarget = false;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var valueBar = templateObject.AddComponent<ValueBarUI>();
            valueBar.Configure(fillImage, valueText);

            var barComponent = templateObject.AddComponent<UnitHealthBar>();
            barComponent.Configure(valueBar, nameText);

            templateObject.SetActive(false);
            barTemplate = barComponent;
        }

        /// <summary>
        /// 获取默认的白色 Sprite（用于 UI 填充）
        /// </summary>
        private static Sprite GetDefaultSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            // 使用 Unity 内置的白色纹理创建简单的填充 Sprite
            var texture = Texture2D.whiteTexture;
            if (texture != null)
            {
                fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }

            return fallbackSprite;
        }
    }
}
