using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class SplitMix64Tests
    {
        [Test]
        public void SeedZero_MatchesReferenceSequence()
        {
            var rng = new SplitMix64(0);
            Assert.AreEqual(0xE220A8397B1DCDAFUL, rng.NextULong());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, rng.NextULong());
            Assert.AreEqual(0x06C45D188009454FUL, rng.NextULong());
        }

        [Test]
        public void Fnv1a64_MatchesReference()
        {
            Assert.AreEqual(0xCBF29CE484222325UL, SplitMix64.Fnv1a64(""));
            Assert.AreEqual(0xAF63DC4C8601EC8CUL, SplitMix64.Fnv1a64("a"));
        }

        [Test]
        public void For_IsDeterministicAndSaltSensitive()
        {
            Assert.AreEqual(SplitMix64.Hash(401, "levels"), SplitMix64.Hash(401, "levels"));
            Assert.AreNotEqual(SplitMix64.Hash(401, "levels"), SplitMix64.Hash(401, "upper"));
        }

        [Test]
        public void Range_StaysInBounds()
        {
            var rng = new SplitMix64(42);
            for (int i = 0; i < 1000; i++)
            {
                float v = rng.Range(0.8f, 1.2f);
                Assert.GreaterOrEqual(v, 0.8f);
                Assert.Less(v, 1.2f);
            }
        }

        [Test]
        public void RangeInclusive_HitsBothEnds()
        {
            var rng = new SplitMix64(7);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.RangeInclusive(1, 3);
                Assert.That(v, Is.InRange(1, 3));
                sawMin |= v == 1;
                sawMax |= v == 3;
            }
            Assert.IsTrue(sawMin && sawMax);
        }

        [Test]
        public void Pick_HandlesEmptyAndRange()
        {
            Assert.AreEqual(-1, SplitMix64.Pick(5, "x", 0));
            Assert.That(SplitMix64.Pick(5, "x", 6), Is.InRange(0, 5));
        }
    }
}
