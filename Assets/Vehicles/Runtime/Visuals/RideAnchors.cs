using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// Where the people on a bike go: the rider's hips on the seat, hands on the handlebar grips (which turn with the
    /// bar) and feet on the footboard; the pillion passenger's hips and footrests. A bike model can carry its own;
    /// a bike without one gets the code scooter's layout (MotorbikeBuilder).
    /// </summary>
    public sealed class RideAnchors : MonoBehaviour
    {
        public Transform seat, gripLeft, gripRight, footLeft, footRight, pillion, pillionFootLeft, pillionFootRight;

        // The code scooter, right-hand side (x mirrors for the left): grips on the bar, the footboard top at 0.46 m,
        // the rider on the front of the seat, the passenger on its back and their feet outside the body panels.
        public static readonly Vector3 Seat = new Vector3(0f, 0.91f, 0.02f);
        public static readonly Vector3 GripOnBar = new Vector3(0.34f, 1.08f, -0.05f);
        public static readonly Vector3 BarOnBike = new Vector3(0f, 0f, 0.62f);
        public static readonly Vector3 Foot = new Vector3(0.12f, 0.47f, 0.33f);
        public static readonly Vector3 Pillion = new Vector3(0f, 0.91f, -0.5f);
        public static readonly Vector3 PillionFoot = new Vector3(0.24f, 0.42f, -0.1f);

        /// <summary>The bike's anchors, made with the code scooter's layout the first time a bike has none.</summary>
        public static RideAnchors Of(Motorbike bike)
        {
            if (bike == null) return null;
            var found = bike.GetComponentInChildren<RideAnchors>(true);
            if (found != null) return found;
            var visuals = bike.Visuals;
            var lean = visuals != null && visuals.LeanPivot != null ? visuals.LeanPivot : bike.transform;
            var bar = visuals != null ? visuals.Handlebar : null;
            var a = lean.gameObject.AddComponent<RideAnchors>();
            a.seat = Point("RiderSeat", lean, Seat);
            var gripRight = bar != null ? GripOnBar : BarOnBike + GripOnBar;
            a.gripRight = Point("GripR", bar != null ? bar : lean, gripRight);
            a.gripLeft = Point("GripL", bar != null ? bar : lean, Mirror(gripRight));
            a.footRight = Point("FootR", lean, Foot);
            a.footLeft = Point("FootL", lean, Mirror(Foot));
            a.pillion = Point("PillionSeat", lean, Pillion);
            a.pillionFootRight = Point("PillionFootR", lean, PillionFoot);
            a.pillionFootLeft = Point("PillionFootL", lean, Mirror(PillionFoot));
            return a;
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        static Transform Point(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }
    }
}
