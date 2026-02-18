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

        /// <summary>
        /// 某些网格型控件（背包格/装备格）不适合做全局缩放反馈，
        /// 否则会出现肉眼可见的格子尺寸不一致。
        /// </summary>
        public static bool ShouldSuppressScaleFeedback(Selectable selectable)
        {
            if (selectable == null)
            {
                return true;
            }

            return selectable.GetComponentInParent<InventorySlotUI>() != null ||
                   selectable.GetComponentInParent<EquipmentSlotUI>() != null;
        }
    }
}
