using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class ObbCalculatorTests
    {
        static List<Vector2> RotatedRect(float w, float h, float degrees, Vector2 center)
        {
            var rot = Quaternion.Euler(0, 0, degrees);
            var pts = new List<Vector2>();
            foreach (var c in new[] { new Vector2(-w / 2, -h / 2), new Vector2(w / 2, -h / 2), new Vector2(w / 2, h / 2), new Vector2(-w / 2, h / 2) })
                pts.Add(center + (Vector2)(rot * c));
            return pts;
        }

        [Test]
        public void RotatedRectangle_RecoversSizeAndAxis()
        {
            var obb = ObbCalculator.Compute(RotatedRect(12, 8, 30, new Vector2(3, 4)));
            Assert.AreEqual(96f, obb.Area, 0.01f);
            Assert.AreEqual(6f, obb.HalfU, 1e-3f);
            Assert.AreEqual(4f, obb.HalfV, 1e-3f);
            var expectedAxis = new Vector2(Mathf.Cos(30 * Mathf.Deg2Rad), Mathf.Sin(30 * Mathf.Deg2Rad));
            Assert.Greater(Mathf.Abs(Vector2.Dot(obb.AxisU, expectedAxis)), 0.999f);
            Assert.AreEqual(3f, obb.Center.x, 1e-3f);
            Assert.AreEqual(4f, obb.Center.y, 1e-3f);
        }

        [Test]
        public void AxisV_IsAxisURotatedCounterClockwise()
        {
            var obb = ObbCalculator.Compute(RotatedRect(4, 10, 75, Vector2.zero));
            Assert.AreEqual(1f, Polygon2D.Cross(obb.AxisU, obb.AxisV), 1e-4f);
            Assert.GreaterOrEqual(obb.HalfU, obb.HalfV);
        }

        [Test]
        public void ConvexHull_DropsInteriorPoint()
        {
            var pts = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(5, 5), new Vector2(10, 10), new Vector2(0, 10) };
            Assert.AreEqual(4, ObbCalculator.ConvexHull(pts).Count);
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, 1f, -90f)]
        [TestCase(0f, -1f, 90f)]
        public void YawForLocalX_MapsAxis(float x, float y, float expectedYaw)
        {
            Assert.AreEqual(expectedYaw, ObbCalculator.YawDegreesForLocalX(new Vector2(x, y)), 1e-3f);
        }
    }
}
