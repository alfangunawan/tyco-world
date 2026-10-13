using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class CrouchModelTests
    {
        static CrouchModel Make() => new CrouchModel(new CrouchTuning(), 1.8f);

        [Test]
        public void StartsStanding()
        {
            var c = Make();
            Assert.IsFalse(c.Crouched);
            Assert.AreEqual(1.8f, c.Height);
            Assert.AreEqual(1.6f, c.EyeHeight);
        }

        [Test]
        public void Toggle_CrouchesThenStands()
        {
            var c = Make();
            c.RequestToggle(true);
            Assert.IsTrue(c.Crouched);
            c.RequestToggle(true);
            Assert.IsFalse(c.Crouched);
        }

        [Test]
        public void Toggle_WithoutHeadroom_StaysCrouched()
        {
            var c = Make();
            c.RequestToggle(true);
            c.RequestToggle(false);
            Assert.IsTrue(c.Crouched);
        }

        [Test]
        public void HeightAndEye_MoveTogether()
        {
            var c = Make();
            c.RequestToggle(true);
            c.Tick(0.1f);
            Assert.AreEqual(1.4f, c.Height, 1e-4f);
            Assert.AreEqual(1.225f, c.EyeHeight, 1e-4f);
            c.Tick(0.2f);
            Assert.AreEqual(1f, c.Height, 1e-4f);
            Assert.AreEqual(0.85f, c.EyeHeight, 1e-4f);
            Assert.AreEqual(1f, c.TargetHeight);
        }
    }
}
