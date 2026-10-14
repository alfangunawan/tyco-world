using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// Camera state while riding: third-person chase with mouse orbit that drifts back behind the bike, or the
    /// rider's head with a limited look; the chase position is pulled in so it never goes through walls (spec #5 §4).
    /// </summary>
    public sealed class RideCamera
    {
        readonly BikeTuning t;
        float idle;

        public RideCamera(BikeTuning tuning) => t = tuning;

        public RideCameraMode Mode { get; private set; } = RideCameraMode.ThirdPerson;
        public float FollowYaw { get; private set; }
        public float OrbitYaw { get; private set; }
        /// <summary>Offset (degrees) added to the default chase elevation.</summary>
        public float OrbitPitch { get; private set; }
        public float LookYaw { get; private set; }
        public float LookPitch { get; private set; }

        public void Reset(float bikeYaw)
        {
            FollowYaw = bikeYaw;
            ClearOffsets();
        }

        public void Toggle()
        {
            Mode = Mode == RideCameraMode.ThirdPerson ? RideCameraMode.FirstPerson : RideCameraMode.ThirdPerson;
            ClearOffsets();
        }

        public void Update(Vector2 lookDegrees, float bikeYaw, float speed, float dt)
        {
            FollowYaw = Mathf.LerpAngle(FollowYaw, bikeYaw, 1f - Mathf.Exp(-t.followSmoothing * dt));
            if (Mode == RideCameraMode.FirstPerson)
            {
                LookYaw = Mathf.Clamp(LookYaw + lookDegrees.x, -t.lookYawLimit, t.lookYawLimit);
                LookPitch = Mathf.Clamp(LookPitch + lookDegrees.y, -t.lookPitchLimit, t.lookPitchLimit);
                return;
            }
            float baseElevation = RideCameraMath.DefaultElevation(t);
            OrbitYaw = Mathf.Clamp(OrbitYaw + lookDegrees.x, -t.orbitYawLimit, t.orbitYawLimit);
            OrbitPitch = Mathf.Clamp(OrbitPitch - lookDegrees.y, t.orbitPitchMin - baseElevation, t.orbitPitchMax - baseElevation);
            idle = lookDegrees.sqrMagnitude > 1e-6f ? 0f : idle + dt;
            if (idle >= t.returnDelay && Mathf.Abs(speed) > t.returnMinSpeed)
            {
                float k = 1f - Mathf.Exp(-t.returnSpeed * dt);
                OrbitYaw = Mathf.Lerp(OrbitYaw, 0f, k);
                OrbitPitch = Mathf.Lerp(OrbitPitch, 0f, k);
            }
        }

        public void PoseThirdPerson(Vector3 bikeRoot, out Vector3 position, out Quaternion rotation)
        {
            var pivot = RideCameraMath.ChasePivot(bikeRoot, t);
            var desired = RideCameraMath.ChasePosition(pivot, FollowYaw, OrbitYaw, OrbitPitch, t);
            position = PullIn(pivot, desired, t.cameraRadius);
            rotation = RideCameraMath.LookAt(position, pivot);
        }

        public void PoseFirstPerson(Vector3 seat, float bikeYaw, float lean, out Vector3 position, out Quaternion rotation)
        {
            position = RideCameraMath.HeadPosition(seat, bikeYaw, t);
            rotation = RideCameraMath.HeadRotation(bikeYaw, lean, LookYaw, LookPitch, t);
        }

        /// <summary>Moves the camera towards the pivot when something stands between them (sphere cast), keeping at least 0.3 m.</summary>
        public static Vector3 PullIn(Vector3 pivot, Vector3 desired, float radius)
        {
            var offset = desired - pivot;
            float distance = offset.magnitude;
            if (distance < 1e-4f) return desired;
            var direction = offset / distance;
            if (Physics.SphereCast(pivot, radius, direction, out var hit, distance, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                return pivot + direction * Mathf.Max(hit.distance - 0.1f, 0.3f);
            return desired;
        }

        void ClearOffsets()
        {
            OrbitYaw = 0f;
            OrbitPitch = 0f;
            LookYaw = 0f;
            LookPitch = 0f;
            idle = 0f;
        }
    }
}
