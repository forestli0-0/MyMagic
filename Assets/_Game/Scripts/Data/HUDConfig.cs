using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// HUD UI 配置定义，用于配置界面显示上限e功能开关。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/UI/HUD Config", fileName = "HUDConfig")]
    public class HUDConfig : DefinitionBase
    {
        [Header("布局配额")]
        [Tooltip("技能栏最大槽位数")]
        [SerializeField] private int maxSkillSlots = 6;
        [Tooltip("Buff 列表最大显示数")]
        [SerializeField] private int maxBuffSlots = 12;
        
        [Header("开关控制")]
        [Tooltip("是否显示施法条")]
        [SerializeField] private bool showCastBar = true;
        [Tooltip("是否显示右下角战斗日志")]
        [SerializeField] private bool showCombatLog = true;
        [Tooltip("是否在场景中显示伤害飘字")]
        [SerializeField] private bool showFloatingText = true;

        public int MaxSkillSlots => maxSkillSlots;
        public int MaxBuffSlots => maxBuffSlots;
        public bool ShowCastBar => showCastBar;
        public bool ShowCombatLog => showCombatLog;
        public bool ShowFloatingText => showFloatingText;
    }
}
