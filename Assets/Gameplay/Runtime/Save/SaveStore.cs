using System;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>Where the save JSON lives (PlayerPrefs in the game, memory in tests).</summary>
    public interface ISaveBackend
    {
        /// <summary>The stored JSON, or an empty string when nothing was saved yet.</summary>
        string Read();

        void Write(string json);
    }

    /// <summary>Stores the save JSON under one PlayerPrefs key (spec #6 §2).</summary>
    public sealed class PlayerPrefsSaveBackend : ISaveBackend
    {
        public const string DefaultKey = "tyco.save";

        readonly string key;

        public PlayerPrefsSaveBackend(string key = DefaultKey) => this.key = key;

        public string Read() => PlayerPrefs.GetString(key, "");

        public void Write(string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }
    }

    public sealed class MemorySaveBackend : ISaveBackend
    {
        public string Json = "";

        public string Read() => Json;

        public void Write(string json) => Json = json;
    }

    /// <summary>Loads and saves SaveData; unreadable data or another version starts a new game with one warning (spec #6 §2).</summary>
    public sealed class SaveStore
    {
        public const string UnreadableWarning = "[Gameplay] Simpanan tidak bisa dibaca; mulai baru.";
        public const string UnknownVersionWarningPrefix = "[Gameplay] Versi simpanan tidak dikenal: ";

        readonly ISaveBackend backend;

        public SaveStore(ISaveBackend backend) => this.backend = backend;

        public bool HasSave => !string.IsNullOrEmpty(backend.Read());

        public SaveData Load()
        {
            string json = backend.Read();
            if (string.IsNullOrEmpty(json)) return new SaveData();
            SaveData loaded = null;
            try
            {
                loaded = JsonUtility.FromJson<SaveData>(json);
            }
            catch (ArgumentException)
            {
            }
            if (loaded == null)
            {
                Debug.LogWarning(UnreadableWarning);
                return new SaveData();
            }
            if (loaded.version != SaveData.CurrentVersion)
            {
                Debug.LogWarning(UnknownVersionWarningPrefix + loaded.version.ToString(CultureInfo.InvariantCulture) + "; mulai baru.");
                return new SaveData();
            }
            Sanitize(loaded);
            return loaded;
        }

        public void Save(SaveData data) => backend.Write(JsonUtility.ToJson(data));

        static void Sanitize(SaveData d)
        {
            d.money = Math.Max(0, d.money);
            d.lapar = Mathf.Clamp(d.lapar, 0f, Needs.Max);
            d.energi = Mathf.Clamp(d.energi, 0f, Needs.Max);
            d.day = Math.Max(1, d.day);
            d.hours = Mathf.Repeat(d.hours, 24f);
            d.itemIds = d.itemIds ?? new string[0];
            d.itemCounts = d.itemCounts ?? new int[0];
            if (d.itemCounts.Length != d.itemIds.Length)
            {
                d.itemIds = new string[0];
                d.itemCounts = new int[0];
            }
            d.wardrobe = d.wardrobe ?? new string[0];
            d.wearing = d.wearing ?? "";
            d.missionsDone = d.missionsDone ?? new string[0];
        }
    }
}
