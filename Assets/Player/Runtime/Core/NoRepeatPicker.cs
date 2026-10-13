namespace TycoWorld.Player
{
    /// <summary>Deterministic xorshift picker that never returns the same index twice in a row.</summary>
    public struct NoRepeatPicker
    {
        uint state;
        int last;

        public NoRepeatPicker(uint seed)
        {
            state = seed == 0u ? 0x9E3779B9u : seed;
            last = -1;
        }

        uint NextUInt()
        {
            if (state == 0u) state = 0x9E3779B9u;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public int Next(int count)
        {
            if (count <= 0) return -1;
            if (count == 1)
            {
                last = 0;
                return 0;
            }
            bool avoid = last >= 0 && last < count;
            int pick = (int)(NextUInt() % (uint)(avoid ? count - 1 : count));
            if (avoid && pick >= last) pick++;
            last = pick;
            return pick;
        }

        public float Range(float min, float max) => min + (max - min) * (NextUInt() / (float)uint.MaxValue);
    }
}
