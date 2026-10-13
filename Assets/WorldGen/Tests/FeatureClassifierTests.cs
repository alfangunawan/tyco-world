using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class FeatureClassifierTests
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

        ClassifiedWorld Classify(OsmData data) => new FeatureClassifier(settings, report).Classify(data);

        ClassifiedWorld Synthetic() => Classify(OsmParser.Parse(FixtureFiles.Read("synthetic_world.json")));

        [Test]
        public void Synthetic_House()
        {
            var b = Synthetic().Buildings.Single(x => x.Id == 401);
            Assert.AreEqual(10f, b.Spec.WallHeight, 1e-4f);
            Assert.AreEqual(3, b.Spec.Levels);
            Assert.IsTrue(b.Spec.AllowGable);
            Assert.IsFalse(b.IsCampus);
            Assert.AreEqual("Residential", b.Rule.name);
            Assert.AreEqual("Rumah Sintetis", b.Name);
            Assert.AreEqual("2023-05-01", b.Tags["check_date"]);
            Assert.AreEqual(95f, b.Spec.Footprint.Area, 0.5f);
        }

        [Test]
        public void Synthetic_RelationBuilding()
        {
            var b = Synthetic().Buildings.Single(x => x.Id == 201);
            Assert.AreEqual(OsmElementType.Relation, b.ElementType);
            Assert.AreEqual(1, b.Spec.Footprint.Holes.Count);
            Assert.IsTrue(b.IsCampus);
            Assert.AreEqual("Institutional", b.Rule.name);
            Assert.AreEqual(9.6f, b.Spec.WallHeight, 1e-4f);
        }

        [Test]
        public void Synthetic_Road()
        {
            var r = Synthetic().Roads.Single(x => x.Id == 402);
            Assert.AreEqual(6f, r.Width, 1e-4f);
            Assert.AreEqual(0.08f, r.Y, 1e-6f);
            Assert.AreEqual("Gang Sintetis", r.Name);
            Assert.AreEqual(1, r.Pieces.Count);
            Assert.IsFalse(r.Pieces[0].ExtendStart);
            Assert.IsFalse(r.Pieces[0].ExtendEnd);
        }

        [Test]
        public void Synthetic_MissingNodeRoad_IsSkipped()
        {
            var world = Synthetic();
            Assert.IsFalse(world.Roads.Any(r => r.Id == 403));
            CollectionAssert.Contains(report.Skipped[SkipReasons.RoadMissingNodes], 403L);
        }

        [Test]
        public void Synthetic_AreasAndTree()
        {
            var world = Synthetic();
            Assert.AreEqual(AreaCategory.Grass, world.Areas.Single(a => a.Id == 404).Rule.category);
            Assert.AreEqual(AreaCategory.Campus, world.Areas.Single(a => a.Id == 405).Rule.category);
            var tree = world.Trees.Single(t => t.Id == 301);
            Assert.AreEqual(-22.10f, tree.Position.x, 0.05f);
            Assert.AreEqual(-27.65f, tree.Position.y, 0.05f);
        }

        [Test]
        public void BuildingWinsOverAmenity()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 10, 10, ("building", "yes"), ("amenity", "parking"));
            var world = Classify(osm.Data);
            Assert.AreEqual(1, world.Buildings.Count);
            Assert.AreEqual(0, world.Areas.Count);
        }

        [TestCase("dormitory", false, 4, 12.8f)]
        [TestCase("university", false, 3, 9.6f)]
        [TestCase("mosque", false, 1, 5f)]
        [TestCase("commercial", false, 2, 6.4f)]
        [TestCase("shed", false, 1, 3.2f)]
        [TestCase("yes", true, 3, 9.6f)]
        public void BuildingDefaults(string value, bool campus, int levels, float wall)
        {
            var osm = new TestOsm();
            if (campus) osm.Rect(2, -50, -50, 100, 100, ("amenity", "university"));
            osm.Rect(1, 0, 0, 10, 10, ("building", value));
            var b = Classify(osm.Data).Buildings.Single(x => x.Id == 1);
            Assert.AreEqual(levels, b.Spec.Levels);
            Assert.AreEqual(wall, b.Spec.WallHeight, 1e-4f);
            Assert.AreEqual(campus, b.IsCampus);
        }

        [Test]
        public void RoofValue_IsRoofOnly()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 6, 4, ("building", "roof"));
            var b = Classify(osm.Data).Buildings.Single();
            Assert.IsTrue(b.Spec.RoofOnly);
            Assert.AreEqual(3f, b.Spec.WallHeight, 1e-4f);
        }

        [Test]
        public void NonCampusResidential_IsDeterministicAndInRange()
        {
            var osm = new TestOsm();
            osm.Rect(12345, 0, 0, 8, 8, ("building", "house"));
            int first = Classify(osm.Data).Buildings.Single().Spec.Levels;
            int second = Classify(osm.Data).Buildings.Single().Spec.Levels;
            Assert.AreEqual(first, second);
            Assert.That(first, Is.InRange(1, 3));
        }

        [TestCase("landuse", "cemetery", AreaCategory.Cemetery)]
        [TestCase("amenity", "parking", AreaCategory.Parking)]
        [TestCase("leisure", "pitch", AreaCategory.Pitch)]
        [TestCase("landuse", "paddy", AreaCategory.Farmland)]
        [TestCase("natural", "wood", AreaCategory.Wood)]
        [TestCase("leisure", "park", AreaCategory.Grass)]
        [TestCase("landuse", "industrial", AreaCategory.Hardground)]
        public void AreaRuleTable(string key, string value, AreaCategory expected)
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 20, 20, (key, value));
            Assert.AreEqual(expected, Classify(osm.Data).Areas.Single().Rule.category);
        }

        [Test]
        public void HighwayAreaYes_IsPaving()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 20, 20, ("highway", "pedestrian"), ("area", "yes"));
            var world = Classify(osm.Data);
            Assert.AreEqual(0, world.Roads.Count);
            Assert.AreEqual(AreaCategory.Paving, world.Areas.Single().Rule.category);
        }

        [Test]
        public void WaterAreaAndLines()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 20, 20, ("natural", "water"));
            osm.Way(2, new[] { new Vector2(0, 50), new Vector2(40, 50) }, false, ("waterway", "drain"));
            osm.Way(3, new[] { new Vector2(0, 60), new Vector2(40, 60) }, false, ("waterway", "canal"), ("width", "3"));
            var world = Classify(osm.Data);
            Assert.AreEqual(1, world.WaterAreas.Count);
            Assert.AreEqual(1.5f, world.WaterLines.Single(w => w.Id == 2).Width, 1e-4f);
            Assert.AreEqual(3f, world.WaterLines.Single(w => w.Id == 3).Width, 1e-4f);
            Assert.AreEqual(0.050f, world.WaterLines[0].Y, 1e-6f);
        }

        [TestCase("abc", "2", 6f)]
        [TestCase(null, null, 5f)]
        [TestCase("6 m", null, 6f)]
        public void Width_FallsBackToLanesThenDefault(string width, string lanes, float expected)
        {
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(0, 0), new Vector2(50, 0) }, false, ("highway", "residential"), ("width", width), ("lanes", lanes));
            Assert.AreEqual(expected, Classify(osm.Data).Roads.Single().Width, 1e-4f);
        }

        [Test]
        public void Width_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                var osm = new TestOsm();
                osm.Way(1, new[] { new Vector2(0, 0), new Vector2(50, 0) }, false, ("highway", "residential"), ("width", "6.5"));
                Assert.AreEqual(6.5f, Classify(osm.Data).Roads.Single().Width, 1e-4f);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void KeyBuildingOverride()
        {
            settings.keyBuildings.Add(new KeyBuilding(77, "Test") { overrideLevels = 5, overrideRoof = RoofOverride.Flat });
            var osm = new TestOsm();
            osm.Rect(77, 0, 0, 10, 10, ("building", "house"));
            var b = Classify(osm.Data).Buildings.Single();
            Assert.AreEqual(5, b.Spec.Levels);
            Assert.AreEqual(16f, b.Spec.WallHeight, 1e-4f);
            Assert.IsNotNull(b.Key);
            Assert.AreEqual(RoofOverride.Flat, b.Spec.RoofOverride);
        }

        [Test]
        public void BuildingOutsideBounds_IsCulled()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 3000, 10, 10, ("building", "yes"));
            Assert.AreEqual(0, Classify(osm.Data).Buildings.Count);
            Assert.AreEqual(1, report.GetCulled(CullReasons.BuildingOutside));
        }

        [Test]
        public void RoadCrossingBounds_IsClipped()
        {
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(0, 0), new Vector2(0, 3000) }, false, ("highway", "residential"));
            var piece = Classify(osm.Data).Roads.Single().Pieces.Single();
            Assert.IsFalse(piece.ExtendEnd);
            Assert.AreEqual(569.54f, piece.Points[piece.Points.Count - 1].y, 0.1f);
        }

        [Test]
        public void TinyBuilding_IsSkipped()
        {
            var osm = new TestOsm();
            osm.Rect(1, 0, 0, 1.5f, 1.5f, ("building", "yes"));
            Classify(osm.Data);
            CollectionAssert.Contains(report.Skipped[SkipReasons.BuildingTooSmall], 1L);
        }

        [Test]
        public void SelfIntersectingBuilding_IsSkipped()
        {
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(0, 0), new Vector2(10, 10), new Vector2(10, 0), new Vector2(0, 10) }, true, ("building", "yes"));
            Classify(osm.Data);
            CollectionAssert.Contains(report.Skipped[SkipReasons.BuildingSelfIntersecting], 1L);
        }

        [Test]
        public void UnclosedBuildingWay_IsSkipped()
        {
            var osm = new TestOsm();
            osm.Way(1, new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10) }, false, ("building", "yes"));
            Classify(osm.Data);
            CollectionAssert.Contains(report.Skipped[SkipReasons.BuildingUnclosed], 1L);
        }

        [Test]
        public void RelationWithOpenOuter_IsSkipped()
        {
            var osm = new TestOsm();
            osm.Way(10, new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10) }, false);
            osm.Relation(500, new[] { (10L, "outer") }, ("type", "multipolygon"), ("building", "yes"));
            Classify(osm.Data);
            CollectionAssert.Contains(report.Skipped[SkipReasons.RelationUnclosedRing], 500L);
        }
    }
}
