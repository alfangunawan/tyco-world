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

        /// <summary>Points every 1 m around an arc of the given radius (left turn), starting east from the origin.</summary>
        static List<Vector2> Arc(float radius, float degrees)
        {
            float step = 2f * Mathf.Asin(0.5f / radius) * Mathf.Rad2Deg;
            var pts = new List<Vector2> { V(-10, 0), V(0, 0) };
            float heading = 0f;
            for (float turned = 0f; turned < degrees; turned += step)
            {
                heading += step * Mathf.Deg2Rad;
                pts.Add(pts[pts.Count - 1] + V(Mathf.Cos(heading), Mathf.Sin(heading)));
            }
            pts.Add(pts[pts.Count - 1] + V(Mathf.Cos(heading), Mathf.Sin(heading)) * 10f);
            return pts;
        }

        static bool CrossesItself(List<Vector2> line)
        {
            for (int a = 0; a + 1 < line.Count; a++)
                for (int b = a + 2; b + 1 < line.Count; b++)
                {
                    Vector2 p = line[a], r = line[a + 1] - p, q = line[b], s = line[b + 1] - q;
                    float denom = Polygon2D.Cross(r, s);
                    if (r.sqrMagnitude < 1e-8f || s.sqrMagnitude < 1e-8f || Mathf.Abs(denom) < 1e-9f) continue;
                    float t = Polygon2D.Cross(q - p, s) / denom, u = Polygon2D.Cross(q - p, r) / denom;
                    if (t > 1e-3f && t < 1f - 1e-3f && u > 1e-3f && u < 1f - 1e-3f) return true;
                }
            return false;
        }

        /// <summary>Every offset segment of real length runs the same way as the centreline at its arc position (no fold runs backwards).</summary>
        static void AssertRunsForward(IReadOnlyList<Vector2> pts, List<Vector2> line, List<float> along, string label)
        {
            var cum = Polyline2D.Cumulative(pts);
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var d = line[i + 1] - line[i];
                if (d.magnitude < 1e-3f || along[i + 1] - along[i] < 1e-4f) continue;
                Polyline2D.PointAt(pts, cum, 0.5f * (along[i] + along[i + 1]), out var t);
                Assert.Greater(Vector2.Dot(d, t), 0f, label + " segment " + i + " runs backwards");
            }
        }

        [Test]
        public void Offset_InsideATightArc_CollapsesTheFold()
        {
            var pts = Arc(2f, 120f);
            var s = Polyline2D.Cumulative(pts);
            var along = new List<float>();
            var inner = Polyline2D.Offset(pts, s, 3f, along);
            var outerAlong = new List<float>();
            var outer = Polyline2D.Offset(pts, s, -3f, outerAlong);
            Assert.AreEqual(outer.Count, inner.Count, "a collapse keeps the point count");
            AssertRunsForward(pts, inner, along, "inner (radius 2 < offset 3)");
            AssertRunsForward(pts, outer, outerAlong, "outer");
            Assert.IsFalse(CrossesItself(inner));
            for (int i = 1; i < along.Count; i++) Assert.GreaterOrEqual(along[i], along[i - 1]);
            for (int i = 0; i + 1 < outer.Count; i++) Assert.Greater(Vector2.Distance(outer[i], outer[i + 1]), 0.01f, "the outside is untouched");
            // The lead-in and lead-out stay on their own offset lines.
            Assert.AreEqual(3f, inner[0].y, 1e-4f);
            Assert.AreEqual(-10f, inner[0].x, 1e-4f);
        }

        [Test]
        public void Offset_ReversedTails_CollapseOntoTheJoint()
        {
            // A 2 m span centred on a 30° left turn, offset 3.775 m to the inside: both 1 m legs lie behind the miter point.
            var pts = new List<Vector2> { V(-1, 0), V(0, 0), V(Mathf.Cos(30f * Mathf.Deg2Rad), Mathf.Sin(30f * Mathf.Deg2Rad)) };
            var along = new List<float>();
            var line = Polyline2D.Offset(pts, Polyline2D.Cumulative(pts), 3.775f, along);
            Assert.AreEqual(3, line.Count);
            AssertRunsForward(pts, line, along, "inside");
            Assert.Less(Vector2.Distance(line[0], line[2]), 1e-4f, "the folded span collapses to one point");
            var outside = Polyline2D.Offset(pts, Polyline2D.Cumulative(pts), -3.775f, new List<float>());
            Assert.Greater(Vector2.Distance(outside[0], outside[2]), 3f, "the outside of the turn keeps its full span");
        }

        [Test]
        public void Offset_InsideASharpBevelledCorner_CutsTheOvershoot()
        {
            // A 160° turn bevels; inside it both legs overshoot the point where their offset lines meet and cross each other.
            float a = 160f * Mathf.Deg2Rad;
            var pts = new List<Vector2> { V(-20, 0), V(0, 0), V(20f * Mathf.Cos(a), 20f * Mathf.Sin(a)) };
            var along = new List<float>();
            var line = Polyline2D.Offset(pts, Polyline2D.Cumulative(pts), 1f, along);
            Assert.AreEqual(4, line.Count, "bevel");
            Assert.IsFalse(CrossesItself(line));
            AssertRunsForward(pts, line, along, "inside");
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
