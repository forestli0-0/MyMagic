#if UNITY_EDITOR
using System;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CombatSystem.Editor
{
    /// <summary>
    /// 将 BossCastStateNotifierBehaviour 绑定到怪物 Animator Controller 的 Cast 状态。
    /// </summary>
    public static class BossCastStateNotifierBinder
    {
        private static readonly string[] ControllerFolders =
        {
            "Assets/_Game/Art/Monsters/Controllers"
        };

        private static bool autoBindQueued;

        [InitializeOnLoadMethod]
        private static void QueueAutoBind()
        {
            if (autoBindQueued || Application.isBatchMode)
            {
                return;
            }

            autoBindQueued = true;
            EditorApplication.delayCall += () =>
            {
                autoBindQueued = false;
                BindAllControllers(verbose: false);
            };
        }

        [MenuItem("Combat/Animation/Bind Cast State Notifier")]
        public static void BindAllControllersMenu()
        {
            BindAllControllers(verbose: true);
        }

        private static void BindAllControllers(bool verbose)
        {
            var guids = AssetDatabase.FindAssets("t:AnimatorController", ControllerFolders);
            if (guids == null || guids.Length == 0)
            {
                if (verbose)
                {
                    Debug.Log("[BossCastStateNotifierBinder] No animator controllers found in configured folders.");
                }

                return;
            }

            var changedControllers = 0;
            var addedBehaviours = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null)
                {
                    continue;
                }

                var controllerChanged = false;
                for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
                {
                    var layer = controller.layers[layerIndex];
                    if (layer.stateMachine == null)
                    {
                        continue;
                    }

                    if (BindStateMachine(layer.stateMachine, ref addedBehaviours))
                    {
                        controllerChanged = true;
                    }
                }

                if (!controllerChanged)
                {
                    continue;
                }

                changedControllers++;
                EditorUtility.SetDirty(controller);
            }

            if (changedControllers > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (verbose)
            {
                Debug.Log($"[BossCastStateNotifierBinder] Controllers changed: {changedControllers}, behaviours added: {addedBehaviours}.");
            }
        }

        private static bool BindStateMachine(AnimatorStateMachine stateMachine, ref int addedBehaviours)
        {
            var changed = false;

            var childStates = stateMachine.states;
            for (int i = 0; i < childStates.Length; i++)
            {
                var state = childStates[i].state;
                if (!IsCastState(state) || HasNotifierBehaviour(state))
                {
                    continue;
                }

                state.AddStateMachineBehaviour<BossCastStateNotifierBehaviour>();
                addedBehaviours++;
                changed = true;
            }

            var nested = stateMachine.stateMachines;
            for (int i = 0; i < nested.Length; i++)
            {
                var nestedMachine = nested[i].stateMachine;
                if (nestedMachine == null)
                {
                    continue;
                }

                if (BindStateMachine(nestedMachine, ref addedBehaviours))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsCastState(AnimatorState state)
        {
            if (state == null)
            {
                return false;
            }

            if (string.Equals(state.name, "Cast", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var tag = state.tag;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            return string.Equals(tag, "Cast", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tag, "Casting", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasNotifierBehaviour(AnimatorState state)
        {
            var behaviours = state.behaviours;
            if (behaviours == null || behaviours.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is BossCastStateNotifierBehaviour)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
