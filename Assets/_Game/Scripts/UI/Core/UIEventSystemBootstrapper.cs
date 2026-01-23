using CombatSystem.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace CombatSystem.UI
{
    public class UIEventSystemBootstrapper : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField] private bool autoFindInputReader = true;

        private void Awake()
        {
            if (autoFindInputReader && inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureEventSystem();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            var system = EventSystem.current;
            if (system == null)
            {
                system = FindFirstObjectByType<EventSystem>();
            }

            if (system == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                go.transform.SetParent(transform, false);
                system = go.GetComponent<EventSystem>();
            }

            var module = system.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = system.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            var legacy = system.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Destroy(legacy);
            }

            var actions = ResolveActions();
            if (actions == null)
            {
                return;
            }

            ConfigureModule(module, actions);
            EnsureInputReader(actions);
        }

        private InputActionAsset ResolveActions()
        {
            if (actionsAsset != null)
            {
                return actionsAsset;
            }

            if (autoFindInputReader && inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }

            return inputReader != null ? inputReader.Actions : null;
        }

        private void EnsureInputReader(InputActionAsset actions)
        {
            if (inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }

            if (inputReader == null)
            {
                var go = new GameObject("InputRoot", typeof(InputReader));
                go.transform.SetParent(transform, false);
                inputReader = go.GetComponent<InputReader>();
            }

            if (actions != null && inputReader != null)
            {
                inputReader.SetActions(actions);
            }
        }

        private static void ConfigureModule(InputSystemUIInputModule module, InputActionAsset actions)
        {
            if (module == null || actions == null)
            {
                return;
            }

            module.actionsAsset = actions;
            module.point = CreateReference(actions, "UI/Point");
            module.leftClick = CreateReference(actions, "UI/Click");
            module.scrollWheel = CreateReference(actions, "UI/ScrollWheel");
            module.move = CreateReference(actions, "UI/Navigate");
            module.submit = CreateReference(actions, "UI/Submit");
            module.cancel = CreateReference(actions, "UI/Cancel");
        }

        private static InputActionReference CreateReference(InputActionAsset asset, string actionPath)
        {
            var action = asset != null ? asset.FindAction(actionPath) : null;
            return action != null ? InputActionReference.Create(action) : null;
        }
    }
}
