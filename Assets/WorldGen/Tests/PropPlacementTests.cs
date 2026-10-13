using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class PropPlacementTests
    {
        static Polygon Square(float x, float z, float w, float d) => new Polygon(new List<Vector2>
            { new Vector2(x, z), new Vector2(x + w, z), new Vector2(x + w, z + d), new Vector2(x, z + d) });

        static BuildingFeature Building(long id, float x, float z, float w, float d) =>
            new BuildingFeature { Id = id, Spec = new BuildingSpec { Id = id, Footprint = Square(x, z, w, d) } };

        static RoadFeature Road(long id, float width, float lampSpacing, params Vector2[] pts)
        {
            var road = new RoadFeature { Id = id, Width = width, Rule = new RoadRule("tertiary", width, 6, lampSpacing) };
            road.Pieces.Add(new LinePiece(new List<Vector2>(pts), false, false));
            return road;
        }

        static AreaFeature Grass(long id, Polygon shape, float spacing) =>
            new AreaFeature { Id = id, Shape = shape, Rule = new AreaRule { category = AreaCategory.Grass, treeSpacing = spacing } };

        [Test]
        public void SpatialGrid_FindsItemsAcrossCells()
        {
            var grid = new SpatialGrid<int>(10f);
            grid.Insert(Rect.MinMaxRect(-5, -5, 25, 5), 1);
            grid.Insert(Rect.MinMaxRect(100, 100, 101, 101), 2);
            var hits = new HashSet<int>();
            grid.Query(Rect.MinMaxRect(20, 0, 21, 1), hits);
            CollectionAssert.AreEquivalent(new[] { 1 }, hits);
        }

        [Test]
        public void SpatialGrid_NegativeCoordinates()
        {
            var grid = new SpatialGrid<string>(20f);
            grid.Insert(Rect.MinMaxRect(-41, -41, -39, -39), "a");
            var hits = new HashSet<string>();
            grid.Query(Rect.MinMaxRect(-40.5f, -40.5f, -40f, -40f), hits);
            Assert.IsTrue(hits.Contains("a"));
        }

        [Test]
        public void Poisson_RespectsMinimumDistanceAndBounds()
        {
            var bounds = Rect.MinMaxRect(0, 0, 100, 60);
            var pts = PoissonDisk.Sample(bounds, 5f, new SplitMix64(11));
            Assert.Greater(pts.Count, 50);
            for (int i = 0; i < pts.Count; i++)
            {
                Assert.IsTrue(bounds.Contains(pts[i]));
                for (int j = i + 1; j < pts.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(pts[i], pts[j]), 5f - 1e-3f);
            }
        }

        [Test]
        public void Poisson_IsDeterministic()
        {
            var a = PoissonDisk.Sample(Rect.MinMaxRect(0, 0, 50, 50), 4f, new SplitMix64(3));
            var b = PoissonDisk.Sample(Rect.MinMaxRect(0, 0, 50, 50), 4f, new SplitMix64(3));
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Poisson_TooFineGrid_ReturnsEmpty()
        {
            Assert.AreEqual(0, PoissonDisk.Sample(Rect.MinMaxRect(0, 0, 100000, 100000), 1f, new SplitMix64(1)).Count);
        }

        [Test]
        public void Trees_AvoidBuildingsAndRoads()
        {
            var building = Building(1, 40, 40, 20, 20);
            var road = Road(2, 6f, 0f, new Vector2(0, 20), new Vector2(100, 20));
            var placer = new PropPlacer(new[] { building }, new[] { road });
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(3, Square(0, 0, 100, 100), 6f) }, 4000, 2, new GenerationReport());
            Assert.Greater(trees.Count, 20);
            foreach (var t in trees)
            {
                Assert.IsFalse(building.Spec.Footprint.Contains(t.Position));
                Assert.GreaterOrEqual(building.Spec.Footprint.DistanceToBoundary(t.Position), PropPlacer.TreeBuildingBuffer - 1e-3f);
                Assert.GreaterOrEqual(Mathf.Abs(t.Position.y - 20f), 3f + PropPlacer.TreeRoadClearance - 1e-3f);
                Assert.That(t.Scale, Is.InRange(0.8f, 1.2f));
                Assert.That(t.Variant, Is.InRange(0, 1));
            }
        }

        [Test]
        public void Trees_CapIsEnforced()
        {
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            var report = new GenerationReport();
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(3, Square(0, 0, 100, 100), 6f) }, 5, 1, report);
            Assert.AreEqual(5, trees.Count);
            Assert.Greater(report.TreesDroppedByCap, 0);
            Assert.AreEqual(5, report.TreesPlaced);
        }

        [Test]
        public void OsmTreeInsideBuilding_IsDropped()
        {
            var placer = new PropPlacer(new[] { Building(1, 0, 0, 10, 10) }, new RoadFeature[0]);
            var trees = placer.PlaceTrees(new[] { new TreeFeature(7, new Vector2(5, 5)), new TreeFeature(8, new Vector2(30, 30)) },
                new AreaFeature[0], 4000, 1, new GenerationReport());
            Assert.AreEqual(1, trees.Count);
            Assert.AreEqual(new Vector2(30, 30), trees[0].Position);
        }

        [Test]
        public void Lamps_AlternateSidesAndFaceRoad()
        {
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            var report = new GenerationReport();
            var lamps = placer.PlaceLamps(new[] { Road(1, 6f, 30f, new Vector2(0, 0), new Vector2(200, 0)) }, 8f, 0.6f, report);
            Assert.AreEqual(7, lamps.Count);
            Assert.AreEqual(7, report.LampsPlaced);
            Assert.AreEqual(15f, lamps[0].Position.x, 1e-3f);
            Assert.AreEqual(3.6f, lamps[0].Position.y, 1e-3f);
            Assert.AreEqual(180f, Mathf.Abs(lamps[0].YawDegrees), 1e-3f);
            Assert.AreEqual(-3.6f, lamps[1].Position.y, 1e-3f);
            Assert.AreEqual(0f, lamps[1].YawDegrees, 1e-3f);
        }

        [Test]
        public void Lamps_KeepMinimumSpacingAcrossRoads()
        {
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            var lamps = placer.PlaceLamps(new[]
            {
                Road(1, 6f, 30f, new Vector2(0, 0), new Vector2(200, 0)),
                Road(2, 6f, 30f, new Vector2(0, 0), new Vector2(200, 0))
            }, 8f, 0.6f, new GenerationReport());
            Assert.AreEqual(7, lamps.Count);
        }

        [Test]
        public void Lamps_AreNotPlacedOnOtherRoads()
        {
            var main = Road(1, 6f, 30f, new Vector2(0, 0), new Vector2(200, 0));
            var side = Road(2, 5f, 0f, new Vector2(15, 0), new Vector2(15, 100));
            var placer = new PropPlacer(new BuildingFeature[0], new[] { main, side });
            var lamps = placer.PlaceLamps(new[] { main, side }, 8f, 0.6f, new GenerationReport());
            Assert.IsFalse(lamps.Any(l => Mathf.Abs(l.Position.x - 15f) < 2.5f && l.Position.y > 0f));
            Assert.AreEqual(6, lamps.Count);
        }

        [Test]
        public void Trees_AvoidNoTreeAreasAndWater()
        {
            var pitch = new AreaFeature
            {
                Id = 5, Shape = Square(20, 20, 20, 20),
                Rule = new AreaRule { category = AreaCategory.Pitch, treeSpacing = 0f }
            };
            var water = Square(60, 60, 20, 20);
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(3, Square(0, 0, 100, 100), 4f), pitch }, 4000, 1,
                new GenerationReport(), new[] { water });
            Assert.Greater(trees.Count, 50);
            Assert.IsFalse(trees.Any(t => pitch.Shape.Contains(t.Position)));
            Assert.IsFalse(trees.Any(t => water.Contains(t.Position)));
        }

        [Test]
        public void Lamps_SkipRoadsWithoutSpacing()
        {
            var placer = new PropPlacer(new BuildingFeature[0], new RoadFeature[0]);
            Assert.AreEqual(0, placer.PlaceLamps(new[] { Road(1, 1.5f, 0f, new Vector2(0, 0), new Vector2(200, 0)) }, 8f, 0.6f, new GenerationReport()).Count);
        }
    }
}
