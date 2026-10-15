namespace TycoWorld.Gameplay
{
    /// <summary>SplitMix64 for runtime choices (looks, spawn picks): the same seed always gives the same sequence.</summary>
    public struct SeededRandom
    {
        ulong state;

        public SeededRandom(ulong seed) => state = seed;

        public ulong NextULong()
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public float Next01() => (NextULong() >> 40) * (1f / (1UL << 24));

        /// <summary>An int in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextULong() % (ulong)(max - min));

        public float Range(float min, float max) => min + (max - min) * Next01();

        public static ulong Mix(ulong a, ulong b)
        {
            var r = new SeededRandom(a ^ (b * 0xD6E8FEB86659FD93UL));
            return r.NextULong();
        }

        public static ulong Hash(string text)
        {
            ulong h = 1469598103934665603UL;
            if (text != null)
                foreach (char c in text)
                {
                    h ^= c;
                    h *= 1099511628211UL;
                }
            return h;
        }
    }
}
