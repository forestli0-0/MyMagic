using System.Collections.Generic;
using CombatSystem.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.UI
{
    public class SaveSelectScreen : UIScreenBase
    {
        [Header("Navigation")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UIScreenBase mainMenuScreen;
        [SerializeField] private UIScreenBase inGameScreen;

        [Header("Save UI")]
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private SaveSlotEntry slotTemplate;
        [SerializeField] private InputField saveNameInput;
        [SerializeField] private Text emptyLabel;
        [SerializeField] private Button newSaveButton;

        [Header("Services")]
        [SerializeField] private SaveGameManager saveManager;

        private readonly List<SaveSlotEntry> entries = new List<SaveSlotEntry>(8);

        private void Reset()
        {
            inputMode = UIInputMode.UI;
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        public override void OnEnter()
        {
            if (uiManager != null)
            {
                uiManager.SetHudVisible(false);
            }

            EnsureManager();
            RefreshList();
        }

        public void BackToMenu()
        {
            if (uiManager != null && mainMenuScreen != null)
            {
                uiManager.ShowScreen(mainMenuScreen, true);
            }
        }

        public void StartGame()
        {
            if (uiManager != null && inGameScreen != null)
            {
                uiManager.ShowScreen(inGameScreen, true);
            }
        }

        public void CreateNewSave()
        {
            if (saveManager == null)
            {
                return;
            }

            var name = saveNameInput != null ? saveNameInput.text : string.Empty;
            saveManager.SaveNew(name);
            RefreshList();
            StartGame();
        }

        private void RefreshList()
        {
            if (slotContainer == null || slotTemplate == null || saveManager == null)
            {
                return;
            }

            var slots = saveManager.GetSlots();
            EnsureEntryCount(slots.Count);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (i < slots.Count)
                {
                    entry.gameObject.SetActive(true);
                    entry.Bind(slots[i], HandleLoad, HandleDelete);
                }
                else
                {
                    entry.gameObject.SetActive(false);
                }
            }

            if (emptyLabel != null)
            {
                emptyLabel.gameObject.SetActive(slots.Count == 0);
            }
        }

        private void HandleLoad(SaveSlotInfo info)
        {
            if (info == null || saveManager == null)
            {
                return;
            }

            if (saveManager.LoadSlot(info.slotId))
            {
                StartGame();
            }
        }

        private void HandleDelete(SaveSlotInfo info)
        {
            if (info == null || saveManager == null)
            {
                return;
            }

            saveManager.DeleteSlot(info.slotId);
            RefreshList();
        }

        private void EnsureManager()
        {
            if (saveManager == null)
            {
                saveManager = FindFirstObjectByType<SaveGameManager>();
            }

            if (newSaveButton != null)
            {
                if (newSaveButton.onClick.GetPersistentEventCount() == 0)
                {
                    newSaveButton.onClick.RemoveListener(CreateNewSave);
                    newSaveButton.onClick.AddListener(CreateNewSave);
                }
            }
        }

        private void EnsureEntryCount(int count)
        {
            while (entries.Count < count)
            {
                var instance = Instantiate(slotTemplate, slotContainer);
                instance.gameObject.SetActive(false);
                entries.Add(instance);
            }
        }

        public override string GetFooterHintText()
        {
            return "{BACK} 返回主菜单    {CONFIRM} 读取存档    输入存档名后可新建";
        }
    }
}
