using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 单位视觉配置：用于将逻辑单位与模型/动画表现解耦。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Units/Unit Visual Profile", fileName = "UnitVisualProfile_")]
    public class UnitVisualProfile : ScriptableObject
    {
        [Header("Model")]
        [Tooltip("运行时实例化到 VisualRoot 下的模型预制体。为空时沿用当前预制体自身渲染。")]
        [SerializeField] private GameObject modelPrefab;
        [Tooltip("模型在 VisualRoot 下的本地位置偏移。")]
        [SerializeField] private Vector3 localPosition = Vector3.zero;
        [Tooltip("模型在 VisualRoot 下的本地旋转（欧拉角）。")]
        [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
        [Tooltip("模型在 VisualRoot 下的本地缩放。")]
        [SerializeField] private Vector3 localScale = Vector3.one;
        [Tooltip("当存在 modelPrefab 实例时，是否隐藏单位根节点上的旧渲染器（胶囊体等）。")]
        [SerializeField] private bool hideRootRenderersWhenModelActive = true;

        [Header("Animator")]
        [Tooltip("可选：覆盖模型上的 AnimatorController。")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [Tooltip("可选：覆盖模型 Animator 的 Avatar。")]
        [SerializeField] private Avatar avatarOverride;
        [Tooltip("是否启用 Root Motion（大多数顶视角战斗建议关闭）。")]
        [SerializeField] private bool applyRootMotion;

        [Header("Animation Parameters")]
        [SerializeField] private string moveSpeedFloat = "MoveSpeed";
        [SerializeField] private string movingBool = "IsMoving";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string castTrigger = "Cast";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string dieTrigger = "Die";
        [SerializeField] private string deadBool = "IsDead";
        [SerializeField] private string castingBool = "IsCasting";

        [Header("Behavior")]
        [Tooltip("基础攻击技能使用 Attack Trigger，其他技能使用 Cast Trigger。")]
        [SerializeField] private bool useAttackTriggerForBasicAttack = true;
        [Tooltip("位移速度写入 Animator 前的平滑速度。")]
        [SerializeField] [Range(1f, 30f)] private float moveSpeedSmoothing = 12f;
        [Tooltip("判定移动状态的速度阈值（m/s）。")]
        [SerializeField] [Range(0.01f, 1f)] private float movingThreshold = 0.08f;

        public GameObject ModelPrefab => modelPrefab;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Vector3 LocalScale => localScale;
        public bool HideRootRenderersWhenModelActive => hideRootRenderersWhenModelActive;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public Avatar AvatarOverride => avatarOverride;
        public bool ApplyRootMotion => applyRootMotion;
        public string MoveSpeedFloat => moveSpeedFloat;
        public string MovingBool => movingBool;
        public string AttackTrigger => attackTrigger;
        public string CastTrigger => castTrigger;
        public string HitTrigger => hitTrigger;
        public string DieTrigger => dieTrigger;
        public string DeadBool => deadBool;
        public string CastingBool => castingBool;
        public bool UseAttackTriggerForBasicAttack => useAttackTriggerForBasicAttack;
        public float MoveSpeedSmoothing => moveSpeedSmoothing;
        public float MovingThreshold => movingThreshold;
    }
}
