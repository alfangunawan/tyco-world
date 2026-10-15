using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>One downloaded model and how to turn it into a vehicle (spec vehicles-3a §3). Positions are in the built prefab's space.</summary>
    public sealed class VehicleModelSpec
    {
        public string Id;
        public VehicleClass Class;
        /// <summary>Real length in metres: the model is scaled uniformly until its bounds are this long (spec §1).</summary>
        public float Length;
        /// <summary>Degrees about +Y that turn the model's nose to +Z.</summary>
        public float YawFix;
        /// <summary>
        /// When the file holds more than one vehicle in one mesh (police: the SUV and the sedan), the triangles to keep: those
        /// whose centre lies in this box, in the imported model's own space (asset root at the origin, its rotation and scale
        /// kept, before YawFix). Null keeps everything.
        /// </summary>
        public Bounds? KeepRegion;
        public string[] FrontWheels = new string[0];
        public string[] RearWheels = new string[0];
        /// <summary>True when the wheels are part of the body mesh: nothing spins, and the report says so.</summary>
        public bool WheelsMerged;
        /// <summary>True when each wheel node is a whole axle (both wheels in one mesh): the wheels spin but the front does not steer.</summary>
        public bool SolidFrontAxle;
        /// <summary>Bike: the handlebar/fork node that turns with the steering; null for cars (their front wheels turn).</summary>
        public string SteerNode;
        /// <summary>Bike: further nodes that turn with the handlebar (the Supra's fork and mudguard).</summary>
        public string[] ExtraSteerNodes = new string[0];
        public string[] PaintMaterials = new string[0];
        public float PaintStrength = 1f;
        /// <summary>Overrides for points the builder otherwise derives from the bounds (null = derive).</summary>
        public Vector3? Headlight, TailLight, DriverSeat;
        /// <summary>Angkot only: where passengers step in and out (spec vehicles-3b §7).</summary>
        public Vector3[] PassengerDoors = new Vector3[0];
        /// <summary>Bikes only (filled in Task 5): right-hand side; the left mirrors in x.</summary>
        public BikeAnchorSpec Bike;
    }

    public sealed class BikeAnchorSpec
    {
        public Vector3 Seat, GripRight, FootRight, Pillion, PillionFootRight;
    }
}
