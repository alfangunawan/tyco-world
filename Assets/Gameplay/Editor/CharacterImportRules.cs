using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Gameplay.Authoring
{
    /// <summary>
    /// Import settings for the downloaded character art (spec #7): every FBX under the Quaternius folder is a Humanoid
    /// (one avatar per model, so animations retarget between them); only the animation library keeps its clips, named
    /// without the "Rig|" prefix, looping when the name ends in "_Loop", and baked in place (code moves the people)
    /// with the body facing the Animator's +Z whichever way the clip was authored. The body models' parts (body, head,
    /// legs, feet: one skeleton) are merged into one skinned mesh with one submesh per material, so a person is skinned
    /// once and drawn in as few calls as it has colours.
    /// </summary>
    public sealed class CharacterImportRules : AssetPostprocessor
    {
        public const string QuaterniusFolder = "Assets/Gameplay/Art/ThirdParty/Quaternius/";
        public const string AnimationFolder = QuaterniusFolder + "UAL/";
        public const string BodiesFolder = QuaterniusFolder + "Characters/";

        // Bumped when the rules change so Unity reimports the models.
        public override uint GetVersion() => 3;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(QuaterniusFolder, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = assetPath.StartsWith(AnimationFolder, System.StringComparison.Ordinal);
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationFolder, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                string name = clip.name;
                int bar = name.LastIndexOf('|');
                if (bar >= 0) name = name.Substring(bar + 1);
                clip.name = name;
                clip.loopTime = name.EndsWith("_Loop", System.StringComparison.Ordinal);
                // In place: the game moves people itself.
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                // Based upon body orientation: the UAL clips are authored facing -Z, and kept as authored they turned
                // every body round to face backwards.
                clip.keepOriginalOrientation = false;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clips[i] = clip;
            }
            importer.clipAnimations = clips;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(BodiesFolder, System.StringComparison.Ordinal)) return;
            var merged = MergeParts(root, out string why);
            if (merged == null)
            {
                if (why.Length > 0) Debug.LogWarning("[Gameplay] " + assetPath + ": bagian tubuh tidak digabung (" + why + ").");
                return;
            }
            context.AddObjectToAsset("MergedBody", merged);
        }

        /// <summary>
        /// Merges every SkinnedMeshRenderer under <paramref name="root"/> into the first one when they share one skeleton
        /// and bind pose; returns the new mesh (null, with the reason, when they cannot be merged).
        /// </summary>
        public static Mesh MergeParts(GameObject root, out string why)
        {
            why = "";
            var parts = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (parts.Length < 2) return null;
            var first = parts[0];
            var bindposes = first.sharedMesh.bindposes;
            foreach (var part in parts)
            {
                if (part.sharedMesh == null)
                {
                    why = part.name + " tanpa mesh";
                    return null;
                }
                if (part.rootBone != first.rootBone || part.bones.Length != first.bones.Length)
                {
                    why = part.name + " memakai rangka lain";
                    return null;
                }
                var poses = part.sharedMesh.bindposes;
                for (int b = 0; b < poses.Length; b++)
                {
                    if (part.bones[b] != first.bones[b] || !Same(poses[b], bindposes[b]))
                    {
                        why = part.name + " berbeda bind pose";
                        return null;
                    }
                }
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var materials = new List<Material>();
            var triangles = new List<List<int>>();
            bool withUv = true;
            foreach (var part in parts) withUv &= part.sharedMesh.uv.Length == part.sharedMesh.vertexCount;
            var bounds = first.localBounds;
            foreach (var part in parts)
            {
                var mesh = part.sharedMesh;
                int offset = vertices.Count;
                vertices.AddRange(mesh.vertices);
                normals.AddRange(mesh.normals);
                if (withUv) uvs.AddRange(mesh.uv);
                weights.AddRange(mesh.boneWeights);
                var mats = part.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var material = s < mats.Length ? mats[s] : null;
                    int slot = materials.IndexOf(material);
                    if (slot < 0)
                    {
                        slot = materials.Count;
                        materials.Add(material);
                        triangles.Add(new List<int>());
                    }
                    foreach (int index in mesh.GetTriangles(s)) triangles[slot].Add(index + offset);
                }
                bounds.Encapsulate(part.localBounds);
            }

            var merged = new Mesh { name = root.name + "_Body" };
            if (vertices.Count > 65535) merged.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            merged.SetVertices(vertices);
            merged.SetNormals(normals);
            if (withUv) merged.SetUVs(0, uvs);
            merged.boneWeights = weights.ToArray();
            merged.bindposes = bindposes;
            merged.subMeshCount = triangles.Count;
            for (int s = 0; s < triangles.Count; s++) merged.SetTriangles(triangles[s], s);
            merged.RecalculateBounds();

            first.sharedMesh = merged;
            first.sharedMaterials = materials.ToArray();
            first.localBounds = bounds;
            first.gameObject.name = "Body";
            for (int i = 1; i < parts.Length; i++)
            {
                var go = parts[i].gameObject;
                Object.DestroyImmediate(parts[i]);
                if (go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1) Object.DestroyImmediate(go);
            }
            return merged;
        }

        static bool Same(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(a[i] - b[i]) > 1e-4f * Mathf.Max(1f, Mathf.Abs(a[i]))) return false;
            return true;
        }
    }
}
