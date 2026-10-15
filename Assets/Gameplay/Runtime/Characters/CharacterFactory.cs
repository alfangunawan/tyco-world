using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Makes a person from the character library (spec #7): a copy of a humanoid body model, picked by seed, with the
    /// Kampung animator, scaled to a kampung height, skin in a sawo matang tone and hair and eyebrows near black
    /// (flat-coloured materials only; textured ones are kept), and for the night figure every material swapped for a
    /// pale glow.
    /// </summary>
    public static class CharacterFactory
    {
        static readonly Color32[] SkinTones =
        {
            new Color32(141, 98, 68, 255), new Color32(160, 113, 79, 255), new Color32(122, 84, 58, 255), new Color32(176, 128, 92, 255),
            new Color32(104, 71, 50, 255)
        };

        static readonly Color32[] HairTones = { new Color32(18, 14, 12, 255), new Color32(28, 20, 16, 255), new Color32(42, 30, 22, 255) };

        public static Color32 SkinTone(ulong seed) => SkinTones[(int)(seed % (ulong)SkinTones.Length)];
        public static Color32 HairTone(ulong seed) => HairTones[(int)((seed >> 4) % (ulong)HairTones.Length)];

        /// <summary>
        /// A new person under <paramref name="parent"/>: an empty holder named <paramref name="name"/> (place and turn
        /// this one, it is <see cref="CharacterRig.Root"/>) with the model inside, turned to face +Z and scaled. Null
        /// when the library has no bodies.
        /// </summary>
        public static CharacterRig Spawn(CharacterLibrary library, Transform parent, string name, ulong seed, Palette palette, bool woman = false,
            bool ghost = false)
        {
            if (library == null || !library.HasPeople) return null;
            var pool = woman && library.womenBodies != null && library.womenBodies.Length > 0 ? library.womenBodies : library.bodies;
            var prefab = pool[(int)(SeededRandom.Mix(seed, 0xB0D1UL) % (ulong)pool.Length)];
            if (prefab == null) return null;
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            var go = Object.Instantiate(prefab, holder, false);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = library.controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            Normalise(go, animator, library.targetHeight * (woman ? 0.95f : 1f) * (0.96f + 0.08f * ((seed >> 8) % 100) / 100f));
            Dress(go, prefab.name, seed, palette, ghost);
            return go.AddComponent<CharacterRig>();
        }

        /// <summary>
        /// Scales the body so the top of the head lands at <paramref name="height"/>. Which way it faces is the
        /// animations' business: they are imported to face the Animator's +Z (see CharacterImportRules).
        /// </summary>
        static void Normalise(GameObject go, Animator animator, float height)
        {
            var head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            float measured;
            if (head != null) measured = (head.position.y - go.transform.position.y) / 0.88f;
            else
            {
                var r = go.GetComponentInChildren<Renderer>();
                measured = r != null ? r.bounds.size.y : 1.8f;
            }
            if (measured > 0.3f) go.transform.localScale *= height / measured;
        }

        /// <summary>Hair, eyebrows and moustaches; Women_Formal's braid is a material named only "Red".</summary>
        static bool IsHair(string model, string material) =>
            material.Contains("hair") || material.Contains("eyebrow") || material.Contains("moustache") ||
            (material == "red" && model.Equals("Women_Formal", System.StringComparison.OrdinalIgnoreCase));

        static void Dress(GameObject go, string model, ulong seed, Palette palette, bool ghost)
        {
            if (palette == null) return;
            var skin = SkinTone(seed);
            var shade = (Color32)Color.Lerp(skin, Color.black, 0.18f);
            var hair = HairTone(seed);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (ghost)
                    {
                        mats[i] = palette.Glow(new Color32(214, 218, 210, 255), 0.35f);
                        continue;
                    }
                    if (m == null || m.mainTexture != null) continue;
                    string n = m.name.ToLowerInvariant();
                    if (n.Contains("skin_dark")) mats[i] = palette.Get(shade);
                    else if (n.Contains("skin") || n.Contains("m_main")) mats[i] = palette.Get(skin);
                    else if (IsHair(model, n)) mats[i] = palette.Get(hair);
                }
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
        }
    }
}
