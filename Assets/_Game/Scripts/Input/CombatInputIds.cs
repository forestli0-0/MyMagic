namespace CombatSystem.Input
{
    /// <summary>
    /// 战斗系统输入标识符常量集合。
    /// 定义所有 InputActionAsset 中使用的 ActionMap 和 Action 名称。
    /// </summary>
    /// <remarks>
    /// 使用说明：
    /// - 通过 CombatInputAssetBuilder 生成对应的 InputActionAsset
    /// - InputReader 使用这些常量查找对应的 Action
    /// - 修改此处常量后需重新生成 InputActionAsset
    /// </remarks>
    public static class CombatInputIds
    {
        #region ActionMap 名称

        /// <summary>游戏玩法输入映射（移动、技能、瞄准等）</summary>
        public const string GameplayMap = "Gameplay";
        /// <summary>UI 输入映射（导航、确认、取消等）</summary>
        public const string UIMap = "UI";
        /// <summary>调试输入映射（切换覆盖层等）</summary>
        public const string DebugMap = "Debug";

        #endregion

        #region Gameplay Action 名称

        /// <summary>移动输入（WASD/方向键/左摇杆）</summary>
        public const string Move = "Move";
        /// <summary>瞄准点输入（鼠标/指针位置）</summary>
        public const string AimPoint = "AimPoint";
        /// <summary>技能槽位 1（数字键 1）</summary>
        public const string Skill1 = "Skill1";
        /// <summary>技能槽位 2（数字键 2）</summary>
        public const string Skill2 = "Skill2";
        /// <summary>技能槽位 3（数字键 3）</summary>
        public const string Skill3 = "Skill3";
        /// <summary>技能槽位 4（数字键 4）</summary>
        public const string Skill4 = "Skill4";
        /// <summary>技能槽位 5（数字键 5）</summary>
        public const string Skill5 = "Skill5";
        /// <summary>技能槽位 6（数字键 6）</summary>
        public const string Skill6 = "Skill6";
        /// <summary>取消操作（右键/ESC）</summary>
        public const string Cancel = "Cancel";
        /// <summary>暂停游戏（ESC/Start）</summary>
        public const string Pause = "Pause";
        /// <summary>打开/关闭物品栏（I）</summary>
        public const string Inventory = "Inventory";

        #endregion

        #region Debug Action 名称

        /// <summary>切换调试覆盖层（F3）</summary>
        public const string ToggleOverlay = "ToggleOverlay";

        #endregion

        #region UI Action 名称

        /// <summary>UI 导航（WASD/方向键/左摇杆/十字键）</summary>
        public const string UINavigate = "Navigate";
        /// <summary>UI 确认（Enter/Space/A键）</summary>
        public const string UISubmit = "Submit";
        /// <summary>UI 取消（ESC/B键）</summary>
        public const string UICancel = "Cancel";
        /// <summary>UI 指针位置（鼠标/触摸）</summary>
        public const string UIPoint = "Point";
        /// <summary>UI 点击（鼠标左键/触摸）</summary>
        public const string UIClick = "Click";
        /// <summary>UI 滚动（鼠标滚轮）</summary>
        public const string UIScroll = "ScrollWheel";

        #endregion
    }
}
