using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Data
{
    [CreateAssetMenu(menuName = "Combat/Items/Affix Definition", fileName = "Affix_")]
    public class AffixDefinition : DefinitionBase
    {
        [Header("Rules")]
        [SerializeField] private bool isPrefix = true;
        [SerializeField] private int weight = 1;
        [SerializeField] private List<ItemSlot> allowedSlots = new List<ItemSlot>();

        [Header("Modifiers")]
        [SerializeField] private List<ModifierDefinition> modifiers = new List<ModifierDefinition>();

        public bool IsPrefix => isPrefix;
        public int Weight => Mathf.Max(0, weight);
        public IReadOnlyList<ItemSlot> AllowedSlots => allowedSlots;
        public IReadOnlyList<ModifierDefinition> Modifiers => modifiers;
    }
}
