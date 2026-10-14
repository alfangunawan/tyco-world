using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Arcade bike motion (spec #5 §4): signed speed, yaw rate and visual lean from one intent per step.</summary>
    public sealed class BikeModel
    {
        const float DeadZone = 0.01f;

        readonly BikeTuning t;

        public BikeModel(BikeTuning tuning) => t = tuning;

        /// <summary>Metres per second; positive is forward.</summary>
        public float Speed { get; private set; }
        public float SpeedKmh => Speed * BikeTuning.KmhPerMs;
        /// <summary>Visual lean in degrees; positive leans right.</summary>
        public float Lean { get; private set; }
        public bool Braking { get; private set; }

        public void Reset()
        {
            Speed = 0f;
            Lean = 0f;
            Braking = false;
        }

        public void Stop() => Speed = 0f;

        public void Scale(float factor) => Speed *= factor;

        public void Step(VehicleIntent intent, float dt)
        {
            if (dt <= 0f) return;
            float throttle = Mathf.Clamp(intent.Throttle, -1f, 1f);
            float v = Speed;
            Braking = false;
            if (intent.Brake)
            {
                Braking = Mathf.Abs(v) > DeadZone;
                v = Mathf.MoveTowards(v, 0f, t.hardBrakeDecel * dt);
            }
            else if (throttle > DeadZone)
            {
                if (v < -DeadZone)
                {
                    Braking = true;
                    v = Mathf.MoveTowards(v, 0f, t.brakeDecel * dt);
                }
                else v = Mathf.Min(t.maxForwardSpeed, v + Acceleration(v) * throttle * dt);
            }
            else if (throttle < -DeadZone)
            {
                if (v > t.reverseThreshold)
                {
                    Braking = true;
                    v = Mathf.MoveTowards(v, 0f, t.brakeDecel * dt);
                }
                else v = Mathf.Max(-t.maxReverseSpeed, v - t.reverseAccel * -throttle * dt);
            }
            else v = Mathf.MoveTowards(v, 0f, t.coastDecel * dt);
            Speed = v;
            float target = Mathf.Clamp(intent.Steer, -1f, 1f) * t.maxLean * Mathf.Clamp01(Mathf.Abs(v) / t.fullLeanSpeed);
            Lean = Mathf.Lerp(Lean, target, 1f - Mathf.Exp(-t.leanSmoothing * dt));
        }

        /// <summary>Degrees per second for <paramref name="steer"/> at the current speed: no turning in place, flipped when reversing.</summary>
        public float YawRate(float steer)
        {
            float speed = Mathf.Abs(Speed);
            if (speed < 1e-4f) return 0f;
            float rate = Mathf.Lerp(t.yawRateLow, t.yawRateHigh, Mathf.Clamp01(speed / t.maxForwardSpeed));
            return Mathf.Clamp(steer, -1f, 1f) * rate * Mathf.Clamp01(speed / t.turnInSpeed) * Mathf.Sign(Speed);
        }

        float Acceleration(float v) => Mathf.Lerp(t.accelAtRest, t.accelAtMax, Mathf.Clamp01(v / t.maxForwardSpeed));
    }
}
