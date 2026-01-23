using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 装备组件，负责管理单位的装备槽位、属性加成应用以及与背包的交互。
    /// </summary>
    public class EquipmentComponent : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("饰品槽位数量")]
        [SerializeField] private int accessorySlotCount = 3;
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool initializeOnAwake = true;
        
        [Header("组件引用")]
        [Tooltip("关联的背包组件（用于穿脱交互）")]
        [SerializeField] private InventoryComponent inventory;
        [Tooltip("Buff 控制器（用于应用装备属性）")]
        [SerializeField] private BuffController buffController;
        [Tooltip("单位根组件")]
        [SerializeField] private UnitRoot unitRoot;
        
        [Header("初始数据")]
        [Tooltip("初始穿戴的装备列表")]
        [SerializeField] private List<StartingEquipmentEntry> startingEquipment = new List<StartingEquipmentEntry>();

        // 运行时状态：槽位列表
        private readonly List<EquipmentSlotState> slots = new List<EquipmentSlotState>(8);

        /// <summary>当装备发生变化时触发（穿戴/脱下/更换）</summary>
        public event Action EquipmentChanged;

        /// <summary>所有装备槽位的状态</summary>
        public IReadOnlyList<EquipmentSlotState> Slots => slots;

        private void Reset()
        {
            inventory = GetComponent<InventoryComponent>();
            buffController = GetComponent<BuffController>();
            unitRoot = GetComponent<UnitRoot>();
        }

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            // 组件销毁时彻底清理并销毁 runtime 资源
            ClearAll();
        }

        /// <summary>
        /// 初始化组件，构建槽位并穿戴初始装备。
        /// </summary>
        public void Initialize()
        {
            EnsureReferences();
            BuildSlots();
            ApplyStartingEquipment();
        }

        /// <summary>
        /// 清空所有槽位上的装备，并移除对应的属性 Buff。
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Item != null)
                {
                    RemoveBuffs(slots[i].AppliedBuffs);
                }

                slots[i].Clear();
            }

            EquipmentChanged?.Invoke();
        }

        /// <summary>
        /// 尝试自动穿戴一件武器或防具到合适的空槽位中。
        /// </summary>
        /// <param name="item">要穿戴的物品实例</param>
        /// <returns>若穿戴成功则返回 true</returns>
        public bool TryEquip(ItemInstance item)
        {
            return TryEquip(item, inventory);
        }

        public bool TryEquip(ItemInstance item, InventoryComponent sourceInventory)
        {
            if (item == null || item.Definition == null || !item.Definition.IsEquippable)
            {
                return false;
            }

            var slotIndex = FindSlotIndex(item.Definition.Slot, true);
            if (slotIndex < 0)
            {
                slotIndex = FindSlotIndex(item.Definition.Slot, false);
            }

            return TryEquipToSlot(item, slotIndex, sourceInventory);
        }

        public bool TryEquipToSlot(ItemInstance item, int slotIndex, InventoryComponent sourceInventory = null)
        {
            if (item == null || item.Definition == null || !item.Definition.IsEquippable)
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return false;
            }

            if (slots[slotIndex].Slot != item.Definition.Slot)
            {
                return false;
            }

            if (slots[slotIndex].Item != null)
            {
                var targetInventory = sourceInventory ?? inventory;
                if (targetInventory != null && !targetInventory.CanAddItem(slots[slotIndex].Item))
                {
                    return false;
                }

                if (!TryUnequip(slotIndex, targetInventory))
                {
                    return false;
                }
            }

            var equipItem = item;
            if (sourceInventory != null)
            {
                equipItem = item.CloneWithStack(1);
                if (!sourceInventory.TryRemoveItem(item, 1))
                {
                    return false;
                }
            }

            if (equipItem.Stack > 1)
            {
                equipItem.SetStack(1);
            }

            var appliedBuffs = BuildEquipmentBuffs(equipItem);
            ApplyBuffs(appliedBuffs);

            slots[slotIndex].SetItem(equipItem, appliedBuffs);
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool TryUnequip(int slotIndex, InventoryComponent targetInventory = null)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return false;
            }

            var slot = slots[slotIndex];
            if (slot.Item == null)
            {
                return false;
            }

            if (targetInventory != null && !targetInventory.TryAddItem(slot.Item))
            {
                return false;
            }

            RemoveBuffs(slot.AppliedBuffs);
            slot.Clear();
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool TryReplaceSlotItem(int slotIndex, ItemInstance item)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return false;
            }

            if (item != null)
            {
                // 仅允许正确槽位的可装备物品
                if (item.Definition == null || !item.Definition.IsEquippable)
                {
                    return false;
                }

                if (slots[slotIndex].Slot != item.Definition.Slot)
                {
                    return false;
                }

                // 装备位只允许 1 个
                if (item.Stack > 1)
                {
                    item.SetStack(1);
                }
            }

            if (slots[slotIndex].Item != null)
            {
                // 移除原装备的 Buff
                RemoveBuffs(slots[slotIndex].AppliedBuffs);
            }

            slots[slotIndex].Clear();

            if (item != null)
            {
                // 应用新装备的 Buff
                var appliedBuffs = BuildEquipmentBuffs(item);
                ApplyBuffs(appliedBuffs);
                slots[slotIndex].SetItem(item, appliedBuffs);
            }

            EquipmentChanged?.Invoke();
            return true;
        }

        public bool TrySwapSlots(int indexA, int indexB)
        {
            if (indexA == indexB)
            {
                return false;
            }

            if (indexA < 0 || indexB < 0 || indexA >= slots.Count || indexB >= slots.Count)
            {
                return false;
            }

            var slotA = slots[indexA];
            var slotB = slots[indexB];
            if (slotA.Slot != slotB.Slot)
            {
                return false;
            }

            var itemA = slotA.Item;
            var itemB = slotB.Item;
            var buffsA = slotA.AppliedBuffs as List<BuffDefinition>;
            var buffsB = slotB.AppliedBuffs as List<BuffDefinition>;

            if (itemA == null && itemB == null)
            {
                return false;
            }

            slotA.SetItem(itemB, buffsB);
            slotB.SetItem(itemA, buffsA);
            EquipmentChanged?.Invoke();
            return true;
        }

        private void EnsureReferences()
        {
            if (inventory == null)
            {
                inventory = GetComponent<InventoryComponent>();
            }

            if (buffController == null)
            {
                buffController = GetComponent<BuffController>();
            }

            if (unitRoot == null)
            {
                unitRoot = GetComponent<UnitRoot>();
            }
        }

        private void BuildSlots()
        {
            if (slots.Count > 0)
            {
                return;
            }

            slots.Add(new EquipmentSlotState(ItemSlot.Weapon));
            slots.Add(new EquipmentSlotState(ItemSlot.Headband));
            slots.Add(new EquipmentSlotState(ItemSlot.Clothes));
            slots.Add(new EquipmentSlotState(ItemSlot.Shoes));

            var accessoryCount = Mathf.Max(0, accessorySlotCount);
            for (int i = 0; i < accessoryCount; i++)
            {
                slots.Add(new EquipmentSlotState(ItemSlot.Accessory));
            }
        }

        private int FindSlotIndex(ItemSlot slot, bool requireEmpty)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var entry = slots[i];
                if (entry.Slot != slot)
                {
                    continue;
                }

                if (requireEmpty && entry.Item != null)
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        private List<BuffDefinition> BuildEquipmentBuffs(ItemInstance item)
        {
            var buffs = new List<BuffDefinition>();
            if (item == null || item.Definition == null)
            {
                return buffs;
            }

            var definition = item.Definition;
            var baseModifiers = definition.BaseModifiers;
            if (baseModifiers != null && baseModifiers.Count > 0)
            {
                buffs.Add(BuffDefinition.CreateRuntime($"{definition.Id}_Base", baseModifiers));
            }

            var equipBuffs = definition.EquipBuffs;
            if (equipBuffs != null && equipBuffs.Count > 0)
            {
                for (int i = 0; i < equipBuffs.Count; i++)
                {
                    var source = equipBuffs[i];
                    if (source == null)
                    {
                        continue;
                    }

                    var runtimeBuff = BuffDefinition.CreateRuntimeClone(source, $"{definition.Id}_{source.name}");
                    if (runtimeBuff != null)
                    {
                        buffs.Add(runtimeBuff);
                    }
                }
            }

            var affixes = item.Affixes;
            if (affixes != null && affixes.Count > 0 && definition.AllowAffixes)
            {
                for (int i = 0; i < affixes.Count; i++)
                {
                    var affix = affixes[i];
                    if (affix == null)
                    {
                        continue;
                    }

                    var modifiers = affix.Modifiers;
                    if (modifiers == null || modifiers.Count == 0)
                    {
                        continue;
                    }

                    buffs.Add(BuffDefinition.CreateRuntime($"{definition.Id}_{affix.Id}", modifiers));
                }
            }

            return buffs;
        }

        private void ApplyBuffs(IReadOnlyList<BuffDefinition> buffs)
        {
            if (buffController == null || buffs == null)
            {
                return;
            }

            for (int i = 0; i < buffs.Count; i++)
            {
                var buff = buffs[i];
                if (buff != null)
                {
                    buffController.ApplyBuff(buff, unitRoot);
                }
            }
        }

        private void RemoveBuffs(IReadOnlyList<BuffDefinition> buffs)
        {
            if (buffController == null || buffs == null)
            {
                return;
            }

            for (int i = 0; i < buffs.Count; i++)
            {
                var buff = buffs[i];
                if (buff != null)
                {
                    // 从单位身上移除逻辑效果
                    buffController.RemoveBuff(buff);

                    // 重要：如果该 Buff 是动态生成的 Runtime 实例，必须手动销毁 SO
                    if (buff.IsRuntime)
                    {
                        Destroy(buff);
                    }
                }
            }
        }

        private void ApplyStartingEquipment()
        {
            if (startingEquipment == null || startingEquipment.Count == 0)
            {
                return;
            }

            for (int i = 0; i < startingEquipment.Count; i++)
            {
                var entry = startingEquipment[i];
                if (entry == null || entry.Item == null || entry.Item.Definition == null)
                {
                    continue;
                }

                TryEquip(entry.Item, null);
            }
        }

        [Serializable]
        private class StartingEquipmentEntry
        {
            public ItemInstance Item;
        }

        [Serializable]
        public sealed class EquipmentSlotState
        {
            [SerializeField] private ItemSlot slot;
            [SerializeField] private ItemInstance item;
            [NonSerialized] private List<BuffDefinition> appliedBuffs;

            public ItemSlot Slot => slot;
            public ItemInstance Item => item;
            public IReadOnlyList<BuffDefinition> AppliedBuffs => appliedBuffs;

            public EquipmentSlotState(ItemSlot slot)
            {
                this.slot = slot;
            }

            public void SetItem(ItemInstance newItem, List<BuffDefinition> buffs)
            {
                item = newItem;
                appliedBuffs = buffs;
            }

            public void Clear()
            {
                item = null;
                appliedBuffs = null;
            }
        }
    }
}
