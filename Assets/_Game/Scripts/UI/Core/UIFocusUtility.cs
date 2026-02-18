using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public static class UIFocusUtility
    {
        public static bool FocusDefault(Selectable preferred, Component searchRoot)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var target = ResolveFocusable(preferred, searchRoot);
            if (target == null)
            {
                return false;
            }

            eventSystem.SetSelectedGameObject(target.gameObject);
            return true;
        }

        public static Selectable ResolveFocusable(Selectable preferred, Component searchRoot)
        {
            if (IsFocusable(preferred))
            {
                return preferred;
            }

            if (searchRoot == null)
            {
                return null;
            }

            var candidates = searchRoot.GetComponentsInChildren<Selectable>(true);
            if (candidates == null || candidates.Length <= 0)
            {
                return null;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsFocusable(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        public static bool IsFocusable(Selectable selectable)
        {
            return selectable != null &&
                   selectable.gameObject.activeInHierarchy &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }
    }
}
