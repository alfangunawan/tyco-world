using System.Linq;
using NUnit.Framework;
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    public class MinimapMathTests
    {
        static void AreClose(Vector2 expected, Vector2 actual) =>
            Assert.Less(Vector2.Distance(expected, actual), 1e-3f, "expected " + expected + " got " + actual);

        [Test]
        public void TheWayYouFace_IsUp()
        {
            var me = new Vector2(100f, 50f);
            // Facing north: north is up (negative y on screen), east is right.
            AreClose(new Vector2(0f, -10f), MinimapMath.ToMap(me + new Vector2(0f, 10f), me, 0f, 1f));
            AreClose(new Vector2(10f, 0f), MinimapMath.ToMap(me + new Vector2(10f, 0f), me, 0f, 1f));
            // Facing east: east is up, north is on the left.
            AreClose(new Vector2(0f, -10f), MinimapMath.ToMap(me + new Vector2(10f, 0f), me, 90f, 1f));
            AreClose(new Vector2(-10f, 0f), MinimapMath.ToMap(me + new Vector2(0f, 10f), me, 90f, 1f));
            // Facing south: north is down. Scale applies.
            AreClose(new Vector2(0f, 20f), MinimapMath.ToMap(me + new Vector2(0f, 10f), me, 180f, 2f));
        }

        [Test]
        public void FarPoints_StopOnTheRim_InTheirDirection()
        {
            var near = MinimapMath.ClampToRim(new Vector2(30f, 40f), 100f, out bool c1);
            Assert.IsFalse(c1);
            AreClose(new Vector2(30f, 40f), near);
            var far = MinimapMath.ClampToRim(new Vector2(300f, 400f), 100f, out bool c2);
            Assert.IsTrue(c2);
            AreClose(new Vector2(60f, 80f), far);
        }

        [Test]
        public void TheView_DrawsNearbyRoads_PinsFarTargets_AndDropsFarShops()
        {
            var data = ScriptableObject.CreateInstance<KampungData>();
            var map = new MinimapView();
            try
            {
                data.mapRoads = new[]
                {
                    new Lane { points = new[] { new Vector2(-50f, 0f), new Vector2(50f, 0f) }, width = 2 },
                    new Lane { points = new[] { new Vector2(2000f, 0f), new Vector2(2100f, 0f) }, width = 1 }
                };
                var markers = new[]
                {
                    new MapMarker { Position = new Vector2(0f, 1000f), Color = MapColors.Target, Size = 8f, Pin = true },
                    new MapMarker { Position = new Vector2(0f, -1000f), Color = MapColors.Shop, Size = 4f },
                    new MapMarker { Position = new Vector2(20f, 0f), Color = MapColors.Kos, Size = 6f }
                };
                map.Show(data, Vector2.zero, 0f, false, markers);
                Assert.AreEqual(1, map.LineCount, "only the road around the player");
                Assert.AreEqual(2, map.Dots.Count, "the far shop is left out");
                var target = map.Dots.First(d => d.Color == MapColors.Target);
                Assert.IsTrue(target.OnRim);
                Assert.Less(target.Offset.y, -90f, "1 km north, facing north: on the rim straight up");
                Assert.AreEqual(0f, target.Offset.x, 1e-3f);
                var kos = map.Dots.First(d => d.Color == MapColors.Kos);
                Assert.IsFalse(kos.OnRim);
                Assert.AreEqual(20f * map.PixelsPerMetre, kos.Offset.x, 1e-3f);
                Assert.Less(map.NorthOffset.y, 0f, "the U mark is at the top when facing north");
                map.Show(data, Vector2.zero, 0f, true, markers);
                Assert.AreEqual(MinimapView.RideRadiusMetres, map.RadiusMetres, "riding zooms out");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }

    public class RoadGraphTests
    {
        static Lane Road(params Vector2[] points) => new Lane { points = points, width = 1 };

        /// <summary>(cells+1) east-west and (cells+1) north-south roads, each one straight line: they only meet where they cross.</summary>
        public static Lane[] GridRoads(Vector2 centre, int cells, float spacing)
        {
            float half = cells * spacing / 2f;
            var roads = new System.Collections.Generic.List<Lane>();
            for (int i = 0; i <= cells; i++)
            {
                float k = -half + i * spacing;
                roads.Add(Road(centre + new Vector2(-half, k), centre + new Vector2(half, k)));
                roads.Add(Road(centre + new Vector2(k, -half), centre + new Vector2(k, half)));
            }
            return roads.ToArray();
        }

        [Test]
        public void CrossingRoads_Meet_AndTheRouteTurnsThere()
        {
            // A plus sign drawn as two straight lines that share no point.
            var graph = new RoadGraph(new[] { Road(new Vector2(-50f, 0f), new Vector2(50f, 0f)), Road(new Vector2(0f, -50f), new Vector2(0f, 50f)) });
            Assert.AreEqual(5, graph.NodeCount, "four ends and the crossing");
            var route = graph.Route(new Vector2(-30f, 2f), new Vector2(3f, 40f));
            Assert.IsNotNull(route);
            Assert.IsTrue(route.Any(q => q.magnitude < 1e-3f), "turns at the crossing");
            Assert.AreEqual(30f + 40f + 3f, RoadGraph.Length(route), 1e-3f);
        }

        [Test]
        public void ARoadEndingShortOfAnother_IsJoinedToIt()
        {
            // A side road stops 4 m short of the main road (a T junction the map lines missed).
            var graph = new RoadGraph(new[] { Road(new Vector2(-50f, 0f), new Vector2(50f, 0f)), Road(new Vector2(10f, 4f), new Vector2(10f, 60f)) });
            var route = graph.Route(new Vector2(-40f, 0f), new Vector2(10f, 50f));
            Assert.IsNotNull(route, "joined");
            Assert.AreEqual(50f + 50f, RoadGraph.Length(route), 1e-3f, "along the main road to x = 10, then up the side road");
        }

        [Test]
        public void FarFromRoads_OrNotConnected_ThereIsNoRoute()
        {
            var graph = new RoadGraph(new[] { Road(new Vector2(0f, 0f), new Vector2(50f, 0f)), Road(new Vector2(500f, 0f), new Vector2(550f, 0f)) });
            Assert.IsNull(graph.Route(new Vector2(0f, 500f), new Vector2(10f, 0f)));
            Assert.IsNull(graph.Route(new Vector2(10f, 0f), new Vector2(520f, 0f)));
            var same = graph.Route(new Vector2(5f, 1f), new Vector2(30f, 1f));
            Assert.AreEqual(25f + 1f, RoadGraph.Length(same), 1e-3f, "along the road, then off it to the target (the caller adds the player's own step)");
        }

        [Test]
        public void AcrossAGrid_TheRouteKeepsToTheStreets_AndIsShortest()
        {
            var graph = new RoadGraph(GridRoads(Vector2.zero, 8, 20f));
            var route = graph.Route(new Vector2(-75f, -80f), new Vector2(80f, 80f));
            Assert.IsNotNull(route);
            Assert.AreEqual(155f + 160f, RoadGraph.Length(route), 0.01f, "a Manhattan path is the shortest on a grid");
            foreach (var q in route)
            {
                bool onStreet = Mathf.Abs(q.x / 20f - Mathf.Round(q.x / 20f)) < 1e-3f || Mathf.Abs(q.y / 20f - Mathf.Round(q.y / 20f)) < 1e-3f;
                Assert.IsTrue(onStreet, q + " is off the streets");
            }
        }
    }

    public class MinimapHudTests
    {
        KampungFixture fx;
        MissionSystem jobs;

        [SetUp]
        public void SetUp()
        {
            Interactable.ResetStatics();
            fx = new KampungFixture(d =>
            {
                KampungFixture.StreetGrid(d, 16, 30f);
                d.mapRoads = RoadGraphTests.GridRoads(new Vector2(KampungFixture.Origin.x, KampungFixture.Origin.z), 16, 30f);
                d.spots = new[]
                {
                    new Spot { kind = SpotKind.Kos, position = KampungFixture.Origin + new Vector3(0f, 0f, -30f) },
                    new Spot { kind = SpotKind.Door, position = KampungFixture.Origin + new Vector3(150f, 0f, 15f), yaw = 180f }
                };
                d.shops = new[] { KampungFixture.Site("warung", ShopType.Warung, new Vector3(30f, 0f, 2f), 180f, "Warung Bu Uji") };
            });
            jobs = new MissionSystem();
            fx.Director.AddSystem(jobs);
            fx.Director.JumpClock(10f);
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        [Test]
        public void TheJobTarget_ShowsOnTheMinimap_PinnedToTheRimWhenFar()
        {
            Assert.IsNotNull(fx.Director.Hud.Minimap);
            var galon = jobs.Get<GalonMission>();
            Assert.IsTrue(jobs.StartJob(galon));
            fx.Director.Tick(0.05f);
            Go(galon.Customer.transform.position + new Vector3(0f, 0f, 400f), 0f);
            fx.Director.RefreshMinimap();
            var far = fx.Director.Hud.Minimap.Dots.Where(d => d.Color == MapColors.Target).ToList();
            Assert.AreEqual(1, far.Count);
            Assert.IsTrue(far[0].OnRim, "the warung target 400 m south sits on the rim");
            Assert.Greater(far[0].Offset.y, 0f, "facing north: south is down");
            Go(galon.Shop.counter + new Vector3(0f, 0f, 20f), 180f);
            fx.Director.RefreshMinimap();
            var near = fx.Director.Hud.Minimap.Dots.First(d => d.Color == MapColors.Target);
            Assert.IsFalse(near.OnRim);
            Assert.Less(near.Offset.y, 0f, "facing south toward it: ahead is up");
        }

        [Test]
        public void TheGpsRoute_RunsFromThePlayer_ToTheTarget_OnBothMaps()
        {
            var galon = jobs.Get<GalonMission>();
            Assert.IsTrue(jobs.StartJob(galon));
            Go(KampungFixture.Origin + new Vector3(-120f, 0f, 95f), 0f);
            jobs.RefreshRoute();
            Assert.IsNotNull(jobs.Route);
            var target = jobs.Active.Target.Value;
            Assert.Less(Vector2.Distance(jobs.Route[jobs.Route.Count - 1], new Vector2(target.x, target.z)), 1e-3f);
            Assert.Greater(RoadGraph.Length(jobs.Route), Vector2.Distance(jobs.Route[0], new Vector2(target.x, target.z)), "along the streets, not straight");
            fx.Director.RefreshMinimap();
            var pixels = fx.Director.Hud.Minimap.RoutePixels;
            Assert.Greater(pixels.Count, 2);
            Assert.Less(pixels[0].magnitude, 1e-3f, "the line starts at the player's arrow");
        }

        void Go(Vector3 p, float yaw)
        {
            fx.Player.Teleport(p, yaw);
            fx.Fx.Sync();
        }
    }
}
