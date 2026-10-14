using System.Collections.Generic;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Low-poly placeholder props for the kos front yard. Create* builds unsaved objects; SaveAsPrefab persists them.</summary>
    public static class PlaceholderPropFactory
    {
        public const float GateWidth = 2f;
        public const float GateHeight = 1.8f;
        public const float GateBottomGap = 0.14f;
        public const float WallHeight = 1.92f;
        public const float WallThickness = 0.15f;
        public const float YardThickness = 0.12f;

        public static GameObject CreateGate(Material metal, AudioClip unlatch, AudioClip creak, AudioClip latch)
        {
            var root = new GameObject("Gate");
            var posts = new MeshData(1);
            float postX = GateWidth * 0.5f + 0.06f;
            PlaceholderMeshes.AddBox(posts, 0, new Vector3(-postX, 1f, 0f), new Vector3(0.1f, 2f, 0.1f));
            PlaceholderMeshes.AddBox(posts, 0, new Vector3(postX, 1f, 0f), new Vector3(0.1f, 2f, 0.1f));
            AddMesh(root, "Gate_Posts", posts, metal);
            AddBoxCollider(root, new Vector3(-postX, 1f, 0f), new Vector3(0.1f, 2f, 0.1f));
            AddBoxCollider(root, new Vector3(postX, 1f, 0f), new Vector3(0.1f, 2f, 0.1f));

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-GateWidth * 0.5f, 0f, 0f);
            var leafGo = new GameObject("Leaf");
            leafGo.transform.SetParent(hinge, false);
            var leaf = new MeshData(1);
            float bottom = GateBottomGap, top = GateBottomGap + GateHeight;
            PlaceholderMeshes.AddBox(leaf, 0, new Vector3(GateWidth * 0.5f, bottom + 0.03f, 0f), new Vector3(GateWidth, 0.06f, 0.04f));
            PlaceholderMeshes.AddBox(leaf, 0, new Vector3(GateWidth * 0.5f, top - 0.03f, 0f), new Vector3(GateWidth, 0.06f, 0.04f));
            for (float x = 0.075f; x < GateWidth; x += 0.15f)
                PlaceholderMeshes.AddBox(leaf, 0, new Vector3(x, (bottom + top) * 0.5f, 0f), new Vector3(0.025f, GateHeight, 0.025f));
            AddMesh(leafGo, "Gate_Leaf", leaf, metal);
            AddBoxCollider(leafGo, new Vector3(GateWidth * 0.5f, (bottom + top) * 0.5f, 0f), new Vector3(GateWidth, GateHeight, 0.06f));

            var source = Add3DSource(root);
            var gate = root.AddComponent<GateInteractable>();
            gate.Configure(hinge, unlatch, creak, latch);
            gate.SetAudioSource(source);
            gate.SetInteractionId("kos-gate");
            return root;
        }

        public static GameObject CreateDoor(Material wood, Material metal, AudioClip rattle)
        {
            var root = new GameObject("KosDoor");
            var slab = new MeshData(2);
            PlaceholderMeshes.AddBox(slab, 0, new Vector3(0f, 1.05f, 0.03f), new Vector3(0.9f, 2.1f, 0.06f));
            PlaceholderMeshes.AddBox(slab, 1, new Vector3(0.33f, 1f, 0.08f), new Vector3(0.12f, 0.03f, 0.04f));
            AddMesh(root, "KosDoor_Slab", slab, wood, metal);
            AddBoxCollider(root, new Vector3(0f, 1.05f, 0.03f), new Vector3(0.9f, 2.1f, 0.06f));
            var door = root.AddComponent<LockedDoorInteractable>();
            door.Configure(rattle);
            door.SetAudioSource(Add3DSource(root));
            door.SetInteractionId("kos-door");
            return root;
        }

        public static GameObject CreateMotor(Material body, Material dark, AudioClip keys)
        {
            var root = new GameObject("Motor_Placeholder");
            var m = new MeshData(2);
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.6f, 0f), new Vector3(0.32f, 0.3f, 1.1f));
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 0.85f, 0.5f), new Vector3(0.4f, 0.45f, 0.25f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 0.86f, -0.2f), new Vector3(0.3f, 0.1f, 0.7f));
            PlaceholderMeshes.AddBox(m, 1, new Vector3(0f, 1.08f, 0.62f), new Vector3(0.7f, 0.04f, 0.04f));
            AddWheel(m, 1, new Vector3(0f, 0.3f, 0.65f), 0.3f, 0.1f, 10);
            AddWheel(m, 1, new Vector3(0f, 0.3f, -0.65f), 0.3f, 0.1f, 10);
            AddMesh(root, "Motor_Body", m, body, dark);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.55f, 0f);
            box.size = new Vector3(0.7f, 1.1f, 1.9f);
            var vehicle = root.AddComponent<PlaceholderVehicleInteractable>();
            vehicle.Configure(keys);
            vehicle.SetAudioSource(Add3DSource(root));
            vehicle.SetInteractionId("kos-motor");
            return root;
        }

        /// <summary>Unit cube with its pivot at the bottom centre (scale = width, height, length).</summary>
        public static UnityEngine.Mesh UnitBoxMesh()
        {
            var data = new MeshData(1);
            PlaceholderMeshes.AddBox(data, 0, new Vector3(0f, 0.5f, 0f), Vector3.one);
            return data.ToMesh("UnitBox", out _);
        }

        /// <summary>Box from worldFrom to worldTo (y of worldFrom is its base), as a scaled unit box with a BoxCollider.</summary>
        public static GameObject CreateBlock(string name, Transform parent, Vector3 worldFrom, Vector3 worldTo, float height, float thickness,
            Material material, UnityEngine.Mesh unitBox)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, true);
            var span = worldTo - worldFrom;
            span.y = 0f;
            float length = span.magnitude;
            var rotation = length > 1e-4f ? Quaternion.LookRotation(span / length, Vector3.up) : Quaternion.identity;
            go.transform.SetPositionAndRotation((worldFrom + worldTo) * 0.5f, rotation);
            go.transform.position = new Vector3(go.transform.position.x, worldFrom.y, go.transform.position.z);
            go.transform.localScale = new Vector3(thickness, height, Mathf.Max(length, 0.01f));
            go.AddComponent<MeshFilter>().sharedMesh = unitBox;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = Vector3.one;
            return go;
        }

        /// <summary>Saves non-asset meshes under Prefabs/Meshes, writes the prefab, destroys the scene object.</summary>
        public static GameObject SaveAsPrefab(GameObject go, string path)
        {
            SaveMeshes(go, PlayerPaths.MeshesFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// Saves every non-asset mesh under <paramref name="folder"/> as "&lt;mesh name&gt;.asset" (an existing asset is
        /// updated in place) and points the filters at the saved meshes; a mesh shared by several filters is saved once.
        /// </summary>
        public static void SaveMeshes(GameObject go, string folder)
        {
            AssetFolders.Ensure(folder);
            var saved = new Dictionary<UnityEngine.Mesh, UnityEngine.Mesh>();
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh)) continue;
                if (!saved.TryGetValue(mesh, out var asset))
                {
                    string meshPath = folder + "/" + mesh.name + ".asset";
                    asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(meshPath);
                    if (asset == null)
                    {
                        AssetDatabase.CreateAsset(mesh, meshPath);
                        asset = mesh;
                    }
                    else EditorUtility.CopySerialized(mesh, asset);
                    saved[mesh] = asset;
                }
                filter.sharedMesh = asset;
            }
            foreach (var pair in saved)
                if (pair.Key != pair.Value) Object.DestroyImmediate(pair.Key);
        }

        static void AddMesh(GameObject go, string meshName, MeshData data, params Material[] materials)
        {
            go.AddComponent<MeshFilter>().sharedMesh = data.ToMesh(meshName, out _);
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        static void AddBoxCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        static AudioSource Add3DSource(GameObject go)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = 25f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        /// <summary>Cylinder whose axis is local X (a wheel seen from the side).</summary>
        public static void AddWheel(MeshData m, int sub, Vector3 center, float radius, float width, int sides)
        {
            var half = Vector3.right * (width * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                var d0 = new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * radius;
                var d1 = new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * radius;
                var outward = ((d0 + d1) * 0.5f).normalized;
                m.AddQuad(sub, center - half + d0, center - half + d1, center + half + d1, center + half + d0,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, outward);
                m.AddTriangle(sub, center + half, center + half + d0, center + half + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.right);
                m.AddTriangle(sub, center - half, center - half + d0, center - half + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.left);
            }
        }
    }
}
