using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>One frame of rider input (spec #5 §5). Throttle +1 = gas (W), -1 = brake/reverse (S); Steer +1 = right (D).</summary>
    public struct VehicleIntent
    {
        public float Throttle;
        public float Steer;
        public bool Brake;
        public bool Horn;
        public bool ToggleHeadlight;
        public bool ToggleCamera;
        public bool Exit;
        public Vector2 LookDegrees;
    }
}
