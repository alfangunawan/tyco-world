using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class RingAssemblerTests
    {
        static List<IReadOnlyList<long>> Ways(params long[][] ways) => new List<IReadOnlyList<long>>(ways);

        [Test]
        public void SingleClosedWay_IsOneRing()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(new long[] { 1, 2, 3, 1 }), out var rings));
            Assert.AreEqual(1, rings.Count);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 1 }, rings[0]);
        }

        [Test]
        public void TwoWays_JoinWithReversal()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(new long[] { 1, 2, 3 }, new long[] { 1, 4, 3 }), out var rings));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 1 }, rings[0]);
        }

        [Test]
        public void ThreeWayChain_Closes()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(new long[] { 1, 2 }, new long[] { 2, 3 }, new long[] { 3, 1 }), out var rings));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 1 }, rings[0]);
        }

        [Test]
        public void OpenChain_Fails()
        {
            Assert.IsFalse(RingAssembler.TryAssemble(Ways(new long[] { 1, 2 }, new long[] { 2, 3 }), out _));
        }

        [Test]
        public void TwoSeparateRings()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(new long[] { 1, 2, 3, 1 }, new long[] { 4, 5, 6, 4 }), out var rings));
            Assert.AreEqual(2, rings.Count);
        }

        [Test]
        public void Empty_Succeeds()
        {
            Assert.IsTrue(RingAssembler.TryAssemble(Ways(), out var rings));
            Assert.AreEqual(0, rings.Count);
        }
    }
}
