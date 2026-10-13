using UnityEngine;

namespace TycoWorld.DayCycle
{
    public enum LampRole { Normal, Flicker, Dead }

    /// <summary>Deterministic per-lamp horror behaviour: the same lamps flicker or die every night.</summary>
    public static class LampFlicker
    {
        public const float SegmentSeconds = 0.35f;

        public static uint Seed(Vector3 position)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)Mathf.RoundToInt(position.x * 10f)) * 16777619u;
                h = (h ^ (uint)Mathf.RoundToInt(position.z * 10f)) * 16777619u;
                return Avalanche(h);
            }
        }

        public static LampRole RoleFor(uint seed, float flickerFraction, float deadFraction)
        {
            float u = Unit(seed);
            if (u < deadFraction) return LampRole.Dead;
            return u < deadFraction + flickerFraction ? LampRole.Flicker : LampRole.Normal;
        }

        /// <summary>Half of the 0.35 s segments contain one 0.05–0.3 s dark gap that never touches a segment edge.</summary>
        public static bool IsLit(uint seed, float timeSeconds)
        {
            int segment = Mathf.FloorToInt(timeSeconds / SegmentSeconds);
            uint s = Avalanche(unchecked(seed ^ ((uint)segment * 0x9E3779B9u)));
            if ((s & 1u) == 0u) return true;
            float offLength = 0.05f + 0.25f * Unit(s >> 1);
            float start = 0.02f + (SegmentSeconds - offLength - 0.04f) * Unit(Avalanche(s));
            float local = timeSeconds - segment * SegmentSeconds;
            return local < start || local >= start + offLength;
        }

        static uint Avalanche(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return h;
            }
        }

        static float Unit(uint h) => (h & 0xFFFFFFu) / 16777216f;
    }
}
