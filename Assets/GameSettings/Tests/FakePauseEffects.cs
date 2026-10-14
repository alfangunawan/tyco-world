namespace TycoWorld.GameSettings.Tests
{
    /// <summary>Records pause side effects instead of touching Time.timeScale (EditMode tests must never change it).</summary>
    internal sealed class FakePauseEffects : IPauseEffects
    {
        public bool Paused;
        public int Calls;

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Calls++;
        }
    }
}
