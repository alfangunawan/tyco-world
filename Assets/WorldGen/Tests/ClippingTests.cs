using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class ClippingTests
    {
        static readonly Rect Box = Rect.MinMaxRect(0, 0, 10, 10);

        static void AssertNear(Vector2 expected, Vector2 actual) =>
            Assert.Less(Vector2.Distance(expected, actual), 1e-4f, $"expected {expected} got {actual}");

        [Test]
        public void Polyline_FullyInside_KeepsBothEnds()
        {
            var pieces = Clipping.ClipPolyline(new[] { new Vector2(1, 1), new Vector2(5, 5), new Vector2(9, 1) }, Box);
            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(3, pieces[0].Points.Count);
            Assert.IsTrue(pieces[0].StartsAtOriginalStart);
            Assert.IsTrue(pieces[0].EndsAtOriginalEnd);
        }

        [Test]
        public void Polyline_CrossingInAndOut_IsCut()
        {
            var pieces = Clipping.ClipPolyline(new[] { new Vector2(-10, 5), new Vector2(5, 5), new Vector2(5, 20) }, Box);
            Assert.AreEqual(1, pieces.Count);
            var p = pieces[0].Points;
            AssertNear(new Vector2(0, 5), p[0]);
            AssertNear(new Vector2(5, 5), p[1]);
            AssertNear(new Vector2(5, 10), p[2]);
            Assert.IsFalse(pieces[0].StartsAtOriginalStart);
            Assert.IsFalse(pieces[0].EndsAtOriginalEnd);
        }

        [Test]
        public void Polyline_ExitAndReenter_MakesTwoPieces()
        {
            var pieces = Clipping.ClipPolyline(
                new[] { new Vector2(2, 2), new Vector2(20, 2), new Vector2(20, 8), new Vector2(2, 8) }, Box);
            Assert.AreEqual(2, pieces.Count);
            Assert.IsTrue(pieces[0].StartsAtOriginalStart);
            Assert.IsFalse(pieces[0].EndsAtOriginalEnd);
            AssertNear(new Vector2(10, 2), pieces[0].Points[1]);
            Assert.IsFalse(pieces[1].StartsAtOriginalStart);
            Assert.IsTrue(pieces[1].EndsAtOriginalEnd);
            AssertNear(new Vector2(10, 8), pieces[1].Points[0]);
        }

        [Test]
        public void Polyline_FullyOutside_ReturnsNothing()
        {
            Assert.AreEqual(0, Clipping.ClipPolyline(new[] { new Vector2(20, 20), new Vector2(30, 20) }, Box).Count);
        }

        [Test]
        public void Polygon_PartiallyOutside_IsTrimmed()
        {
            var square = new List<Vector2> { new Vector2(5, 5), new Vector2(15, 5), new Vector2(15, 15), new Vector2(5, 15) };
            var clipped = Clipping.ClipPolygon(square, Box);
            Assert.AreEqual(25f, Mathf.Abs(Polygon2D.SignedArea(clipped)), 1e-3f);
        }

        [Test]
        public void Polygon_FullyOutside_IsEmpty()
        {
            var square = new List<Vector2> { new Vector2(20, 20), new Vector2(30, 20), new Vector2(30, 30), new Vector2(20, 30) };
            Assert.AreEqual(0, Clipping.ClipPolygon(square, Box).Count);
        }
    }
}
