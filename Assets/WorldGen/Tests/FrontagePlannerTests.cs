using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class FrontagePlannerTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        List<FrontageInterval> Plan(StreetFixture fx) => FrontagePlanner.Plan(fx.Network, fx.Context, settings);

        static List<FrontageInterval> Of(IEnumerable<FrontageInterval> list, long roadId, int side) =>
            list.Where(x => x.Side == side && x.Chain.Members.Any(m => m.Road.Id == roadId)).ToList();

        static FrontageInterval At(IEnumerable<FrontageInterval> list, long roadId, int side, float s) =>
            Of(list, roadId, side).FirstOrDefault(x => x.S0 <= s && s <= x.S1);

        static bool Covers(IEnumerable<FrontageInterval> list, float s0, float s1) => list.Any(x => x.S1 > s0 && x.S0 < s1);

        static IEnumerable<WallRun> WallsAt(IEnumerable<WallRun> walls, long roadId, int side, float s) =>
            walls.Where(w => w.Side == side && w.Chain.Members.Any(m => m.Road.Id == roadId) && w.S0 <= s && s <= w.S1);

        [Test]
        public void SetbackHouse_IsALotWithOneGateFacingIt()
        {
            var plan = Plan(new StreetFixture(settings));
            var lot = At(plan, StreetFixture.Residential, -1, 25f);
            Assert.IsNotNull(lot);
            Assert.AreEqual(FrontageKind.Lot, lot.Kind);
            Assert.AreEqual(StreetFixture.SetbackHouse, lot.Building.Id);
            Assert.That(lot.S0, Is.InRange(19f, 20.5f));
            Assert.That(lot.S1, Is.InRange(29.5f, 31f));
            var run = WallsAt(FrontagePlanner.Walls(plan, settings.frontage), StreetFixture.Residential, -1, 25f).Single();
            Assert.AreEqual(1, run.Openings.Count);
            Assert.IsTrue(run.Openings[0].IsGate);
            Assert.AreEqual(2f, run.Openings[0].Width);
            Assert.AreEqual(25f, run.Openings[0].S, 0.05f, "gate faces the house centroid");
            Assert.AreEqual(EdgeProfile.Gutter, run.Profile);
        }

        /// <summary>A residential chain turning 30° right at s = 20 (legs of 20 m), with one lot on its left (outside) between s0 and s1.</summary>
        (RoadChain chain, FrontageInterval lot) BentLot(float s0, float s1)
        {
            float a = 30f * Mathf.Deg2Rad;
            var rule = settings.roadRules.Single(r => r.highway == "residential");
            var road = new RoadFeature { Id = 9, Highway = "residential", Rule = rule, Width = 5f, Y = 0.08f, Tags = TestOsm.Tags(("highway", "residential")) };
            road.Pieces.Add(new LinePiece(new List<Vector2> { V(0, 0), V(20, 0), V(20f + 20f * Mathf.Cos(a), -20f * Mathf.Sin(a)) }, false, false));
            var chain = new RoadNetwork(new[] { road }, new Polygon[0], settings.roadEdges).Chains[0];
            // The house sits on the outside of the bend, on its bisector, so it faces (projects onto) the vertex itself.
            var bisector = (V(0, 1) + V(Mathf.Sin(a), Mathf.Cos(a))).normalized;
            var house = new StreetBuilding { Id = 77, Centroid = V(20, 0) + bisector * 9f };
            return (chain, new FrontageInterval { Chain = chain, Side = 1, Profile = EdgeProfile.Gutter, S0 = s0, S1 = s1, Kind = FrontageKind.Lot, Building = house });
        }

        [Test]
        public void GateFacingABend_SlidesToTheStraightPartOfItsLot()
        {
            var (_, lot) = BentLot(12f, 30f);
            var gate = FrontagePlanner.Walls(new[] { lot }, settings.frontage).Single().Openings.Single();
            float clear = gate.Width * 0.5f + settings.frontage.pilasterSize + 0.05f;
            Assert.GreaterOrEqual(Mathf.Abs(gate.S - 20f), clear - 0.01f, "gate and posts clear the 30° vertex");
            Assert.LessOrEqual(Mathf.Abs(gate.S - 20f), clear + 0.15f, "but stays as close to the house as it can");
        }

        [Test]
        public void GateOnALotThatIsAllBend_StaysFacingTheHouse()
        {
            var (_, lot) = BentLot(18f, 22.5f);
            var gate = FrontagePlanner.Walls(new[] { lot }, settings.frontage).Single().Openings.Single();
            Assert.AreEqual(20f, gate.S, 0.05f, "no straight stretch long enough: the old placement");
        }

        [Test]
        public void FlushHouse_GetsNoWall()
        {
            var plan = Plan(new StreetFixture(settings));
            Assert.AreEqual(FrontageKind.Flush, At(plan, StreetFixture.Residential, -1, 65f).Kind);
            Assert.IsFalse(WallsAt(FrontagePlanner.Walls(plan, settings.frontage), StreetFixture.Residential, -1, 65f).Any());
        }

        [Test]
        public void CampusSide_IsOneContinuousFence()
        {
            var plan = Plan(new StreetFixture(settings));
            var north = Of(plan, StreetFixture.Tertiary, 1);
            Assert.IsTrue(north.All(x => x.Kind == FrontageKind.Campus));
            Assert.Greater(north.Sum(x => x.Length), 195f);
            var walls = FrontagePlanner.Walls(plan, settings.frontage).Where(w => w.Side == 1 && w.Chain.Members.Any(m => m.Road.Id == StreetFixture.Tertiary)).ToList();
            Assert.AreEqual(1, walls.Count);
            Assert.AreEqual(WallStyle.Campus, walls[0].Style.style);
            Assert.AreEqual(0, walls[0].Openings.Count);
            Assert.AreEqual(EdgeProfile.Sidewalk, walls[0].Profile);
        }

        [Test]
        public void ParkAndWater_GetNothing()
        {
            var plan = Plan(new StreetFixture(settings));
            Assert.AreEqual(FrontageKind.Empty, At(plan, StreetFixture.Residential, -1, 170f).Kind, "park behind the line");
            Assert.AreEqual(FrontageKind.Empty, At(plan, StreetFixture.Residential, -1, 182f).Kind, "park wins over the shed on the line");
            Assert.AreEqual(FrontageKind.Empty, At(plan, StreetFixture.Residential, 1, 50f).Kind, "drain behind the line");
            var walls = FrontagePlanner.Walls(plan, settings.frontage);
            Assert.IsFalse(WallsAt(walls, StreetFixture.Residential, -1, 170f).Any());
            Assert.IsFalse(WallsAt(walls, StreetFixture.Residential, 1, 50f).Any());
        }

        [Test]
        public void Kos_IsExcluded()
        {
            var plan = Plan(new StreetFixture(settings));
            Assert.AreEqual(FrontageKind.Excluded, At(plan, StreetFixture.Residential, 1, 125f).Kind);
            Assert.IsFalse(WallsAt(FrontagePlanner.Walls(plan, settings.frontage), StreetFixture.Residential, 1, 125f).Any());
        }

        [Test]
        public void RoadMouths_LeaveGaps()
        {
            var plan = Plan(new StreetFixture(settings));
            Assert.IsFalse(Covers(Of(plan, StreetFixture.Residential, 1), 97f, 103f), "service: 1.75 + 0.8 + 0.4 + 0.15");
            Assert.IsFalse(Covers(Of(plan, StreetFixture.Residential, -1), 38.8f, 41.2f), "footway: 0.75 + 0.4 + 0.15");
            Assert.IsFalse(Covers(Of(plan, StreetFixture.Residential, -1), 147.8f, 152.2f), "driveway: 1.75 + 0.4 + 0.15");
        }

        [Test]
        public void EmptyStretch_IsVacant()
        {
            var plan = Plan(new StreetFixture(settings));
            var vacant = At(plan, StreetFixture.Residential, -1, 110f);
            Assert.AreEqual(FrontageKind.Vacant, vacant.Kind);
            Assert.Greater(vacant.Length, 60f);
            Assert.AreEqual(FrontageKind.Vacant, At(plan, StreetFixture.Tertiary, -1, 50f).Kind);
        }

        [Test]
        public void Walls_AreStable()
        {
            var plan = Plan(new StreetFixture(settings));
            var a = FrontagePlanner.Walls(plan, settings.frontage);
            var b = FrontagePlanner.Walls(Plan(new StreetFixture(settings)), settings.frontage);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Style.style, b[i].Style.style);
                Assert.AreEqual(a[i].Height, b[i].Height);
                Assert.AreEqual(a[i].Band, b[i].Band);
                Assert.AreEqual(a[i].S0, b[i].S0);
            }
            Assert.IsTrue(a.All(w => w.Band >= w.Style.firstBand && w.Band < w.Style.firstBand + w.Style.bandCount));
            Assert.IsTrue(a.All(w => w.Height >= w.Style.minHeight - 1e-5f && w.Height <= w.Style.maxHeight + 1e-5f));
        }

        [Test]
        public void MergeShort_FoldsAShortStretchIntoItsLongerNeighbour()
        {
            var chain = new RoadChain();
            var list = new List<FrontageInterval>
            {
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Vacant, S0 = 0f, S1 = 5f },
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Flush, S0 = 5f, S1 = 6f },
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Vacant, S0 = 6f, S1 = 12f },
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Blocked, S0 = 12f, S1 = 13f },
                new FrontageInterval { Chain = chain, Kind = FrontageKind.Lot, S0 = 13f, S1 = 14f }
            };
            FrontagePlanner.MergeShort(list, 1.5f);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(FrontageKind.Vacant, list[0].Kind);
            Assert.AreEqual(0f, list[0].S0);
            Assert.AreEqual(12f, list[0].S1);
            Assert.AreEqual(FrontageKind.Blocked, list[1].Kind);
            Assert.AreEqual(FrontageKind.Lot, list[2].Kind, "a short stretch next to a blocked one has nobody to join");
        }

        [Test]
        public void RotatedHouse_IsOneLotWithOneGate()
        {
            // An 8 × 8 m house turned 30°, centred 7 m behind the property line of a residential road along z = -200.
            var c = V(-250f, -200f - 3.7f - 7f);
            var corners = new[] { V(4, 4), V(-4, 4), V(-4, -4), V(4, -4) }
                .Select(p => c + new Vector2(p.x * 0.8660254f - p.y * 0.5f, p.x * 0.5f + p.y * 0.8660254f)).ToArray();
            var fx = new StreetFixture(settings, osm =>
            {
                osm.Way(3001, new[] { V(-300f, -200f), V(-200f, -200f) }, false, ("highway", "residential"));
                osm.Way(3002, corners, true, ("building", "house"));
            });
            var plan = Plan(fx);
            var lots = Of(plan, 3001, -1).Where(x => x.Kind == FrontageKind.Lot).ToList();
            Assert.AreEqual(1, lots.Count);
            Assert.AreEqual(3002, lots[0].Building.Id);
            var walls = FrontagePlanner.Walls(plan, settings.frontage).Where(w => w.Side == -1 && w.Chain.Members.Any(m => m.Road.Id == 3001)).ToList();
            Assert.AreEqual(1, walls.Sum(w => w.Openings.Count));
        }

        [Test]
        public void ParallelRoads_WallsStayOutOfTheOtherRoad()
        {
            var fx = new StreetFixture(settings, osm =>
            {
                osm.Way(3101, new[] { V(-300f, -400f), V(-200f, -400f) }, false, ("highway", "residential"));
                osm.Way(3102, new[] { V(-300f, -393f), V(-200f, -393f) }, false, ("highway", "service"));
            });
            var service = fx.Chain(3102);
            var walls = FrontagePlanner.Walls(Plan(fx), settings.frontage).Where(w => w.Chain.Members.Any(m => m.Road.Id == 3101)).ToList();
            Assert.Greater(walls.Count, 0);
            foreach (var w in walls)
                for (float s = w.S0; s <= w.S1; s += 1f)
                {
                    var p = w.Chain.SidePoint(s, w.Side, FrontagePlanner.LineOffset(w.Chain, w.Profile, settings.roadEdges, settings.frontage) + 0.075f);
                    Assert.GreaterOrEqual(Polyline2D.DistanceTo(service.Points, p), service.HalfWidth + service.MaxEdgeWidth);
                }
        }

        [Test]
        public void RightAngleBend_WallFollowsTheCorner()
        {
            var fx = new StreetFixture(settings, osm => osm.Way(2001, new[] { V(-300, -300), V(-250, -300), V(-250, -250) }, false, ("highway", "residential")));
            var plan = Plan(fx);
            foreach (int side in RoadNetwork.Sides)
            {
                var list = Of(plan, 2001, side);
                Assert.AreEqual(1, list.Count, "side " + side);
                Assert.AreEqual(FrontageKind.Vacant, list[0].Kind);
                Assert.Greater(list[0].Length, 95f);
            }
        }
    }
}
