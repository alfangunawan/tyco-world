using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.WorldGen.Tests
{
    public class MeshBuilderTests
    {
        static Vector3 FaceNormal(MeshData m, int sub, int tri)
        {
            var idx = m.Indices(sub);
            Vector3 a = m.Vertices[idx[tri * 3]], b = m.Vertices[idx[tri * 3 + 1]], c = m.Vertices[idx[tri * 3 + 2]];
            return Vector3.Cross(b - a, c - a).normalized;
        }

        static float DistanceToPolyline(Vector2 p, IList<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < line.Count; i++) best = Mathf.Min(best, Polygon2D.DistanceToSegment(p, line[i], line[i + 1]));
            return best;
        }

        static MeshData Ribbon(float width, bool extendStart, params Vector2[] pts)
        {
            var m = new MeshData(1);
            RoadMeshBuilder.AddRibbon(m, 0, pts, width, 0.07f, extendStart, false);
            return m;
        }

        [Test]
        public void Ribbon_OnALoopWay_MitersItsSeam()
        {
            // A closed square way starting at a corner: both ends must meet in the corner's miter, not leave a notch.
            var m = Ribbon(5f, false, new Vector2(0, 0), new Vector2(20, 0), new Vector2(20, 20), new Vector2(0, 20), new Vector2(0, 0));
            var corners = m.Vertices.Where(v => new Vector2(v.x, v.z).magnitude < 3.75f)
                .Select(v => new Vector2(Mathf.Round(v.x * 1000f), Mathf.Round(v.z * 1000f))).Distinct().Count();
            Assert.AreEqual(2, corners, "one inner and one outer corner at the seam");
        }

        [Test]
        public void Ribbon_OnALoopWayWithASharpSeam_BevelsIt()
        {
            // A thin triangle starting at its sharp (171°) corner: too sharp to miter, so the seam needs a bevel triangle.
            var m = Ribbon(4f, false, new Vector2(0, 0), new Vector2(20, 0), new Vector2(19, 3), new Vector2(0, 0));
            Vector2 n0 = new Vector2(0f, 1f), n1 = Vector2.Perpendicular(new Vector2(-19f, -3f).normalized);
            var outside = -(n0 + n1).normalized * 0.1f;
            bool covered = false;
            for (int i = 0; i + 2 < m.Indices(0).Count; i += 3)
            {
                var idx = m.Indices(0);
                Vector3 a = m.Vertices[idx[i]], b = m.Vertices[idx[i + 1]], c = m.Vertices[idx[i + 2]];
                if (Polygon2D.ContainsPoint(new List<Vector2> { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z) }, outside)) covered = true;
            }
            Assert.IsTrue(covered, "the outside of the seam at " + outside + " is asphalt");
        }

        [Test]
        public void AddQuad_FlipsToDesiredNormal()
        {
            var m = new MeshData(1);
            m.AddQuad(0, new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
            Assert.Greater(Vector3.Dot(FaceNormal(m, 0, 0), Vector3.up), 0.99f);
            Assert.Greater(Vector3.Dot(FaceNormal(m, 0, 1), Vector3.up), 0.99f);
            foreach (var n in m.Normals) Assert.Greater(n.y, 0.99f);
        }

        [Test]
        public void AddTriangle_DegenerateIsSkipped()
        {
            var m = new MeshData(1);
            m.AddTriangle(0, Vector3.zero, Vector3.right, Vector3.right * 2f, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            Assert.IsTrue(m.IsEmpty);
        }

        [Test]
        public void ToMesh_DropsEmptySubmeshes()
        {
            var m = new MeshData(4);
            m.AddTriangle(1, Vector3.zero, Vector3.forward, Vector3.right, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            m.AddTriangle(3, Vector3.zero, Vector3.forward, Vector3.right, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            var mesh = m.ToMesh("t", out var used);
            try
            {
                CollectionAssert.AreEqual(new[] { 1, 3 }, used);
                Assert.AreEqual(2, mesh.subMeshCount);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ToMesh_UsesUInt32AboveLimit()
        {
            var m = new MeshData(1);
            for (int i = 0; i < 22000; i++)
                m.AddTriangle(0, new Vector3(i, 0, 0), new Vector3(i, 0, 1), new Vector3(i + 1, 0, 0), Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            var mesh = m.ToMesh("big", out _);
            try { Assert.AreEqual(IndexFormat.UInt32, mesh.indexFormat); }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ContentHash_StableAndSensitive()
        {
            var a = Ribbon(6f, false, new Vector2(0, 0), new Vector2(20, 0));
            var b = Ribbon(6f, false, new Vector2(0, 0), new Vector2(20, 0));
            var c = Ribbon(6f, false, new Vector2(0, 0), new Vector2(21, 0));
            Assert.AreEqual(a.ContentHash(), b.ContentHash());
            Assert.AreNotEqual(a.ContentHash(), c.ContentHash());
        }

        [Test]
        public void Ribbon_Straight_HasConstantHalfWidth()
        {
            var m = Ribbon(6f, false, new Vector2(0, 0), new Vector2(20, 0));
            Assert.AreEqual(4, m.Vertices.Count);
            foreach (var v in m.Vertices)
            {
                Assert.AreEqual(3f, Mathf.Abs(v.z), 1e-4f);
                Assert.AreEqual(0.07f, v.y, 1e-6f);
            }
        }

        [Test]
        public void Ribbon_RightAngle_UsesMiter()
        {
            var m = Ribbon(2f, false, new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10));
            Assert.AreEqual(8, m.Vertices.Count);
            bool foundOuterCorner = false;
            foreach (var v in m.Vertices)
                if (Vector2.Distance(new Vector2(v.x, v.z), new Vector2(11, -1)) < 1e-3f) foundOuterCorner = true;
            Assert.IsTrue(foundOuterCorner);
        }

        [Test]
        public void Ribbon_SharpTurn_RespectsMiterLimit()
        {
            var line = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 1) };
            var m = Ribbon(4f, false, line.ToArray());
            foreach (var v in m.Vertices)
                Assert.LessOrEqual(DistanceToPolyline(new Vector2(v.x, v.z), line), RoadMeshBuilder.MiterLimit * 2f + 1e-3f);
        }

        [Test]
        public void Ribbon_UTurn_ProducesNoNaN()
        {
            var m = Ribbon(4f, false, new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 0));
            Assert.IsFalse(m.IsEmpty);
            Assert.IsFalse(m.HasInvalidValues());
        }

        [Test]
        public void Ribbon_DuplicatePoints_Ignored()
        {
            var m = Ribbon(4f, false, new Vector2(0, 0), new Vector2(0, 0), new Vector2(5, 0));
            Assert.AreEqual(4, m.Vertices.Count);
        }

        [Test]
        public void Ribbon_ExtendStart_AddsHalfWidth()
        {
            var m = Ribbon(4f, true, new Vector2(0, 0), new Vector2(10, 0));
            float minX = float.MaxValue;
            foreach (var v in m.Vertices) minX = Mathf.Min(minX, v.x);
            Assert.AreEqual(-2f, minX, 1e-4f);
        }

        [Test]
        public void Ribbon_UvAcrossWidth()
        {
            var m = Ribbon(6f, false, new Vector2(0, 0), new Vector2(20, 0));
            foreach (var uv in m.Uvs) Assert.IsTrue(Mathf.Abs(uv.x) < 1e-4f || Mathf.Abs(uv.x - 6f) < 1e-4f);
        }

        [Test]
        public void Ribbon_FacesUp()
        {
            var m = Ribbon(3f, false, new Vector2(0, 0), new Vector2(10, 3), new Vector2(15, -4), new Vector2(30, 0));
            for (int t = 0; t < m.TriangleCount; t++) Assert.Greater(FaceNormal(m, 0, t).y, 0.99f);
        }

        [Test]
        public void FlatPolygon_UpFacingAndAreaPreserved()
        {
            var m = new MeshData(1);
            var square = new Polygon(new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) });
            Assert.IsTrue(AreaMeshBuilder.AddFlatPolygon(m, 0, square, 0.025f));
            Assert.AreEqual(2, m.TriangleCount);
            float area = 0f;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                Assert.Greater(FaceNormal(m, 0, t).y, 0.99f);
                var idx = m.Indices(0);
                Vector3 a = m.Vertices[idx[t * 3]], b = m.Vertices[idx[t * 3 + 1]], c = m.Vertices[idx[t * 3 + 2]];
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            Assert.AreEqual(100f, area, 1e-3f);
        }

        [Test]
        public void FlatPolygon_FaceDown()
        {
            var m = new MeshData(1);
            var square = new Polygon(new List<Vector2> { new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 4), new Vector2(0, 4) });
            Assert.IsTrue(AreaMeshBuilder.AddFlatPolygon(m, 0, square, 3f, faceDown: true));
            for (int t = 0; t < m.TriangleCount; t++) Assert.Less(FaceNormal(m, 0, t).y, -0.99f);
        }

        [Test]
        public void Ground_CoversBoundsAndColliderTopAtZero()
        {
            var bounds = Rect.MinMaxRect(-10, -20, 30, 40);
            var m = GroundBuilder.Build(bounds);
            Assert.AreEqual(4, m.Vertices.Count);
            foreach (var v in m.Vertices)
            {
                Assert.IsTrue(Mathf.Approximately(v.x, -10) || Mathf.Approximately(v.x, 30));
                Assert.IsTrue(Mathf.Approximately(v.z, -20) || Mathf.Approximately(v.z, 40));
                Assert.AreEqual(0f, v.y);
            }
            GroundBuilder.ColliderBox(bounds, out var center, out var size);
            Assert.AreEqual(0f, center.y + size.y / 2f, 1e-6f);
            Assert.AreEqual(40f, size.x, 1e-4f);
            Assert.AreEqual(60f, size.z, 1e-4f);
        }
    }
}
