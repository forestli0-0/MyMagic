using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 可见性查询系统（简化版 FoW 入口）。
    /// </summary>
    public class VisionSystem : MonoBehaviour
    {
        private static VisionSystem instance;

        public static VisionSystem Instance => instance;

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static bool IsTargetVisible(UnitRoot observer, CombatTarget target)
        {
            if (!target.IsValid)
            {
                return false;
            }

            if (target.Visibility == null)
            {
                return true;
            }

            var observerTeam = observer != null ? observer.Team : null;
            return target.Visibility.IsVisibleTo(observerTeam);
        }
    }
}
