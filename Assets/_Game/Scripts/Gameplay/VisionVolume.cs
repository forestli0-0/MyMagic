using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 视野体积：可作为草丛/潜行区使用。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VisionVolume : MonoBehaviour
    {
        [SerializeField] private bool concealUnitsInside = true;
        [SerializeField] private bool concealAsCamouflage = true;

        private void OnValidate()
        {
            var colliderRef = GetComponent<Collider>();
            if (colliderRef != null)
            {
                colliderRef.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!concealUnitsInside || other == null)
            {
                return;
            }

            var visibility = other.GetComponentInParent<VisibilityComponent>();
            visibility?.AddConcealment(concealAsCamouflage);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!concealUnitsInside || other == null)
            {
                return;
            }

            var visibility = other.GetComponentInParent<VisibilityComponent>();
            visibility?.RemoveConcealment();
        }
    }
}
