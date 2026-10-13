using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class StepCycleTests
    {
        [Test]
        public void Walking_GivesOneStepPerStride()
        {
            var c = new StepCycle(3f);
            int steps = 0;
            for (int i = 0; i < 76; i++) steps += c.Advance(3f, true, 0.75f, 1f / 30f);
            Assert.AreEqual(10, steps);
            Assert.AreEqual(10, c.StepCount);
        }

        [Test]
        public void IdleOrAirborne_GivesNoSteps()
        {
            var c = new StepCycle(3f);
            Assert.AreEqual(0, c.Advance(0f, true, 0.75f, 1f));
            Assert.AreEqual(0, c.Advance(5f, false, 0.75f, 1f));
            Assert.AreEqual(0, c.StepCount);
        }

        [Test]
        public void Phase_WrapsEveryTwoStrides()
        {
            var c = new StepCycle(3f);
            c.Advance(1f, true, 1f, 0.5f);
            Assert.AreEqual(0.25f, c.CyclePhase01, 1e-4f);
            c.Advance(1f, true, 1f, 2f);
            Assert.AreEqual(0.25f, c.CyclePhase01, 1e-4f);
        }

        [Test]
        public void Landing_ReportedOnceWithImpactSpeed()
        {
            var c = new StepCycle(3f);
            Assert.IsFalse(c.UpdateLanding(false, -2f, out _));
            Assert.IsFalse(c.UpdateLanding(false, -5f, out _));
            Assert.IsTrue(c.UpdateLanding(true, -2f, out float impact));
            Assert.AreEqual(5f, impact, 1e-5f);
            Assert.IsFalse(c.UpdateLanding(true, -2f, out _));
        }

        [Test]
        public void Landing_SmallDropIgnored()
        {
            var c = new StepCycle(3f);
            c.UpdateLanding(false, -2.2f, out _);
            Assert.IsFalse(c.UpdateLanding(true, -2f, out _));
        }
    }
}
