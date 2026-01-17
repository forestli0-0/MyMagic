using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 关卡出生点，用于玩家进入场景时的位置放置。
    /// </summary>
    public class LevelSpawnPoint : MonoBehaviour
    {
        [Tooltip("出生点唯一标识")]
        [SerializeField] private string spawnId = "Start";
        [Tooltip("是否为默认出生点")]
        [SerializeField] private bool isDefault;
        [Tooltip("是否为返回出生点（从其他关卡返回时使用）")]
        [SerializeField] private bool isReturn;

        public string SpawnId => spawnId;
        public bool IsDefault => isDefault;
        public bool IsReturn => isReturn;

        /// <summary>
        /// 将目标 Transform 放置到此出生点的位置和朝向。
        /// </summary>
        public void ApplyTo(Transform target)
        {
            if (target == null)
            {
                return;
            }

            target.SetPositionAndRotation(transform.position, transform.rotation);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isDefault ? new Color(0.2f, 0.8f, 0.3f, 0.9f) : new Color(0.2f, 0.6f, 0.9f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
#endif
    }
}
