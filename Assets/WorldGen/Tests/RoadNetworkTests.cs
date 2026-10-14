using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class RoadNetworkTests
    {
        WorldGenSettings s;

        [SetUp]
        public void SetUp() => s = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(s);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static Polygon Square(float x, float y, float w, float h) => new Polygon(new List<Vector2> { V(x, y), V(x + w, y), V(x + w, y + h), V(x, y + h) });

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

        RoadNetwork Net(IReadOnlyList<Polygon> campus, params RoadFeature[] roads) => new RoadNetwork(roads, campus ?? new Polygon[0], s.roadEdges);

        [Test]
        public void TwoCollinearWays_FormOneChain()
        {
            var net = Net(null, Road(7, "residential", 5f, V(0, 0), V(50, 0)), Road(3, "residential", 5f, V(50, 0), V(100, 0)));
            Assert.AreEqual(1, net.Chains.Count);
            var c = net.Chains[0];
            Assert.AreEqual(100f, c.Length, 1e-3f);
            Assert.AreEqual(3, c.Points.Count);
            Assert.AreEqual(3, c.Id, "chain id = smallest way id");
            Assert.AreEqual(2, c.Members.Count);
            Assert.AreEqual(0f, c.Members[0].S0, 1e-3f);
            Assert.AreEqual(50f, c.Members[0].S1, 1e-3f);
            Assert.AreEqual(50f, c.Members[1].S0, 1e-3f);
            Assert.AreEqual(100f, c.Members[1].S1, 1e-3f);
            Assert.AreEqual(2.5f, c.HalfWidth);
        }

        [Test]
        public void ReversedWay_IsFlippedIntoTheChain()
        {
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0)), Road(2, "residential", 5f, V(100, 0), V(50, 0)));
            Assert.AreEqual(1, net.Chains.Count);
            var c = net.Chains[0];
            Assert.AreEqual(V(0, 0), c.Points[0]);
            Assert.AreEqual(V(50, 0), c.Points[1]);
            Assert.AreEqual(V(100, 0), c.Points[2]);
            Assert.AreEqual(2, c.Members[1].Road.Id);
            Assert.AreEqual(50f, c.Members[1].S0, 1e-3f);
        }

        [Test]
        public void ThreeWaysAtANode_StaySeparate()
        {
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0)), Road(2, "residential", 5f, V(50, 0), V(100, 0)),
                Road(3, "residential", 5f, V(50, 0), V(50, 50)));
            Assert.AreEqual(3, net.Chains.Count);
        }

        [Test]
        public void DifferentHighwaysOrWidths_StaySeparate()
        {
            Assert.AreEqual(2, Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0)), Road(2, "tertiary", 5f, V(50, 0), V(100, 0))).Chains.Count);
            Assert.AreEqual(2, Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0)), Road(2, "residential", 6f, V(50, 0), V(100, 0))).Chains.Count);
        }

        [Test]
        public void ClosedLoop_IsOneChain()
        {
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(50, 0), V(50, 50)), Road(2, "residential", 5f, V(50, 50), V(0, 50), V(0, 0)));
            Assert.AreEqual(1, net.Chains.Count);
            Assert.AreEqual(200f, net.Chains[0].Length, 1e-3f);
        }

        [TestCase("primary", null, EdgeProfile.Sidewalk)]
        [TestCase("secondary", null, EdgeProfile.Sidewalk)]
        [TestCase("tertiary", null, EdgeProfile.Gutter)]
        [TestCase("residential", null, EdgeProfile.Gutter)]
        [TestCase("living_street", null, EdgeProfile.Gutter)]
        [TestCase("service", null, EdgeProfile.Gutter)]
        [TestCase("service", "alley", EdgeProfile.Gutter)]
        [TestCase("service", "driveway", EdgeProfile.None)]
        [TestCase("service", "parking_aisle", EdgeProfile.None)]
        [TestCase("service", "drive-through", EdgeProfile.None)]
        [TestCase("footway", null, EdgeProfile.None)]
        [TestCase("pedestrian", null, EdgeProfile.None)]
        [TestCase("track", null, EdgeProfile.None)]
        public void BaseProfile_FollowsHighwayAndServiceTag(string highway, string service, EdgeProfile expected)
        {
            var road = Road(1, highway, 5f, V(0, 0), V(10, 0));
            road.Tags = TestOsm.Tags(("highway", highway), ("service", service));
            Assert.AreEqual(expected, RoadNetwork.BaseProfileOf(road, s.roadEdges));
        }

        [Test]
        public void Tertiary_SideFacingTheCampus_GetsSidewalk()
        {
            // Travel east: +1 (left) is north. The campus starts 2 m north of the asphalt edge.
            var net = Net(new[] { Square(0, 5, 100, 55) }, Road(1, "tertiary", 6f, V(0, 0), V(100, 0)));
            var c = net.Chains[0];
            Assert.IsTrue(c.Left.All(p => p == EdgeProfile.Sidewalk));
            Assert.IsTrue(c.Right.All(p => p == EdgeProfile.Gutter), "the far side is within 10 m too, but faces away from the campus");
            Assert.AreEqual(1.5f, c.MaxEdgeWidth, 1e-5f);
            Assert.AreEqual(201, c.Left.Length);
        }

        [Test]
        public void Tertiary_InsideTheCampus_GetsSidewalksOnBothSides()
        {
            var c = Net(new[] { Square(-10, -50, 120, 100) }, Road(1, "tertiary", 6f, V(0, 0), V(100, 0))).Chains[0];
            Assert.IsTrue(c.Left.All(p => p == EdgeProfile.Sidewalk));
            Assert.IsTrue(c.Right.All(p => p == EdgeProfile.Sidewalk));
        }

        [Test]
        public void Residential_NextToTheCampus_StaysGutter()
        {
            var c = Net(new[] { Square(0, 5, 100, 55) }, Road(1, "residential", 5f, V(0, 0), V(100, 0))).Chains[0];
            Assert.IsTrue(c.Left.All(p => p == EdgeProfile.Gutter));
            Assert.AreEqual(0.8f, c.MaxEdgeWidth, 1e-5f);
        }

        [Test]
        public void ShortCampusStretch_FollowsItsNeighbours()
        {
            // A 1 m wide campus corner 9.55 m from the north sample line: within 10 m over only ~7 m of road (< 8 m).
            var c = Net(new[] { Square(50, 13.3f, 1, 7) }, Road(1, "tertiary", 6f, V(0, 0), V(100, 0))).Chains[0];
            Assert.IsTrue(c.Left.All(p => p == EdgeProfile.Gutter));
        }

        [Test]
        public void Smooth_ShortRunTakesTheLongerNeighbour()
        {
            var g = EdgeProfile.Gutter;
            var w = EdgeProfile.Sidewalk;
            var a = new[] { g, g, g, g, w, w, g, g, g };
            RoadNetwork.Smooth(a, 3);
            Assert.IsTrue(a.All(p => p == g));
            var b = new[] { g, g, w, w, w, w, w, w };
            RoadNetwork.Smooth(b, 3);
            Assert.IsTrue(b.All(p => p == w));
            var c = new[] { w, w, w, w, g, g, g, g };
            RoadNetwork.Smooth(c, 3);
            CollectionAssert.AreEqual(new[] { w, w, w, w, g, g, g, g }, c, "runs at least minSamples long stay");
        }

        [Test]
        public void Beats_RankThenWidthThenId()
        {
            var a = new RoadChain { Rank = 6, Width = 6f, Id = 9, Index = 0 };
            var b = new RoadChain { Rank = 4, Width = 8f, Id = 1, Index = 1 };
            Assert.IsTrue(RoadNetwork.Beats(a, b));
            Assert.IsFalse(RoadNetwork.Beats(b, a));
            var c = new RoadChain { Rank = 4, Width = 5f, Id = 0, Index = 2 };
            Assert.IsTrue(RoadNetwork.Beats(b, c), "same rank: wider wins");
            var d = new RoadChain { Rank = 4, Width = 5f, Id = 3, Index = 3 };
            Assert.IsTrue(RoadNetwork.Beats(c, d), "same rank and width: smaller id wins");
            var e = new RoadChain { Rank = 4, Width = 5f, Id = 3, Index = 4 };
            Assert.IsTrue(RoadNetwork.Beats(d, e), "same id (pieces of one way): lower index wins");
        }

        [Test]
        public void Nearby_ReportsTheClosestDistancePerChain()
        {
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(100, 0)), Road(2, "residential", 5f, V(50, 10), V(50, 60)));
            var near = new Dictionary<RoadChain, float>();
            net.Nearby(V(50, 5), 20f, null, near);
            Assert.AreEqual(2, near.Count);
            Assert.IsTrue(near.Values.All(d => Mathf.Abs(d - 5f) < 1e-4f));
            net.Nearby(V(50, 5), 20f, net.Chains[0], near);
            Assert.AreEqual(1, near.Count);
            net.Nearby(V(50, 5), 3f, null, near);
            Assert.AreEqual(0, near.Count);
        }

        [Test]
        public void DistanceToChain_IgnoresTheNearbyStretch()
        {
            // A U-turn: the return leg runs 6 m north of the outbound leg.
            var net = Net(null, Road(1, "residential", 5f, V(0, 0), V(100, 0), V(100, 6), V(0, 6)));
            var chain = net.Chains[0];
            Assert.AreEqual(3f, net.DistanceToChain(chain, V(50, 3), 10f, 45f, 55f), 1e-4f, "return leg (s 106..206) still counts");
            Assert.AreEqual(10f, net.DistanceToChain(chain, V(50, 3), 10f, 0f, 300f), 1e-4f, "everything ignored: radius");
        }

        [Test]
        public void EdgeLookup_ByRoadAndSide()
        {
            var net = Net(new[] { Square(0, 5, 100, 55) }, Road(1, "tertiary", 6f, V(0, 0), V(100, 0)));
            Assert.AreEqual(EdgeProfile.Sidewalk, net.ProfileAt(1, V(50, 0), V(0, 1)));
            Assert.AreEqual(EdgeProfile.Gutter, net.ProfileAt(1, V(50, 0), V(0, -1)));
            Assert.AreEqual(EdgeProfile.None, net.ProfileAt(99, V(50, 0), V(0, 1)));
            Assert.AreEqual(1.5f, net.MaxEdgeWidth(1), 1e-5f);
            Assert.AreEqual(0f, net.MaxEdgeWidth(99));
            Assert.AreEqual(3f, net.MaxHalfWidth, 1e-5f);
            Assert.AreEqual(3f + 1.5f + 1f, net.MaxReach, 1e-5f);
        }

        [Test]
        public void ChainKey_DiffersForPiecesOfOneWay()
        {
            var road = Road(1, "residential", 5f, V(0, 0), V(50, 0));
            road.Pieces.Add(new LinePiece(new List<Vector2> { V(200, 0), V(250, 0) }, false, false));
            var net = Net(null, road);
            Assert.AreEqual(2, net.Chains.Count);
            Assert.AreEqual(net.Chains[0].Id, net.Chains[1].Id);
            Assert.AreNotEqual(net.Chains[0].Key, net.Chains[1].Key);
        }
    }
}
