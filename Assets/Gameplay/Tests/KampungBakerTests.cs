using System.Linq;
using NUnit.Framework;
using TycoWorld.Gameplay.Authoring;
using TycoWorld.WorldGen.Authoring;
using TycoWorld.WorldGen.Tests;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    public class KampungBakerTests
    {
        WorldGenSettings settings;
        KampungData data;

        [SetUp]
        public void SetUp()
        {
            settings = DefaultSettingsFactory.CreateInMemory();
            var fx = new StreetFixture(settings);
            data = KampungBaker.Bake(fx.World, settings, fx.Report);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void SameWorld_SameData()
        {
            var fx = new StreetFixture(settings);
            var again = KampungBaker.Bake(fx.World, settings, fx.Report);
            try
            {
                Assert.AreEqual(JsonUtility.ToJson(data), JsonUtility.ToJson(again));
            }
            finally
            {
                Object.DestroyImmediate(again);
            }
        }

        [Test]
        public void ShopsOnR1sSouthSide_StandOnItsPropertyLine_FacingTheRoad()
        {
            // R1 runs along z = 0 (half width 2.5, gutter 0.8, gap 0.4): its south property line is z = -3.7.
            var south = data.shops.Where(s => s.counter.z < -1f && s.counter.z > -6f && s.counter.x > 0f && s.counter.x < 200f).ToList();
            Assert.Greater(south.Count, 0, "the flush fronts south of R1 (house H2, flush infill) host a shop");
            foreach (var shop in south)
            {
                Assert.AreEqual(-3.7f, shop.counter.z, 0.05f, shop.name);
                var face = new Vector2(Mathf.Sin(shop.yaw * Mathf.Deg2Rad), Mathf.Cos(shop.yaw * Mathf.Deg2Rad));
                Assert.Greater(face.y, 0.99f, shop.name + " faces north, to the road");
                Assert.AreNotEqual(0, shop.buildingId);
                Assert.AreEqual(0.9f, shop.slabLength, 0.01f, "slab from the shoulder (2.8) to the property line (3.7)");
                Assert.IsFalse(string.IsNullOrEmpty(shop.name));
            }
            for (int i = 0; i < data.shops.Length; i++)
                for (int j = i + 1; j < data.shops.Length; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(data.shops[i].counter, data.shops[j].counter), KampungBaker.MinShopSpacing - 1e-3f);
        }

        [Test]
        public void SetbackHouse_HasADoorSpot_AtItsGate()
        {
            var doors = data.spots.Where(s => s.kind == SpotKind.Door && s.refId == StreetFixture.SetbackHouse).ToList();
            Assert.AreEqual(1, doors.Count);
            Assert.AreEqual(25f, doors[0].position.x, 0.6f, "the gate faces the house centroid");
        }

        [Test]
        public void WalkLanes_RunBesideTheRoads_AndEveryEndHasANode()
        {
            Assert.Greater(data.walkLanes.Length, 4);
            foreach (var lane in data.walkLanes)
            {
                Assert.GreaterOrEqual(lane.points.Length, 2);
                Assert.That(lane.startNode, Is.InRange(0, data.walkNodes.Length - 1));
                Assert.That(lane.endNode, Is.InRange(0, data.walkNodes.Length - 1));
                Assert.Contains(System.Array.IndexOf(data.walkLanes, lane), data.walkNodes[lane.startNode].lanes);
            }
            // R1 (z = 0, half width 2.5) has a lane on its south shoulder at z ≈ -2.65.
            Assert.IsTrue(data.walkLanes.Any(l => l.points.All(p => Mathf.Abs(p.y + 2.65f) < 0.05f) && l.points.Any(p => p.x > 100f)));
            // The footway F1 (x = 40, a path without edges) is a centreline lane.
            Assert.IsTrue(data.walkLanes.Any(l => l.points.All(p => Mathf.Abs(p.x - 40f) < 0.05f)));
        }

        [Test]
        public void ParkingSpots_StayOutOfBuildings_AndLandmarksIncludeTheKos()
        {
            var bikes = data.spots.Where(s => s.kind == SpotKind.ParkingBike).ToList();
            Assert.Greater(bikes.Count, 0);
            foreach (var spot in data.spots)
                Assert.IsTrue(data.bounds.Contains(new Vector2(spot.position.x, spot.position.z)), spot.kind + " " + spot.position);
            Assert.AreEqual(1, data.spots.Count(s => s.kind == SpotKind.Kos));
            Assert.Greater(data.mapRoads.Length, 3);
            StringAssert.Contains("shops=", data.summary);
        }
    }
}
