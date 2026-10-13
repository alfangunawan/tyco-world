namespace TycoWorld.Player
{
    /// <summary>The player lives on the built-in "Ignore Raycast" layer so its own queries never hit itself.</summary>
    public static class PlayerLayers
    {
        public const int Player = 2;

        /// <summary>Layer mask for every player query (ground, headroom, interaction, spawn).</summary>
        public static int QueryMask => ~(1 << Player);
    }
}
