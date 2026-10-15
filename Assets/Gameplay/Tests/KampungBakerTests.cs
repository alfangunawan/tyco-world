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

        /// <summary>The bake sees the cut roads (RoadClearance runs inside Classify): the phone map and GPS stop short of the walls.</summary>
        public static KampungData BakeRoadThroughNamedBuilding(WorldGenSettings settings, out Polygon footprint)
        {
            var report = new GenerationReport();
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
            osm.Rect(20, -15f, -15f, 30f, 30f, ("building", "university"), ("name", "Gedung Uji"));
            var world = new FeatureClassifier(settings, report).Classify(osm.Data);
            footprint = world.Buildings.First(b => b.Id == 20).Spec.Footprint;
            return KampungBaker.Bake(world, settings, report);
        }

        /// <summary>How many points every 0.5 m along the lanes fall inside the footprint.</summary>
        public static int PointsInside(Lane[] lanes, Polygon footprint)
        {
            int inside = 0;
            foreach (var lane in lanes)
                for (int i = 1; i < lane.points.Length; i++)
                {
                    Vector2 a = lane.points[i - 1], b = lane.points[i];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
                    for (int k = 0; k <= steps; k++)
                        if (footprint.Contains(Vector2.Lerp(a, b, k / (float)steps))) inside++;
                }
            return inside;
        }

        [Test]
        public void MapRoads_StopShortOfANamedBuilding_ARoadRanThrough()
        {
            var baked = BakeRoadThroughNamedBuilding(settings, out var footprint);
            try
            {
                Assert.IsNotEmpty(baked.mapRoads);
                Assert.AreEqual(0, PointsInside(baked.mapRoads, footprint), "the phone map and GPS do not route through the building");
                Assert.AreEqual(0, PointsInside(baked.walkLanes, footprint), "nor do the warga walk through it");
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
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
