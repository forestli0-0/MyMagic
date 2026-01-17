using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 关卡传送门，玩家进入触发区域后加载目标关卡。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LevelPortal : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("目标关卡 ID")]
        [SerializeField] private string targetLevelId;
        [Tooltip("目标出生点 ID")]
        [SerializeField] private string targetSpawnId = "Start";

        [Header("过滤")]
        [Tooltip("是否要求玩家标签才能触发")]
        [SerializeField] private bool requirePlayerTag = true;
        [Tooltip("玩家标签名称")]
        [SerializeField] private string playerTag = "Player";

        private void Reset()
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            if (requirePlayerTag && !string.IsNullOrEmpty(playerTag))
            {
                if (!other.CompareTag(playerTag))
                {
                    return;
                }
            }

            var flow = LevelFlowController.Instance;
            if (flow == null)
            {
                flow = FindFirstObjectByType<LevelFlowController>();
            }

            if (flow == null)
            {
                Debug.LogWarning("[LevelPortal] 未找到 LevelFlowController。");
                return;
            }

            flow.LoadLevel(targetLevelId, targetSpawnId);
        }
    }
}
