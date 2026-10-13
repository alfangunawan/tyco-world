using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class KosFrontyardBuilderTests
    {
        static readonly Vector2 Offset = new Vector2(5000f, 5000f);
        PhysicsFixture fx;

        [SetUp]
        public void SetUp() => fx = new PhysicsFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        GameObject Build(out KosSitePlan plan)
        {
            var kos = new List<Vector2> { Offset, Offset + new Vector2(10f, 0f), Offset + new Vector2(10f, 12f), Offset + new Vector2(0f, 12f) };
            var road = new RoadLine { Id = 1, HalfWidth = 2.5f, Points = new List<Vector2> { Offset + new Vector2(3f, -9f), Offset + new Vector2(3f, -30f) } };
            Assert.IsTrue(KosSitePlanner.TryPlan(kos, new[] { road }, new List<IReadOnlyList<Vector2>>(), new KosSiteOptions(), out plan, out _));
            var gate = fx.Track(PlaceholderPropFactory.CreateGate(null, null, null, null));
            var door = fx.Track(PlaceholderPropFactory.CreateDoor(null, null, null));
            var motor = fx.Track(PlaceholderPropFactory.CreateMotor(null, null, null));
            var unit = fx.Track(PlaceholderPropFactory.UnitBoxMesh());
            var root = fx.Track(KosFrontyardBuilder.Build(plan, gate, door, motor, null, null, unit));
            Physics.SyncTransforms();
            return root;
        }

        static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-3f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, tolerance, "y");
            Assert.AreEqual(expected.z, actual.z, tolerance, "z");
        }

        [Test]
        public void Build_PlacesPartsFromPlan()
        {
            var root = Build(out _);
            Assert.AreEqual("KosFrontyard", root.name);
            float slab = PlaceholderPropFactory.YardThickness;
            var spawn = root.GetComponentInChildren<PlayerSpawnPoint>();
            Assert.AreEqual("kos", spawn.Id);
            Assert.AreEqual("Kos Raflesia 3", spawn.ArrivalMessage);
            Near(new Vector3(5003f, slab, 4998.8f), spawn.Feet);
            Assert.AreEqual(180f, spawn.Yaw, 1e-2f);
            Near(new Vector3(5003f, 0f, 4994.5f), root.GetComponentInChildren<GateInteractable>().transform.position);
            Near(new Vector3(5003f, slab, 5000f), root.GetComponentInChildren<LockedDoorInteractable>().transform.position);
            var motor = root.GetComponentInChildren<PlaceholderVehicleInteractable>().transform;
            Near(new Vector3(5007f, slab, 4997.525f), motor.position);
            Assert.AreEqual(90f, motor.eulerAngles.y, 1e-2f);
        }

        [Test]
        public void Yard_IsConcreteSlabCoveringTheYard()
        {
            var root = Build(out _);
            var yard = root.transform.Find("Yard");
            Assert.AreEqual(SurfaceType.Concrete, yard.GetComponent<GroundSurface>().Type);
            var b = yard.GetComponent<BoxCollider>().bounds;
            Assert.AreEqual(PlaceholderPropFactory.YardThickness, b.max.y, 1e-3f);
            Assert.AreEqual(5000f, b.min.x, 1e-2f);
            Assert.AreEqual(5010f, b.max.x, 1e-2f);
            Assert.AreEqual(4994.5f, b.min.z, 1e-2f);
            Assert.AreEqual(5000f, b.max.z, 1e-2f);
        }

        [Test]
        public void Walls_AreTallerThanJumpReach_AndLeaveTheGateOpen()
        {
            var root = Build(out _);
            int walls = 0;
            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith("Wall_")) continue;
                walls++;
                var b = child.GetComponent<BoxCollider>().bounds;
                Assert.GreaterOrEqual(b.size.y, 1.8f, child.name);
                bool front = child.name.StartsWith("Wall_Front");
                if (front) Assert.IsTrue(b.max.x <= 5002f + 1e-3f || b.min.x >= 5004f - 1e-3f, child.name + " blocks the gate");
            }
            Assert.AreEqual(4, walls);
        }
    }
}
