using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CombatSystem.Persistence
{
    public static class SaveService
    {
        private const string SaveFolderName = "Saves";
        private const string SaveExtension = ".json";

        public static string SaveFolderPath => Path.Combine(Application.persistentDataPath, SaveFolderName);

        public static List<SaveSlotInfo> ListSlots()
        {
            EnsureSaveFolder();

            var slots = new List<SaveSlotInfo>(8);
            var files = Directory.GetFiles(SaveFolderPath, $"*{SaveExtension}");
            for (var i = 0; i < files.Length; i++)
            {
                var data = ReadSave(files[i]);
                if (data != null && data.slotInfo != null && !string.IsNullOrEmpty(data.slotInfo.slotId))
                {
                    slots.Add(data.slotInfo);
                }
            }

            slots.Sort((a, b) => b.lastSavedUtcTicks.CompareTo(a.lastSavedUtcTicks));
            return slots;
        }

        public static bool TryLoad(string slotId, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return false;
            }

            var path = GetSlotPath(slotId);
            if (!File.Exists(path))
            {
                return false;
            }

            data = ReadSave(path);
            return data != null;
        }

        public static void Save(SaveData data)
        {
            if (data == null || data.slotInfo == null || string.IsNullOrWhiteSpace(data.slotInfo.slotId))
            {
                return;
            }

            EnsureSaveFolder();
            var path = GetSlotPath(data.slotInfo.slotId);
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
        }

        public static void Delete(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return;
            }

            var path = GetSlotPath(slotId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static string CreateSlotId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static void EnsureSaveFolder()
        {
            if (!Directory.Exists(SaveFolderPath))
            {
                Directory.CreateDirectory(SaveFolderPath);
            }
        }

        private static string GetSlotPath(string slotId)
        {
            var safeId = SanitizeSlotId(slotId);
            return Path.Combine(SaveFolderPath, $"{safeId}{SaveExtension}");
        }

        private static string SanitizeSlotId(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return "slot";
            }

            var chars = slotId.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static SaveData ReadSave(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }

                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
