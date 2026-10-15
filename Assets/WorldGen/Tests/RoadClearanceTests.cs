using System;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>Roads clear of buildings (spec road-clearance §2), on small synthetic OSM worlds.</summary>
    public class RoadClearanceTests
    {
        WorldGenSettings settings;
        GenerationReport report;

        [SetUp]
        public void SetUp()
        {
            settings = DefaultSettingsFactory.CreateInMemory();
            report = new GenerationReport();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        ClassifiedWorld World(Action<TestOsm> build)
        {
            var osm = new TestOsm();
            build(osm);
            return new FeatureClassifier(settings, report).Classify(osm.Data);
        }

        /// <summary>A residential street along z = 0 (5 m wide: clear distance 2.5 + 0.3 = 2.8 m).</summary>
        static void Street(TestOsm osm) => osm.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));

        float Clear(ClassifiedWorld w) => w.Roads.First(r => r.Id == 1).Width * 0.5f + settings.clearance.margin;

        static BuildingFeature Building(ClassifiedWorld w, long id) => w.Buildings.FirstOrDefault(b => b.Id == id);

        static float Front(ClassifiedWorld w, long id) => Building(w, id).Spec.Footprint.Outer.Min(p => p.y);

        [Test]
        public void HouseOneMetreIntoTheRoad_MovesBackOut()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(10, -4f, 1.8f, 8f, 8f, ("building", "house"));
            });
            Assert.IsNotNull(Building(w, 10), "kept");
            Assert.GreaterOrEqual(Front(w, 10), Clear(w) - 1e-3f, "front wall now clear of the road");
            Assert.Less(Front(w, 10), Clear(w) + 0.2f, "moved just enough");
            Assert.AreEqual(1, report.GetCount(RoadClearance.Shifted));
        }

        [Test]
        public void HouseTooFarIntoTheRoad_IsDropped()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(10, -4f, 0.3f, 8f, 8f, ("building", "house"));
            });
            Assert.IsNull(Building(w, 10), "2.5 m in: more than the 2 m it may be moved");
            Assert.AreEqual(1, report.GetCount(RoadClearance.Dropped));
        }

        [Test]
        public void HouseAcrossTheCentreline_IsDropped()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(10, -4f, -4f, 8f, 8f, ("building", "house"));
            });
            Assert.IsNull(Building(w, 10));
        }

        [Test]
        public void HouseClearOfTheRoad_IsUntouched()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(10, -4f, 12f, 8f, 8f, ("building", "house"));
            });
            Assert.AreEqual(12f, Front(w, 10), 0.01f);
            Assert.AreEqual(0, report.GetCount(RoadClearance.Shifted));
            Assert.AreEqual(0, report.GetCount(RoadClearance.Dropped));
        }

        [Test]
        public void ShiftThatHitsAnotherHouse_Drops()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(10, -4f, 1.8f, 8f, 8f, ("building", "house"));
                o.Rect(11, -4f, 10.2f, 8f, 8f, ("building", "house"));
            });
            Assert.IsNull(Building(w, 10), "moved back it would overlap house 11");
            Assert.IsNotNull(Building(w, 11));
        }

        [Test]
        public void HouseBetweenTwoRoads_Drops()
        {
            var w = World(o =>
            {
                Street(o);
                o.Way(2, new[] { new Vector2(-100f, 9f), new Vector2(100f, 9f) }, false, ("highway", "residential"));
                o.Rect(10, -4f, 1.8f, 8f, 5.4f, ("building", "house"));
            });
            Assert.IsNull(Building(w, 10), "no way out of both roads");
        }

        [Test]
        public void NamedBuilding_Stays_AndTheRoadIsCutShortOfItsWalls()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(20, -15f, -15f, 30f, 30f, ("building", "university"), ("name", "Gedung Uji"));
            });
            Assert.IsNotNull(Building(w, 20));
            var pieces = w.Roads.Where(r => r.Id == 1).SelectMany(r => r.Pieces).ToList();
            Assert.AreEqual(2, pieces.Count, "one piece each side");
            float clear = Clear(w);
            foreach (var piece in pieces)
            foreach (var p in piece.Points)
                Assert.GreaterOrEqual(Mathf.Abs(p.x), 15f + clear - 0.6f, "stops a clear distance before the wall (sampling tolerance)");
            var west = pieces.First(p => p.Points[0].x < 0f);
            var east = pieces.First(p => p.Points[0].x > 0f);
            Assert.IsFalse(west.ExtendEnd, "the cut end is a dead end");
            Assert.IsFalse(east.ExtendStart, "the cut end is a dead end");
            Assert.AreEqual(1, report.GetCount(RoadClearance.RoadCuts));
        }

        [Test]
        public void RoadAlongNamedBuilding_IsNotCut()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(20, -15f, 2.0f, 30f, 30f, ("building", "university"), ("name", "Gedung Uji"));
            });
            Assert.AreEqual(1, w.Roads.First(r => r.Id == 1).Pieces.Count);
            Assert.AreEqual(0, report.GetCount(RoadClearance.RoadCuts));
            Assert.AreEqual(2.0f, Front(w, 20), 0.01f, "named buildings are never moved");
        }

        [Test]
        public void ShortStub_IsDropped()
        {
            var w = World(o =>
            {
                o.Way(1, new[] { new Vector2(-2f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));
                o.Rect(20, 0f, -10f, 20f, 20f, ("building", "university"), ("name", "Gedung Uji"));
            });
            var pieces = w.Roads.First(r => r.Id == 1).Pieces;
            Assert.AreEqual(1, pieces.Count, "the 2 m stub before the wall is gone");
            Assert.Greater(pieces[0].Points[0].x, 20f);
        }

        [Test]
        public void RoadUnderARoofOnlyCanopy_IsNotCut()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(20, -20f, -15f, 40f, 30f, ("building", "roof"));
            });
            Assert.AreEqual(1, w.Roads.First(r => r.Id == 1).Pieces.Count, "an open canopy is driven under");
            Assert.AreEqual(0, report.GetCount(RoadClearance.RoadCuts));
        }

        [Test]
        public void SmallCanopyOverTheRoad_StaysPut()
        {
            var w = World(o =>
            {
                Street(o);
                o.Rect(10, -4f, 1.8f, 8f, 8f, ("building", "roof"));
            });
            Assert.IsNotNull(Building(w, 10), "a carport roof over the road edge is not a house in the way");
            Assert.AreEqual(1.8f, Front(w, 10), 0.01f);
        }

        [Test]
        public void KeyBuildings_AreNeverMoved()
        {
            settings.keyBuildings.Add(new KeyBuilding(10, "Kos Uji"));
            var w = World(o =>
            {
                Street(o);
                o.Rect(10, -4f, 1.8f, 8f, 8f, ("building", "house"));
            });
            Assert.AreEqual(1.8f, Front(w, 10), 0.01f);
        }

        [Test]
        public void SameWorldTwice_SameResult()
        {
            string Run()
            {
                report = new GenerationReport();
                var w = World(o =>
                {
                    Street(o);
                    for (int i = 0; i < 6; i++) o.Rect(30 + i, -50f + i * 18f, 1.2f + 0.3f * i, 8f, 8f, ("building", "house"));
                });
                return string.Join(";", w.Buildings.OrderBy(b => b.Id).Select(b => b.Id + ":" + b.Spec.Footprint.Outer[0].ToString("F3")));
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
