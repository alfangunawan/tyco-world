using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Distance-based footsteps: one step per stride, two steps per bob cycle; detects hard landings.</summary>
    public sealed class StepCycle
    {
        readonly float landingMinSpeed;
        float cycleDistance;
        bool airborne;
        float peakFallSpeed;

        public StepCycle(float landingMinSpeed) { this.landingMinSpeed = landingMinSpeed; }

        public float CyclePhase01 { get; private set; }
        public int StepCount { get; private set; }

        public int Advance(float horizontalSpeed, bool grounded, float stride, float dt)
        {
            if (!grounded || stride <= 0f || horizontalSpeed <= 0f) return 0;
            float before = cycleDistance;
            cycleDistance += horizontalSpeed * dt;
            int steps = Mathf.FloorToInt(cycleDistance / stride) - Mathf.FloorToInt(before / stride);
            float cycle = stride * 2f;
            cycleDistance %= cycle;
            CyclePhase01 = cycleDistance / cycle;
            StepCount += steps;
            return steps;
        }

        /// <summary>True on the first grounded frame after falling at least <c>landingMinSpeed</c>.</summary>
        public bool UpdateLanding(bool grounded, float verticalVelocity, out float impactSpeed)
        {
            impactSpeed = 0f;
            if (!grounded)
            {
                airborne = true;
                peakFallSpeed = Mathf.Max(peakFallSpeed, -verticalVelocity);
                return false;
            }
            if (!airborne) return false;
            airborne = false;
            float peak = peakFallSpeed;
            peakFallSpeed = 0f;
            if (peak < landingMinSpeed) return false;
            impactSpeed = peak;
            return true;
        }
    }
}
