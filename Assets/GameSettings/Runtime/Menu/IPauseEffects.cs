namespace TycoWorld.GameSettings
{
    /// <summary>The global side effects of pausing; tests use a fake so Time.timeScale is never touched in EditMode.</summary>
    public interface IPauseEffects
    {
        void SetPaused(bool paused);
    }
}
