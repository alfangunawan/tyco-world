using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class InfillPlannerTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        List<InfillHouse> Plan(StreetFixture fx, GenerationReport report = null) =>
            InfillPlanner.Plan(FrontagePlanner.Plan(fx.Network, fx.Context, settings), fx.Network, fx.Context, settings, report ?? new GenerationReport());

        [Test]
        public void Houses_FitTheSpecRanges()
        {
            var houses = Plan(new StreetFixture(settings));
            Assert.Greater(houses.Count, 5);
            foreach (var h in houses)
            {
                var obb = ObbCalculator.Compute(h.Spec.Footprint.Outer);
                float a = 2f * obb.HalfU, b = 2f * obb.HalfV;
                Assert.GreaterOrEqual(Mathf.Min(a, b), 6f - 1e-3f);
                Assert.LessOrEqual(Mathf.Max(a, b), 16f + 1e-3f);
                Assert.That(h.Spec.Levels, Is.InRange(1, 2));
                Assert.AreEqual(h.Spec.Levels * settings.levelHeight, h.Spec.WallHeight, 1e-4f);
                Assert.AreEqual(h.Id, h.Spec.Id);
                if (h.Flush)
                {
                    Assert.AreEqual(2, h.Spec.Levels);
                    Assert.AreEqual(RoofOverride.Flat, h.Spec.RoofOverride);
                    Assert.IsFalse(h.Spec.AllowGable);
                }
                else
                {
                    Assert.AreEqual(RoofOverride.Auto, h.Spec.RoofOverride);
                    Assert.IsTrue(h.Spec.AllowGable);
                }
            }
            Assert.IsTrue(houses.Any(h => h.Flush));
            Assert.IsTrue(houses.Any(h => !h.Flush));
        }

        [Test]
        public void Houses_OverlapNothing()
        {
            var fx = new StreetFixture(settings);
            var houses = Plan(fx);
            var areas = fx.World.Areas.Select(a => a.Shape.Outer).ToList();
            foreach (var h in houses)
            {
                var ring = h.Spec.Footprint.Outer;
                foreach (var b in fx.World.Buildings) Assert.IsFalse(Polygon2D.RingsOverlap(ring, b.Spec.Footprint.Outer), "osm " + b.Id);
                foreach (var other in houses)
                    if (other != h) Assert.IsFalse(Polygon2D.RingsOverlap(ring, other.Spec.Footprint.Outer), h.Id + " vs " + other.Id);
                foreach (var area in areas) Assert.IsFalse(Polygon2D.RingsOverlap(ring, area));
                Assert.IsFalse(fx.Context.OverlapsWater(ring), "water");
                Assert.IsFalse(fx.Context.NearKos(ring, settings.frontage.kosExclusionRadius - 1e-3f), "kos");
                foreach (var p in ring)
                    foreach (var chain in fx.Network.Chains)
                    {
                        float line = chain.HalfWidth + (chain.BaseProfile == EdgeProfile.None ? 0f : 0.8f) + 0.4f;
                        Assert.GreaterOrEqual(Polyline2D.DistanceTo(chain.Points, p), line - 0.02f, "behind the property line of " + chain.Id);
                    }
            }
        }

        [Test]
        public void NoHouses_OnTheCampusOrDrainFrontage()
        {
            var houses = Plan(new StreetFixture(settings));
            Assert.IsFalse(houses.Any(h => h.Centroid.y > 60f), "campus side of T1");
            Assert.IsFalse(houses.Any(h => h.Centroid.x < 80f && h.Centroid.y > 0f && h.Centroid.y < 20f), "R1 north frontage behind the drain");
        }

        [Test]
        public void Ids_AreNegativeUniqueAndStable()
        {
            var a = Plan(new StreetFixture(settings)).Select(h => h.Id).ToList();
            var b = Plan(new StreetFixture(settings, osm => osm.Way(4001, new[] { V(-300f, -600f), V(-200f, -600f) }, false, ("highway", "residential"))))
                .Select(h => h.Id).ToList();
            Assert.IsTrue(a.All(id => id < 0));
            Assert.AreEqual(a.Count, a.Distinct().Count());
            CollectionAssert.IsSubsetOf(a, b, "an unrelated road elsewhere does not renumber these houses");
            Assert.Greater(b.Count, a.Count);
        }

        [Test]
        public void Cap_StopsPlacementAndIsReported()
        {
            settings.infill.maxInfill = 3;
            var report = new GenerationReport();
            Assert.AreEqual(3, Plan(new StreetFixture(settings), report).Count);
            Assert.Greater(report.SkippedTotal(InfillPlanner.SkipCap), 0);
        }

        [Test]
        public void Disabled_PlacesNothing()
        {
            settings.infill.enabled = false;
            Assert.AreEqual(0, Plan(new StreetFixture(settings)).Count);
        }

        [Test]
        public void AcceptedHouses_AreRegisteredInTheContext()
        {
            var fx = new StreetFixture(settings);
            var houses = Plan(fx);
            Assert.AreEqual(houses.Count, fx.Context.Buildings.Count(b => b.IsInfill));
            Assert.IsTrue(houses.All(h => fx.Context.BuildingAt(h.Centroid) != null));
        }

        [Test]
        public void Houses_StayInsideTheWorld()
        {
            var bounds = new StreetFixture(settings).World.LocalBounds;
            var fx = new StreetFixture(settings, osm => osm.Way(4002, new[] { V(0f, bounds.yMax - 6f), V(100f, bounds.yMax - 6f) }, false, ("highway", "residential")));
            var houses = Plan(fx);
            foreach (var h in houses)
                foreach (var p in h.Spec.Footprint.Outer) Assert.IsTrue(bounds.Contains(p), "house " + h.Id);
            Assert.IsTrue(houses.Any(h => h.Centroid.y > bounds.yMax - 30f), "the south side of the edge road still fills");
        }

        [Test, Timeout(60000)]
        public void ZeroWidthAndGap_StillTerminates()
        {
            settings.infill.minWidth = 0f;
            settings.infill.maxWidth = 0f;
            settings.infill.minGap = 0f;
            settings.infill.maxGap = 0f;
            Assert.DoesNotThrow(() => Plan(new StreetFixture(settings)));
        }
    }
}
