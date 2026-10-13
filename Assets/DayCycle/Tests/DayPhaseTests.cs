using NUnit.Framework;

namespace TycoWorld.DayCycle.Tests
{
    public class DayPhaseTests
    {
        [TestCase(4.99f, DayPhaseKind.Night)]
        [TestCase(5f, DayPhaseKind.Dawn)]
        [TestCase(6.49f, DayPhaseKind.Dawn)]
        [TestCase(6.5f, DayPhaseKind.Day)]
        [TestCase(17.49f, DayPhaseKind.Day)]
        [TestCase(17.5f, DayPhaseKind.Dusk)]
        [TestCase(18.99f, DayPhaseKind.Dusk)]
        [TestCase(19f, DayPhaseKind.Night)]
        [TestCase(0f, DayPhaseKind.Night)]
        public void PhaseAt_FollowsSpec(float hours, DayPhaseKind expected) => Assert.AreEqual(expected, DayPhase.PhaseAt(hours));

        [TestCase(18.99f, false)]
        [TestCase(19f, true)]
        [TestCase(23.9f, true)]
        [TestCase(2f, true)]
        [TestCase(4.99f, true)]
        [TestCase(5f, false)]
        [TestCase(12f, false)]
        public void HorrorAt_Is1900To0500(float hours, bool expected) => Assert.AreEqual(expected, DayPhase.HorrorAt(hours));

        static (DayPhase Phase, int[] Counts) Make(float start)
        {
            var p = new DayPhase();
            var counts = new int[2];
            p.HorrorStarted += () => counts[0]++;
            p.HorrorEnded += () => counts[1]++;
            p.Initialize(start);
            return (p, counts);
        }

        [Test]
        public void Initialize_DoesNotFireEvents()
        {
            var (p, counts) = Make(22f);
            Assert.IsTrue(p.IsHorror);
            Assert.AreEqual(DayPhaseKind.Night, p.Phase);
            CollectionAssert.AreEqual(new[] { 0, 0 }, counts);
        }

        [Test]
        public void Crossing1900_FiresStartedOnce()
        {
            var (p, counts) = Make(18.9f);
            p.Update(18.95f);
            p.Update(19.01f);
            p.Update(19.5f);
            CollectionAssert.AreEqual(new[] { 1, 0 }, counts);
        }

        [Test]
        public void Crossing0500_FiresEndedOnce()
        {
            var (p, counts) = Make(4.9f);
            p.Update(5.01f);
            p.Update(6f);
            CollectionAssert.AreEqual(new[] { 0, 1 }, counts);
        }

        [Test]
        public void LargeJump_StillFiresOnce()
        {
            var (p, counts) = Make(18f);
            p.Update(20.5f);
            CollectionAssert.AreEqual(new[] { 1, 0 }, counts);
        }

        [Test]
        public void JumpBackAndForth_FiresEachCrossing()
        {
            var (p, counts) = Make(12f);
            p.Update(19.5f);
            p.Update(6.5f);
            p.Update(19.5f);
            CollectionAssert.AreEqual(new[] { 2, 1 }, counts);
        }
    }
}
