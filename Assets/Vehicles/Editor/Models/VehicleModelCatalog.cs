using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>
    /// The 14 vehicles of spec vehicles-3a §1, in that order. Lengths are the spec's; node and material names come from the
    /// GLB inspection (Task 1: the glTFast hierarchy, front found from renders of each model); bike anchors from the pose fit (Task 5).
    /// </summary>
    public static class VehicleModelCatalog
    {
        static List<VehicleModelSpec> all;

        public static IReadOnlyList<VehicleModelSpec> All => all ??= Build();

        public static VehicleModelSpec Get(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return null;
        }

        static List<VehicleModelSpec> Build() => new List<VehicleModelSpec>
        {
            // The fairing, fork and handlebar are one mesh (stang_vario): steering it would swing the whole front, so only the wheel turns.
            new VehicleModelSpec
            {
                Id = "vario", Class = VehicleClass.MotorMatic, Length = 1.92f, YawFix = 0f,
                FrontWheels = new[] { "wheel.vario" }, RearWheels = new[] { "wheel.vario.001" },
                PaintMaterials = new[] { "material" }, PaintStrength = 0.6f
            },
            new VehicleModelSpec
            {
                Id = "supra", Class = VehicleClass.MotorBebek, Length = 1.89f, YawFix = 180f,
                FrontWheels = new[] { "wheel_front" }, RearWheels = new[] { "wheel_rear" },
                SteerNode = "handlebars", ExtraSteerNodes = new[] { "forks_front" },
                PaintMaterials = new[] { "chassis.002" }, PaintStrength = 0.6f
            },
            new VehicleModelSpec
            {
                Id = "sedan", Class = VehicleClass.CarSmall, Length = 4.45f, YawFix = 0f,
                FrontWheels = new[] { "cvc_wheel", "cvc_wheel.003" }, RearWheels = new[] { "cvc_wheel.001", "cvc_wheel.002" },
                PaintMaterials = new[] { "Material.004" }, PaintStrength = 0.6f
            },
            new VehicleModelSpec
            {
                Id = "hatch", Class = VehicleClass.CarSmall, Length = 3.80f, YawFix = 0f,
                FrontWheels = new[] { "wheel", "wheel.002" }, RearWheels = new[] { "wheel.001", "wheel.003" },
                PaintMaterials = new[] { "hatchback1" }, PaintStrength = 0.6f
            },
            new VehicleModelSpec
            {
                Id = "lmpv1", Class = VehicleClass.Mpv, Length = 4.40f, YawFix = 0f, WheelsMerged = true,
                PaintMaterials = new[] { "mpv1" }, PaintStrength = 0.6f
            },
            new VehicleModelSpec
            {
                Id = "lmpv2", Class = VehicleClass.Mpv, Length = 4.10f, YawFix = 0f,
                FrontWheels = new[] { "Plane.002_1", "Plane.010_3" }, RearWheels = new[] { "Plane.001_0", "Plane.003_2" },
                PaintMaterials = new[] { "body.001" }, PaintStrength = 1f
            },
            new VehicleModelSpec
            {
                Id = "mpv2", Class = VehicleClass.Mpv, Length = 4.70f, YawFix = 0f,
                FrontWheels = new[] { "Cylinder.009", "Cylinder.011" }, RearWheels = new[] { "Cylinder.008", "Cylinder.010" },
                PaintMaterials = new[] { "color" }, PaintStrength = 1f
            },
            new VehicleModelSpec
            {
                Id = "van", Class = VehicleClass.Van, Length = 4.05f, YawFix = 0f,
                FrontWheels = new[] { "wheel.004", "wheel.006" }, RearWheels = new[] { "wheel.005", "wheel.007" },
                PaintMaterials = new[] { "material" }, PaintStrength = 0.6f
            },
            // Meshes are split by material, not by part: the tyres are in the black underbody mesh.
            new VehicleModelSpec
            {
                Id = "l300", Class = VehicleClass.Pickup, Length = 4.30f, YawFix = 0f, WheelsMerged = true,
                PaintMaterials = new[] { "Material" }, PaintStrength = 1f
            },
            new VehicleModelSpec
            {
                Id = "pickup", Class = VehicleClass.Pickup, Length = 5.30f, YawFix = 0f,
                FrontWheels = new[] { "Pickup1.Wheel.Ft.L_2", "Pickup1.Wheel.Ft.R_3" },
                RearWheels = new[] { "Pickup1.Wheel.Bk.L_0", "Pickup1.Wheel.Bk.R_1" },
                PaintMaterials = new[] { "pickup1_body" }, PaintStrength = 1f
            },
            // One mesh holds the SUV (x below -0.4) and the sedan (x above 0.7); keep the sedan. Fixed livery.
            new VehicleModelSpec
            {
                Id = "police", Class = VehicleClass.Police, Length = 4.45f, YawFix = 0f, WheelsMerged = true,
                KeepRegion = new Bounds(new Vector3(1.75f, 1f, 0f), new Vector3(2.5f, 4f, 8f))
            },
            // "ban" (front) and "ban blk" (belakang) each hold both wheels of an axle. Fixed livery.
            new VehicleModelSpec
            {
                Id = "angkot", Class = VehicleClass.Angkot, Length = 3.90f, YawFix = 0f,
                FrontWheels = new[] { "ban" }, RearWheels = new[] { "ban blk" }, SolidFrontAxle = true,
                PassengerDoors = new[] { new Vector3(-1.35f, 0f, -0.2f), new Vector3(0f, 0f, -2.35f) }
            },
            new VehicleModelSpec
            {
                Id = "carry", Class = VehicleClass.Pickup, Length = 3.40f, YawFix = 0f, WheelsMerged = true,
                PaintMaterials = new[] { "suzuki" }, PaintStrength = 0.6f
            },
            // Modelled along -X at 1/150 scale; all tyres are one mesh.
            new VehicleModelSpec
            {
                Id = "elf", Class = VehicleClass.Truck, Length = 6.00f, YawFix = 90f, WheelsMerged = true,
                PaintMaterials = new[] { "lambert3" }, PaintStrength = 1f
            }
        };
    }
}
