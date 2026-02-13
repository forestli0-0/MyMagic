using System;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class QuestJournalEntryUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Color normalColor = new Color(0.14f, 0.17f, 0.22f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.26f, 0.34f, 0.46f, 1f);

        private string questId;

        public string QuestId => questId;

        public void Bind(QuestDefinition definition, QuestRuntimeState state, bool selected, Action<QuestJournalEntryUI> onClick)
        {
            questId = definition != null ? definition.Id : string.Empty;

            if (titleText != null)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    titleText.text = definition != null ? definition.Id : "Unknown Quest";
                }
                else
                {
                    titleText.text = definition.DisplayName;
                }
            }

            if (statusText != null)
            {
                statusText.text = ResolveStatusText(state);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null)
                {
                    button.onClick.AddListener(() => onClick(this));
                }
            }

            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (background != null)
            {
                background.color = selected ? selectedColor : normalColor;
            }
        }

        private static string ResolveStatusText(QuestRuntimeState state)
        {
            if (state == null)
            {
                return "未接取";
            }

            switch (state.Status)
            {
                case QuestStatus.InProgress:
                    return "进行中";
                case QuestStatus.ReadyToTurnIn:
                    return "可提交";
                case QuestStatus.Completed:
                    return "已完成";
                default:
                    return "未接取";
            }
        }
    }
}
