namespace CombatSystem.Data
{
    /// <summary>
    /// 控制效果行为规则映射。
    /// 定义每种控制类型具体会阻止哪些行为（移动/旋转/施法/普攻）。
    /// </summary>
    /// <remarks>
    /// 设计原则：
    /// - 数据驱动：控制行为通过标记位组合定义，无需修改业务代码
    /// - 单一职责：此类仅负责"控制类型 → 行为标记"的映射
    /// - 可扩展性：新增控制类型只需在 GetFlags 中添加 case
    /// 
    /// 使用示例：
    /// <code>
    /// // 检查眩晕是否阻止施法
    /// bool blocksSkill = ControlRules.HasFlag(ControlType.Stun, ControlFlag.BlocksCasting);
    /// </code>
    /// </remarks>
    public static class ControlRules
    {
        /// <summary>
        /// 检查指定控制类型是否包含某个行为标记。
        /// </summary>
        /// <param name="type">控制类型</param>
        /// <param name="flag">要检查的行为标记</param>
        /// <returns>若该控制类型包含此标记则返回 true</returns>
        public static bool HasFlag(ControlType type, ControlFlag flag)
        {
            return (GetFlags(type) & flag) != 0;
        }

        /// <summary>
        /// 获取指定控制类型的所有行为标记。
        /// </summary>
        /// <param name="type">控制类型</param>
        /// <returns>该控制类型对应的行为标记组合</returns>
        /// <remarks>
        /// 控制分类：
        /// - 完全控制（Stun/Knockup/Sleep等）：阻止所有行为并打断施法
        /// - 强制移动（Fear/Taunt/Charm）：阻止所有行为但强制单位移动
        /// - 沉默（Silence）：仅阻止技能施放
        /// - 定身（Root）：仅阻止移动
        /// - 缴械/致盲（Disarm/Blind）：仅阻止普攻
        /// - 减速（Slow）：不阻止任何行为（仅影响移速）
        /// </remarks>
        public static ControlFlag GetFlags(ControlType type)
        {
            switch (type)
            {
                // 完全控制：阻止所有行为 + 打断施法
                case ControlType.Stun:
                case ControlType.Knockup:
                case ControlType.Knockback:
                case ControlType.Suppression:
                case ControlType.Sleep:
                case ControlType.Polymorph:
                    return ControlFlag.BlocksMovement
                        | ControlFlag.BlocksRotation
                        | ControlFlag.BlocksCasting
                        | ControlFlag.BlocksBasicAttack
                        | ControlFlag.InterruptsCasting;

                // 强制移动控制：阻止玩家操作但由 AI 接管移动
                case ControlType.Fear:
                case ControlType.Taunt:
                case ControlType.Charm:
                    return ControlFlag.BlocksMovement
                        | ControlFlag.BlocksRotation
                        | ControlFlag.BlocksCasting
                        | ControlFlag.BlocksBasicAttack
                        | ControlFlag.InterruptsCasting
                        | ControlFlag.ForcesMovement;

                // 沉默：仅阻止技能释放
                case ControlType.Silence:
                    return ControlFlag.BlocksCasting | ControlFlag.InterruptsCasting;

                // 定身：仅阻止移动，可以施法和普攻
                case ControlType.Root:
                    return ControlFlag.BlocksMovement;

                // 缴械/致盲：仅阻止普攻
                case ControlType.Disarm:
                case ControlType.Blind:
                    return ControlFlag.BlocksBasicAttack;

                // 减速/All/默认：不阻止任何行为
                case ControlType.Slow:
                case ControlType.All:
                default:
                    return ControlFlag.None;
            }
        }
    }
}
