using UnityEngine;

namespace CombatSystem.UI
{
    public class UnitHealthBarAnchor : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);

        public Vector3 WorldOffset => worldOffset;
    }
}
