using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class FrontageMeshBuilderTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        /// <summary>Residential chain (0,0) → (20,0), half width 2.5; its south (-1) property line is at z = -3.7.</summary>
        RoadChain Straight()
        {
            var rule = settings.roadRules.Single(r => r.highway == "residential");
            var road = new RoadFeature { Id = 1, Highway = "residential", Rule = rule, Width = 5f, Y = 0.08f, Tags = TestOsm.Tags(("highway", "residential")) };
            road.Pieces.Add(new LinePiece(new List<Vector2> { new Vector2(0f, 0f), new Vector2(20f, 0f) }, false, false));
            return new RoadNetwork(new[] { road }, new Polygon[0], settings.roadEdges).Chains[0];
        }

        WallRun Run(RoadChain chain, WallStyle style, float height, int band, float s0, float s1, params WallOpening[] openings)
        {
            var run = new WallRun
            {
                Chain = chain, Side = -1, Profile = EdgeProfile.Gutter, S0 = s0, S1 = s1,
                Style = settings.frontage.Style(style), Height = height, Band = band
            };
            run.Openings.AddRange(openings);
            return run;
        }

        FrontageMeshResult Build(params WallRun[] runs) => FrontageMeshBuilder.Build(runs, settings.roadEdges, settings.frontage, 100f);

        static List<Vector3> Verts(MeshData m, int sub) => m.Indices(sub).Select(i => m.Vertices[i]).ToList();

        static WallOpening Gate(float s) => new WallOpening { S = s, Width = 2f, IsGate = true };

        [Test]
        public void PlasterWall_FollowsTheStyleAndTheLine()
        {
            var r = Build(Run(Straight(), WallStyle.Plaster, 1.8f, 2, 2f, 18f));
            var m = r.Render.Values.Single();
            var wall = Verts(m, FrontageMeshBuilder.SubWall);
            Assert.AreEqual(1.8f, wall.Max(v => v.y), 1e-4f);
            Assert.AreEqual(-3.7f, wall.Max(v => v.z), 1e-4f, "road-side face on the property line");
            Assert.AreEqual(-3.85f, wall.Min(v => v.z), 1e-4f);
            Assert.AreEqual(1.8f + 0.05f + 0.1f, Verts(m, FrontageMeshBuilder.SubConcrete).Max(v => v.y), 1e-4f, "pilaster top");
            Assert.AreEqual(16f, r.WallMeters, 1e-3f);
            Assert.AreEqual(16f, r.MetersByStyle["Plaster"], 1e-3f);
            Assert.AreEqual(0, m.Indices(FrontageMeshBuilder.SubFence).Count);
        }

        [Test]
        public void WallUv_StaysInsideItsBand()
        {
            var m = Build(Run(Straight(), WallStyle.Plaster, 2.0f, 2, 2f, 18f)).Render.Values.Single();
            foreach (int i in m.Indices(FrontageMeshBuilder.SubWall))
                Assert.That(m.Uvs[i].y, Is.InRange(2f / 8f, 3f / 8f));
        }

        [Test]
        public void Gate_LeavesAGapInTheWall_ButHasACollider()
        {
            var r = Build(Run(Straight(), WallStyle.Plaster, 1.8f, 0, 2f, 18f, Gate(10f)));
            var m = r.Render.Values.Single();
            Assert.IsFalse(Verts(m, FrontageMeshBuilder.SubWall).Any(v => v.x > 9.01f && v.x < 10.99f));
            Assert.Greater(m.Indices(FrontageMeshBuilder.SubMetal).Count, 0);
            Assert.Greater(m.Indices(FrontageMeshBuilder.SubFence).Count, 0);
            var collider = r.Collider.Values.Single();
            Assert.IsTrue(collider.Vertices.Any(v => Mathf.Abs(v.x - 9f) < 1e-3f && Mathf.Abs(v.y - 1.6f) < 1e-3f), "closed gate box");
            Assert.AreEqual(1, r.Gates);
            Assert.AreEqual(0, r.Doors);
            Assert.AreEqual(14f, r.WallMeters, 1e-3f);
            Assert.IsTrue(r.Blockers.Any(b => Mathf.Abs(Vector2.Distance(b.A, b.B) - 2f) < 1e-3f));
        }

        [Test]
        public void GutterSide_GetsASlabInFrontOfTheGate()
        {
            var r = Build(Run(Straight(), WallStyle.Plaster, 1.8f, 0, 2f, 18f, Gate(10f)));
            Assert.AreEqual(1, r.Slabs);
            var concrete = Verts(r.Render.Values.Single(), FrontageMeshBuilder.SubConcrete);
            Assert.IsTrue(concrete.Any(v => Mathf.Abs(v.y - 0.165f) < 1e-4f && v.x >= 8.79f && v.x <= 11.21f && v.z <= -2.79f && v.z >= -3.71f));
            var sidewalkRun = Run(Straight(), WallStyle.Plaster, 1.8f, 0, 2f, 18f, Gate(10f));
            sidewalkRun.Profile = EdgeProfile.Sidewalk;
            Assert.AreEqual(0, Build(sidewalkRun).Slabs, "no gutter, no slab");
        }

        [Test]
        public void LowFence_HasABandARailAndAFullHeightCollider()
        {
            var r = Build(Run(Straight(), WallStyle.LowFence, 0.8f, 1, 2f, 18f));
            var m = r.Render.Values.Single();
            Assert.AreEqual(0.8f, Verts(m, FrontageMeshBuilder.SubWall).Max(v => v.y), 1e-4f);
            Assert.AreEqual(0.85f + 0.9f - 0.04f, Verts(m, FrontageMeshBuilder.SubFence).Max(v => v.y), 1e-4f);
            Assert.AreEqual(0.85f + 0.9f, Verts(m, FrontageMeshBuilder.SubMetal).Max(v => v.y), 1e-4f);
            Assert.IsTrue(r.Collider.Values.Single().Vertices.Any(v => Mathf.Abs(v.y - 1.75f) < 1e-4f), "solid box over the whole fence height");
        }

        [Test]
        public void AdjacentRuns_ShareTheirBoundaryPilaster()
        {
            var chain = Straight();
            var r = Build(Run(chain, WallStyle.Plaster, 1.8f, 0, 2f, 10f), Run(chain, WallStyle.Brick, 1.7f, 6, 10f, 18f));
            Assert.AreEqual(7, r.Pilasters, "2 ends + 2 inner per 8 m run, the shared end at s = 10 counted once");
        }

        [Test]
        public void TriangleBudget_PerMetre()
        {
            var r = Build(Run(Straight(), WallStyle.LowFence, 0.8f, 0, 2f, 18f, Gate(10f)));
            int tris = r.Render.Values.Sum(m => m.TriangleCount);
            Assert.Less(tris / 16f, 40f);
        }
    }
}
