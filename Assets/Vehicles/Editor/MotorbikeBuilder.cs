using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>The bike's clips (any may be null; the bike then stays silent for that sound).</summary>
    public sealed class VehicleClipSet
    {
        public AudioClip Engine, Starter, Horn, Bump;
    }

    /// <summary>
    /// Builds the primitive motorbike rig (spec #5 §1/§3): spinning wheels, steering handlebar, lean pivot, headlight,
    /// tail light, a seated rider and two 3D audio sources. Build is idempotent on the same root.
    /// </summary>
    public static class MotorbikeBuilder
    {
        public const string RootName = "Motorbike";
        public const string LeanPivotName = "LeanPivot";
        public const string BodyName = "Body";
        public const string HandlebarName = "Handlebar";
        public const string FrontWheelName = "FrontWheel";
        public const string RearWheelName = "RearWheel";
        public const string HeadlightName = "Headlight";
        public const string LensName = "HeadlightLens";
        public const string TailLightName = "TailLight";
        public const string SeatName = "Seat";
        public const string RiderName = "Rider";
        public const string AudioName = "Audio";
        public const string InteractionId = "kos-motor";
        const float WheelRadius = 0.3f;

        public static Vector3 SeatPosition => new Vector3(0f, 0.86f, -0.2f);

        /// <summary>
        /// Builds or refreshes the rig under <paramref name="root"/>. Children are found by name and components reused,
        /// so a re-run keeps the same objects (and their file ids inside the prefab); only meshes and settings are rewritten.
        /// </summary>
        public static void Build(GameObject root, BikeTuning tuning, VehicleMaterialSet materials, VehicleClipSet clips)
        {
            Motorbike.ConfigureBody(GetOrAdd<CharacterController>(root), tuning);

            var lean = Child(LeanPivotName, root.transform, Vector3.zero);
            AddMesh(Child(BodyName, lean, Vector3.zero), BodyMesh(), materials.Body, materials.Dark, materials.Chrome);

            var bar = Child(HandlebarName, lean, new Vector3(0f, 0f, 0.62f));
            AddMesh(bar, HandlebarMesh(), materials.Body, materials.Dark, materials.Chrome);
            var wheelMesh = WheelMesh();
            var front = Child(FrontWheelName, bar, new Vector3(0f, WheelRadius, 0.03f));
            AddMesh(front, wheelMesh, materials.Dark);
            var rear = Child(RearWheelName, lean, new Vector3(0f, WheelRadius, -0.65f));
            AddMesh(rear, wheelMesh, materials.Dark);

            var headlight = GetOrAdd<Light>(Child(HeadlightName, bar, new Vector3(0f, 0.97f, 0.13f)).gameObject);
            headlight.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            headlight.type = LightType.Spot;
            MotorbikeVisuals.ConfigureHeadlight(headlight, tuning);
            headlight.color = VehicleMaterials.LensEmission;
            headlight.shadows = LightShadows.None;
            headlight.enabled = false;
            var lens = Child(LensName, bar, new Vector3(0f, 0.97f, 0.112f));
            AddMesh(lens, Box("Motorbike_HeadlightLens", new Vector3(0.15f, 0.1f, 0.02f)), materials.HeadlightLens);
            var tail = Child(TailLightName, lean, new Vector3(0f, 0.72f, -0.82f));
            AddMesh(tail, Box("Motorbike_TailLight", new Vector3(0.16f, 0.07f, 0.03f)), materials.TailLight);

            var rider = Child(RiderName, lean, Vector3.zero);
            AddMesh(rider, RiderMesh(), materials.Shirt, materials.Pants, materials.Skin, materials.Helmet);
            rider.gameObject.SetActive(false);

            var seat = Child(SeatName, root.transform, SeatPosition);
            var audioGo = Child(AudioName, root.transform, new Vector3(0f, 0.5f, 0f)).gameObject;
            var sources = audioGo.GetComponents<AudioSource>();
            var engine = Configure3DSource(sources.Length > 0 ? sources[0] : audioGo.AddComponent<AudioSource>(), true);
            var oneShot = Configure3DSource(sources.Length > 1 ? sources[1] : audioGo.AddComponent<AudioSource>(), false);

            var visuals = GetOrAdd<MotorbikeVisuals>(root);
            visuals.Configure(lean, bar, front, rear, rider, headlight, tail.GetComponent<Renderer>(), lens.GetComponent<Renderer>());
            var sound = GetOrAdd<MotorbikeAudio>(root);
            sound.Configure(engine, oneShot, clips.Engine, clips.Starter, clips.Horn, clips.Bump);
            GetOrAdd<Motorbike>(root).Configure(tuning, seat, visuals, sound);
            GetOrAdd<MotorbikeInteractable>(root).SetInteractionId(InteractionId);
        }

        static Mesh BodyMesh()
        {
            var m = new MeshData(3);
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.55f, -0.05f), new Vector3(0.3f, 0.25f, 1f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.8f, 0.5f), new Vector3(0.38f, 0.5f, 0.22f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.66f, -0.6f), new Vector3(0.26f, 0.08f, 0.42f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 0.86f, -0.25f), new Vector3(0.3f, 0.1f, 0.65f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 0.44f, 0.15f), new Vector3(0.34f, 0.04f, 0.5f));
            AddBeam(m, 2, new Vector3(0.17f, 0.35f, -0.25f), new Vector3(0.17f, 0.45f, -0.8f), new Vector2(0.08f, 0.08f));
            AddBeam(m, 2, new Vector3(0f, WheelRadius, -0.65f), new Vector3(0f, 0.55f, -0.25f), new Vector2(0.05f, 0.05f));
            return m.ToMesh("Motorbike_Body", out _);
        }

        static Mesh HandlebarMesh()
        {
            var m = new MeshData(3);
            AddBeam(m, 2, new Vector3(0f, WheelRadius, 0.03f), new Vector3(0f, 1.05f, -0.05f), new Vector2(0.06f, 0.06f));
            PlaceholderMeshes.AddBox(m, 2, new Vector3(0f, 1.08f, -0.05f), new Vector3(0.62f, 0.035f, 0.035f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(-0.34f, 1.08f, -0.05f), new Vector3(0.1f, 0.045f, 0.045f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0.34f, 1.08f, -0.05f), new Vector3(0.1f, 0.045f, 0.045f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.97f, 0.05f), new Vector3(0.2f, 0.16f, 0.12f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.64f, 0.03f), new Vector3(0.14f, 0.04f, 0.4f));
            return m.ToMesh("Motorbike_Handlebar", out _);
        }

        static Mesh WheelMesh()
        {
            var m = new MeshData(1);
            PlaceholderPropFactory.AddWheel(m, 0, Vector3.zero, WheelRadius, 0.1f, 12);
            return m.ToMesh("Motorbike_Wheel", out _);
        }

        /// <summary>Seated rider in bike coordinates: shirt, pants, skin, helmet/shoes.</summary>
        static Mesh RiderMesh()
        {
            var m = new MeshData(4);
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 0.99f, -0.3f), new Vector3(0.34f, 0.16f, 0.26f));
            AddBeam(m, 0, new Vector3(0f, 1.05f, -0.3f), new Vector3(0f, 1.52f, -0.18f), new Vector2(0.36f, 0.22f));
            PlaceholderMeshes.AddBox(m, 2, new Vector3(0f, 1.58f, -0.17f), new Vector3(0.1f, 0.08f, 0.1f));
            PlaceholderMeshes.AddBox(m, 3, new Vector3(0f, 1.73f, -0.15f), new Vector3(0.26f, 0.26f, 0.28f));
            PlaceholderMeshes.AddBox(m, 2, new Vector3(0f, 1.7f, 0f), new Vector3(0.18f, 0.14f, 0.02f));
            foreach (float side in new[] { -1f, 1f })
            {
                AddBeam(m, 1, new Vector3(0.12f * side, 0.98f, -0.25f), new Vector3(0.14f * side, 0.92f, 0.12f), new Vector2(0.14f, 0.14f));
                AddBeam(m, 1, new Vector3(0.14f * side, 0.92f, 0.12f), new Vector3(0.15f * side, 0.5f, 0.16f), new Vector2(0.11f, 0.11f));
                PlaceholderMeshes.AddBox(m, 3, new Vector3(0.15f * side, 0.48f, 0.2f), new Vector3(0.1f, 0.07f, 0.22f));
                AddBeam(m, 0, new Vector3(0.21f * side, 1.47f, -0.18f), new Vector3(0.27f * side, 1.22f, 0.18f), new Vector2(0.1f, 0.1f));
                AddBeam(m, 2, new Vector3(0.27f * side, 1.22f, 0.18f), new Vector3(0.33f * side, 1.1f, 0.55f), new Vector2(0.08f, 0.08f));
            }
            return m.ToMesh("Motorbike_Rider", out _);
        }

        static Mesh Box(string name, Vector3 size)
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddBox(m, 0, Vector3.zero, size);
            return m.ToMesh(name, out _);
        }

        /// <summary>Box from <paramref name="from"/> to <paramref name="to"/> with a <paramref name="section"/> (width, depth) cross-section.</summary>
        static void AddBeam(MeshData m, int sub, Vector3 from, Vector3 to, Vector2 section)
        {
            var axis = to - from;
            float length = axis.magnitude;
            if (length < 1e-4f) return;
            var forward = axis / length;
            var rotation = Quaternion.LookRotation(forward, Mathf.Abs(forward.y) > 0.99f ? Vector3.forward : Vector3.up);
            var center = (from + to) * 0.5f;
            var half = new Vector3(section.x, section.y, length) * 0.5f;
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                var w = Vector3.Cross(n, u);
                Vector3 Corner(float su, float sw) => center + rotation * Vector3.Scale(n + u * su + w * sw, half);
                m.AddQuad(sub, Corner(-1f, -1f), Corner(1f, -1f), Corner(1f, 1f), Corner(-1f, 1f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, rotation * n);
            }
        }

        /// <summary>Finds (also inactive) or creates the named child and resets its local pose.</summary>
        static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        static void AddMesh(Transform t, Mesh mesh, params Material[] materials)
        {
            GetOrAdd<MeshFilter>(t.gameObject).sharedMesh = mesh;
            GetOrAdd<MeshRenderer>(t.gameObject).sharedMaterials = materials;
        }

        static AudioSource Configure3DSource(AudioSource source, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 40f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
