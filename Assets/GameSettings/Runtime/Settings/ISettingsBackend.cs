namespace TycoWorld.GameSettings
{
    /// <summary>Where the settings JSON lives (PlayerPrefs in the game, memory in tests).</summary>
    public interface ISettingsBackend
    {
        /// <summary>The stored JSON, or an empty string when nothing was saved yet.</summary>
        string Read();

        void Write(string json);
    }
}
