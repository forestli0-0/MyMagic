using System;
using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 单位战斗状态组件：统一管理不可选取/无敌/潜行/法术护盾等状态标记。
    /// </summary>
    public class CombatStateComponent : MonoBehaviour
    {
        [SerializeField] private CombatStateFlags initialFlags = CombatStateFlags.None;
        [SerializeField] private int initialSpellShieldCharges;

        private CombatStateFlags flags;
        private int spellShieldCharges;
        private readonly Dictionary<int, float> revealExpireByTeam = new Dictionary<int, float>(4);
        private readonly List<int> expiredRevealTeams = new List<int>(4);

        public event Action<CombatStateFlags> StateChanged;
        public event Action<int> SpellShieldConsumed;

        public CombatStateFlags Flags => flags;
        public int SpellShieldCharges => spellShieldCharges;

        private void Awake()
        {
            flags = initialFlags;
            spellShieldCharges = Mathf.Max(0, initialSpellShieldCharges);
            if (spellShieldCharges > 0)
            {
                flags |= CombatStateFlags.SpellShielded;
            }
        }

        private void Update()
        {
            if (revealExpireByTeam.Count == 0)
            {
                return;
            }

            var now = Time.time;
            expiredRevealTeams.Clear();
            foreach (var pair in revealExpireByTeam)
            {
                if (pair.Value > 0f && pair.Value <= now)
                {
                    expiredRevealTeams.Add(pair.Key);
                }
            }

            for (int i = 0; i < expiredRevealTeams.Count; i++)
            {
                revealExpireByTeam.Remove(expiredRevealTeams[i]);
            }
        }

        public bool HasFlag(CombatStateFlags flag)
        {
            return (flags & flag) != 0;
        }

        public void AddFlag(CombatStateFlags flag)
        {
            SetFlag(flag, true);
        }

        public void RemoveFlag(CombatStateFlags flag)
        {
            SetFlag(flag, false);
        }

        public void SetFlag(CombatStateFlags flag, bool value)
        {
            var oldFlags = flags;
            if (value)
            {
                flags |= flag;
            }
            else
            {
                flags &= ~flag;
            }

            if (oldFlags != flags)
            {
                StateChanged?.Invoke(flags);
            }
        }

        public void GrantSpellShield(int charges = 1)
        {
            if (charges <= 0)
            {
                return;
            }

            spellShieldCharges += charges;
            SetFlag(CombatStateFlags.SpellShielded, true);
        }

        public bool ConsumeSpellShield()
        {
            if (spellShieldCharges <= 0)
            {
                return false;
            }

            spellShieldCharges = Mathf.Max(0, spellShieldCharges - 1);
            if (spellShieldCharges <= 0)
            {
                SetFlag(CombatStateFlags.SpellShielded, false);
            }

            SpellShieldConsumed?.Invoke(spellShieldCharges);
            return true;
        }

        public void RevealToTeam(int teamId, float durationSeconds)
        {
            if (teamId < 0)
            {
                return;
            }

            var expireTime = durationSeconds > 0f ? Time.time + durationSeconds : -1f;
            if (revealExpireByTeam.TryGetValue(teamId, out var current))
            {
                if (current < 0f || expireTime < 0f || current >= expireTime)
                {
                    return;
                }
            }

            revealExpireByTeam[teamId] = expireTime;
        }

        public bool IsRevealedToTeam(int teamId)
        {
            if (teamId < 0)
            {
                return false;
            }

            return revealExpireByTeam.ContainsKey(teamId);
        }
    }
}
