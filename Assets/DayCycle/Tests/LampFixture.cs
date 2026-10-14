using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    /// <summary>Code-built street lamps that use the real lamp materials, for lamp and director tests.</summary>
    internal static class LampFixture
    {
        public static Material HeadAsset => AssetDatabase.LoadAssetAtPath<Material>(WorldGenPaths.MaterialsFolder + "/M_Lamp_Head.mat");
        public static Material MetalAsset => AssetDatabase.LoadAssetAtPath<Material>(WorldGenPaths.MaterialsFolder + "/M_Lamp_Metal.mat");

        public static StreetLamp Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("TestLamp");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { MetalAsset, HeadAsset };
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            var lamp = go.AddComponent<StreetLamp>();
            lamp.Initialize(light, renderer, 1);
            return lamp;
        }

        /// <summary>First position along +x (3 m apart, far from the world) whose lamp gets the requested role.</summary>
        public static Vector3 PositionWithRole(LampRole role, DayCycleProfile profile)
        {
            for (int i = 0; i < 10000; i++)
            {
                var p = new Vector3(5000f + i * 3f, 1000f, 5000f);
                if (LampFlicker.RoleFor(LampFlicker.Seed(p), profile.flickerFraction, profile.deadFraction) == role) return p;
            }
            throw new System.InvalidOperationException("Tidak ada posisi dengan peran " + role);
        }
    }
}
