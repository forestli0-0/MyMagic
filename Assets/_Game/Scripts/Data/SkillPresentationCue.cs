using System;
using UnityEngine;

namespace CombatSystem.Data
{
    public enum PresentationEventType
    {
        StepExecuted = 0,
        EffectBeforeApply = 1,
        EffectAfterApply = 2,
        ProjectileSpawn = 3,
        ProjectileHit = 4,
        ProjectileReturn = 5,
        ProjectileSplit = 6
    }

    public enum PresentationAnchorType
    {
        Caster = 0,
        PrimaryTarget = 1,
        ExplicitTarget = 2,
        AimPoint = 3,
        Projectile = 4,
        World = 5
    }

    public enum PresentationSpawnSpace
    {
        World = 0,
        LocalToAnchor = 1
    }

    public enum AudioBusType
    {
        Sfx = 0,
        Voice = 1,
        Ui = 2,
        Ambient = 3
    }

    [Serializable]
    public class SkillPresentationCue
    {
        [Tooltip("可选：用于查找和审计的 Cue 标识。")]
        public string cueId;

        [Tooltip("触发此 Cue 的事件类型。")]
        public PresentationEventType eventType = PresentationEventType.StepExecuted;

        [Tooltip("生成位置锚点。")]
        public PresentationAnchorType anchorType = PresentationAnchorType.Caster;

        [Tooltip("生成空间。")]
        public PresentationSpawnSpace spawnSpace = PresentationSpawnSpace.World;

        [Tooltip("若 anchor 上存在子节点路径，可用于挂点查找（例如 FX/Muzzle）。")]
        public string anchorChildPath;

        [Tooltip("相对锚点的位置偏移。")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("额外欧拉角偏移。")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("仅在效果事件中生效：限制到指定 Effect 类型。")]
        public bool filterByEffectType;

        [Tooltip("效果过滤类型（filterByEffectType=true 时生效）。")]
        public EffectType effectTypeFilter = EffectType.Damage;

        [Tooltip("仅在效果事件中生效：限制到指定 Effect 资产。")]
        public EffectDefinition effectFilter;

        [Header("Animation")]
        [Tooltip("可选：触发施法者 Animator Trigger。")]
        public string animationTrigger;

        [Header("Visual")]
        [Tooltip("可选：要播放的 VFX 预制体。")]
        public GameObject vfxPrefab;

        [Tooltip("VFX 是否跟随锚点。")]
        public bool followAnchor;

        [Tooltip("VFX 最长生命周期，超时自动回收。")]
        public float maxLifetime = 2f;

        [Header("Audio")]
        [Tooltip("可选：要播放的音效。")]
        public AudioClip sfx;

        [Tooltip("音频总线标签（MVP 仅存储，不做混音路由）。")]
        public AudioBusType audioBus = AudioBusType.Sfx;

        [Range(0f, 1f)]
        public float audioVolume = 1f;

        [Range(0.1f, 3f)]
        public float audioPitch = 1f;

        [Range(0f, 1f)]
        public float audioSpatialBlend = 1f;

        [Tooltip("World 锚点时使用的世界坐标。")]
        public Vector3 worldPosition = Vector3.zero;

        public bool HasPayload =>
            !string.IsNullOrWhiteSpace(animationTrigger)
            || vfxPrefab != null
            || sfx != null;
    }
}
