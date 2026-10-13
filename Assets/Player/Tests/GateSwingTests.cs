using System.Linq;
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class GateSwingTests
    {
        [Test]
        public void Opening_EmitsUnlatchAndCreak_ThenOpens()
        {
            var g = new GateSwing();
            Assert.IsTrue(g.Toggle(1f));
            Assert.AreEqual(GateSwing.State.Opening, g.Current);
            CollectionAssert.AreEqual(new[] { GateEvent.Unlatch, GateEvent.Creak }, g.Tick(0.01f).ToArray());
            g.Tick(1f);
            Assert.AreEqual(GateSwing.State.Open, g.Current);
            Assert.AreEqual(100f, g.AngleDeg, 1e-3f);
            Assert.AreEqual(0, g.Tick(0.1f).Count);
        }

        [Test]
        public void NegativeSign_SwingsTheOtherWay()
        {
            var g = new GateSwing();
            g.Toggle(-1f);
            g.Tick(1f);
            Assert.AreEqual(-100f, g.AngleDeg, 1e-3f);
        }

        [Test]
        public void Toggle_IgnoredWhileMoving()
        {
            var g = new GateSwing();
            g.Toggle(1f);
            g.Tick(0.1f);
            Assert.IsTrue(g.IsMoving);
            Assert.IsFalse(g.Toggle(1f));
        }

        [Test]
        public void Closing_EmitsCreakThenLatch()
        {
            var g = new GateSwing();
            g.Toggle(1f);
            g.Tick(1f);
            Assert.IsTrue(g.Toggle(-1f));
            Assert.AreEqual(GateSwing.State.Closing, g.Current);
            CollectionAssert.AreEqual(new[] { GateEvent.Creak, GateEvent.Latch }, g.Tick(1f).ToArray());
            Assert.AreEqual(GateSwing.State.Closed, g.Current);
            Assert.AreEqual(0f, g.AngleDeg, 1e-3f);
        }
    }
}
