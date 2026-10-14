using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    public class LowPolyMeshTests
    {
        [TestCase(PrimitiveType.Sphere, 1f, 1f, 120)]
        [TestCase(PrimitiveType.Capsule, 1f, 2f, 160)]
        [TestCase(PrimitiveType.Cylinder, 1f, 2f, 60)]
        public void SameSizeAsUnitysPrimitive_FewTriangles_FacingOut(PrimitiveType type, float width, float height, int maxTriangles)
        {
            var mesh = LowPolyMeshes.For(type);
            Assert.IsNotNull(mesh);
            Assert.AreSame(mesh, LowPolyMeshes.For(type), "built once, shared");
            Assert.AreEqual(width, mesh.bounds.size.x, 1e-3f);
            Assert.AreEqual(height, mesh.bounds.size.y, 1e-3f);
            Assert.AreEqual(width, mesh.bounds.size.z, 1e-3f);
            Assert.AreEqual(Vector3.zero, mesh.bounds.center);
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            Assert.LessOrEqual(t.Length / 3, maxTriangles);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var face = Vector3.Cross(b - a, c - a);
                Assert.Greater(face.sqrMagnitude, 1e-10f, "no degenerate triangles");
                var centre = (a + b + c) / 3f;
                // Unity's front faces are clockwise: their cross product points out of the solid, along the vertex normals.
                Assert.Greater(Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]), 0f, type + " triangle " + i / 3 + " faces in");
                Assert.Greater(Vector3.Dot(face, centre), 0f, type + " triangle " + i / 3 + " faces the centre");
            }
        }

        [Test]
        public void Cubes_KeepUnitysMesh_AndParts_UseTheLowPolyOnes()
        {
            Assert.IsNull(LowPolyMeshes.For(PrimitiveType.Cube));
            var parent = new GameObject("Parent").transform;
            try
            {
                var ball = Primitives.Part(PrimitiveType.Sphere, parent, "Ball", Vector3.zero, Vector3.one, null, keepCollider: true);
                Assert.AreSame(LowPolyMeshes.For(PrimitiveType.Sphere), ball.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsNotNull(ball.GetComponent<SphereCollider>(), "the collider stays analytic");
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }
    }
}
