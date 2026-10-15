using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class RoadEdgeBuilderTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        RoadEdgeResult Build(StreetFixture fx) => RoadEdgeBuilder.Build(fx.Network, fx.Context, settings.roadEdges, 100f);

        static List<EdgeRun> RunsOf(RoadEdgeResult r, long roadId, int side) =>
            r.Runs.Where(x => x.Side == side && x.Chain.Members.Any(m => m.Road.Id == roadId)).ToList();

        static bool Covers(IEnumerable<EdgeRun> runs, float s0, float s1) => runs.Any(r => r.S1 > s0 && r.S0 < s1);

        static IEnumerable<MeshData> Meshes(RoadEdgeResult r) => r.Gutter.Values.Concat(r.Sidewalk.Values);

        [Test]
        public void Heights_AreOnlyTheSpecLayers()
        {
            var r = Build(new StreetFixture(settings));
            var allowed = new[] { 0f, 0.055f, 0.155f, 0.21f };
            Assert.Greater(Meshes(r).Sum(m => m.Vertices.Count), 0);
            foreach (var mesh in Meshes(r))
                foreach (var v in mesh.Vertices)
                    Assert.IsTrue(allowed.Any(y => Mathf.Abs(v.y - y) < 1e-4f), "y=" + v.y);
        }

        [Test]
        public void ResidentialEdge_LeavesAMouthForTheServiceRoad()
        {
            var r = Build(new StreetFixture(settings));
            // R1 runs east, so +1 (left) is north, where S1 joins at x = 100: mouth = 1.75 + 0.3.
            var north = RunsOf(r, StreetFixture.Residential, 1);
            Assert.IsFalse(Covers(north, 100f - 2.0f, 100f + 2.0f));
            Assert.IsTrue(Covers(north, 103f, 110f));
            Assert.IsTrue(north.All(x => x.Profile == EdgeProfile.Gutter));
        }

        [Test]
        public void ServiceEdge_StopsAtTheBandsOfTheRoadsItJoins()
        {
            var r = Build(new StreetFixture(settings));
            var runs = RunsOf(r, StreetFixture.Service, 1).Concat(RunsOf(r, StreetFixture.Service, -1)).ToList();
            Assert.AreEqual(2, runs.Count);
            foreach (var run in runs)
            {
                Assert.GreaterOrEqual(run.S0, 2.5f + 0.8f - 0.07f, "R1 band: half width + gutter");
                Assert.LessOrEqual(run.S1, 60f - (3f + 1.5f) + 0.07f, "T1 band: half width + widest edge (campus sidewalk)");
                Assert.Greater(run.Length, 50f);
            }
        }

        [Test]
        public void DrivewayAndFootway_HaveNoEdges()
        {
            var r = Build(new StreetFixture(settings));
            Assert.IsFalse(r.Runs.Any(x => x.Chain.Members.Any(m => m.Road.Id == StreetFixture.Driveway || m.Road.Id == StreetFixture.Footway)));
        }

        [Test]
        public void FootwayAndDriveway_CutMouthsIntoTheResidentialEdge()
        {
            var south = RunsOf(Build(new StreetFixture(settings)), StreetFixture.Residential, -1);
            Assert.IsFalse(Covers(south, 40f - 1.0f, 40f + 1.0f), "footway: 0.75 + 0.3");
            Assert.IsFalse(Covers(south, 150f - 2.0f, 150f + 2.0f), "driveway: 1.75 + 0.3");
        }

        [Test]
        public void ShedOverTheEdge_CutsIt()
        {
            var south = RunsOf(Build(new StreetFixture(settings)), StreetFixture.Residential, -1);
            Assert.IsFalse(Covers(south, 180.1f, 184.9f));
            Assert.IsTrue(Covers(south, 170f, 175f));
        }

        [Test]
        public void CampusSide_IsSidewalk_OtherSideIsGutter()
        {
            var r = Build(new StreetFixture(settings));
            var north = RunsOf(r, StreetFixture.Tertiary, 1);
            var south = RunsOf(r, StreetFixture.Tertiary, -1);
            Assert.IsTrue(north.All(x => x.Profile == EdgeProfile.Sidewalk));
            Assert.IsTrue(south.All(x => x.Profile == EdgeProfile.Gutter));
            Assert.Greater(north.Sum(x => x.Length), 195f);
            Assert.Greater(r.Sidewalk.Count, 0);
        }

        [Test]
        public void Drain_BesideTheEdge_DoesNotCutIt()
        {
            var north = RunsOf(Build(new StreetFixture(settings)), StreetFixture.Residential, 1);
            Assert.Greater(north.Where(x => x.S0 < 90f).Sum(x => Mathf.Min(x.S1, 90f) - x.S0), 85f);
        }

        [Test]
        public void SameInput_SameMeshes()
        {
            var a = Build(new StreetFixture(settings));
            var b = Build(new StreetFixture(settings));
            Assert.AreEqual(a.Runs.Count, b.Runs.Count);
            CollectionAssert.AreEquivalent(a.Gutter.Keys, b.Gutter.Keys);
            foreach (var key in a.Gutter.Keys) Assert.AreEqual(a.Gutter[key].ContentHash(), b.Gutter[key].ContentHash());
            foreach (var key in a.Sidewalk.Keys) Assert.AreEqual(a.Sidewalk[key].ContentHash(), b.Sidewalk[key].ContentHash());
        }

        [Test]
        public void Meshes_AreValid_AndMetersMatchTheRuns()
        {
            var r = Build(new StreetFixture(settings));
            foreach (var mesh in Meshes(r)) Assert.IsFalse(mesh.HasInvalidValues());
            Assert.AreEqual(r.Runs.Where(x => x.Profile == EdgeProfile.Gutter).Sum(x => x.Length), r.GutterMeters, 1e-2f);
            Assert.AreEqual(r.Runs.Where(x => x.Profile == EdgeProfile.Sidewalk).Sum(x => x.Length), r.SidewalkMeters, 1e-2f);
            Assert.IsTrue(r.Runs.All(x => x.Length >= settings.roadEdges.minRunLength));
            Assert.IsTrue(r.Gutter.Values.All(m => m.SubmeshCount == RoadEdgeBuilder.GutterSubmeshes));
            Assert.IsTrue(r.Sidewalk.Values.All(m => m.SubmeshCount == RoadEdgeBuilder.SidewalkSubmeshes));
            Assert.IsTrue(r.Gutter.Values.Any(m => m.Indices(RoadEdgeBuilder.GutterChannel).Count > 0));
            Assert.IsTrue(r.Sidewalk.Values.Any(m => m.Indices(RoadEdgeBuilder.SidewalkKerb).Count > 0));
        }

        [Test]
        public void RightAngleBend_EdgesContinueAroundTheCorner()
        {
            var fx = new StreetFixture(settings, osm => osm.Way(2001, new[] { V(-300, -300), V(-250, -300), V(-250, -250) }, false, ("highway", "residential")));
            var r = Build(fx);
            foreach (int side in RoadNetwork.Sides)
            {
                var runs = RunsOf(r, 2001, side);
                Assert.AreEqual(1, runs.Count, "side " + side);
                Assert.LessOrEqual(runs[0].S0, 0.01f);
                Assert.GreaterOrEqual(runs[0].S1, 99.99f);
            }
        }

        /// <summary>
        /// A left turn of 90° cut by a 2.2 m chamfer (two 45° turns). Inside, an offset line beyond 2.2 / (2 tan 22.5°) ≈ 2.66 m
        /// folds over the chamfer: the outer gutter lines (hw + 0.38 … 0.8) do, the shoulder lines (hw − 0.05, hw + 0.3) do not.
        /// </summary>
        static Vector2[] TightBend()
        {
            float c = 2.2f * Mathf.Sqrt(0.5f);
            return new[] { V(-300, -150), V(-260, -150), V(-260 + c, -150 + c), V(-260 + c, -110) };
        }

        static bool OnTop(MeshData m, int sub, Vector2 p, float y)
        {
            var idx = m.Indices(sub);
            for (int i = 0; i + 2 < idx.Count; i += 3)
            {
                Vector3 a = m.Vertices[idx[i]], b = m.Vertices[idx[i + 1]], c = m.Vertices[idx[i + 2]];
                if (Mathf.Abs(a.y - y) > 1e-4f || Mathf.Abs(b.y - y) > 1e-4f || Mathf.Abs(c.y - y) > 1e-4f) continue;
                if (Polygon2D.ContainsPoint(new List<Vector2> { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z) }, p)) return true;
            }
            return false;
        }

        [Test]
        public void TightBend_InsideStripsStayWhereTheOuterLipFoldsAway()
        {
            var fx = new StreetFixture(settings, osm => osm.Way(2003, TightBend(), false, ("highway", "residential")));
            var r = Build(fx);
            var run = RunsOf(r, 2003, 1).Single();
            var chain = run.Chain;
            // Walk the mitred middle line of the shoulder (hw + 0.15, which does not fold) through the corner.
            float shoulderMiddle = chain.HalfWidth + settings.roadEdges.shoulderWidth * 0.5f;
            var along = new List<float>();
            var middle = Polyline2D.Offset(Polyline2D.Slice(chain.Points, chain.Cum, 35f, 50f, along), along, shoulderMiddle, new List<float>());
            var cum = Polyline2D.Cumulative(middle);
            for (float u = 0f; u <= cum[cum.Length - 1]; u += 0.1f)
            {
                var p = Polyline2D.PointAt(middle, cum, u, out _);
                Assert.IsTrue(r.Gutter.Values.Any(m => OnTop(m, RoadEdgeBuilder.GutterConcrete, p, settings.roadEdges.edgeBaseY)), "shoulder missing at " + p);
            }
        }

        [Test]
        public void RoadLeavingTheWorld_EdgesStayInside()
        {
            var bounds = new StreetFixture(settings).World.LocalBounds;
            var fx = new StreetFixture(settings, osm => osm.Way(2002, new[] { V(bounds.xMax - 50f, 300f), V(bounds.xMax + 50f, 300f) }, false, ("highway", "residential")));
            var r = Build(fx);
            Assert.Greater(RunsOf(r, 2002, 1).Count, 0);
            foreach (var mesh in Meshes(r))
                foreach (var v in mesh.Vertices)
                    Assert.LessOrEqual(v.x, bounds.xMax + 0.01f);
        }
    }
}
