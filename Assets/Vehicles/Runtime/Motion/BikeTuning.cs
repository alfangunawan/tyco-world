using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Every tunable number of the motorbike (spec #5 §4). Defaults live only here.</summary>
    public sealed class BikeTuning : ScriptableObject
    {
        public const float KmhPerMs = 3.6f;

        [Header("Speed")]
        public float maxForwardSpeed = 16.67f;
        public float maxReverseSpeed = 2.5f;

        [Header("Throttle and brakes")]
        public float accelAtRest = 4f;
        public float accelAtMax = 1.5f;
        public float brakeDecel = 7f;
        public float hardBrakeDecel = 10f;
        public float coastDecel = 0.8f;
        public float reverseThreshold = 0.3f;
        public float reverseAccel = 2f;

        [Header("Steering")]
        public float yawRateLow = 90f;
        public float yawRateHigh = 35f;
        public float turnInSpeed = 1.5f;
        public float maxSteerAngle = 25f;

        [Header("Kind")]
        [Tooltip("A car: sit inside (no body shown, no pillion), third-person camera only (spec vehicles-3a §5).")]
        public bool isCar;

        [Header("Lean (visual only)")]
        public float maxLean = 25f;
        public float fullLeanSpeed = 8f;
        public float leanSmoothing = 6f;

        [Header("Body")]
        public float gravity = 20f;
        public float slopeLimit = 35f;
        public float stepOffset = 0.3f;
        public float bodyRadius = 0.5f;
        public float bodyHeight = 1.4f;
        public Vector3 blockHalfExtents = new Vector3(0.35f, 0.5f, 0.95f);
        public float headOnDot = 0.5f;
        public float bumpSpeed = 2.8f;
        public float glanceSpeedFactor = 0.8f;
        public float respawnBelowY = -20f;

        [Header("Getting off")]
        public float dismountMaxSpeed = 1.4f;
        public float dismountSideOffset = 1.1f;
        public float exitGuardSeconds = 0.3f;

        [Header("Third-person camera")]
        public float chaseDistance = 4.5f;
        public float chaseHeight = 1.9f;
        public float lookHeight = 1.2f;
        public float orbitYawLimit = 120f;
        public float orbitPitchMin = -10f;
        public float orbitPitchMax = 35f;
        public float returnDelay = 1.5f;
        public float returnMinSpeed = 2f;
        public float returnSpeed = 3f;
        public float followSmoothing = 5f;
        public float cameraRadius = 0.25f;

        [Header("First-person camera")]
        public float headHeight = 0.75f;
        public float headBack = 0.1f;
        public float lookYawLimit = 100f;
        public float lookPitchLimit = 60f;
        public float rollFactor = 0.5f;

        [Header("Headlight")]
        public float headlightRange = 30f;
        public float headlightAngle = 60f;
        public float headlightIntensity = 60f;

        [Header("Engine sound")]
        public float idlePitch = 0.8f;
        public float maxPitch = 2f;
        public float throttlePitch = 0.1f;
        public float idleVolume = 0.6f;
        public float maxVolume = 0.9f;
        public float hornCooldown = 0.4f;
    }
}
