using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    public class LaneGraphTests
    {
        // Two lanes meeting at node 1: an L (0,0)->(10,0)->(10,10) and a stub (10,0)->(20,0); node 2 and 0 are dead ends.
        static LaneGraph Graph() => new LaneGraph(
            new[]
            {
                new Lane { points = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, startNode = 0, endNode = 1, width = 1 },
                new Lane { points = new[] { new Vector2(10f, 0f), new Vector2(10f, 10f) }, startNode = 1, endNode = 2, width = 1 },
                new Lane { points = new[] { new Vector2(10f, 0f), new Vector2(20f, 0f) }, startNode = 1, endNode = 3, width = 0 }
            },
            new[]
            {
                new LaneNode { position = new Vector2(0f, 0f), lanes = new[] { 0 } },
                new LaneNode { position = new Vector2(10f, 0f), lanes = new[] { 0, 1, 2 } },
                new LaneNode { position = new Vector2(10f, 10f), lanes = new[] { 1 } },
                new LaneNode { position = new Vector2(20f, 0f), lanes = new[] { 2 } }
            });

        [Test]
        public void PointAt_WalksByArcLength_AndClamps()
        {
            var g = Graph();
            Assert.AreEqual(10f, g.Length(0), 1e-4f);
            var p = g.PointAt(0, 4f, out var dir);
            Assert.AreEqual(new Vector2(4f, 0f), p);
            Assert.AreEqual(Vector2.right, dir);
            Assert.AreEqual(new Vector2(10f, 0f), g.PointAt(0, 99f, out _));
            Assert.AreEqual(new Vector2(0f, 0f), g.PointAt(0, -5f, out _));
        }

        [Test]
        public void NextLane_PicksAnotherLane_OrTurnsBackAtADeadEnd()
        {
            var g = Graph();
            var rng = new SeededRandom(7);
            var seen = new HashSet<int>();
            for (int i = 0; i < 40; i++) seen.Add(g.NextLane(1, 0, ref rng));
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, seen, "never back onto the lane it came from at a junction");
            Assert.AreEqual(1, g.NextLane(2, 1, ref rng), "a dead end turns the walker around");
        }

        [Test]
        public void Near_FindsLanesAroundAPoint_AndProjectGivesTheArcLength()
        {
            var g = Graph();
            var near = new List<int>();
            g.Near(new Vector2(18f, 1f), 3f, near);
            CollectionAssert.Contains(near, 2);
            g.Near(new Vector2(80f, 80f), 3f, near);
            CollectionAssert.IsEmpty(near, "nothing out in the fields");
            float s = g.Project(1, new Vector2(11f, 6f), out float d);
            Assert.AreEqual(6f, s, 1e-4f);
            Assert.AreEqual(1f, d, 1e-4f);
        }
    }

    public class PedestrianScheduleTests
    {
        [Test]
        public void BusyByDay_EmptyAtNight_TheFigureOnlyInTheDeepNight()
        {
            Assert.AreEqual(PedestrianSchedule.Day, PedestrianSchedule.TargetCount(10f));
            Assert.AreEqual(PedestrianSchedule.Evening, PedestrianSchedule.TargetCount(19f));
            Assert.AreEqual(PedestrianSchedule.Night, PedestrianSchedule.TargetCount(23.5f));
            Assert.AreEqual(PedestrianSchedule.Night, PedestrianSchedule.TargetCount(2f));
            Assert.Less(PedestrianSchedule.Night, PedestrianSchedule.Evening);
            Assert.Less(PedestrianSchedule.Evening, PedestrianSchedule.Day);
            Assert.AreEqual(0, PedestrianSchedule.SosokCount(12f));
            Assert.AreEqual(0, PedestrianSchedule.SosokCount(20f));
            Assert.AreEqual(1, PedestrianSchedule.SosokCount(23f));
            Assert.AreEqual(1, PedestrianSchedule.SosokCount(1f));
        }
    }

    public class PedestrianSystemTests
    {
        KampungFixture fx;
        PedestrianSystem people;

        [SetUp]
        public void SetUp()
        {
            Interactable.ResetStatics();
            fx = new KampungFixture(KampungFixture.StreetGrid);
            people = new PedestrianSystem();
            fx.Director.AddSystem(people);
            fx.Fx.Sync();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [Test]
        public void ByDay_TheStreetsFillUp_AwayFromThePlayer_OnTheGround()
        {
            fx.Director.JumpClock(10f);
            fx.Run(8f, 0.05f);
            Assert.AreEqual(PedestrianSchedule.Day, people.ActiveCount);
            var player = fx.Player.transform.position;
            foreach (var p in people.Pool.Where(p => p.Active))
            {
                Assert.GreaterOrEqual(Flat(p.Position, player), PedestrianSystem.SpawnMin - 12f, "spawned out of arm's reach (and walked a little since)");
                Assert.AreEqual(KampungFixture.Origin.y, p.Position.y, 0.05f, "standing on the floor");
                Assert.IsNotNull(p.Talk, "every passer-by can be greeted");
            }
            Assert.IsFalse(people.Sosok.Active, "no figure in daylight");
        }

        [Test]
        public void AtNight_TheStreetsEmpty_ButTheFigureWaitsAhead_AndVanishesUpClose()
        {
            fx.Director.JumpClock(10f);
            fx.Run(6f, 0.05f);
            fx.Director.JumpClock(23f);
            fx.Run(60f, 0.1f);
            Assert.LessOrEqual(people.ActiveCount, PedestrianSchedule.Night + 6, "most people went home (those in view may linger)");
            Assert.IsTrue(people.ForceSosok() || people.Sosok.Active, "a lane 28-48 m ahead exists in the grid");
            Assert.IsTrue(people.Sosok.Active);
            int vanished = 0;
            people.Vanished += () => vanished++;
            fx.Player.Teleport(people.Sosok.Position + people.Sosok.Root.transform.forward * 3f, 0f);
            fx.Fx.Sync();
            fx.Director.Tick(0.05f);
            Assert.IsFalse(people.Sosok.Active, "gone when you come close");
            Assert.AreEqual(1, vanished);
        }

        [Test]
        public void Walkers_FollowTheLanes_AndTurnAtJunctions()
        {
            fx.Director.JumpClock(10f);
            int lane = LaneAround(fx.Player.transform.position, 25f);
            var p = people.SpawnAt(lane, 2f, 1);
            Assert.IsNotNull(p);
            var start = p.Position;
            var lanes = new HashSet<int>();
            for (int i = 0; i < 600; i++)
            {
                people.Tick(0.05f);
                lanes.Add(p.Lane);
            }
            Assert.Greater(Flat(start, p.Position) + lanes.Count, 2f);
            Assert.Greater(lanes.Count, 1, "30 s of walking crosses a 20 m block into another lane");
            var graph = people.Graph;
            var on = graph.PointAt(p.Lane, p.S, out _);
            Assert.Less(Vector2.Distance(on, new Vector2(p.Position.x, p.Position.z)), 0.5f, "on the lane (with the side offset)");
        }

        [Test]
        public void Greeting_StopsTheWalker_AndToastsALine()
        {
            fx.Director.JumpClock(10f);
            var p = people.SpawnAt(LaneAround(fx.Player.transform.position, 25f), 5f, 1);
            Assert.AreEqual(TalkInteractable.PromptText, p.Talk.GetPrompt(fx.Context));
            Assert.IsTrue(p.Talk.Interact(fx.Context));
            Assert.AreEqual(PedestrianState.Talk, p.State);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.StartsWith("Warga:")));
            var at = p.Position;
            people.Tick(1f);
            Assert.AreEqual(0f, Flat(at, p.Position), 1e-3f, "stands still while talking");
            people.Tick(PedestrianSystem.TalkSeconds);
            Assert.AreEqual(PedestrianState.Walk, p.State);
        }

        [Test]
        public void Alarm_SendsTheNearestAfterTheThief_TheRestRun_AndTheyCatchYou()
        {
            fx.Director.JumpClock(10f);
            var player = fx.Player.transform.position;
            var near = people.SpawnAt(LaneNearest(player, 0), 0f, 1);
            near.Place(player + new Vector3(6f, 0f, 0f), Vector3.left);
            var far = people.SpawnAt(LaneNearest(player, 1), 0f, 1);
            far.Place(player + new Vector3(0f, 0f, 12f), Vector3.back);
            fx.Fx.Sync();
            var witnesses = people.Witnesses(player, PedestrianSystem.WitnessRadius);
            Assert.AreEqual(2, witnesses.Count);
            Assert.AreEqual(1, people.Alarm(player, witnesses, 1));
            Assert.AreEqual(PedestrianState.Chase, near.State);
            Assert.AreEqual(PedestrianState.Flee, far.State);
            Assert.IsFalse(near.Talk.CanInteract(fx.Context), "no small talk with an angry warga");
            Pedestrian caught = null;
            people.Caught += c => caught = c;
            for (int i = 0; i < 60 && caught == null; i++)
            {
                people.Tick(0.05f);
                fx.Fx.Sync();
            }
            Assert.AreSame(near, caught, "5.6 m/s closes 6 m in about a second");
            people.CalmDown();
            Assert.AreEqual(0, people.ChaserCount);
            Assert.AreEqual(PedestrianState.Walk, near.State);
        }

        [Test]
        public void Witnesses_DoNotSeeThroughWalls()
        {
            fx.Director.JumpClock(10f);
            var player = fx.Player.transform.position;
            var p = people.SpawnAt(0, 0f, 1);
            p.Place(player + new Vector3(10f, 0f, 0f), Vector3.left);
            fx.Fx.Box("Wall", new Vector3(5f, 1.5f, 0f) + (player - KampungFixture.Origin), new Vector3(0.3f, 3f, 6f));
            fx.Fx.Sync();
            Assert.AreEqual(0, people.Witnesses(player, PedestrianSystem.WitnessRadius).Count);
        }

        /// <summary>A lane whose middle is about <paramref name="distance"/> metres from p (inside the despawn range).</summary>
        int LaneAround(Vector3 p, float distance)
        {
            var at = new Vector2(p.x, p.z);
            int best = 0;
            float bestError = float.PositiveInfinity;
            for (int i = 0; i < people.Graph.LaneCount; i++)
            {
                var mid = people.Graph.PointAt(i, people.Graph.Length(i) / 2f, out _);
                float error = Mathf.Abs(Vector2.Distance(mid, at) - distance);
                if (error < bestError)
                {
                    bestError = error;
                    best = i;
                }
            }
            return best;
        }

        int LaneNearest(Vector3 p, int skip)
        {
            var near = new List<int>();
            people.Graph.Near(new Vector2(p.x, p.z), 30f, near);
            return near[Mathf.Min(skip, near.Count - 1)];
        }
    }
}
