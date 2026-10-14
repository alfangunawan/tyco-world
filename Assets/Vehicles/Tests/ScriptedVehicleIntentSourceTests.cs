using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class ScriptedVehicleIntentSourceTests
    {
        [Test]
        public void Hold_RepeatsForItsDuration_ThenIdles()
        {
            var source = new ScriptedVehicleIntentSource();
            source.Hold(new VehicleIntent { Throttle = 1f, Steer = -0.5f, Brake = true, LookDegrees = new Vector2(10f, 0f) }, 0.5f);
            var first = source.Read(0.25f);
            Assert.AreEqual(1f, first.Throttle);
            Assert.AreEqual(-0.5f, first.Steer);
            Assert.IsTrue(first.Brake);
            Assert.AreEqual(2.5f, first.LookDegrees.x, 1e-5f, "derajat per detik × dt");
            source.Read(0.25f);
            Assert.IsTrue(source.IsDone);
            Assert.AreEqual(0f, source.Read(0.25f).Throttle);
        }

        [Test]
        public void Press_FiresEdgesOnce()
        {
            var source = new ScriptedVehicleIntentSource();
            source.Press(new VehicleIntent { Horn = true, ToggleHeadlight = true, ToggleCamera = true, Exit = true, LookDegrees = new Vector2(0f, 5f) });
            var once = source.Read(0.02f);
            Assert.IsTrue(once.Horn && once.ToggleHeadlight && once.ToggleCamera && once.Exit);
            Assert.AreEqual(5f, once.LookDegrees.y, 1e-5f);
            var next = source.Read(0.02f);
            Assert.IsFalse(next.Horn || next.ToggleHeadlight || next.ToggleCamera || next.Exit);
            Assert.IsTrue(source.IsDone);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var source = new ScriptedVehicleIntentSource();
            source.Hold(new VehicleIntent { Throttle = 1f }, 5f);
            source.Press(new VehicleIntent { Exit = true });
            source.Clear();
            Assert.IsTrue(source.IsDone);
            Assert.IsFalse(source.Read(0.1f).Exit);
        }
    }
}
