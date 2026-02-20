using System.Collections.Generic;
using CombatSystem.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        [Header("Discovery")]
        [SerializeField] private bool discoverOnSceneLoad = true;
        [SerializeField] private bool showOnDiscover = true;

        [Header("Hover Target")]
        [SerializeField] private bool enableHoverTargetInfo = true;
        [SerializeField] private bool hoverEnemiesOnly = true;
        [SerializeField] private bool hideHoverWhenPointerOverUI = true;
        [SerializeField] private LayerMask hoverRaycastMask = ~0;
        [SerializeField] private float hoverRaycastDistance = 240f;
        [SerializeField] private TeamComponent playerTeam;

        [Header("Hover Panel")]
        [SerializeField] private Vector2 hoverPanelSize = new Vector2(220f, 70f);
        [SerializeField] private Vector2 hoverPanelTopLeftOffset = new Vector2(20f, -98f);
        [SerializeField] private Color hoverPanelBackgroundColor = new Color(0.02f, 0.08f, 0.16f, 0.92f);
        [SerializeField] private Color hoverPanelOutlineColor = new Color(0.35f, 0.65f, 1f, 0.85f);
        [SerializeField] private Color hoverHealthFillColor = new Color(0.92f, 0.26f, 0.26f, 1f);

        [Header("Hover Ring")]
        [SerializeField] private bool showHoverRing = true;
        [SerializeField] private float hoverRingRadius = 0.82f;
        [SerializeField] private float hoverRingHeight = 0.05f;
        [SerializeField] private float hoverRingWidth = 0.055f;
        [SerializeField] private int hoverRingSegments = 36;
        [SerializeField] private Color hoverRingColor = new Color(1f, 0.92f, 0.32f, 0.9f);

        private readonly Dictionary<HealthComponent, UnitHealthBar> bars = new Dictionary<HealthComponent, UnitHealthBar>(32);
        private readonly List<HealthComponent> releaseQueue = new List<HealthComponent>(8);
        private readonly List<UnitHealthBar> pool = new List<UnitHealthBar>(16);
        private readonly HashSet<HealthComponent> observedHealth = new HashSet<HealthComponent>();

        private HealthComponent currentTarget;
        private HealthComponent hoveredTarget;
        private bool templateBuilt;
        private static Sprite fallbackSprite;
        private RectTransform hoverPanelRect;
        private Text hoverNameText;
        private Text hoverHpText;
        private Image hoverHpFill;
        private LineRenderer hoverRing;
        private Material hoverRingMaterial;

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
            if (discoverOnSceneLoad)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }
            DiscoverUnits();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnsubscribeLocal();
            HideHoverPresentation();
            DestroyHoverRuntime();
            if (discoverOnSceneLoad)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        private void LateUpdate()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                HideHoverPresentation();
                return;
            }

            if (bars.Count > 0)
            {
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

            UpdateHoverTargetPresentation();
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

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DiscoverUnits();
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

            UnregisterLocal(source);
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

        private void DiscoverUnits()
        {
            var healthComponents = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            if (healthComponents == null || healthComponents.Length == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            for (int i = 0; i < healthComponents.Length; i++)
            {
                var health = healthComponents[i];
                if (health == null || !CanShowFor(health))
                {
                    continue;
                }

                RegisterLocal(health);
                var bar = GetOrCreateBar(health);
                if (bar == null)
                {
                    continue;
                }

                bar.Refresh();
                if (showOnDiscover)
                {
                    bar.ShowForDuration(showDuration, now);
                }
            }
        }

        private void UpdateHoverTargetPresentation()
        {
            if (!enableHoverTargetInfo || barsRoot == null || worldCamera == null || !UIRoot.IsGameplayInputAllowed())
            {
                HideHoverPresentation();
                return;
            }

            if (hideHoverWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HideHoverPresentation();
                return;
            }

            if (!TryGetPointerPosition(out var pointerScreen))
            {
                HideHoverPresentation();
                return;
            }

            if (!TryResolveHoveredTarget(pointerScreen, out var health))
            {
                HideHoverPresentation();
                return;
            }

            hoveredTarget = health;
            EnsureHoverPanel();
            RefreshHoverPanel(health);
            UpdateHoverPanelPosition();
            UpdateHoverRing(health);
        }

        private bool TryGetPointerPosition(out Vector2 pointerScreen)
        {
            pointerScreen = default;

            if (Mouse.current != null)
            {
                pointerScreen = Mouse.current.position.ReadValue();
                return true;
            }

            return false;
        }

        private bool TryResolveHoveredTarget(Vector2 pointerScreen, out HealthComponent health)
        {
            health = null;

            var ray = worldCamera.ScreenPointToRay(pointerScreen);
            if (!Physics.Raycast(ray, out var hit, hoverRaycastDistance, hoverRaycastMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            health = hit.collider != null ? hit.collider.GetComponentInParent<HealthComponent>() : null;
            if (health == null || !health.IsAlive || !CanShowFor(health))
            {
                return false;
            }

            if (hoverEnemiesOnly && !IsEnemyTarget(health))
            {
                return false;
            }

            return true;
        }

        private bool IsEnemyTarget(HealthComponent target)
        {
            if (target == null)
            {
                return false;
            }

            if (playerTeam == null)
            {
                var player = PlayerUnitLocator.FindPlayerUnit();
                if (player != null)
                {
                    playerTeam = player.Team != null ? player.Team : player.GetComponent<TeamComponent>();
                }
            }

            var targetTeam = target.GetComponent<TeamComponent>();
            if (playerTeam != null && targetTeam != null)
            {
                return !playerTeam.IsSameTeam(targetTeam);
            }

            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                return !target.CompareTag(playerTag);
            }

            return true;
        }

        private void EnsureHoverPanel()
        {
            if (hoverPanelRect != null)
            {
                return;
            }

            var panelGo = new GameObject("HoverTargetPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelGo.transform.SetParent(barsRoot, false);

            hoverPanelRect = panelGo.GetComponent<RectTransform>();
            hoverPanelRect.anchorMin = new Vector2(0f, 1f);
            hoverPanelRect.anchorMax = new Vector2(0f, 1f);
            hoverPanelRect.pivot = new Vector2(0f, 1f);
            hoverPanelRect.sizeDelta = hoverPanelSize;

            var panelBg = panelGo.GetComponent<Image>();
            panelBg.sprite = GetDefaultSprite();
            panelBg.color = hoverPanelBackgroundColor;
            panelBg.raycastTarget = false;

            var outline = panelGo.GetComponent<Outline>();
            outline.effectColor = hoverPanelOutlineColor;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            hoverNameText = CreateHoverLabel(panelGo.transform, "Name", 15, TextAnchor.MiddleLeft, new Color(0.94f, 0.97f, 1f, 1f));
            var nameRect = hoverNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(10f, -8f);
            nameRect.sizeDelta = new Vector2(-20f, 24f);

            hoverHpText = CreateHoverLabel(panelGo.transform, "HpText", 13, TextAnchor.MiddleRight, new Color(0.9f, 0.95f, 1f, 0.95f));
            var hpTextRect = hoverHpText.rectTransform;
            hpTextRect.anchorMin = new Vector2(0f, 0f);
            hpTextRect.anchorMax = new Vector2(1f, 0f);
            hpTextRect.pivot = new Vector2(1f, 0f);
            hpTextRect.anchoredPosition = new Vector2(-10f, 8f);
            hpTextRect.sizeDelta = new Vector2(-20f, 20f);

            var barBgRect = new GameObject("HealthBarBg", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            barBgRect.SetParent(panelGo.transform, false);
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0f);
            barBgRect.pivot = new Vector2(0.5f, 0f);
            barBgRect.anchoredPosition = new Vector2(0f, 30f);
            barBgRect.sizeDelta = new Vector2(-20f, 14f);

            var barBg = barBgRect.GetComponent<Image>();
            barBg.sprite = GetDefaultSprite();
            barBg.color = new Color(0f, 0f, 0f, 0.5f);
            barBg.raycastTarget = false;

            var fillRect = new GameObject("HealthBarFill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            fillRect.SetParent(barBgRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);

            hoverHpFill = fillRect.GetComponent<Image>();
            hoverHpFill.sprite = GetDefaultSprite();
            hoverHpFill.color = hoverHealthFillColor;
            hoverHpFill.type = Image.Type.Filled;
            hoverHpFill.fillMethod = Image.FillMethod.Horizontal;
            hoverHpFill.fillOrigin = 0;
            hoverHpFill.fillAmount = 1f;
            hoverHpFill.raycastTarget = false;

            panelGo.SetActive(false);
        }

        private Text CreateHoverLabel(Transform parent, string objectName, int fontSize, TextAnchor alignment, Color color)
        {
            var textRect = new GameObject(objectName, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            textRect.SetParent(parent, false);
            var text = textRect.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private void RefreshHoverPanel(HealthComponent health)
        {
            if (hoverPanelRect == null || health == null)
            {
                return;
            }

            var displayName = ResolveDisplayName(health);
            if (hoverNameText != null)
            {
                hoverNameText.text = displayName;
            }

            var current = Mathf.Max(0f, health.Current);
            var max = Mathf.Max(1f, health.Max);

            if (hoverHpText != null)
            {
                hoverHpText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
            }

            if (hoverHpFill != null)
            {
                hoverHpFill.fillAmount = Mathf.Clamp01(current / max);
            }

            if (!hoverPanelRect.gameObject.activeSelf)
            {
                hoverPanelRect.gameObject.SetActive(true);
            }
        }

        private string ResolveDisplayName(HealthComponent health)
        {
            var unit = health != null ? health.GetComponent<UnitRoot>() : null;
            var baseName = unit != null && unit.Definition != null && !string.IsNullOrWhiteSpace(unit.Definition.DisplayName)
                ? unit.Definition.DisplayName
                : health != null ? health.name : "Target";

            var progression = health != null ? health.GetComponent<PlayerProgression>() : null;
            if (progression != null)
            {
                return $"{baseName}  Lv {Mathf.Max(1, progression.Level)}";
            }

            return baseName;
        }

        private void UpdateHoverPanelPosition()
        {
            if (hoverPanelRect == null || barsRoot == null)
            {
                return;
            }

            var panelSize = hoverPanelRect.rect.size;
            var rootRect = barsRoot.rect;
            var minX = rootRect.xMin;
            var maxX = rootRect.xMax - panelSize.x;
            var minY = rootRect.yMin + panelSize.y;
            var maxY = rootRect.yMax;

            var anchored = hoverPanelTopLeftOffset;
            anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
            anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
            hoverPanelRect.anchoredPosition = anchored;
        }

        private void EnsureHoverRing()
        {
            if (hoverRing != null)
            {
                return;
            }

            var ringGo = new GameObject("HoverTargetRing", typeof(LineRenderer));
            hoverRing = ringGo.GetComponent<LineRenderer>();
            hoverRing.useWorldSpace = true;
            hoverRing.loop = true;
            hoverRing.positionCount = 0;
            hoverRing.widthMultiplier = Mathf.Max(0.005f, hoverRingWidth);
            hoverRing.startColor = hoverRingColor;
            hoverRing.endColor = hoverRingColor;
            hoverRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hoverRing.receiveShadows = false;
            hoverRing.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                hoverRingMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                hoverRing.sharedMaterial = hoverRingMaterial;
            }

            hoverRing.enabled = false;
        }

        private void UpdateHoverRing(HealthComponent health)
        {
            if (!showHoverRing || health == null)
            {
                if (hoverRing != null)
                {
                    hoverRing.enabled = false;
                }
                return;
            }

            EnsureHoverRing();
            if (hoverRing == null)
            {
                return;
            }

            var center = health.transform.position;
            var radius = ResolveHoverRingRadius(health);
            var y = center.y + hoverRingHeight;
            var segmentCount = Mathf.Max(12, hoverRingSegments);
            var step = Mathf.PI * 2f / segmentCount;

            hoverRing.positionCount = segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                var angle = step * i;
                var x = center.x + Mathf.Sin(angle) * radius;
                var z = center.z + Mathf.Cos(angle) * radius;
                hoverRing.SetPosition(i, new Vector3(x, y, z));
            }

            hoverRing.enabled = true;
        }

        private float ResolveHoverRingRadius(HealthComponent health)
        {
            var radius = Mathf.Max(0.2f, hoverRingRadius);
            var controller = health != null ? health.GetComponent<CharacterController>() : null;
            if (controller != null)
            {
                return Mathf.Max(radius, controller.radius * 1.18f);
            }

            var capsule = health != null ? health.GetComponent<CapsuleCollider>() : null;
            if (capsule != null)
            {
                return Mathf.Max(radius, capsule.radius * 1.15f);
            }

            return radius;
        }

        private void HideHoverPresentation()
        {
            hoveredTarget = null;

            if (hoverPanelRect != null && hoverPanelRect.gameObject.activeSelf)
            {
                hoverPanelRect.gameObject.SetActive(false);
            }

            if (hoverRing != null && hoverRing.enabled)
            {
                hoverRing.enabled = false;
                hoverRing.positionCount = 0;
            }
        }

        private void DestroyHoverRuntime()
        {
            if (hoverPanelRect != null)
            {
                Destroy(hoverPanelRect.gameObject);
                hoverPanelRect = null;
            }

            if (hoverRing != null)
            {
                Destroy(hoverRing.gameObject);
                hoverRing = null;
            }

            if (hoverRingMaterial != null)
            {
                Destroy(hoverRingMaterial);
                hoverRingMaterial = null;
            }
        }

        private void RegisterLocal(HealthComponent health)
        {
            if (health == null || !observedHealth.Add(health))
            {
                return;
            }

            health.HealthChanged += HandleHealthChanged;
            health.Died += HandleUnitDied;
        }

        private void UnregisterLocal(HealthComponent health)
        {
            if (health == null || !observedHealth.Remove(health))
            {
                return;
            }

            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleUnitDied;
        }

        private void UnsubscribeLocal()
        {
            if (observedHealth.Count == 0)
            {
                return;
            }

            foreach (var health in observedHealth)
            {
                if (health == null)
                {
                    continue;
                }

                health.HealthChanged -= HandleHealthChanged;
                health.Died -= HandleUnitDied;
            }

            observedHealth.Clear();
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
