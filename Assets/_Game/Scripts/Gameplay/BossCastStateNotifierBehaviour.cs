using CombatSystem.Core;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 施法状态通知器：挂在 Animator 的 Cast 状态上，用于在状态进出时广播事件。
    /// </summary>
    public sealed class BossCastStateNotifierBehaviour : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            ResolveProxy(animator)?.NotifyCastStateEntered();
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            ResolveProxy(animator)?.NotifyCastStateExited();
        }

        private static UnitAnimationEventProxy ResolveProxy(Animator animator)
        {
            if (animator == null)
            {
                return null;
            }

            var proxy = animator.GetComponent<UnitAnimationEventProxy>();
            if (proxy != null)
            {
                return proxy;
            }

            proxy = animator.GetComponentInChildren<UnitAnimationEventProxy>(true);
            if (proxy != null)
            {
                return proxy;
            }

            return animator.GetComponentInParent<UnitAnimationEventProxy>();
        }
    }
}
