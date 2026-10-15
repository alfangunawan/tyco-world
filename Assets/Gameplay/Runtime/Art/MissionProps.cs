using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>Primitive props for missions (spec #6 §1: no licensed art): the waypoint pillar, a cat, an old radio, a kentongan.</summary>
    public static class MissionProps
    {
        static readonly Color32 BeaconColor = new Color32(255, 196, 64, 255);

        /// <summary>
        /// A ring on the ground, a turning diamond above head height and a thin glowing pillar from there up, so a
        /// person standing on the target stays visible. No colliders.
        /// </summary>
        public static GameObject Beacon(Transform parent, Palette palette)
        {
            var root = new GameObject("MissionBeacon");
            root.transform.SetParent(parent, false);
            var glow = palette.Glow(BeaconColor, 1.1f);
            var pillar = Primitives.Part(PrimitiveType.Cylinder, root.transform, "Pillar", new Vector3(0f, 23.2f, 0f), new Vector3(0.18f, 20f, 0.18f), glow);
            pillar.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var ring = Primitives.Part(PrimitiveType.Cylinder, root.transform, "Ring", new Vector3(0f, 0.03f, 0f), new Vector3(2.4f, 0.015f, 2.4f), glow);
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var diamond = Primitives.Part(PrimitiveType.Cube, root.transform, "Diamond", new Vector3(0f, 2.6f, 0f), new Vector3(0.3f, 0.3f, 0.3f), glow);
            diamond.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            diamond.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        /// <summary>Spins and bobs the beacon's diamond.</summary>
        public static void Animate(GameObject beacon, float time)
        {
            var diamond = beacon.transform.Find("Diamond");
            if (diamond == null) return;
            diamond.localPosition = new Vector3(0f, 2.6f + Mathf.Sin(time * 2f) * 0.12f, 0f);
            diamond.localRotation = Quaternion.Euler(0f, time * 90f, 0f) * Quaternion.Euler(45f, 0f, 45f);
        }

        /// <summary>A sitting ginger cat, about 0.45 m long.</summary>
        public static GameObject Cat(Transform parent, Palette palette)
        {
            var root = new GameObject("Kucing");
            root.transform.SetParent(parent, false);
            var fur = palette.Get(new Color32(214, 128, 52, 255));
            var white = palette.Get(new Color32(236, 228, 214, 255));
            Primitives.Part(PrimitiveType.Capsule, root.transform, "Body", new Vector3(0f, 0.16f, 0f), new Vector3(0.2f, 0.2f, 0.36f), fur)
                .transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            Primitives.Part(PrimitiveType.Sphere, root.transform, "Head", new Vector3(0f, 0.3f, 0.18f), new Vector3(0.17f, 0.15f, 0.16f), fur);
            Primitives.Part(PrimitiveType.Sphere, root.transform, "Muzzle", new Vector3(0f, 0.27f, 0.25f), new Vector3(0.08f, 0.06f, 0.05f), white);
            Primitives.Part(PrimitiveType.Cube, root.transform, "EarL", new Vector3(-0.05f, 0.38f, 0.17f), new Vector3(0.04f, 0.06f, 0.03f), fur)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            Primitives.Part(PrimitiveType.Cube, root.transform, "EarR", new Vector3(0.05f, 0.38f, 0.17f), new Vector3(0.04f, 0.06f, 0.03f), fur)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            Primitives.Part(PrimitiveType.Capsule, root.transform, "Tail", new Vector3(0f, 0.2f, -0.24f), new Vector3(0.04f, 0.14f, 0.04f), fur)
                .transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            return root;
        }

        /// <summary>An old transistor radio on the ground.</summary>
        public static GameObject Radio(Transform parent, Palette palette)
        {
            var root = new GameObject("RadioTua");
            root.transform.SetParent(parent, false);
            var body = palette.Get(new Color32(88, 62, 40, 255));
            var grille = palette.Get(new Color32(170, 160, 140, 255));
            var dark = palette.Get(new Color32(20, 20, 20, 255));
            Primitives.Part(PrimitiveType.Cube, root.transform, "Body", new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.22f, 0.12f), body);
            Primitives.Part(PrimitiveType.Cube, root.transform, "Grille", new Vector3(-0.07f, 0.12f, 0.062f), new Vector3(0.16f, 0.15f, 0.01f), grille);
            Primitives.Part(PrimitiveType.Cylinder, root.transform, "Dial", new Vector3(0.11f, 0.14f, 0.065f), new Vector3(0.06f, 0.005f, 0.06f), dark)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitives.Part(PrimitiveType.Cylinder, root.transform, "Antenna", new Vector3(0.14f, 0.38f, 0f), new Vector3(0.008f, 0.16f, 0.008f), dark)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            return root;
        }

        /// <summary>A pole with a warm bulb and a small point light: the pos ronda is lit at night so Pak RT can be seen from the road.</summary>
        public static GameObject Lantern(Transform parent, Palette palette)
        {
            var root = new GameObject("Lentera");
            root.transform.SetParent(parent, false);
            var wood = palette.Get(new Color32(90, 64, 40, 255));
            Primitives.Part(PrimitiveType.Cube, root.transform, "Pole", new Vector3(0f, 1.3f, 0f), new Vector3(0.08f, 2.6f, 0.08f), wood);
            Primitives.Part(PrimitiveType.Cube, root.transform, "Arm", new Vector3(0f, 2.55f, 0.2f), new Vector3(0.05f, 0.05f, 0.4f), wood);
            var bulb = Primitives.Part(PrimitiveType.Sphere, root.transform, "Bulb", new Vector3(0f, 2.4f, 0.38f), new Vector3(0.16f, 0.16f, 0.16f),
                palette.Glow(new Color32(255, 214, 140, 255), 3f));
            bulb.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0f, 2.3f, 0.38f);
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 3f;
            light.color = new Color(1f, 0.82f, 0.55f);
            light.shadows = LightShadows.None;
            return root;
        }

        /// <summary>A bamboo kentongan hanging from a post at the pos ronda.</summary>
        public static GameObject Kentongan(Transform parent, Palette palette)
        {
            var root = new GameObject("Kentongan");
            root.transform.SetParent(parent, false);
            var wood = palette.Get(new Color32(110, 78, 44, 255));
            var bamboo = palette.Get(new Color32(196, 168, 92, 255));
            Primitives.Part(PrimitiveType.Cube, root.transform, "Post", new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), wood);
            Primitives.Part(PrimitiveType.Cube, root.transform, "Arm", new Vector3(0f, 2.1f, 0.25f), new Vector3(0.08f, 0.08f, 0.5f), wood);
            Primitives.Part(PrimitiveType.Cylinder, root.transform, "Bamboo", new Vector3(0f, 1.55f, 0.42f), new Vector3(0.13f, 0.35f, 0.13f), bamboo);
            return root;
        }
    }
}
