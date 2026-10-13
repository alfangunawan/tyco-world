namespace TycoWorld.Player
{
    /// <summary>Optional player part (footsteps, interaction, flashlight, HUD) ticked by FirstPersonPlayer after movement, by ascending Order.</summary>
    public interface IPlayerModule
    {
        int Order { get; }
        void Initialize(FirstPersonPlayer player);
        void Tick(FirstPersonPlayer player, float dt);
    }
}
