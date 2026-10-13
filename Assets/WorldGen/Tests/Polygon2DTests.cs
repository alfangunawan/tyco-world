using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class Polygon2DTests
    {
        static List<Vector2> Square(float size) => new List<Vector2>
            { new Vector2(0, 0), new Vector2(size, 0), new Vector2(size, size), new Vector2(0, size) };

        static List<Vector2> LShape() => new List<Vector2>
            { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 4), new Vector2(4, 4), new Vector2(4, 10), new Vector2(0, 10) };

        [Test]
        public void SignedArea_CcwPositive_CwNegative()
        {
            var sq = Square(10);
            Assert.AreEqual(100f, Polygon2D.SignedArea(sq), 1e-4f);
            sq.Reverse();
            Assert.AreEqual(-100f, Polygon2D.SignedArea(sq), 1e-4f);
        }

        [Test]
        public void EnsureWinding_ReversesWhenNeeded()
        {
            var sq = Square(10);
            sq.Reverse();
            Polygon2D.EnsureWinding(sq, true);
            Assert.IsTrue(Polygon2D.IsCcw(sq));
        }

        [Test]
        public void Centroid_OfLShape()
        {
            var c = Polygon2D.Centroid(LShape());
            Assert.AreEqual(3.875f, c.x, 1e-4f);
            Assert.AreEqual(3.875f, c.y, 1e-4f);
        }

        [Test]
        public void ContainsPoint_InsideOutside()
        {
            var l = LShape();
            Assert.IsTrue(Polygon2D.ContainsPoint(l, new Vector2(2, 8)));
            Assert.IsFalse(Polygon2D.ContainsPoint(l, new Vector2(8, 8)));
        }

        [Test]
        public void Polygon_HoleExcludesPoint()
        {
            var hole = new List<Vector2> { new Vector2(3, 3), new Vector2(3, 7), new Vector2(7, 7), new Vector2(7, 3) };
            var poly = new Polygon(Square(10), new List<List<Vector2>> { hole });
            Assert.IsFalse(poly.Contains(new Vector2(5, 5)));
            Assert.IsTrue(poly.Contains(new Vector2(1, 1)));
            Assert.AreEqual(84f, poly.Area, 1e-4f);
            Assert.AreEqual(1f, poly.DistanceToBoundary(new Vector2(1, 5)), 1e-4f);
        }

        [Test]
        public void Clean_RemovesDuplicatesCollinearAndClosingPoint()
        {
            var input = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(5, 0), new Vector2(10, 0), new Vector2(10, 10),
                new Vector2(10, 10.01f), new Vector2(0, 10), new Vector2(0, 0)
            };
            var cleaned = Polygon2D.Clean(input);
            Assert.AreEqual(4, cleaned.Count);
            Assert.AreEqual(100f, Mathf.Abs(Polygon2D.SignedArea(cleaned)), 0.2f);
        }

        [Test]
        public void IsSelfIntersecting_DetectsBowtie()
        {
            var bowtie = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 10), new Vector2(10, 0), new Vector2(0, 10) };
            Assert.IsTrue(Polygon2D.IsSelfIntersecting(bowtie));
            Assert.IsFalse(Polygon2D.IsSelfIntersecting(Square(10)));
            Assert.IsFalse(Polygon2D.IsSelfIntersecting(LShape()));
        }

        [Test]
        public void DistanceToSegment_ClampsToEnds()
        {
            Assert.AreEqual(5f, Polygon2D.DistanceToSegment(new Vector2(5, 5), new Vector2(0, 0), new Vector2(10, 0)), 1e-5f);
            Assert.AreEqual(5f, Polygon2D.DistanceToSegment(new Vector2(-3, 4), new Vector2(0, 0), new Vector2(10, 0)), 1e-5f);
        }

        [Test]
        public void Bounds_CoversAllPoints()
        {
            var b = Polygon2D.Bounds(LShape());
            Assert.AreEqual(Rect.MinMaxRect(0, 0, 10, 10), b);
        }
    }
}
