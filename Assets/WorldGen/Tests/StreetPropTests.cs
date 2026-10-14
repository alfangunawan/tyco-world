using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class StreetPropTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        static Polygon Square(float x, float z, float w, float d) => new Polygon(new List<Vector2>
            { new Vector2(x, z), new Vector2(x + w, z), new Vector2(x + w, z + d), new Vector2(x, z + d) });

        static AreaFeature Grass(Polygon shape) =>
            new AreaFeature { Id = 77, Shape = shape, Rule = new AreaRule { category = AreaCategory.Grass, treeSpacing = 3f } };

        PropPlacer Placer(StreetFixture fx) => new PropPlacer(fx.World.Buildings, fx.World.Roads, fx.Network, settings.roadEdges);

        [Test]
        public void Lamps_StandOnTheShoulderOfGutterRoads()
        {
            var fx = new StreetFixture(settings);
            var lamps = Placer(fx).PlaceLamps(fx.World.Roads, 8f, 0.6f, new GenerationReport());
            var onR1 = lamps.Where(l => Mathf.Abs(l.Position.y) < 4f && l.Position.x > 5f && l.Position.x < 195f).ToList();
            Assert.GreaterOrEqual(onR1.Count, 3);
            foreach (var l in onR1) Assert.AreEqual(2.5f + 0.15f, Mathf.Abs(l.Position.y), 1e-3f);
        }

        [Test]
        public void Lamps_OnTheCampusSide_StandOnTheSidewalk()
        {
            var fx = new StreetFixture(settings);
            var lamps = Placer(fx).PlaceLamps(fx.World.Roads, 8f, 0.6f, new GenerationReport());
            var onT1 = lamps.Where(l => Mathf.Abs(l.Position.y - 60f) < 5f && l.Position.x > 5f && l.Position.x < 195f).ToList();
            Assert.IsTrue(onT1.Any(l => l.Position.y > 60f));
            Assert.IsTrue(onT1.Any(l => l.Position.y < 60f));
            foreach (var l in onT1)
                Assert.AreEqual(l.Position.y > 60f ? 60f + 3f + 0.45f : 60f - 3f - 0.15f, l.Position.y, 1e-3f);
        }

        [Test]
        public void ScatterTrees_KeepOffTheEdges()
        {
            var fx = new StreetFixture(settings);
            // The grass reaches the R1 centreline, so the edge clearance (hw 2.5 + gutter 0.8 + 0.5) is what keeps trees off.
            var trees = Placer(fx).PlaceTrees(new TreeFeature[0], new[] { Grass(Square(5f, -30f, 190f, 30f)) }, 4000, 1, new GenerationReport());
            Assert.Greater(trees.Count, 50);
            Assert.IsTrue(trees.Any(t => Mathf.Abs(t.Position.y) < 4.5f), "some trees stand right behind the clearance");
            foreach (var t in trees) Assert.GreaterOrEqual(Mathf.Abs(t.Position.y), 2.5f + 0.8f + 0.5f - 1e-3f);
        }

        [Test]
        public void LampsAndTrees_KeepOffWalls()
        {
            var fx = new StreetFixture(settings);
            var placer = Placer(fx);
            var wall = new WallSegment(new Vector2(0f, -4.5f), new Vector2(200f, -4.5f));
            placer.AddWalls(new[] { wall });
            Assert.IsTrue(placer.IsNearWall(new Vector2(50f, -4.3f), 0.35f));
            Assert.IsFalse(placer.IsNearWall(new Vector2(50f, -5f), 0.35f));
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(Square(5f, -30f, 190f, 30f)) }, 4000, 1, new GenerationReport());
            Assert.Greater(trees.Count, 50);
            foreach (var t in trees) Assert.GreaterOrEqual(Polygon2D.DistanceToSegment(t.Position, wall.A, wall.B), 0.8f - 1e-3f);
            var lamps = placer.PlaceLamps(fx.World.Roads, 8f, 0.6f, new GenerationReport());
            foreach (var l in lamps) Assert.GreaterOrEqual(Polygon2D.DistanceToSegment(l.Position, wall.A, wall.B), 0.35f - 1e-3f);
        }

        [Test]
        public void AddedBuildings_BlockScatterTrees()
        {
            var fx = new StreetFixture(settings);
            var placer = Placer(fx);
            var house = Square(100f, -30f, 20f, 20f);
            placer.AddBuildings(new[] { new BuildingFeature { Id = -5, Spec = new BuildingSpec { Id = -5, Footprint = house } } });
            var trees = placer.PlaceTrees(new TreeFeature[0], new[] { Grass(Square(5f, -40f, 190f, 35f)) }, 4000, 1, new GenerationReport());
            Assert.IsFalse(trees.Any(t => house.Contains(t.Position)));
        }
    }
}
