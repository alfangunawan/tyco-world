namespace TycoWorld.GameSettings
{
    /// <summary>Shadow values of one quality tier (spec #4 §4).</summary>
    public readonly struct ShadowTier
    {
        public ShadowTier(bool directional, float distance, int cascades, int resolution, bool soft)
        {
            Directional = directional;
            Distance = distance;
            Cascades = cascades;
            Resolution = resolution;
            Soft = soft;
        }

        /// <summary>False: the sun and moon cast no shadows. The flashlight (an additional light) keeps its own.</summary>
        public bool Directional { get; }
        public float Distance { get; }
        public int Cascades { get; }
        public int Resolution { get; }
        public bool Soft { get; }
    }

    public static class ShadowTiers
    {
        public static ShadowTier For(ShadowQuality quality)
        {
            switch (quality)
            {
                case ShadowQuality.Off: return new ShadowTier(false, 25f, 1, 1024, false);
                case ShadowQuality.Low: return new ShadowTier(true, 30f, 1, 1024, false);
                case ShadowQuality.High: return new ShadowTier(true, 80f, 4, 2048, true);
                default: return new ShadowTier(true, 50f, 2, 2048, true);
            }
        }
    }
}
