using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class WorldChunkIndexTests
    {
        CullingFixture fx;

        [SetUp]
        public void SetUp() => fx = new CullingFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        [TestCase("Chunk_3_-2", true, 3, -2)]
        [TestCase("Chunk_-10_5", true, -10, 5)]
        [TestCase("Chunk_0_0", true, 0, 0)]
        [TestCase("Chunk_x_1", false, 0, 0)]
        [TestCase("Chunk_1", false, 0, 0)]
        [TestCase("Chunk_1_2_3", false, 0, 0)]
        [TestCase("Trees", false, 0, 0)]
        public void TryParseChunk(string name, bool ok, int x, int z)
        {
            Assert.AreEqual(ok, WorldChunkIndex.TryParseChunk(name, out var chunk));
            if (ok) Assert.AreEqual(new Vector2Int(x, z), chunk);
        }

        [Test]
        public void Build_IndexesOnlyTheKnownChunkContainers()
        {
            fx.Cube("Roads/Chunk_0_0", new Vector3(0f, 0f, 0f));
            fx.Cube("Buildings/Chunk_1_0", new Vector3(100f, 0f, 0f));
            fx.Cube("Buildings/Chunk_1_0", new Vector3(120f, 0f, 10f));
            fx.Cube("Areas/Chunk_0_1", new Vector3(0f, 0f, 100f));
            fx.Cube("Water/Chunk_2_2", new Vector3(200f, 0f, 200f));
            fx.Cube("Props/Trees/Chunk_0_0", new Vector3(5f, 0f, 5f));
            fx.Cube("Props/Lamps/Chunk_1_1", new Vector3(105f, 0f, 105f));
            fx.Cube("Ground", Vector3.zero);
            fx.Cube("KeyBuildings/Kos Raflesia 3", new Vector3(10f, 0f, 10f));
            fx.Cube("Roads/Misc", new Vector3(0f, 0f, 50f));
            fx.Ensure("Buildings/Chunk_9_9");

            var groups = WorldChunkIndex.Build(fx.Root.transform);

            Assert.AreEqual(6, groups.Count);
            int structures = 0, props = 0;
            foreach (var g in groups)
            {
                if (g.Category == CullCategory.Structure) structures++;
                else props++;
                Assert.IsTrue(g.Visible);
            }
            Assert.AreEqual(4, structures);
            Assert.AreEqual(2, props);
            var buildings = groups.Find(g => g.Chunk == new Vector2Int(1, 0));
            Assert.AreEqual(2, buildings.Renderers.Count);
            Assert.IsTrue(buildings.Bounds.Contains(CullingFixture.Origin + new Vector3(100f, 0f, 0f)));
            Assert.IsTrue(buildings.Bounds.Contains(CullingFixture.Origin + new Vector3(120f, 0f, 10f)));
        }

        [Test]
        public void Build_IndexesEveryContainerTheGeneratorCreates()
        {
            var structures = new[] { FeatureGroups.Roads, FeatureGroups.Buildings, FeatureGroups.Areas, FeatureGroups.Water, FeatureGroups.Frontage };
            var props = new[] { WorldMeshPipeline.TreesGroup, WorldMeshPipeline.LampsGroup, FeatureGroups.RoadEdges };
            var paths = new List<string>(structures);
            paths.AddRange(props);
            for (int i = 0; i < paths.Count; i++)
                fx.Cube(paths[i] + "/" + WorldMeshPipeline.ChunkName(new Vector2Int(i, 0)), new Vector3(i * 100f, 0f, 0f));

            var groups = WorldChunkIndex.Build(fx.Root.transform);

            Assert.AreEqual(paths.Count, groups.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                var chunk = new Vector2Int(i, 0);
                var group = groups.Find(g => g.Chunk == chunk);
                Assert.IsNotNull(group, paths[i]);
                Assert.AreEqual(i < structures.Length ? CullCategory.Structure : CullCategory.Prop, group.Category, paths[i]);
            }
        }

        [Test]
        public void Build_NullRoot_IsEmpty() => Assert.AreEqual(0, WorldChunkIndex.Build(null).Count);

        [Test]
        public void DistanceTo_IsMeasuredToTheBoundsEdge()
        {
            fx.Cube("Roads/Chunk_1_0", new Vector3(150.5f, 0f, 0f));
            var group = WorldChunkIndex.Build(fx.Root.transform)[0];
            Assert.AreEqual(150f, group.DistanceTo(CullingFixture.Origin), 1e-3f);
        }
    }
}
