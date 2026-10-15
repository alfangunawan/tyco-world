using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>Handling per class (spec vehicles-3a §5) and the body fitted to a model's bounds.</summary>
    public class VehicleClassPresetsTests
    {
        BikeTuning t;

        [SetUp]
        public void SetUp() => t = ScriptableObject.CreateInstance<BikeTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(t);

        [TestCase(VehicleClass.MotorMatic, 60f, 4.0f, 1.6f, 150f, 60f, true)]
        [TestCase(VehicleClass.MotorBebek, 65f, 4.2f, 1.6f, 145f, 58f, true)]
        [TestCase(VehicleClass.CarSmall, 50f, 3.2f, 1.2f, 75f, 30f, false)]
        [TestCase(VehicleClass.Mpv, 45f, 3.0f, 1.1f, 70f, 28f, false)]
        [TestCase(VehicleClass.Van, 40f, 2.7f, 1.0f, 65f, 26f, false)]
        [TestCase(VehicleClass.Angkot, 40f, 2.7f, 1.0f, 65f, 26f, false)]
        [TestCase(VehicleClass.Pickup, 45f, 2.8f, 1.0f, 65f, 26f, false)]
        [TestCase(VehicleClass.Truck, 35f, 1.8f, 0.7f, 50f, 20f, false)]
        [TestCase(VehicleClass.Police, 55f, 3.6f, 1.3f, 78f, 32f, false)]
        public void Apply_MatchesTheSpecTable(VehicleClass c, float kmh, float accelRest, float accelMax, float yawLow, float yawHigh, bool leans)
        {
            VehicleClassPresets.Apply(t, c);
            Assert.AreEqual(kmh / BikeTuning.KmhPerMs, t.maxForwardSpeed, 1e-3f);
            Assert.AreEqual(kmh, VehicleClassPresets.MaxKmh(c), 1e-4f);
            Assert.AreEqual(accelRest, t.accelAtRest, 1e-4f);
            Assert.AreEqual(accelMax, t.accelAtMax, 1e-4f);
            Assert.AreEqual(yawLow, t.yawRateLow, 1e-4f);
            Assert.AreEqual(yawHigh, t.yawRateHigh, 1e-4f);
            Assert.AreEqual(leans, t.maxLean > 0f);
            Assert.AreEqual(!leans, t.isCar, "cars sit inside, bikes are ridden");
        }

        [Test]
        public void Truck_CameraSitsFurtherBack_ThanACar()
        {
            VehicleClassPresets.Apply(t, VehicleClass.CarSmall);
            float car = t.chaseDistance;
            VehicleClassPresets.Apply(t, VehicleClass.Truck);
            Assert.Greater(t.chaseDistance, car);
        }

        [Test]
        public void FitBody_BoxCoversTheModel()
        {
            var bounds = new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.7f, 1.6f, 4.4f));
            VehicleClassPresets.FitBody(t, bounds, VehicleClass.Mpv);
            Assert.AreEqual(0.85f, t.blockHalfExtents.x, 1e-3f);
            Assert.AreEqual(2.2f, t.blockHalfExtents.z, 1e-3f);
            Assert.LessOrEqual(t.stepOffset + 2f * t.blockHalfExtents.y, bounds.max.y + 1e-3f, "the box stays inside the model's height");
            Assert.AreEqual(0.85f, t.bodyRadius, 1e-3f);
            Assert.GreaterOrEqual(t.bodyHeight, 2f * t.bodyRadius);
        }

        [Test]
        public void FitBody_DismountClearsTheSide()
        {
            var bounds = new Bounds(new Vector3(0f, 1.2f, 0f), new Vector3(2.0f, 2.4f, 6f));
            VehicleClassPresets.FitBody(t, bounds, VehicleClass.Truck);
            const float playerRadius = 0.35f;
            Assert.GreaterOrEqual(t.dismountSideOffset, 1.0f + playerRadius + 0.1f);
        }

        [Test]
        public void FitBody_BikeKeepsANarrowCapsule()
        {
            var bounds = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 1.92f));
            VehicleClassPresets.FitBody(t, bounds, VehicleClass.MotorMatic);
            Assert.AreEqual(0.35f, t.blockHalfExtents.x, 1e-3f);
            Assert.AreEqual(0.96f, t.blockHalfExtents.z, 1e-3f);
            Assert.LessOrEqual(t.bodyRadius, 0.5f);
        }
    }
}
