using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// Handling per vehicle class (spec vehicles-3a §5) and the arcade body fitted to a model's bounds. Every number of the
    /// table lives here and only here.
    /// </summary>
    public static class VehicleClassPresets
    {
        readonly struct Row
        {
            public readonly float Kmh, AccelRest, AccelMax, YawLow, YawHigh, Chase, ChaseHeight;
            public readonly bool Leans;

            public Row(float kmh, float accelRest, float accelMax, float yawLow, float yawHigh, bool leans, float chase, float chaseHeight)
            {
                Kmh = kmh;
                AccelRest = accelRest;
                AccelMax = accelMax;
                YawLow = yawLow;
                YawHigh = yawHigh;
                Leans = leans;
                Chase = chase;
                ChaseHeight = chaseHeight;
            }
        }

        static Row Of(VehicleClass c)
        {
            switch (c)
            {
                case VehicleClass.MotorMatic: return new Row(60f, 4.0f, 1.6f, 150f, 60f, true, 4.5f, 1.9f);
                case VehicleClass.MotorBebek: return new Row(65f, 4.2f, 1.6f, 145f, 58f, true, 4.5f, 1.9f);
                case VehicleClass.CarSmall: return new Row(50f, 3.2f, 1.2f, 75f, 30f, false, 6.5f, 2.6f);
                case VehicleClass.Mpv: return new Row(45f, 3.0f, 1.1f, 70f, 28f, false, 7f, 2.8f);
                case VehicleClass.Van: return new Row(40f, 2.7f, 1.0f, 65f, 26f, false, 7f, 2.9f);
                case VehicleClass.Angkot: return new Row(40f, 2.7f, 1.0f, 65f, 26f, false, 7f, 2.9f);
                case VehicleClass.Pickup: return new Row(45f, 2.8f, 1.0f, 65f, 26f, false, 7f, 2.8f);
                case VehicleClass.Truck: return new Row(35f, 1.8f, 0.7f, 50f, 20f, false, 9f, 3.6f);
                default: return new Row(55f, 3.6f, 1.3f, 78f, 32f, false, 6.5f, 2.6f);
            }
        }

        public static float MaxKmh(VehicleClass c) => Of(c).Kmh;

        public static void Apply(BikeTuning t, VehicleClass c)
        {
            var r = Of(c);
            t.maxForwardSpeed = r.Kmh / BikeTuning.KmhPerMs;
            t.maxReverseSpeed = VehicleClasses.IsBike(c) ? 2.5f : 3f;
            t.accelAtRest = r.AccelRest;
            t.accelAtMax = r.AccelMax;
            t.yawRateLow = r.YawLow;
            t.yawRateHigh = r.YawHigh;
            t.maxLean = r.Leans ? 25f : 0f;
            t.isCar = !VehicleClasses.IsBike(c);
            t.chaseDistance = r.Chase;
            t.chaseHeight = r.ChaseHeight;
        }

        /// <summary>
        /// The controller capsule, the wall-stopping box, where the rider gets off and the camera look point, from the model's
        /// bounds in its own space (ground at y = 0, nose to +Z).
        /// </summary>
        public static void FitBody(BikeTuning t, Bounds local, VehicleClass c)
        {
            bool bike = VehicleClasses.IsBike(c);
            float halfWidth = local.size.x * 0.5f, halfLength = local.size.z * 0.5f;
            t.bodyRadius = bike ? Mathf.Min(0.5f, halfWidth + 0.15f) : Mathf.Min(halfWidth, 0.95f);
            t.bodyHeight = Mathf.Max(2f * t.bodyRadius, Mathf.Min(local.size.y, bike ? 1.4f : 1.8f));
            float boxHeight = Mathf.Max(0.2f, local.max.y - t.stepOffset - 0.05f);
            t.blockHalfExtents = new Vector3(halfWidth, boxHeight * 0.5f, halfLength);
            t.dismountSideOffset = Mathf.Max(bike ? 1.1f : 0f, halfWidth + 0.35f + 0.25f);
            t.lookHeight = bike ? 1.2f : Mathf.Max(1.2f, local.max.y * 0.8f);
            t.headHeight = bike ? 0.75f : 0.7f;
        }
    }
}
