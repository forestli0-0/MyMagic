using UnityEngine;

namespace CombatSystem.Persistence
{
    public class SettingsBootstrapper : MonoBehaviour
    {
        [SerializeField] private bool applyOnAwake = true;

        private void Awake()
        {
            if (applyOnAwake)
            {
                SettingsService.LoadOrCreate();
            }
        }
    }
}
