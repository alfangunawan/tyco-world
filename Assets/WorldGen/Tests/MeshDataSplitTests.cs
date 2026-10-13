using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class MeshDataSplitTests
    {
        internal static float MaxEdge(MeshData m)
        {
            float max = 0f;
            for (int s = 0; s < m.SubmeshCount; s++)
            {
                var idx = m.Indices(s);
                for (int t = 0; t < idx.Count; t += 3)
                {
                    Vector3 a = m.Vertices[idx[t]], b = m.Vertices[idx[t + 1]], c = m.Vertices[idx[t + 2]];
                    max = Mathf.Max(max, Mathf.Max(Vector3.Distance(a, b), Mathf.Max(Vector3.Distance(b, c), Vector3.Distance(c, a))));
                }
            }
            return max;
        }

        static float Area(MeshData m)
        {
            float sum = 0f;
            for (int s = 0; s < m.SubmeshCount; s++)
            {
                var idx = m.Indices(s);
                for (int t = 0; t < idx.Count; t += 3)
                    sum += Vector3.Cross(m.Vertices[idx[t + 1]] - m.Vertices[idx[t]], m.Vertices[idx[t + 2]] - m.Vertices[idx[t]]).magnitude * 0.5f;
            }
            return sum;
        }

        static MeshData BigTriangle()
        {
            var m = new MeshData(2);
            Vector2 a = new Vector2(0f, 0f), b = new Vector2(600f, 0f), c = new Vector2(0f, 450f);
            m.AddTriangle(1, Xz.At(a, 0.03f), Xz.At(b, 0.03f), Xz.At(c, 0.03f), a, b, c, Vector3.up);
            return m;
        }

        [Test]
        public void SplitLongTriangles_BoundsEveryEdge_KeepsAreaUvNormalAndSubmesh()
        {
            var m = BigTriangle();
            var split = m.SplitLongTriangles(100f);
            Assert.AreNotSame(m, split);
            Assert.AreEqual(0, split.Indices(0).Count);
            Assert.Greater(split.TriangleCount, 1);
            Assert.LessOrEqual(MaxEdge(split), 100f + 1e-3f);
            Assert.AreEqual(Area(m), Area(split), 1f);
            for (int i = 0; i < split.Vertices.Count; i++)
            {
                Assert.AreEqual(split.Vertices[i].x, split.Uvs[i].x, 1e-3f);
                Assert.AreEqual(split.Vertices[i].z, split.Uvs[i].y, 1e-3f);
                Assert.AreEqual(0.03f, split.Vertices[i].y, 1e-5f);
                Assert.Greater(split.Normals[i].y, 0.99f);
            }
        }

        [Test]
        public void SplitLongTriangles_ShortMeshIsReturnedAsIs()
        {
            var m = new MeshData(1);
            m.AddTriangle(0, Vector3.zero, Vector3.right * 10f, Vector3.forward * 10f, Vector2.zero, Vector2.right, Vector2.up, Vector3.up);
            Assert.AreSame(m, m.SplitLongTriangles(100f));
        }

        [Test]
        public void SplitLongTriangles_IsDeterministic() =>
            Assert.AreEqual(BigTriangle().SplitLongTriangles(100f).ContentHash(), BigTriangle().SplitLongTriangles(100f).ContentHash());
    }
}
