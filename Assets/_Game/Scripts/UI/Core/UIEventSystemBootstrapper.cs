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
        [SerializeField] private bool isolateEventSystemActionsAtRuntime = true;
        [SerializeField] private bool forceDynamicInputUpdateForUi = true;

        private InputActionAsset runtimeModuleActions;
        private InputActionAsset runtimeModuleActionsSource;
        private bool inputUpdateModeAdjustedLogged;

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
            ReleaseRuntimeModuleActions();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            var system = ResolvePrimaryEventSystem();
            if (system == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                go.transform.SetParent(transform, false);
                system = go.GetComponent<EventSystem>();
            }

            if (system == null)
            {
                return;
            }

            EnsureInputUpdateMode();

            if (!system.gameObject.activeSelf)
            {
                system.gameObject.SetActive(true);
            }

            if (!system.enabled)
            {
                system.enabled = true;
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

            var sourceActions = ResolveActions();
            if (sourceActions == null)
            {
                return;
            }

            var moduleActions = ResolveModuleActions(sourceActions);
            if (moduleActions == null)
            {
                return;
            }

            ConfigureModule(module, moduleActions);
            EnsureModuleActionsEnabled(module);
            EnsureInputReader(sourceActions);
        }

        private static EventSystem ResolvePrimaryEventSystem()
        {
            var systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (systems == null || systems.Length == 0)
            {
                return null;
            }

            var primary = EventSystem.current;
            if (primary != null && (!primary.isActiveAndEnabled || !primary.gameObject.activeInHierarchy))
            {
                primary = null;
            }

            if (primary == null)
            {
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i] != null && systems[i].isActiveAndEnabled && systems[i].gameObject.activeInHierarchy)
                    {
                        primary = systems[i];
                        break;
                    }
                }
            }

            if (primary == null)
            {
                primary = systems[0];
            }

            return primary;
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

        private static void EnsureModuleActionsEnabled(InputSystemUIInputModule module)
        {
            if (module == null)
            {
                return;
            }

            TryEnable(module.point);
            TryEnable(module.leftClick);
            TryEnable(module.rightClick);
            TryEnable(module.middleClick);
            TryEnable(module.scrollWheel);
            TryEnable(module.move);
            TryEnable(module.submit);
            TryEnable(module.cancel);
        }

        private static void TryEnable(InputActionReference reference)
        {
            var action = reference != null ? reference.action : null;
            if (action != null && !action.enabled)
            {
                action.Enable();
            }
        }

        private void EnsureInputUpdateMode()
        {
            if (!forceDynamicInputUpdateForUi || !Application.isPlaying)
            {
                return;
            }

            var settings = InputSystem.settings;
            if (settings == null)
            {
                return;
            }

            if (settings.updateMode != InputSettings.UpdateMode.ProcessEventsInFixedUpdate)
            {
                return;
            }

            settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            if (!inputUpdateModeAdjustedLogged)
            {
                Debug.Log("[UIEventSystemBootstrapper] Input update mode forced to Dynamic for UI interaction while paused.", this);
                inputUpdateModeAdjustedLogged = true;
            }
        }

        private InputActionAsset ResolveModuleActions(InputActionAsset sourceActions)
        {
            if (sourceActions == null)
            {
                return null;
            }

            if (!isolateEventSystemActionsAtRuntime || !Application.isPlaying)
            {
                ReleaseRuntimeModuleActions();
                return sourceActions;
            }

            if (runtimeModuleActions != null && runtimeModuleActionsSource == sourceActions)
            {
                return runtimeModuleActions;
            }

            ReleaseRuntimeModuleActions();
            runtimeModuleActions = Instantiate(sourceActions);
            runtimeModuleActions.name = $"{sourceActions.name}_UIRuntime";
            runtimeModuleActionsSource = sourceActions;
            return runtimeModuleActions;
        }

        private void ReleaseRuntimeModuleActions()
        {
            if (runtimeModuleActions == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeModuleActions);
            }
            else
            {
                DestroyImmediate(runtimeModuleActions);
            }

            runtimeModuleActions = null;
            runtimeModuleActionsSource = null;
        }
    }
}
