using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Builds the warga's vehicles (spec #6 §2 "Kendaraan dunia"): bikes from the Motorbike prefab (or a plain code bike
    /// when there is none, as in tests) and boxy angkot/minibus cars on the same arcade controller with car tuning.
    /// Both carry a painted body the bengkel can recolour.
    /// </summary>
    public static class VehicleFactory
    {
        public const string PaintName = "Paint";

        static readonly Color32[] BikeColours =
        {
            new Color32(150, 24, 28, 255), new Color32(24, 40, 120, 255), new Color32(20, 20, 22, 255), new Color32(210, 210, 214, 255),
            new Color32(30, 110, 60, 255), new Color32(220, 120, 20, 255)
        };

        static readonly Color32[] CarColours =
        {
            new Color32(40, 140, 70, 255), new Color32(30, 90, 170, 255), new Color32(225, 225, 228, 255), new Color32(70, 70, 76, 255),
            new Color32(180, 40, 40, 255), new Color32(230, 190, 40, 255)
        };

        public static Color32 BikeColour(int i) => BikeColours[((i % BikeColours.Length) + BikeColours.Length) % BikeColours.Length];
        public static Color32 CarColour(int i) => CarColours[((i % CarColours.Length) + CarColours.Length) % CarColours.Length];

        /// <summary>Tuning for a car: slower, wider, no lean, longer blocking box and a further chase camera.</summary>
        public static BikeTuning CarTuning(BikeTuning from)
        {
            var t = from != null ? Object.Instantiate(from) : ScriptableObject.CreateInstance<BikeTuning>();
            t.name = "CarTuning";
            t.hideFlags = HideFlags.DontSave;
            t.maxForwardSpeed = 13.9f;
            t.maxReverseSpeed = 3f;
            t.accelAtRest = 3f;
            t.accelAtMax = 1.1f;
            t.yawRateLow = 70f;
            t.yawRateHigh = 28f;
            t.maxLean = 0f;
            t.bodyRadius = 0.85f;
            t.bodyHeight = 1.7f;
            t.blockHalfExtents = new Vector3(0.82f, 0.55f, 1.95f);
            t.dismountSideOffset = 1.7f;
            t.dismountMaxSpeed = 1.2f;
            t.chaseDistance = 7f;
            t.chaseHeight = 2.8f;
            t.lookHeight = 1.4f;
            t.headHeight = 0.7f;
            return t;
        }

        /// <summary>A parked bike under <paramref name="parent"/>: a copy of the prefab, else a code bike with a primitive body.</summary>
        public static Motorbike Bike(Transform parent, GameObject prefab, Palette palette, Color32 colour)
        {
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent, false);
                go.name = "WargaBike";
            }
            else
            {
                // Built inactive: Motorbike initialises on Awake and must already have its tuning.
                go = new GameObject("WargaBike");
                go.SetActive(false);
                go.transform.SetParent(parent, false);
                go.AddComponent<CharacterController>();
                var seat = new GameObject("Seat").transform;
                seat.SetParent(go.transform, false);
                seat.localPosition = new Vector3(0f, 0.86f, -0.2f);
                var bike = go.AddComponent<Motorbike>();
                var tuning = ScriptableObject.CreateInstance<BikeTuning>();
                tuning.hideFlags = HideFlags.DontSave;
                bike.Configure(tuning, seat, null, null);
                var dark = palette.Get(new Color32(24, 24, 26, 255));
                Primitives.Part(PrimitiveType.Cube, go.transform, PaintName, new Vector3(0f, 0.62f, -0.05f), new Vector3(0.28f, 0.3f, 1.3f), palette.Get(colour));
                Primitives.Part(PrimitiveType.Cylinder, go.transform, "WheelF", new Vector3(0f, 0.3f, 0.62f), new Vector3(0.6f, 0.05f, 0.6f), dark)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Primitives.Part(PrimitiveType.Cylinder, go.transform, "WheelR", new Vector3(0f, 0.3f, -0.65f), new Vector3(0.6f, 0.05f, 0.6f), dark)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                go.SetActive(true);
            }
            var interactable = go.GetComponent<MotorbikeInteractable>();
            if (interactable != null) DestroyNow(interactable);
            Paint(go, palette, colour);
            return go.GetComponent<Motorbike>();
        }

        /// <summary>A parked angkot-like minibus: driver seat on the right, a code-built body, no rider model.</summary>
        public static Motorbike Car(Transform parent, BikeTuning carTuning, Palette palette, Color32 colour)
        {
            var go = new GameObject("WargaCar");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.AddComponent<CharacterController>();
            var seat = new GameObject("Seat").transform;
            seat.SetParent(go.transform, false);
            seat.localPosition = new Vector3(0.38f, 0.62f, 0.35f);
            var car = go.AddComponent<Motorbike>();
            car.Configure(carTuning, seat, null, null);
            var paint = palette.Get(colour);
            var glass = palette.Get(new Color32(28, 34, 40, 255));
            var dark = palette.Get(new Color32(22, 22, 24, 255));
            var lamp = palette.Glow(new Color32(255, 240, 200, 255), 0.6f);
            var tail = palette.Get(new Color32(170, 20, 20, 255));
            Primitives.Part(PrimitiveType.Cube, go.transform, PaintName, new Vector3(0f, 0.78f, 0f), new Vector3(1.66f, 0.8f, 3.9f), paint);
            Primitives.Part(PrimitiveType.Cube, go.transform, "Cabin", new Vector3(0f, 1.56f, -0.25f), new Vector3(1.6f, 0.78f, 3.1f), paint);
            Primitives.Part(PrimitiveType.Cube, go.transform, "Windows", new Vector3(0f, 1.6f, -0.25f), new Vector3(1.63f, 0.48f, 2.9f), glass);
            Primitives.Part(PrimitiveType.Cube, go.transform, "Windscreen", new Vector3(0f, 1.58f, 1.31f), new Vector3(1.45f, 0.6f, 0.04f), glass)
                .transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Primitives.Part(PrimitiveType.Cube, go.transform, "Bumper", new Vector3(0f, 0.42f, 1.97f), new Vector3(1.7f, 0.18f, 0.1f), dark);
            Primitives.Part(PrimitiveType.Cube, go.transform, "LampL", new Vector3(-0.62f, 0.86f, 1.96f), new Vector3(0.28f, 0.14f, 0.04f), lamp);
            Primitives.Part(PrimitiveType.Cube, go.transform, "LampR", new Vector3(0.62f, 0.86f, 1.96f), new Vector3(0.28f, 0.14f, 0.04f), lamp);
            Primitives.Part(PrimitiveType.Cube, go.transform, "TailL", new Vector3(-0.66f, 0.9f, -1.96f), new Vector3(0.2f, 0.18f, 0.04f), tail);
            Primitives.Part(PrimitiveType.Cube, go.transform, "TailR", new Vector3(0.66f, 0.9f, -1.96f), new Vector3(0.2f, 0.18f, 0.04f), tail);
            foreach (var (x, z) in new[] { (-0.8f, 1.25f), (0.8f, 1.25f), (-0.8f, -1.25f), (0.8f, -1.25f) })
                Primitives.Part(PrimitiveType.Cylinder, go.transform, "Wheel", new Vector3(x, 0.34f, z), new Vector3(0.68f, 0.1f, 0.68f), dark)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // The controller's capsule is round; a box hull keeps people out of the nose and tail. The controller ignores it.
            var hull = new GameObject("Hull");
            hull.transform.SetParent(go.transform, false);
            var box = hull.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.05f, 0f);
            box.size = new Vector3(1.66f, 1.6f, 3.9f);
            Activate(go);
            return car;
        }

        /// <summary>
        /// Shows a (pooled) vehicle. The controller-ignores-hull pair is reset whenever a collider is deactivated, so it
        /// is set again here.
        /// </summary>
        public static void Activate(GameObject vehicle)
        {
            vehicle.SetActive(true);
            var hull = vehicle.transform.Find("Hull");
            var controller = vehicle.GetComponent<CharacterController>();
            var box = hull != null ? hull.GetComponent<BoxCollider>() : null;
            if (controller != null && box != null) Physics.IgnoreCollision(controller, box, true);
        }

        /// <summary>Recolours the body: the "Paint" part of a code-built vehicle, or the body slot of the prefab bike.</summary>
        public static void Paint(GameObject vehicle, Palette palette, Color32 colour)
        {
            var material = palette.Get(colour);
            var paint = vehicle.transform.Find(PaintName);
            if (paint != null)
            {
                var r = paint.GetComponent<MeshRenderer>();
                if (r != null) r.sharedMaterial = material;
                return;
            }
            var visuals = vehicle.GetComponent<MotorbikeVisuals>();
            var body = visuals != null && visuals.LeanPivot != null ? visuals.LeanPivot.Find("Body") : null;
            var renderer = body != null ? body.GetComponent<MeshRenderer>() : null;
            if (renderer == null) return;
            var slots = renderer.sharedMaterials;
            if (slots.Length == 0) return;
            slots[0] = material;
            renderer.sharedMaterials = slots;
        }

        static void DestroyNow(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
