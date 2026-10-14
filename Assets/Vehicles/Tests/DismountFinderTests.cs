using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class DismountFinderTests
    {
        VehicleFixture fx;
        Transform bike;

        static Vector3 O => VehicleFixture.Origin;

        [SetUp]
        public void SetUp()
        {
            fx = new VehicleFixture();
            fx.Floor();
            bike = fx.Track(new GameObject("Bike")).transform;
            bike.SetPositionAndRotation(O, Quaternion.identity);
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        bool Find(out Vector3 feet)
        {
            fx.Sync();
            return DismountFinder.TryFind(bike, 1.1f, 0.35f, 1.8f, out feet);
        }

        [Test]
        public void OpenGround_GetsOffOnTheLeft()
        {
            Assert.IsTrue(Find(out var feet));
            Assert.Less(Vector3.Distance(O + new Vector3(-1.1f, 0f, 0f), feet), 1e-3f);
        }

        [Test]
        public void LeftBlocked_GetsOffOnTheRight()
        {
            fx.Box("WallLeft", new Vector3(-1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            Assert.IsTrue(Find(out var feet));
            Assert.Greater(feet.x, O.x);
        }

        [Test]
        public void BothSidesBlocked_Fails()
        {
            fx.Box("WallLeft", new Vector3(-1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            fx.Box("WallRight", new Vector3(1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            Assert.IsFalse(Find(out _));
        }

        [Test]
        public void LowCeiling_Fails()
        {
            fx.Box("Ceiling", new Vector3(0f, 1.3f, 0f), new Vector3(6f, 0.2f, 6f));
            Assert.IsFalse(Find(out _));
        }

        [Test]
        public void RaisedGround_PutsTheFeetOnTop()
        {
            fx.Box("Curb", new Vector3(-1.1f, 0.1f, 0f), new Vector3(1f, 0.2f, 2f));
            Assert.IsTrue(Find(out var feet));
            Assert.AreEqual(O.y + 0.2f, feet.y, 1e-3f);
        }

        [Test]
        public void TurnedBike_UsesItsOwnLeftSide()
        {
            bike.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.IsTrue(Find(out var feet));
            Assert.Less(Vector3.Distance(O + new Vector3(0f, 0f, 1.1f), feet), 1e-3f, "kiri motor yang menghadap +X adalah +Z");
        }
    }
}
