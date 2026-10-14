using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Stores the settings JSON under one PlayerPrefs key (spec #4 §4).</summary>
    public sealed class PlayerPrefsBackend : ISettingsBackend
    {
        public const string DefaultKey = "tyco.settings";

        readonly string key;

        public PlayerPrefsBackend(string key = DefaultKey) => this.key = key;

        public string Read() => PlayerPrefs.GetString(key, "");

        public void Write(string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }
    }
}
