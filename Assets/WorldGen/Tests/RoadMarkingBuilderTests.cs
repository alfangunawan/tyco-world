using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class RoadMarkingBuilderTests
    {
        WorldGenSettings s;

        [SetUp]
        public void SetUp() => s = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(s);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        RoadFeature Road(long id, string highway, float width, params Vector2[] pts)
        {
            var rule = s.roadRules.Single(r => r.highway == highway);
            var road = new RoadFeature
            {
                Id = id, Highway = highway, Rule = rule, Width = width, Y = s.roadBaseY + rule.rank * s.roadRankStep,
                Tags = TestOsm.Tags(("highway", highway))
            };
            road.Pieces.Add(new LinePiece(new List<Vector2>(pts), false, false));
            return road;
        }

        int Mark(out Dictionary<long, MeshData> meshes, params RoadFeature[] roads)
        {
            meshes = roads.ToDictionary(r => r.Id, r => new MeshData(2));
            var net = new RoadNetwork(roads, new Polygon[0], s.roadEdges);
            return RoadMarkingBuilder.Build(net, s.roadEdges, meshes);
        }

        static float Area(MeshData m, int sub)
        {
            float area = 0f;
            var idx = m.Indices(sub);
            for (int t = 0; t < idx.Count; t += 3)
                area += Vector3.Cross(m.Vertices[idx[t + 1]] - m.Vertices[idx[t]], m.Vertices[idx[t + 2]] - m.Vertices[idx[t]]).magnitude * 0.5f;
            return area;
        }

        [Test]
        public void Primary100m_HasTheDashPattern()
        {
            var road = Road(1, "primary", 6f, V(0, 0), V(100, 0));
            Assert.AreEqual(12, Mark(out var meshes, road), "dashes start at 2 m, every 8 m, the last ends by 100 m");
            Assert.AreEqual(12 * 6, meshes[1].Indices(RoadMarkingBuilder.Submesh).Count);
            Assert.AreEqual(0, meshes[1].Indices(0).Count, "asphalt submesh untouched");
            foreach (var v in meshes[1].Vertices)
            {
                Assert.AreEqual(road.Y + 0.003f, v.y, 1e-5f);
                Assert.AreEqual(0.06f, Mathf.Abs(v.z), 1e-4f);
            }
            Assert.AreEqual(12 * 3f * 0.12f, Area(meshes[1], RoadMarkingBuilder.Submesh), 1e-3f);
        }

        [Test]
        public void Dashes_StopNearACrossingRoad()
        {
            // Crossing residential at x = 50 (half width 2.5): no dash within 2.5 + 3 m of it.
            Assert.AreEqual(10, Mark(out _, Road(1, "primary", 6f, V(0, 0), V(100, 0)), Road(2, "residential", 5f, V(50, -50), V(50, 50))));
        }

        [Test]
        public void ResidentialAndNarrowTertiary_AreNotMarked()
        {
            Assert.AreEqual(0, Mark(out _, Road(1, "residential", 5f, V(0, 0), V(100, 0))));
            Assert.AreEqual(0, Mark(out _, Road(1, "tertiary", 6f, V(0, 0), V(100, 0))));
        }

        [Test]
        public void WideTertiary_IsMarked()
        {
            Assert.AreEqual(12, Mark(out _, Road(1, "tertiary", 12f, V(0, 0), V(100, 0))));
        }

        [Test]
        public void Dashes_FollowTheChainAcrossWays()
        {
            int count = Mark(out var meshes, Road(1, "primary", 6f, V(0, 0), V(44, 0)), Road(2, "primary", 6f, V(44, 0), V(100, 0)));
            Assert.AreEqual(12, count);
            Assert.Greater(meshes[1].Indices(RoadMarkingBuilder.Submesh).Count, 0);
            Assert.Greater(meshes[2].Indices(RoadMarkingBuilder.Submesh).Count, 0);
            float total = Area(meshes[1], RoadMarkingBuilder.Submesh) + Area(meshes[2], RoadMarkingBuilder.Submesh);
            Assert.AreEqual(12 * 3f * 0.12f, total, 1e-3f, "the dash over the way boundary (42..45) is split, not lost or doubled");
        }

        [Test, Timeout(20000)]
        public void ZeroDashPeriod_ReturnsWithoutDashes()
        {
            s.roadEdges.dashLength = 0f;
            s.roadEdges.dashGap = 0f;
            Assert.AreEqual(0, Mark(out _, Road(1, "primary", 8f, V(0, 0), V(100, 0))));
        }
    }
}
