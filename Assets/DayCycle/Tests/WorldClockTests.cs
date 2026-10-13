using NUnit.Framework;

namespace TycoWorld.DayCycle.Tests
{
    public class WorldClockTests
    {
        [Test]
        public void StartsAtGivenTime_OnDayOne()
        {
            var c = new WorldClock(6.5f);
            Assert.AreEqual(6.5f, c.Hours, 1e-5f);
            Assert.AreEqual(1, c.Day);
            Assert.AreEqual("06:30", c.Display);
        }

        [Test]
        public void OneRealMinute_IsOneGameHour()
        {
            var c = new WorldClock(6.5f);
            c.Tick(60f);
            Assert.AreEqual(7.5f, c.Hours, 1e-4f);
        }

        [Test]
        public void Speed_MultipliesRate()
        {
            var c = new WorldClock(6.5f) { SpeedMultiplier = 60f };
            c.Tick(1f);
            Assert.AreEqual(7.5f, c.Hours, 1e-4f);
        }

        [Test]
        public void PassingMidnight_IncrementsDay()
        {
            var c = new WorldClock(23.5f);
            c.Tick(60f);
            Assert.AreEqual(0.5f, c.Hours, 1e-4f);
            Assert.AreEqual(2, c.Day);
        }

        [Test]
        public void SetTime_WrapsIntoRange_AndKeepsDay()
        {
            var c = new WorldClock(6.5f);
            c.SetTime(-1f);
            Assert.AreEqual(23f, c.Hours, 1e-4f);
            c.SetTime(25.5f);
            Assert.AreEqual(1.5f, c.Hours, 1e-4f);
            Assert.AreEqual(1, c.Day);
        }

        [Test]
        public void Display_IsPadded()
        {
            var c = new WorldClock(19.5f);
            Assert.AreEqual("19:30", c.Display);
            c.SetTime(0.25f);
            Assert.AreEqual("00:15", c.Display);
        }

        [Test]
        public void ZeroOrNegativeDt_DoesNothing()
        {
            var c = new WorldClock(6.5f);
            c.Tick(0f);
            c.Tick(-5f);
            Assert.AreEqual(6.5f, c.Hours, 1e-5f);
        }
    }
}
