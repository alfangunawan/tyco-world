using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class StreetGeometryTests
    {
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static List<Vector2> Square(float x, float y, float w, float h) =>
            new List<Vector2> { V(x, y), V(x + w, y), V(x + w, y + h), V(x, y + h) };

        [Test]
        public void Cumulative_AndPointAt_FollowArcLength()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(10, 10) };
            var cum = Polyline2D.Cumulative(pts);
            CollectionAssert.AreEqual(new[] { 0f, 10f, 20f }, cum);
            var p = Polyline2D.PointAt(pts, cum, 15f, out var t);
            Assert.AreEqual(V(10, 5), p);
            Assert.AreEqual(V(0, 1), t);
            Assert.AreEqual(V(10, 10), Polyline2D.PointAt(pts, cum, 99f, out _), "clamped to the end");
            Assert.AreEqual(V(0, 0), Polyline2D.PointAt(pts, cum, -5f, out _), "clamped to the start");
        }

        [Test]
        public void Slice_KeepsInteriorVertices()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(10, 10) };
            var along = new List<float>();
            var slice = Polyline2D.Slice(pts, Polyline2D.Cumulative(pts), 5f, 15f, along);
            CollectionAssert.AreEqual(new[] { V(5, 0), V(10, 0), V(10, 5) }, slice);
            CollectionAssert.AreEqual(new[] { 5f, 10f, 15f }, along);
        }

        [Test]
        public void Offset_StraightLine_IsParallel()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0) };
            var along = new List<float>();
            var left = Polyline2D.Offset(pts, new[] { 0f, 10f }, 2f, along);
            CollectionAssert.AreEqual(new[] { V(0, 2), V(10, 2) }, left);
            var right = Polyline2D.Offset(pts, new[] { 0f, 10f }, -2f, along);
            CollectionAssert.AreEqual(new[] { V(0, -2), V(10, -2) }, right);
            CollectionAssert.AreEqual(new[] { 0f, 10f }, along);
        }

        [Test]
        public void Offset_RightAngle_MitersToTheCorner()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(10, 10) };
            var along = new List<float>();
            var inner = Polyline2D.Offset(pts, new[] { 0f, 10f, 20f }, 1f, along);
            Assert.AreEqual(3, inner.Count);
            Assert.AreEqual(9f, inner[1].x, 1e-4f);
            Assert.AreEqual(1f, inner[1].y, 1e-4f);
            var outer = Polyline2D.Offset(pts, new[] { 0f, 10f, 20f }, -1f, along);
            Assert.AreEqual(11f, outer[1].x, 1e-4f);
            Assert.AreEqual(-1f, outer[1].y, 1e-4f);
        }

        [Test]
        public void Offset_Hairpin_BevelsWithTheSameCountAtAnyDistance()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(0, 1) };
            var s = new[] { 0f, 10f, 10f + Vector2.Distance(V(10, 0), V(0, 1)) };
            var along = new List<float>();
            var a = Polyline2D.Offset(pts, s, 1f, along);
            var b = Polyline2D.Offset(pts, s, 3f, along);
            Assert.AreEqual(4, a.Count);
            Assert.AreEqual(4, b.Count);
            CollectionAssert.AreEqual(new[] { 0f, 10f, 10f, s[2] }, along);
        }

        [Test]
        public void Project_FindsArcLengthAndDistance()
        {
            var pts = new List<Vector2> { V(0, 0), V(10, 0), V(10, 10) };
            float s = Polyline2D.Project(pts, Polyline2D.Cumulative(pts), V(12, 4), out float d);
            Assert.AreEqual(14f, s, 1e-4f);
            Assert.AreEqual(2f, d, 1e-4f);
            Assert.AreEqual(2f, Polyline2D.DistanceTo(pts, V(12, 4)), 1e-4f);
        }

        [Test]
        public void RayToRing_HitsTheNearestEdge()
        {
            var ring = Square(10, 0, 10, 10);
            Assert.AreEqual(10f, Polyline2D.RayToRing(V(0, 5), V(1, 0), ring, 50f), 1e-4f);
            Assert.IsTrue(float.IsPositiveInfinity(Polyline2D.RayToRing(V(0, 5), V(-1, 0), ring, 50f)));
            Assert.IsTrue(float.IsPositiveInfinity(Polyline2D.RayToRing(V(0, 5), V(1, 0), ring, 5f)), "beyond maxDistance");
        }

        [Test]
        public void Bisect_ConvergesOnTheKeepSide()
        {
            Assert.AreEqual(3f, Polyline2D.Bisect(0f, 4f, x => x < 3.3f, 3), 1e-5f);
            Assert.AreEqual(4f, Polyline2D.Bisect(4f, 0f, x => x > 3.6f, 1), 1e-5f);
        }

        [Test]
        public void RingsOverlap_DetectsCrossingContainedAndApart()
        {
            Assert.IsTrue(Polygon2D.RingsOverlap(Square(0, 0, 10, 10), Square(5, 5, 10, 10)));
            Assert.IsTrue(Polygon2D.RingsOverlap(Square(0, 0, 10, 10), Square(2, 2, 2, 2)));
            Assert.IsTrue(Polygon2D.RingsOverlap(Square(2, 2, 2, 2), Square(0, 0, 10, 10)));
            Assert.IsFalse(Polygon2D.RingsOverlap(Square(0, 0, 10, 10), Square(20, 0, 5, 5)));
        }

        [Test]
        public void SegmentDistance_ZeroWhenCrossing()
        {
            Assert.AreEqual(0f, Polygon2D.SegmentDistance(V(0, -1), V(0, 1), V(-1, 0), V(1, 0)), 1e-6f);
            Assert.AreEqual(2f, Polygon2D.SegmentDistance(V(0, 0), V(10, 0), V(5, 2), V(5, 8)), 1e-6f);
        }

        [Test]
        public void SolidBox_HasFiveOutwardFaces()
        {
            var m = new MeshData(1);
            SolidBox.Add(m, 0, V(0, 0), V(2, 0), 0f, 1f, 0.2f, BoxUv.Meters);
            Assert.AreEqual(20, m.Vertices.Count);
            Assert.AreEqual(10, m.TriangleCount);
            var centre = new Vector3(1f, 0.5f, 0f);
            var idx = m.Indices(0);
            for (int t = 0; t < idx.Count; t += 3)
            {
                var c = (m.Vertices[idx[t]] + m.Vertices[idx[t + 1]] + m.Vertices[idx[t + 2]]) / 3f;
                Assert.Greater(Vector3.Dot(c - centre, m.Normals[idx[t]]), 0f, "face " + t / 3);
            }
            Assert.AreEqual(-0.1f, m.Vertices.Min(v => v.z), 1e-5f);
            Assert.AreEqual(1f, m.Vertices.Max(v => v.y), 1e-5f);
        }

        [Test]
        public void SolidBox_WithoutEnds_HasThreeFaces()
        {
            var m = new MeshData(1);
            SolidBox.Add(m, 0, V(0, 0), V(2, 0), 0f, 1f, 0.2f, BoxUv.Meters, false);
            Assert.AreEqual(6, m.TriangleCount);
        }

        [Test]
        public void SolidBox_WithoutTop_OrWithOneEnd_DropsExactlyThoseFaces()
        {
            var noTop = new MeshData(1);
            SolidBox.Add(noTop, 0, V(0, 0), V(2, 0), 0f, 1f, 0.2f, BoxUv.Meters, BoxEnds.Both, false);
            Assert.AreEqual(8, noTop.TriangleCount);
            Assert.AreEqual(1f, noTop.Vertices.Max(v => v.y), 1e-5f);
            var startOnly = new MeshData(1);
            SolidBox.Add(startOnly, 0, V(0, 0), V(2, 0), 0f, 1f, 0.2f, BoxUv.Meters, BoxEnds.Start, true);
            Assert.AreEqual(8, startOnly.TriangleCount);
            var none = new MeshData(1);
            SolidBox.Add(none, 0, V(0, 0), V(2, 0), 0f, 1f, 0.2f, BoxUv.Meters, BoxEnds.None, false);
            Assert.AreEqual(4, none.TriangleCount);
        }

        [Test]
        public void AtlasBand_MapsIntoTheInsetBand()
        {
            var uv = BoxUv.AtlasBand(2, 8, 2f, 4f);
            Assert.AreEqual(1f, uv.Side(4f, 2f).x, 1e-6f);
            Assert.AreEqual(2.98f / 8f, uv.Side(4f, 2f).y, 1e-6f);
            Assert.AreEqual(2.02f / 8f, uv.Side(0f, 0f).y, 1e-6f);
            Assert.AreEqual(2.98f / 8f, uv.Side(0f, 5f).y, 1e-6f, "heights above the band clamp to its top");
            Assert.AreEqual(new Vector2(3f, 0.15f), BoxUv.Meters.Top(3f, 0.15f, 1f));
        }

        [Test]
        public void Append_OffsetsIndicesIntoMappedSubmeshes()
        {
            var a = new MeshData(2);
            a.AddQuad(0, Vector3.zero, Vector3.right, new Vector3(1, 0, 1), Vector3.forward, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            var b = new MeshData(1);
            b.AddQuad(0, Vector3.zero, Vector3.right, new Vector3(1, 0, 1), Vector3.forward, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
            a.Append(b, new[] { 1 });
            Assert.AreEqual(8, a.Vertices.Count);
            Assert.AreEqual(8, a.Normals.Count);
            Assert.AreEqual(8, a.Uvs.Count);
            Assert.AreEqual(6, a.Indices(0).Count);
            CollectionAssert.AreEqual(new[] { 4, 5, 6, 4, 6, 7 }, a.Indices(1));
            var c = new MeshData(2);
            c.Append(a);
            CollectionAssert.AreEqual(a.Indices(1), c.Indices(1));
        }
    }
}
