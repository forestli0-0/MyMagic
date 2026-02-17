using System;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Input;
using CombatSystem.UI;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 按 Tab 切换技能分页，便于快速测试多组技能。
    /// </summary>
    /// <remarks>
    /// 生命周期说明：
    /// - 该组件不订阅任何外部事件，无需在 OnDestroy 中清理
    /// - 切换分页时会调用 SkillUserComponent.SetSkills 更新技能列表
    /// - 若 SkillBarUI 引用存在，会同步刷新 UI 绑定
    /// </remarks>
    public class SkillPageSwitcher : MonoBehaviour
    {
        [Serializable]
        private class SkillPage
        {
            public string name;
            public List<SkillDefinition> skills = new List<SkillDefinition>();
        }

        [Header("References")]
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private SkillBarUI skillBar;

        [Header("Paging")]
        [SerializeField] private int slotsPerPage = 6;
        [SerializeField] private bool includeBasicAttack = true;
        [SerializeField] private bool wrapPages = true;
        [SerializeField] private int initialPage;

        [Header("Input")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private bool autoFindInputReader = true;
        [Tooltip("是否允许通过 Tab 热键切换技能页。默认关闭，避免与 Tab 功能菜单冲突。")]
        [SerializeField] private bool allowSwitchOnTab = false;
        [Tooltip("切换冷却时间，防止连按")]
        [SerializeField] private float switchCooldown = 0.2f;

        [Header("Pages")]
        [SerializeField] private List<SkillPage> pages = new List<SkillPage>();

        private int currentPage = -1;
        private readonly List<SkillDefinition> loadout = new List<SkillDefinition>(8);

        // 缓存组件引用，避免运行时 GetComponent
        private CooldownComponent cachedCooldown;
        // 上次切换时间，用于冷却判定
        private float lastSwitchTime;

        private void Reset()
        {
            skillUser = GetComponent<SkillUserComponent>();
        }

        private void Start()
        {
            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            // 缓存 CooldownComponent 引用
            if (skillUser != null)
            {
                cachedCooldown = skillUser.GetComponent<CooldownComponent>();
            }

            ApplyPage(Mathf.Clamp(initialPage, 0, Mathf.Max(0, pages.Count - 1)));
        }

        private void OnEnable()
        {
            if (autoFindInputReader && inputReader == null)
            {
                inputReader = FindFirstObjectByType<InputReader>();
            }

            if (inputReader != null)
            {
                inputReader.SwitchPage += HandleSwitchPage;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.SwitchPage -= HandleSwitchPage;
            }
        }

        private void HandleSwitchPage(int delta)
        {
            if (!allowSwitchOnTab)
            {
                return;
            }

            if (!UIRoot.IsGameplayInputAllowed())
            {
                return;
            }

            if (pages == null || pages.Count == 0)
            {
                return;
            }

            if (Time.time < lastSwitchTime + switchCooldown)
            {
                return;
            }

            lastSwitchTime = Time.time;
            SwitchPage(delta);
        }

        private void SwitchPage(int delta)
        {
            if (skillUser != null && skillUser.IsCasting)
            {
                return;
            }

            var targetPage = currentPage + delta;
            if (wrapPages && pages.Count > 0)
            {
                targetPage = (targetPage % pages.Count + pages.Count) % pages.Count;
            }
            else
            {
                targetPage = Mathf.Clamp(targetPage, 0, pages.Count - 1);
            }

            ApplyPage(targetPage);
        }

        private void ApplyPage(int pageIndex)
        {
            if (skillUser == null || pages == null || pages.Count == 0)
            {
                return;
            }

            pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1);
            if (pageIndex == currentPage)
            {
                return;
            }

            currentPage = pageIndex;
            var page = pages[currentPage];

            loadout.Clear();
            if (page != null && page.skills != null)
            {
                var maxPageSkills = Mathf.Max(0, slotsPerPage - (includeBasicAttack ? 1 : 0));
                for (int i = 0; i < page.skills.Count; i++)
                {
                    var skill = page.skills[i];
                    if (skill == null || loadout.Contains(skill))
                    {
                        continue;
                    }

                    loadout.Add(skill);
                    if (loadout.Count >= maxPageSkills)
                    {
                        break;
                    }
                }
            }

            skillUser.SetSkills(loadout, includeBasicAttack);
            if (skillBar != null)
            {
                skillBar.Bind(skillUser, cachedCooldown, slotsPerPage);
            }

            if (page != null && !string.IsNullOrEmpty(page.name))
            {
                Debug.Log($"[SkillPageSwitcher] Switched to page: {page.name}", this);
            }
        }

    }
}
