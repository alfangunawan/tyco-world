using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Engine loop pitch and volume from speed (0..1 of top speed) and throttle (spec #5 §4).</summary>
    public static class EngineSound
    {
        public static float Pitch(float speed01, float throttle, BikeTuning t) =>
            Mathf.Lerp(t.idlePitch, t.maxPitch, Mathf.Clamp01(speed01)) + t.throttlePitch * Mathf.Clamp01(throttle);

        public static float Volume(float speed01, BikeTuning t) => Mathf.Lerp(t.idleVolume, t.maxVolume, Mathf.Clamp01(speed01));
    }
}
