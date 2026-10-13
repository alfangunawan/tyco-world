namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Deterministic PRNG (SplitMix64). All generator randomness must come from here.</summary>
    public sealed class SplitMix64
    {
        ulong state;

        public SplitMix64(ulong seed) { state = seed; }

        public static SplitMix64 For(long id, string salt) => new SplitMix64(unchecked((ulong)id) ^ Fnv1a64(salt));

        public static ulong Hash(long id, string salt) => For(id, salt).NextULong();

        public static int Pick(long id, string salt, int count) =>
            count <= 0 ? -1 : (int)(Hash(id, salt) % (ulong)count);

        public static ulong Fnv1a64(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                if (text != null)
                    foreach (char c in text)
                    {
                        hash ^= c;
                        hash *= 1099511628211UL;
                    }
                return hash;
            }
        }

        public ulong NextULong()
        {
            unchecked
            {
                ulong z = state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public float Range(float min, float max) => (float)(min + (max - min) * NextDouble());

        public int RangeInclusive(int min, int max) => min + (int)(NextULong() % (ulong)(max - min + 1));
    }
}
