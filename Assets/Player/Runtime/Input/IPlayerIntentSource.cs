namespace TycoWorld.Player
{
    public interface IPlayerIntentSource
    {
        PlayerIntent Read(float dt);
    }
}
