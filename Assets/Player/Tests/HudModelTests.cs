using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class HudModelTests
    {
        static HudModel Make() => new HudModel(new HudTuning());

        [Test]
        public void Defaults_HideStaminaAndShowCrosshair()
        {
            var m = Make();
            Assert.AreEqual("", m.Prompt);
            Assert.AreEqual("", m.Message);
            Assert.IsFalse(m.StaminaVisible);
            Assert.IsTrue(m.CrosshairVisible);
        }

        [Test]
        public void Prompt_SetAndClear()
        {
            var m = Make();
            m.SetPrompt("[E] Buka pagar");
            Assert.AreEqual("[E] Buka pagar", m.Prompt);
            m.SetPrompt(null);
            Assert.AreEqual("", m.Prompt);
        }

        [Test]
        public void Clock_HiddenByDefault_AndSettable()
        {
            var m = Make();
            Assert.AreEqual("", m.Clock);
            Assert.IsFalse(m.ClockVisible);
            m.SetClock("19:30", true);
            Assert.AreEqual("19:30", m.Clock);
            Assert.IsTrue(m.ClockVisible);
            m.SetClock(null, false);
            Assert.AreEqual("", m.Clock);
            Assert.IsFalse(m.ClockVisible);
        }

        [Test]
        public void Message_ExpiresAfterSeconds()
        {
            var m = Make();
            m.ShowMessage("Pintunya terkunci.", 1f);
            m.Tick(0.5f, 1f);
            Assert.AreEqual("Pintunya terkunci.", m.Message);
            m.Tick(0.6f, 1f);
            Assert.AreEqual("", m.Message);
        }

        [Test]
        public void Message_DefaultDurationComesFromTuning()
        {
            var m = Make();
            m.ShowMessage("Kos Raflesia 3");
            m.Tick(2.4f, 1f);
            Assert.AreEqual("Kos Raflesia 3", m.Message);
            m.Tick(0.2f, 1f);
            Assert.AreEqual("", m.Message);
        }

        [Test]
        public void Stamina_VisibleBelowFull_HiddenAfterDelay()
        {
            var m = Make();
            m.Tick(0.1f, 0.5f);
            Assert.IsTrue(m.StaminaVisible);
            Assert.AreEqual(0.5f, m.StaminaFill, 1e-5f);
            m.Tick(0.5f, 1f);
            Assert.IsTrue(m.StaminaVisible);
            m.Tick(0.4f, 1f);
            Assert.IsFalse(m.StaminaVisible);
        }
    }
}
