namespace TycoWorld.Player
{
    /// <summary>Short text feedback to the player (HUD). seconds ≤ 0 uses the tuning default.</summary>
    public interface IPlayerFeedback
    {
        void ShowMessage(string text, float seconds = -1f);
    }
}
