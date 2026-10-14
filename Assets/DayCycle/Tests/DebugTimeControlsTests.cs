using NUnit.Framework;

namespace TycoWorld.DayCycle.Tests
{
    public class DebugTimeControlsTests
    {
        static readonly DebugKeys T = new DebugKeys { JumpTime = true };
        static readonly DebugKeys Faster = new DebugKeys { FasterSpeed = true };
        static readonly DebugKeys Slower = new DebugKeys { SlowerSpeed = true };

        [Test]
        public void T_FromDay_JumpsTo1930()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            Assert.IsTrue(d.Apply(T, c));
            Assert.AreEqual(19.5f, c.Hours, 1e-4f);
            Assert.AreEqual("Lompat ke 19:30", d.LastMessage);
        }

        [TestCase(19.5f)]
        [TestCase(3f)]
        [TestCase(23f)]
        public void T_FromNight_JumpsTo0630(float start)
        {
            var c = new WorldClock(start);
            var d = new DebugTimeControls();
            d.Apply(T, c);
            Assert.AreEqual(6.5f, c.Hours, 1e-4f);
            Assert.AreEqual("Lompat ke 06:30", d.LastMessage);
        }

        [Test]
        public void Faster_Cycles_1_10_60_1_AndSetsClock()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            Assert.AreEqual(1f, d.Speed);
            d.Apply(Faster, c);
            Assert.AreEqual(10f, d.Speed);
            Assert.AreEqual(10f, c.SpeedMultiplier);
            Assert.AreEqual("Kecepatan waktu x10", d.LastMessage);
            d.Apply(Faster, c);
            Assert.AreEqual(60f, d.Speed);
            d.Apply(Faster, c);
            Assert.AreEqual(1f, d.Speed);
            Assert.AreEqual("Kecepatan waktu x1", d.LastMessage);
        }

        [Test]
        public void Slower_Cycles_1_60_10()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            d.Apply(Slower, c);
            Assert.AreEqual(60f, d.Speed);
            d.Apply(Slower, c);
            Assert.AreEqual(10f, d.Speed);
        }

        [Test]
        public void F3_TogglesClock()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            d.Apply(new DebugKeys { ToggleClock = true }, c);
            Assert.IsTrue(d.ClockVisible);
            d.Apply(new DebugKeys { ToggleClock = true }, c);
            Assert.IsFalse(d.ClockVisible);
        }

        [Test]
        public void NoKeys_ChangesNothing()
        {
            var c = new WorldClock(12f);
            var d = new DebugTimeControls();
            Assert.IsFalse(d.Apply(default, c));
            Assert.AreEqual(12f, c.Hours, 1e-5f);
            Assert.AreEqual("", d.LastMessage);
        }
    }
}
