using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// "Panas" (spec #6 §2 Wanted): 0-3 stars. A seen theft adds stars; while warga keep seeing you the heat holds,
    /// and each <see cref="CoolSeconds"/> unseen takes one star off. Pure.
    /// </summary>
    public sealed class HeatModel
    {
        public const int Max = 3;
        public const float CoolSeconds = 25f;

        float unseen;

        public int Level { get; private set; }
        public float Unseen => unseen;
        public bool IsHot => Level > 0;

        public void Crime(int stars)
        {
            if (stars <= 0) return;
            Level = Mathf.Min(Max, Level + stars);
            unseen = 0f;
        }

        public void Tick(float dt, bool seen)
        {
            if (Level == 0) return;
            if (seen)
            {
                unseen = 0f;
                return;
            }
            unseen += dt;
            if (unseen < CoolSeconds) return;
            Level--;
            unseen = 0f;
        }

        public void Clear()
        {
            Level = 0;
            unseen = 0f;
        }
    }
}
