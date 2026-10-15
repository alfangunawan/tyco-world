using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>Which road ends get a barrier, and what kind (spec road-clearance §3).</summary>
    public class DeadEndPlannerTests
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

        List<DeadEnd> Plan(Action<TestOsm> build)
        {
            var osm = new TestOsm();
            build(osm);
            var world = new FeatureClassifier(settings, report).Classify(osm.Data);
            var network = new RoadNetwork(world.Roads, new List<Polygon>(), settings.roadEdges);
            var street = StreetContext.From(world, settings);
            return DeadEndPlanner.Plan(network, world, street, settings.deadEnds, settings.deadEnds.kosRadius, report);
        }

        static void Street(TestOsm o) => o.Way(1, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, false, ("highway", "residential"));

        [Test]
        public void GangEndingInTheOpen_GetsALowWall_AcrossIt_FacingOut()
        {
            var ends = Plan(o =>
            {
                Street(o);
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 60f) }, false, ("highway", "residential"));
            });
            var end = ends.Single(e => e.RoadId == 2);
            Assert.AreEqual(DeadEndKind.Wall, end.Kind);
            Assert.Less(Vector2.Distance(Vector2.up, end.Outward), 1e-4f);
            Assert.AreEqual(60f - settings.deadEnds.endInset, end.Position.y, 0.01f);
            Assert.GreaterOrEqual(end.Width, 5f, "spans the 5 m carriageway");
            Assert.AreEqual(ends.Count(e => e.Kind == DeadEndKind.Wall), report.GetCount(DeadEndPlanner.Walls), "the street's own two ends count too");
        }

        [Test]
        public void TJunction_IsNotADeadEnd()
        {
            var ends = Plan(o =>
            {
                Street(o);
                o.Way(2, new[] { new Vector2(0f, 1.5f), new Vector2(0f, 60f) }, false, ("highway", "residential"));
            });
            Assert.IsFalse(ends.Any(e => e.RoadId == 2 && e.Position.y < 10f), "the end inside road 1's carriageway meets it");
        }

        [Test]
        public void ServiceRoad_GetsAPortal()
        {
            var ends = Plan(o =>
            {
                Street(o);
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 40f) }, false, ("highway", "service"));
            });
            Assert.AreEqual(DeadEndKind.Portal, ends.Single(e => e.RoadId == 2).Kind);
        }

        [Test]
        public void EndAtTheMapEdge_GetsTheRoadClosedBarrier()
        {
            var ends = Plan(o => o.Way(1, new[] { new Vector2(0f, 0f), new Vector2(5000f, 0f) }, false, ("highway", "residential")));
            Assert.IsTrue(ends.Any(e => e.Kind == DeadEndKind.Boundary), "the road is clipped at the bounds");
            Assert.Greater(report.GetCount(DeadEndPlanner.Boundaries), 0);
        }

        [Test]
        public void EndAtAHouse_IsAlreadyClosed()
        {
            var ends = Plan(o =>
            {
                Street(o);
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 30f) }, false, ("highway", "footway"));
                o.Rect(50, -5f, 31.5f, 10f, 10f, ("building", "house"));
            });
            Assert.IsFalse(ends.Any(e => e.RoadId == 2));
            Assert.Greater(report.GetCount(DeadEndPlanner.Skipped), 0);
        }

        [Test]
        public void EndInsideAParkingLot_IsSkipped()
        {
            var ends = Plan(o =>
            {
                Street(o);
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 30f) }, false, ("highway", "service"));
                o.Rect(60, -15f, 25f, 30f, 20f, ("amenity", "parking"));
            });
            Assert.IsFalse(ends.Any(e => e.RoadId == 2));
        }

        [Test]
        public void EndInFrontOfTheKos_StaysOpen()
        {
            settings.frontage.kosBuildingId = 77;
            var ends = Plan(o =>
            {
                Street(o);
                o.Way(2, new[] { new Vector2(0f, 0f), new Vector2(0f, 8f) }, false, ("highway", "residential"));
                o.Rect(77, -5f, 20f, 10f, 10f, ("building", "house"));
            });
            Assert.IsFalse(ends.Any(e => e.RoadId == 2), "the gang ends at the kos gate: no wall in the way out");
            Assert.AreEqual(20f, settings.deadEnds.kosRadius, 1e-6f);
        }

        [Test]
        public void ClosedLoop_HasNoEnds()
        {
            var ends = Plan(o => o.Way(1, new[] { new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(40f, 40f), new Vector2(0f, 40f), new Vector2(0f, 0f) },
                false, ("highway", "residential")));
            Assert.IsEmpty(ends);
        }

        [Test]
        public void SameWorldTwice_SamePlan()
        {
            string Run()
            {
                report = new GenerationReport();
                var ends = Plan(o =>
                {
                    Street(o);
                    for (int i = 0; i < 5; i++)
                        o.Way(10 + i, new[] { new Vector2(-60f + i * 30f, 0f), new Vector2(-60f + i * 30f, 40f + i * 5f) }, false,
                            ("highway", i % 2 == 0 ? "residential" : "service"));
                });
                return string.Join(";", ends.Select(e => e.RoadId + ":" + e.Kind + ":" + e.Position.ToString("F3")));
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
