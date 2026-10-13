using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Sprint stamina: drains while sprinting and moving, regenerates after a delay, locks out when empty until a threshold.</summary>
    public sealed class StaminaModel
    {
        readonly StaminaTuning t;
        float current;
        float sinceSpend;

        public StaminaModel(StaminaTuning tuning)
        {
            t = tuning;
            current = t.max;
            sinceSpend = t.regenDelay;
        }

        public float Normalized => t.max > 0f ? current / t.max : 0f;
        public bool Exhausted { get; private set; }
        public bool CanSprint => !Exhausted && current > 0f;

        public void Tick(bool sprintingAndMoving, float dt)
        {
            if (sprintingAndMoving && CanSprint)
            {
                current = Mathf.Max(0f, current - t.sprintDrainPerSecond * dt);
                sinceSpend = 0f;
                if (current <= 0f) Exhausted = true;
            }
            else
            {
                sinceSpend += dt;
                if (sinceSpend >= t.regenDelay) current = Mathf.Min(t.max, current + t.regenPerSecond * dt);
            }
            if (Exhausted && Normalized >= t.recoverThreshold) Exhausted = false;
        }

        public bool TrySpend(float amount)
        {
            if (Exhausted || current < amount) return false;
            current -= amount;
            sinceSpend = 0f;
            if (current <= 0f) Exhausted = true;
            return true;
        }

        public void Refill()
        {
            current = t.max;
            Exhausted = false;
            sinceSpend = t.regenDelay;
        }
    }
}
