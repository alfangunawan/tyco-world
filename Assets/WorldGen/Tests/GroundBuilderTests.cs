using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class GroundBuilderTests
    {
        [Test]
        public void ColliderBox_TopFaceAtZero()
        {
            GroundBuilder.ColliderBox(new Rect(-10f, -20f, 30f, 50f), out var center, out var size);
            Assert.AreEqual(0f, center.y + size.y * 0.5f, 1e-5f);
        }

        [Test]
        public void EdgeWalls_InnerFacesOnBounds()
        {
            var r = new Rect(-10f, -20f, 30f, 50f);
            var w = GroundBuilder.EdgeWalls(r, 30f, 2f);
            Assert.AreEqual(4, w.Length);
            Assert.AreEqual(r.xMin, w[0].Center.x + w[0].Size.x * 0.5f, 1e-4f, "west");
            Assert.AreEqual(r.xMax, w[1].Center.x - w[1].Size.x * 0.5f, 1e-4f, "east");
            Assert.AreEqual(r.yMin, w[2].Center.z + w[2].Size.z * 0.5f, 1e-4f, "south");
            Assert.AreEqual(r.yMax, w[3].Center.z - w[3].Size.z * 0.5f, 1e-4f, "north");
            foreach (var b in w)
            {
                Assert.AreEqual(-1f, b.Center.y - b.Size.y * 0.5f, 1e-4f);
                Assert.AreEqual(30f, b.Center.y + b.Size.y * 0.5f, 1e-4f);
            }
        }

        [Test]
        public void EdgeWalls_CoverCorners()
        {
            var r = new Rect(0f, 0f, 10f, 10f);
            var w = GroundBuilder.EdgeWalls(r, 30f, 2f);
            var corners = new[] { new Vector3(-0.5f, 1f, -0.5f), new Vector3(10.5f, 1f, -0.5f), new Vector3(10.5f, 1f, 10.5f), new Vector3(-0.5f, 1f, 10.5f) };
            foreach (var corner in corners)
                Assert.IsTrue(w.Any(b => new Bounds(b.Center, b.Size).Contains(corner)), corner.ToString());
        }
    }
}
