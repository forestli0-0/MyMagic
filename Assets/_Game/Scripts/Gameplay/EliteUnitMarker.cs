using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 精英单位标记，便于运行时识别与调试。
    /// </summary>
    public class EliteUnitMarker : MonoBehaviour
    {
        [SerializeField] private EnemyAffixDefinition affix;

        public EnemyAffixDefinition Affix => affix;

        public void SetAffix(EnemyAffixDefinition value)
        {
            affix = value;
        }
    }
}
