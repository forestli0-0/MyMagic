using CombatSystem.Gameplay;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 统一的玩家单位定位工具，避免 UI/调试脚本误绑定到敌方单位。
    /// </summary>
    public static class PlayerUnitLocator
    {
        /// <summary>
        /// 安全地按标签查找对象，标签未在 TagManager 中注册时返回 null。
        /// </summary>
        public static GameObject FindGameObjectWithTagSafe(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            try
            {
                return GameObject.FindGameObjectWithTag(tag);
            }
            catch (UnityException)
            {
                return null;
            }
        }

        public static UnitRoot FindPlayerUnit()
        {
            var taggedPlayer = FindGameObjectWithTagSafe("Player");
            if (taggedPlayer != null)
            {
                var taggedUnit = taggedPlayer.GetComponent<UnitRoot>();
                if (IsPlayerUnit(taggedUnit))
                {
                    return taggedUnit;
                }
            }

            var movementDrivers = Object.FindObjectsByType<PlayerMovementDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < movementDrivers.Length; i++)
            {
                if (movementDrivers[i] == null)
                {
                    continue;
                }

                var movementUnit = movementDrivers[i].GetComponent<UnitRoot>();
                if (movementUnit != null)
                {
                    return movementUnit;
                }
            }

            var units = Object.FindObjectsByType<UnitRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].CompareTag("Player"))
                {
                    return units[i];
                }
            }

            return null;
        }

        public static bool IsPlayerUnit(UnitRoot unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.CompareTag("Player"))
            {
                return true;
            }

            if (unit.GetComponent<PlayerMovementDriver>() != null)
            {
                return true;
            }

            return false;
        }
    }
}
