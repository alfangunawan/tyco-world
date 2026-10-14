using System;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>A code-built generated-world hierarchy (Group/Chunk_x_z/cubes) far outside the real world.</summary>
    internal sealed class CullingFixture : IDisposable
    {
        public static readonly Vector3 Origin = new Vector3(5000f, 1000f, 5000f);

        public readonly GameObject Root;
        public readonly GeneratedRoot World;

        public CullingFixture()
        {
            Root = new GameObject("World_Generated_Test");
            World = Root.AddComponent<GeneratedRoot>();
        }

        /// <summary>A 1 m cube (MeshRenderer + BoxCollider) under <paramref name="path"/>, centred at Origin + local.</summary>
        public GameObject Cube(string path, Vector3 local)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(Ensure(path), false);
            cube.transform.position = Origin + local;
            return cube;
        }

        public Transform Ensure(string path)
        {
            var t = Root.transform;
            foreach (var part in path.Split('/'))
            {
                var child = t.Find(part);
                if (child == null)
                {
                    child = new GameObject(part).transform;
                    child.SetParent(t, false);
                }
                t = child;
            }
            return t;
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
