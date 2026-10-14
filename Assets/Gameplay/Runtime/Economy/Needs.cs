using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Lapar (hunger: 100 = full) and Energi (energy) on a 0..100 scale, draining with game time (spec #6 §2). Hungry
    /// below 15 blocks sprinting; Energi 0 means the player faints.
    /// </summary>
    public sealed class Needs
    {
        public const float Max = 100f;
        public const float HungerPerHour = 6f;
        public const float EnergyPerHour = 4.5f;
        /// <summary>Extra energy drain per hour at night without a jacket (cold).</summary>
        public const float NightChillPerHour = 1.5f;
        public const float StarvingBelow = 15f;
        public const float LowWarning = 25f;

        public float Lapar { get; private set; } = 80f;
        public float Energi { get; private set; } = 90f;

        public bool IsStarving => Lapar < StarvingBelow;
        public bool IsExhausted => Energi <= 0f;

        public void Set(float lapar, float energi)
        {
            Lapar = Mathf.Clamp(lapar, 0f, Max);
            Energi = Mathf.Clamp(energi, 0f, Max);
        }

        /// <summary>Drains for <paramref name="gameHours"/> of game time; sprinting doubles the energy drain.</summary>
        public void Tick(float gameHours, bool night, bool warm, bool sprinting)
        {
            if (gameHours <= 0f) return;
            float energy = EnergyPerHour * (sprinting ? 2f : 1f) + (night && !warm ? NightChillPerHour : 0f);
            Set(Lapar - HungerPerHour * gameHours, Energi - energy * gameHours);
        }

        public void Restore(float lapar, float energi) => Set(Lapar + lapar, Energi + energi);
    }
}
