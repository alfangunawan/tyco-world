using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Makes a person from the character library (spec #7): a copy of a humanoid body model, picked by seed, with the
    /// Kampung animator, scaled to a kampung height, skin in a sawo matang tone (flat-coloured skin materials only;
    /// textured ones are kept), and for the night figure every material swapped for a pale glow.
    /// </summary>
    public static class CharacterFactory
    {
        static readonly Color32[] SkinTones =
        {
            new Color32(141, 98, 68, 255), new Color32(160, 113, 79, 255), new Color32(122, 84, 58, 255), new Color32(176, 128, 92, 255),
            new Color32(104, 71, 50, 255)
        };

        public static Color32 SkinTone(ulong seed) => SkinTones[(int)(seed % (ulong)SkinTones.Length)];

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
            Dress(go, seed, palette, ghost);
            return go.AddComponent<CharacterRig>();
        }

        /// <summary>
        /// Turns the body to face the parent's +Z (some models are exported facing -Z: their right shoulder lies on -X)
        /// and scales it so the top of the head lands at <paramref name="height"/>.
        /// </summary>
        static void Normalise(GameObject go, Animator animator, float height)
        {
            var left = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            var right = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            if (left != null && right != null)
            {
                var across = right.position - left.position;
                across.y = 0f;
                var facing = Vector3.Cross(across, Vector3.up);
                if (Vector3.Dot(facing, go.transform.forward) < 0f) go.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);
            }
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

        static void Dress(GameObject go, ulong seed, Palette palette, bool ghost)
        {
            if (palette == null) return;
            var skin = SkinTone(seed);
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
                    if (n.Contains("skin") || n.Contains("m_main")) mats[i] = palette.Get(skin);
                }
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
        }
    }
}
