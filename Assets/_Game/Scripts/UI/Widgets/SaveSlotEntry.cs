using System;
using CombatSystem.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class SaveSlotEntry : MonoBehaviour
    {
        [SerializeField] private Button loadButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Text detailText;

        private SaveSlotInfo slotInfo;

        public void Bind(SaveSlotInfo info, Action<SaveSlotInfo> onLoad, Action<SaveSlotInfo> onDelete)
        {
            slotInfo = info;

            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(info.displayName) ? info.slotId : info.displayName;
            }

            if (detailText != null)
            {
                detailText.text = FormatDetail(info);
            }

            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                if (onLoad != null)
                {
                    loadButton.onClick.AddListener(() => onLoad(slotInfo));
                }
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                if (onDelete != null)
                {
                    deleteButton.onClick.AddListener(() => onDelete(slotInfo));
                }
            }
        }

        private static string FormatDetail(SaveSlotInfo info)
        {
            if (info == null)
            {
                return string.Empty;
            }

            var time = info.lastSavedUtcTicks > 0
                ? new DateTime(info.lastSavedUtcTicks, DateTimeKind.Utc).ToLocalTime()
                : DateTime.MinValue;
            var timeText = time == DateTime.MinValue ? "Unknown Time" : time.ToString("yyyy-MM-dd HH:mm");
            var sceneText = string.IsNullOrEmpty(info.sceneName) ? "Unknown Scene" : info.sceneName;
            return $"{sceneText} | {timeText}";
        }
    }
}
