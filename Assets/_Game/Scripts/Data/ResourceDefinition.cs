using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 资源配置定义，既可描述主资源，也可描述专属条。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Resources/Resource Definition", fileName = "Resource_")]
    public class ResourceDefinition : DefinitionBase
    {
        [Header("表现")]
        [SerializeField] private Sprite icon;
        [SerializeField] private bool showInHud = true;
        [SerializeField] private int hudPriority;
        [SerializeField] private Color hudColor = Color.white;

        [Header("兼容")]
        [SerializeField] private bool useLegacyType;
        [SerializeField] private ResourceType legacyType = ResourceType.Mana;

        [Header("数值")]
        [SerializeField] private StatDefinition maxResourceStat;
        [SerializeField] private StatDefinition regenStat;
        [SerializeField] private float baseMaxResource = 100f;
        [SerializeField] private bool initializeToMax;
        [SerializeField] private bool clampToMax = true;

        public Sprite Icon => icon;
        public bool ShowInHud => showInHud;
        public int HudPriority => hudPriority;
        public Color HudColor => hudColor;
        public bool UseLegacyType => useLegacyType;
        public ResourceType LegacyType => legacyType;
        public StatDefinition MaxResourceStat => maxResourceStat;
        public StatDefinition RegenStat => regenStat;
        public float BaseMaxResource => Mathf.Max(0f, baseMaxResource);
        public bool InitializeToMax => initializeToMax;
        public bool ClampToMax => clampToMax;
    }
}
