namespace CombatSystem.Data
{
    /// <summary>
    /// 战斗属性ID常量集合，避免硬编码字符串。
    /// </summary>
    /// <remarks>
    /// 命名规范：Stat_[属性名]，与 ScriptableObjects/Stats/ 下的资产文件名一致。
    /// </remarks>
    public static class CombatStatIds
    {
        /// <summary>护甲（物理减伤）</summary>
        public const string Armor = "Stat_Armor";
        /// <summary>魔法抗性（魔法减伤）</summary>
        public const string MagicResist = "Stat_MagicResist";
        /// <summary>固定护甲穿透</summary>
        public const string ArmorPenFlat = "Stat_ArmorPenFlat";
        /// <summary>百分比护甲穿透</summary>
        public const string ArmorPenPercent = "Stat_ArmorPenPercent";
        /// <summary>固定魔法穿透</summary>
        public const string MagicPenFlat = "Stat_MagicPenFlat";
        /// <summary>百分比魔法穿透</summary>
        public const string MagicPenPercent = "Stat_MagicPenPercent";
        /// <summary>技能急速（非线性减少冷却时间）</summary>
        public const string AbilityHaste = "Stat_AbilityHaste";
        /// <summary>攻击速度加成</summary>
        public const string AttackSpeed = "Stat_AttackSpeed";
        /// <summary>韧性（减少控制效果持续时间）</summary>
        /// <remarks>
        /// TODO: 需要在 BuffController 中实现韧性对控制效果持续时间的影响。
        /// 公式建议：实际持续时间 = 基础持续时间 × 100 / (100 + Tenacity)
        /// </remarks>
        public const string Tenacity = "Stat_Tenacity";
        /// <summary>生命偷取（仅物理伤害）</summary>
        public const string Lifesteal = "Stat_Lifesteal";
        /// <summary>全能吸血（所有伤害类型）</summary>
        public const string Omnivamp = "Stat_Omnivamp";
        /// <summary>移动速度</summary>
        public const string MoveSpeed = "Stat_MoveSpeed";
    }
}
