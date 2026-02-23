using UnityEngine;
using UnityEngine.EventSystems;

namespace CombatSystem.UI
{
    /// <summary>
    /// 角色属性文本悬浮目标，将鼠标进入/离开事件转发给 CharacterScreen。
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterStatHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string statId;
        [SerializeField] private string statLabel;

        private CharacterScreen owner;

        public string StatId => statId;
        public string StatLabel => statLabel;

        public void Configure(CharacterScreen screen, string id, string label)
        {
            owner = screen;
            statId = id ?? string.Empty;
            statLabel = string.IsNullOrWhiteSpace(label) ? statId : label;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.HandleStatHoverEnter(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HandleStatHoverExit(this, eventData);
        }
    }
}
