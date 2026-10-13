using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class StaminaModelTests
    {
        [Test]
        public void StartsFull() => Assert.AreEqual(1f, new StaminaModel(new StaminaTuning()).Normalized);

        [Test]
        public void Drains_OnlyWhileSprintingAndMoving()
        {
            var s = new StaminaModel(new StaminaTuning());
            s.Tick(false, 1f);
            Assert.AreEqual(1f, s.Normalized, 1e-5f);
            s.Tick(true, 1f);
            Assert.AreEqual(0.82f, s.Normalized, 1e-4f);
        }

        [Test]
        public void Regen_WaitsForDelay()
        {
            var s = new StaminaModel(new StaminaTuning());
            s.Tick(true, 1f);
            s.Tick(false, 0.5f);
            Assert.AreEqual(0.82f, s.Normalized, 1e-4f);
            s.Tick(false, 0.5f);
            Assert.AreEqual(0.89f, s.Normalized, 1e-4f);
        }

        [Test]
        public void Exhausted_LocksSprintUntilThreshold()
        {
            var s = new StaminaModel(new StaminaTuning());
            s.Tick(true, 6f);
            Assert.AreEqual(0f, s.Normalized, 1e-5f);
            Assert.IsTrue(s.Exhausted);
            Assert.IsFalse(s.CanSprint);
            s.Tick(true, 1f);
            s.Tick(true, 1f);
            Assert.AreEqual(0.28f, s.Normalized, 1e-4f);
            Assert.IsTrue(s.Exhausted);
            s.Tick(true, 0.2f);
            Assert.IsFalse(s.Exhausted);
            Assert.IsTrue(s.CanSprint);
        }

        [Test]
        public void TrySpend_TakesCost_AndRefusesWhenExhaustedOrShort()
        {
            var s = new StaminaModel(new StaminaTuning());
            Assert.IsTrue(s.TrySpend(8f));
            Assert.AreEqual(0.92f, s.Normalized, 1e-4f);
            Assert.IsFalse(s.TrySpend(500f));
            s.Tick(true, 6f);
            Assert.IsFalse(s.TrySpend(1f));
        }

        [Test]
        public void Refill_RestoresFullAndClearsExhaustion()
        {
            var s = new StaminaModel(new StaminaTuning());
            s.Tick(true, 6f);
            s.Refill();
            Assert.AreEqual(1f, s.Normalized);
            Assert.IsFalse(s.Exhausted);
        }
    }
}
