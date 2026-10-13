using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class TriangulatorTests
    {
        static List<Vector2> V(params float[] xy)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < xy.Length; i += 2) list.Add(new Vector2(xy[i], xy[i + 1]));
            return list;
        }

        static float SignedTriangleArea(List<Vector2> v, List<int> t)
        {
            float sum = 0f;
            for (int i = 0; i < t.Count; i += 3)
                sum += Polygon2D.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]) * 0.5f;
            return sum;
        }

        static (List<Vector2> v, List<int> t) Run(Polygon polygon)
        {
            var v = new List<Vector2>();
            var t = new List<int>();
            Assert.IsTrue(Triangulator.Triangulate(polygon, v, t));
            return (v, t);
        }

        static List<Vector2> Comb() => V(0, 0, 10, 0, 10, 10, 8, 10, 8, 2, 6, 2, 6, 10, 4, 10, 4, 2, 2, 2, 2, 10, 0, 10);

        [Test]
        public void Square_TwoTriangles()
        {
            var (v, t) = Run(new Polygon(V(0, 0, 10, 0, 10, 10, 0, 10)));
            Assert.AreEqual(2, t.Count / 3);
            Assert.AreEqual(100f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void LShape_FourTriangles()
        {
            var (v, t) = Run(new Polygon(V(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10)));
            Assert.AreEqual(4, t.Count / 3);
            Assert.AreEqual(64f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void ClockwiseOuter_StillProducesCcwTriangles()
        {
            var (v, t) = Run(new Polygon(V(0, 0, 0, 10, 10, 10, 10, 0)));
            Assert.AreEqual(100f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void SquareWithHole_CoversRingOnly()
        {
            var poly = new Polygon(V(0, 0, 10, 0, 10, 10, 0, 10), new List<List<Vector2>> { V(3, 3, 7, 3, 7, 7, 3, 7) });
            var (v, t) = Run(poly);
            Assert.AreEqual(8, t.Count / 3);
            Assert.AreEqual(84f, SignedTriangleArea(v, t), 1e-3f);
            for (int i = 0; i < t.Count; i += 3)
            {
                var centroid = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                Assert.IsFalse(centroid.x > 3f && centroid.x < 7f && centroid.y > 3f && centroid.y < 7f, "triangle inside hole");
            }
        }

        [Test]
        public void TwoHoles_AreaPreserved()
        {
            var poly = new Polygon(V(0, 0, 20, 0, 20, 10, 0, 10),
                new List<List<Vector2>> { V(2, 2, 6, 2, 6, 6, 2, 6), V(12, 3, 16, 3, 16, 7, 12, 7) });
            var (v, t) = Run(poly);
            Assert.AreEqual(14, t.Count / 3);
            Assert.AreEqual(168f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void Comb_AreaPreserved()
        {
            var (v, t) = Run(new Polygon(Comb()));
            Assert.AreEqual(10, t.Count / 3);
            Assert.AreEqual(68f, SignedTriangleArea(v, t), 1e-3f);
        }

        [Test]
        public void AllTriangles_AreCcw()
        {
            var (v, t) = Run(new Polygon(Comb()));
            for (int i = 0; i < t.Count; i += 3)
                Assert.Greater(Polygon2D.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]), 0f);
        }

        [Test]
        public void Bowtie_DoesNotThrow()
        {
            var v = new List<Vector2>();
            var t = new List<int>();
            Assert.DoesNotThrow(() => Triangulator.Triangulate(new Polygon(V(0, 0, 10, 10, 10, 0, 0, 10)), v, t));
        }

        [Test]
        public void Degenerate_ReturnsFalse()
        {
            Assert.IsFalse(Triangulator.Triangulate(new Polygon(V(0, 0, 1, 1)), new List<Vector2>(), new List<int>()));
        }
    }
}
