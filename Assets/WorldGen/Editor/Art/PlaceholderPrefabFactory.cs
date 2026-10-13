using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class PlaceholderPrefabs
    {
        public GameObject TreeA, TreeB, Lamp;
    }

    /// <summary>Creates low-poly placeholder prefabs (two trees, one street lamp). Re-running updates them in place.</summary>
    public static class PlaceholderPrefabFactory
    {
        public const string MeshFolder = WorldGenPaths.PrefabsFolder + "/Meshes";

        public static PlaceholderPrefabs CreateAll(IReadOnlyDictionary<string, Material> materials)
        {
            AssetFolders.Ensure(MeshFolder);
            var cleanScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isDirty && s.path.Length > 0) cleanScenes.Add(s);
            }

            var trunk = materials["M_Tree_Trunk"];
            var leaves = materials["M_Tree_Leaves"];
            var metal = materials["M_Lamp_Metal"];
            var head = materials["M_Lamp_Head"];

            var treeA = new MeshData(2);
            PlaceholderMeshes.AddCylinder(treeA, 0, Vector3.zero, 0.18f, 2.2f, 6);
            PlaceholderMeshes.AddIcosahedron(treeA, 1, new Vector3(0f, 3.0f, 0f), 1.6f);
            var treeB = new MeshData(2);
            PlaceholderMeshes.AddCylinder(treeB, 0, Vector3.zero, 0.15f, 1.6f, 6);
            PlaceholderMeshes.AddCone(treeB, 1, new Vector3(0f, 1.4f, 0f), 1.4f, 3.2f, 6);
            var lamp = new MeshData(2);
            PlaceholderMeshes.AddCylinder(lamp, 0, Vector3.zero, 0.08f, 6f, 6);
            PlaceholderMeshes.AddBox(lamp, 0, new Vector3(0f, 5.9f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f));
            PlaceholderMeshes.AddBox(lamp, 1, new Vector3(0f, 5.85f, 1.2f), new Vector3(0.35f, 0.12f, 0.25f));

            var result = new PlaceholderPrefabs
            {
                TreeA = SavePrefab("Tree_LowPoly_A", treeA, new[] { trunk, leaves }, go => AddCapsule(go, 0.2f, 2.2f)),
                TreeB = SavePrefab("Tree_LowPoly_B", treeB, new[] { trunk, leaves }, go => AddCapsule(go, 0.2f, 1.6f)),
                Lamp = SavePrefab("StreetLamp_A", lamp, new[] { metal, head }, go =>
                {
                    AddCapsule(go, 0.1f, 6f);
                    var light = AddLampLight(go);
                    go.AddComponent<StreetLamp>().Initialize(light, go.GetComponent<MeshRenderer>(), 1);
                })
            };

            foreach (var s in cleanScenes)
                if (s.IsValid() && s.isDirty) EditorSceneManager.SaveScene(s);
            return result;
        }

        static Mesh SaveMesh(string name, MeshData data)
        {
            string path = MeshFolder + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                data.WriteTo(mesh, out _);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                data.WriteTo(mesh, out _);
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        static GameObject SavePrefab(string name, MeshData data, Material[] materials, Action<GameObject> decorate)
        {
            var mesh = SaveMesh(name, data);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, preview);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                decorate(go);
                return PrefabUtility.SaveAsPrefabAsset(go, WorldGenPaths.PrefabsFolder + "/" + name + ".prefab");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void AddCapsule(GameObject go, float radius, float height)
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.direction = 1;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }

        static Light AddLampLight(GameObject lamp)
        {
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(lamp.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 5.6f, 1.2f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 3f;
            light.color = new Color(1f, 0.77f, 0.54f);
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
