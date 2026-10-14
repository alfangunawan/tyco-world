using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Pure camera poses while riding (spec #5 §4): a chase orbit around a pivot above the bike, and the rider's head.</summary>
    public static class RideCameraMath
    {
        /// <summary>What the chase camera looks at: lookHeight above the bike root (the ground under the bike).</summary>
        public static Vector3 ChasePivot(Vector3 bikeRoot, BikeTuning t) => bikeRoot + Vector3.up * t.lookHeight;

        /// <summary>Elevation (degrees) of the default chase position (chaseDistance behind, chaseHeight up) seen from the pivot.</summary>
        public static float DefaultElevation(BikeTuning t) => Mathf.Atan2(t.chaseHeight - t.lookHeight, t.chaseDistance) * Mathf.Rad2Deg;

        public static float ChaseRadius(BikeTuning t)
        {
            float up = t.chaseHeight - t.lookHeight;
            return Mathf.Sqrt(t.chaseDistance * t.chaseDistance + up * up);
        }

        /// <summary>Chase position for a follow yaw plus orbit offsets; yaw within ±orbitYawLimit, elevation within [orbitPitchMin, orbitPitchMax].</summary>
        public static Vector3 ChasePosition(Vector3 pivot, float followYaw, float orbitYaw, float orbitPitch, BikeTuning t)
        {
            float elevation = Mathf.Clamp(DefaultElevation(t) + orbitPitch, t.orbitPitchMin, t.orbitPitchMax);
            float yaw = followYaw + Mathf.Clamp(orbitYaw, -t.orbitYawLimit, t.orbitYawLimit);
            return pivot + Quaternion.Euler(elevation, yaw, 0f) * new Vector3(0f, 0f, -ChaseRadius(t));
        }

        public static Quaternion LookAt(Vector3 from, Vector3 target) => Quaternion.LookRotation(target - from, Vector3.up);

        public static Vector3 HeadPosition(Vector3 seat, float bikeYaw, BikeTuning t) =>
            seat + Quaternion.Euler(0f, bikeYaw, 0f) * new Vector3(0f, t.headHeight, -t.headBack);

        /// <summary>Head rotation: bike yaw, then rollFactor × lean, then the clamped look (positive pitch looks up).</summary>
        public static Quaternion HeadRotation(float bikeYaw, float lean, float lookYaw, float lookPitch, BikeTuning t)
        {
            float yaw = Mathf.Clamp(lookYaw, -t.lookYawLimit, t.lookYawLimit);
            float pitch = Mathf.Clamp(lookPitch, -t.lookPitchLimit, t.lookPitchLimit);
            return Quaternion.Euler(0f, bikeYaw, 0f) * Quaternion.Euler(0f, 0f, -lean * t.rollFactor) * Quaternion.Euler(-pitch, yaw, 0f);
        }
    }
}
