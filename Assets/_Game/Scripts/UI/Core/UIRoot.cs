using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.UI
{
    public class UIRoot : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private Canvas screensCanvas;
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Canvas modalCanvas;
        [SerializeField] private Canvas overlayCanvas;

        [Header("Settings")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Manager")]
        [SerializeField] private UIManager uiManager;

        private static UIRoot instance;

        public static UIRoot Instance => instance;
        public Canvas ScreensCanvas => screensCanvas;
        public Canvas HudCanvas => hudCanvas;
        public Canvas ModalCanvas => modalCanvas;
        public Canvas OverlayCanvas => overlayCanvas;
        public UIManager Manager => uiManager;
        public static bool IsGameplayInputAllowed()
        {
            if (instance == null || instance.uiManager == null)
            {
                return true;
            }

            return instance.uiManager.CurrentInputMode == UIInputMode.Gameplay;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                instance.MergeUniqueUiFrom(this);
                Destroy(gameObject);
                return;
            }

            instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (uiManager == null)
            {
                uiManager = GetComponentInChildren<UIManager>();
                if (uiManager == null)
                {
                    uiManager = gameObject.AddComponent<UIManager>();
                }
            }

            uiManager.Initialize(this);
            EnsureQuestJournalHotkey();
        }

        private void MergeUniqueUiFrom(UIRoot incoming)
        {
            if (incoming == null)
            {
                return;
            }

            MoveUniqueChildrenByComponentType<UIScreenBase>(incoming.screensCanvas, screensCanvas);
            MoveUniqueChildrenByComponentType<UIModalBase>(incoming.modalCanvas, modalCanvas);
            MoveUniqueChildrenByComponentType<UIOverlayBase>(incoming.overlayCanvas, overlayCanvas);
            MoveMissingDirectChildrenByName(incoming.hudCanvas, hudCanvas);
            EnsureQuestJournalHotkey();
        }

        private static void MoveUniqueChildrenByComponentType<T>(Canvas fromCanvas, Canvas toCanvas) where T : Component
        {
            if (fromCanvas == null || toCanvas == null)
            {
                return;
            }

            var targetRoot = toCanvas.transform;
            var sourceRoot = fromCanvas.transform;
            if (targetRoot == null || sourceRoot == null)
            {
                return;
            }

            var existing = targetRoot.GetComponentsInChildren<T>(true);
            var existingTypes = new HashSet<System.Type>();
            for (int i = 0; i < existing.Length; i++)
            {
                var component = existing[i];
                if (component != null)
                {
                    existingTypes.Add(component.GetType());
                }
            }

            var incoming = sourceRoot.GetComponentsInChildren<T>(true);
            for (int i = 0; i < incoming.Length; i++)
            {
                var component = incoming[i];
                if (component == null)
                {
                    continue;
                }

                var type = component.GetType();
                if (existingTypes.Contains(type))
                {
                    continue;
                }

                component.transform.SetParent(targetRoot, false);
                existingTypes.Add(type);
            }
        }

        private static void MoveMissingDirectChildrenByName(Canvas fromCanvas, Canvas toCanvas)
        {
            if (fromCanvas == null || toCanvas == null)
            {
                return;
            }

            var sourceRoot = fromCanvas.transform;
            var targetRoot = toCanvas.transform;
            if (sourceRoot == null || targetRoot == null)
            {
                return;
            }

            var incomingChildren = new List<Transform>(sourceRoot.childCount);
            for (int i = 0; i < sourceRoot.childCount; i++)
            {
                incomingChildren.Add(sourceRoot.GetChild(i));
            }

            for (int i = 0; i < incomingChildren.Count; i++)
            {
                var child = incomingChildren[i];
                if (child == null)
                {
                    continue;
                }

                if (targetRoot.Find(child.name) != null)
                {
                    continue;
                }

                child.SetParent(targetRoot, false);
            }
        }

        private void EnsureQuestJournalHotkey()
        {
            if (GetComponent<QuestJournalHotkey>() != null)
            {
                return;
            }

            gameObject.AddComponent<QuestJournalHotkey>();
        }
    }
}
