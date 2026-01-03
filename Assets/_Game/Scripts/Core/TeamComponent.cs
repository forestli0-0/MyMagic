using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 阵营组件，用于标识单位所属的阵营，是目标筛选和友敌判断的基础。
    /// </summary>
    /// <remarks>
    /// 在战斗系统中，TargetingSystem 会根据阵营 ID 来区分敌我关系。
    /// 相同 TeamId 的单位被视为友方单位。
    /// </remarks>
    public class TeamComponent : MonoBehaviour
    {
        [Tooltip("阵营唯一标识符，相同数值表示同一阵营")]
        [SerializeField] private int teamId;

        /// <summary>
        /// 获取当前阵营的 ID。
        /// </summary>
        public int TeamId => teamId;

        /// <summary>
        /// 判断另一个单位是否与当前单位处于同一阵营。
        /// </summary>
        /// <param name="other">待比较的阵营组件</param>
        /// <returns>若阵营 ID 相同则返回 true</returns>
        public bool IsSameTeam(TeamComponent other)
        {
            return other != null && other.teamId == teamId;
        }
    }
}
