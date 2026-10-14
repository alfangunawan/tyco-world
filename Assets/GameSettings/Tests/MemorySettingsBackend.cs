namespace TycoWorld.GameSettings.Tests
{
    /// <summary>In-memory settings storage for tests.</summary>
    internal sealed class MemorySettingsBackend : ISettingsBackend
    {
        public string Json = "";
        public int Writes;

        public string Read() => Json;

        public void Write(string json)
        {
            Json = json;
            Writes++;
        }
    }
}
