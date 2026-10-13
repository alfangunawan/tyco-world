using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class KosSitePlannerTests
    {
        static readonly List<Vector2> Kos = new List<Vector2> { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 12f), new Vector2(0f, 12f) };
        static readonly List<IReadOnlyList<Vector2>> NoNeighbours = new List<IReadOnlyList<Vector2>>();

        static RoadLine Road(long id, float halfWidth, params Vector2[] points) =>
            new RoadLine { Id = id, HalfWidth = halfWidth, Points = new List<Vector2>(points) };

        static KosSitePlan Plan(IReadOnlyList<Vector2> footprint, IReadOnlyList<RoadLine> roads, IReadOnlyList<IReadOnlyList<Vector2>> neighbours, KosSiteOptions options = null)
        {
            Assert.IsTrue(KosSitePlanner.TryPlan(footprint, roads, neighbours, options ?? new KosSiteOptions(), out var plan, out string reason), reason);
            return plan;
        }

        static void Near(Vector2 expected, Vector2 actual, float tolerance = 1e-3f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, tolerance, "y");
        }

        [Test]
        public void PicksEdgeFacingNearestRoad()
        {
            var plan = Plan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)) }, NoNeighbours);
            Assert.AreEqual(0, plan.EdgeIndex);
            Near(new Vector2(0f, -1f), plan.Outward);
            Assert.AreEqual(180f, plan.SpawnYaw, 1e-3f);
            Assert.AreEqual(5.5f, plan.YardDepth, 1e-3f);
            Assert.AreEqual(6.5f, plan.RoadClearance, 1e-3f);
            Assert.AreEqual(1, plan.RoadId);
            Near(new Vector2(3f, 0f), plan.Door);
            Near(new Vector2(3f, -5.5f), plan.Gate);
            Near(new Vector2(3f, -1.2f), plan.Spawn);
            Near(new Vector2(7f, -2.475f), plan.Motor);
            Assert.AreEqual(90f, plan.MotorYaw, 1e-3f);
        }

        [Test]
        public void Motor_StaysOutOfGateCorridor()
        {
            var plan = Plan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)) }, NoNeighbours);
            Assert.GreaterOrEqual(Mathf.Abs(Vector2.Dot(plan.Motor - plan.Door, plan.Along)), 1.5f);
        }

        [Test]
        public void NeighbourBlockingYard_FallsBackToNextEdge()
        {
            var roads = new[]
            {
                Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)),
                Road(2, 2f, new Vector2(20f, -10f), new Vector2(20f, 30f))
            };
            var blocker = new List<IReadOnlyList<Vector2>>
            {
                new List<Vector2> { new Vector2(0f, -3f), new Vector2(10f, -3f), new Vector2(10f, -1f), new Vector2(0f, -1f) }
            };
            var plan = Plan(Kos, roads, blocker);
            Assert.AreEqual(1, plan.EdgeIndex);
            Assert.AreEqual(90f, plan.SpawnYaw, 1e-3f);
            Assert.AreEqual(6f, plan.YardDepth, 1e-3f);
            Assert.AreEqual(2, plan.RoadId);
        }

        [Test]
        public void Yard_ClampsToMaxAndMin()
        {
            Assert.AreEqual(6f, Plan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -40f), new Vector2(3f, -60f)) }, NoNeighbours).YardDepth, 1e-3f);
            Assert.AreEqual(2.5f, Plan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -3f), new Vector2(3f, -30f)) }, NoNeighbours).YardDepth, 1e-3f);
        }

        [Test]
        public void Gate_ClampedInsideEdge()
        {
            var plan = Plan(Kos, new[] { Road(1, 2.5f, new Vector2(0.5f, -9f), new Vector2(0.5f, -30f)) }, NoNeighbours);
            Assert.AreEqual(0, plan.EdgeIndex);
            Near(new Vector2(1.5f, 0f), plan.Door);
        }

        [Test]
        public void ClockwiseFootprint_GivesSameResult()
        {
            var clockwise = new List<Vector2>(Kos);
            clockwise.Reverse();
            var plan = Plan(clockwise, new[] { Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)) }, NoNeighbours);
            Assert.AreEqual(180f, plan.SpawnYaw, 1e-3f);
            Near(new Vector2(3f, 0f), plan.Door);
        }

        [Test]
        public void PreferredOutward_OverridesNearestRoad()
        {
            var roads = new[]
            {
                Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)),
                Road(2, 2f, new Vector2(20f, -10f), new Vector2(20f, 30f))
            };
            var plan = Plan(Kos, roads, NoNeighbours, new KosSiteOptions { preferredOutward = new Vector2(1f, 0f) });
            Assert.AreEqual(90f, plan.SpawnYaw, 1e-3f);
            Assert.AreEqual(2, plan.RoadId);
        }

        [Test]
        public void NoRoads_ReturnsFalseWithReason()
        {
            Assert.IsFalse(KosSitePlanner.TryPlan(Kos, new RoadLine[0], NoNeighbours, new KosSiteOptions(), out _, out string reason));
            StringAssert.Contains("jalan", reason);
        }

        [Test]
        public void AllRoadFacingEdgesBlocked_ReturnsFalse()
        {
            var blocker = new List<IReadOnlyList<Vector2>>
            {
                new List<Vector2> { new Vector2(0f, -3f), new Vector2(10f, -3f), new Vector2(10f, -1f), new Vector2(0f, -1f) }
            };
            Assert.IsFalse(KosSitePlanner.TryPlan(Kos, new[] { Road(1, 2.5f, new Vector2(3f, -9f), new Vector2(3f, -30f)) }, blocker,
                new KosSiteOptions(), out _, out string reason));
            StringAssert.Contains("terhalang", reason);
        }

        [Test]
        public void YawOf_UsesUnityConvention()
        {
            Assert.AreEqual(0f, KosSitePlanner.YawOf(new Vector2(0f, 1f)), 1e-4f);
            Assert.AreEqual(90f, KosSitePlanner.YawOf(new Vector2(1f, 0f)), 1e-4f);
            Assert.AreEqual(180f, KosSitePlanner.YawOf(new Vector2(0f, -1f)), 1e-4f);
            Assert.AreEqual(270f, KosSitePlanner.YawOf(new Vector2(-1f, 0f)), 1e-4f);
        }
    }
}
