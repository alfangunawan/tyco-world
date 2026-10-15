using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>Colours and shape of one person (spec #6 §2): seeded from an id so the same person always looks the same.</summary>
    public struct PersonLook
    {
        public Color32 Skin, Shirt, Pants, Hair;
        public bool Hijab;
        public float Height;

        static readonly Color32[] Skins = { new Color32(214, 170, 130, 255), new Color32(190, 140, 100, 255), new Color32(160, 115, 80, 255), new Color32(225, 185, 150, 255) };
        static readonly Color32[] Shirts =
        {
            new Color32(200, 60, 60, 255), new Color32(60, 90, 160, 255), new Color32(240, 240, 235, 255), new Color32(40, 40, 45, 255),
            new Color32(80, 140, 90, 255), new Color32(230, 190, 70, 255), new Color32(150, 90, 150, 255), new Color32(110, 110, 120, 255)
        };
        static readonly Color32[] PantsColors = { new Color32(40, 45, 70, 255), new Color32(30, 30, 32, 255), new Color32(110, 95, 75, 255), new Color32(70, 75, 80, 255) };
        static readonly Color32[] Hijabs = { new Color32(235, 230, 220, 255), new Color32(60, 60, 90, 255), new Color32(170, 90, 110, 255), new Color32(40, 40, 40, 255) };

        public static PersonLook From(ulong seed)
        {
            var rng = new SeededRandom(seed);
            bool hijab = rng.Next01() < 0.3f;
            return new PersonLook
            {
                Skin = Skins[rng.Range(0, Skins.Length)],
                Shirt = Shirts[rng.Range(0, Shirts.Length)],
                Pants = PantsColors[rng.Range(0, PantsColors.Length)],
                Hair = hijab ? Hijabs[rng.Range(0, Hijabs.Length)] : new Color32(25, 20, 18, 255),
                Hijab = hijab,
                Height = 0.92f + rng.Next01() * 0.14f
            };
        }

        /// <summary>The pale figure that stands by the road at night: white cloth, long dark hair, no face colour.</summary>
        public static PersonLook Sosok => new PersonLook
        {
            Skin = new Color32(205, 205, 200, 255), Shirt = new Color32(232, 230, 222, 255), Pants = new Color32(232, 230, 222, 255),
            Hair = new Color32(12, 10, 10, 255), Hijab = false, Height = 1.06f
        };
    }

    /// <summary>
    /// A person made of primitives (spec #6 §2): body, head, hair or hijab, arms and legs on pivots that swing with the
    /// walk. Root at the feet; faces +Z. No colliders of its own.
    /// </summary>
    public sealed class PersonFigure
    {
        public readonly Transform Root;
        readonly Transform leftLeg, rightLeg, leftArm, rightArm;
        float phase;

        PersonFigure(Transform root, Transform leftLeg, Transform rightLeg, Transform leftArm, Transform rightArm)
        {
            Root = root;
            this.leftLeg = leftLeg;
            this.rightLeg = rightLeg;
            this.leftArm = leftArm;
            this.rightArm = rightArm;
        }

        public static PersonFigure Build(Transform parent, string name, PersonLook look, Palette palette, bool ghost = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localScale = Vector3.one * look.Height;
            var skin = ghost ? palette.Glow(look.Skin, 0.35f) : palette.Get(look.Skin);
            var shirt = ghost ? palette.Glow(look.Shirt, 0.25f) : palette.Get(look.Shirt);
            var pants = palette.Get(look.Pants);
            var hair = palette.Get(look.Hair);
            Primitives.Part(PrimitiveType.Capsule, body, "Torso", new Vector3(0f, 1.2f, 0f), new Vector3(0.42f, 0.34f, 0.26f), shirt);
            Primitives.Part(PrimitiveType.Sphere, body, "Head", new Vector3(0f, 1.66f, 0f), new Vector3(0.22f, 0.25f, 0.23f), skin);
            if (look.Hijab)
            {
                Primitives.Part(PrimitiveType.Sphere, body, "Hijab", new Vector3(0f, 1.67f, -0.015f), new Vector3(0.27f, 0.29f, 0.27f), hair);
                Primitives.Part(PrimitiveType.Cylinder, body, "Drape", new Vector3(0f, 1.48f, 0f), new Vector3(0.36f, 0.08f, 0.3f), hair);
            }
            else if (ghost)
            {
                // Long hair hanging over the face and back.
                Primitives.Part(PrimitiveType.Capsule, body, "Hair", new Vector3(0f, 1.48f, 0.02f), new Vector3(0.27f, 0.36f, 0.27f), hair);
            }
            else
            {
                Primitives.Part(PrimitiveType.Sphere, body, "Hair", new Vector3(0f, 1.73f, -0.02f), new Vector3(0.235f, 0.15f, 0.24f), hair);
            }
            var leftArm = Limb(body, "ArmL", new Vector3(-0.25f, 1.47f, 0f), 0.3f, 0.09f, shirt);
            var rightArm = Limb(body, "ArmR", new Vector3(0.25f, 1.47f, 0f), 0.3f, 0.09f, shirt);
            var leftLeg = Limb(body, "LegL", new Vector3(-0.1f, 0.88f, 0f), 0.42f, 0.13f, ghost ? shirt : pants);
            var rightLeg = Limb(body, "LegR", new Vector3(0.1f, 0.88f, 0f), 0.42f, 0.13f, ghost ? shirt : pants);
            return new PersonFigure(root, leftLeg, rightLeg, leftArm, rightArm);
        }

        static Transform Limb(Transform body, string name, Vector3 pivot, float halfLength, float thickness, Material material)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(body, false);
            joint.localPosition = pivot;
            Primitives.Part(PrimitiveType.Capsule, joint, name + "Mesh", new Vector3(0f, -halfLength, 0f), new Vector3(thickness, halfLength, thickness), material);
            return joint;
        }

        /// <summary>Swings legs and arms with the walking speed (m/s); standing still eases them back.</summary>
        public void Animate(float speed, float dt)
        {
            if (speed > 0.05f) phase += dt * speed * 3.4f;
            else phase = Mathf.MoveTowards(phase, Mathf.Round(phase / Mathf.PI) * Mathf.PI, dt * 4f);
            float swing = Mathf.Sin(phase) * Mathf.Clamp01(speed / 1.4f);
            leftLeg.localRotation = Quaternion.Euler(28f * swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-28f * swing, 0f, 0f);
            leftArm.localRotation = Quaternion.Euler(-22f * swing, 0f, 0f);
            rightArm.localRotation = Quaternion.Euler(22f * swing, 0f, 0f);
        }

        /// <summary>Arms raised (a shopkeeper waving, an angry warga).</summary>
        public void RaiseArms(float amount)
        {
            leftArm.localRotation = Quaternion.Euler(-150f * amount, 0f, 0f);
            rightArm.localRotation = Quaternion.Euler(-150f * amount, 0f, 0f);
        }
    }
}
