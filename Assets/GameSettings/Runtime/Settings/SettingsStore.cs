using System;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Loads and saves SettingsData as JSON; bad data falls back to defaults with exactly one warning (spec #4 §4).</summary>
    public sealed class SettingsStore
    {
        public const string UnreadableWarning = "[Settings] Pengaturan tersimpan tidak bisa dibaca; memakai nilai default.";
        public const string UnknownVersionWarningPrefix = "[Settings] Versi pengaturan tidak dikenal: ";
        public const string ClampedWarning = "[Settings] Beberapa pengaturan di luar rentang; nilainya disesuaikan.";

        readonly ISettingsBackend backend;

        public SettingsStore(ISettingsBackend backend) => this.backend = backend;

        public SettingsData Load()
        {
            string json = backend.Read();
            if (string.IsNullOrEmpty(json)) return new SettingsData();
            SettingsData loaded = null;
            try
            {
                loaded = JsonUtility.FromJson<SettingsData>(json);
            }
            catch (ArgumentException)
            {
            }
            if (loaded == null)
            {
                Debug.LogWarning(UnreadableWarning);
                return new SettingsData();
            }
            if (loaded.version != SettingsData.CurrentVersion)
            {
                Debug.LogWarning(UnknownVersionWarningPrefix + loaded.version.ToString(CultureInfo.InvariantCulture) + "; memakai nilai default.");
                return new SettingsData();
            }
            if (SettingsValidator.Sanitize(loaded)) Debug.LogWarning(ClampedWarning);
            return loaded;
        }

        public void Save(SettingsData settings) => backend.Write(JsonUtility.ToJson(settings));
    }
}
