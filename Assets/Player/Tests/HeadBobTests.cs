using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class HeadBobTests
    {
        [Test]
        public void FootContacts_HaveNoOffset()
        {
            Assert.AreEqual(Vector3.zero, HeadBob.Offset(0f, 0.05f, 0.5f));
            Assert.AreEqual(0f, HeadBob.Offset(0.5f, 0.05f, 0.5f).y, 1e-5f);
        }

        [Test]
        public void MidStep_PeaksAndSwaysSideToSide()
        {
            var a = HeadBob.Offset(0.25f, 0.05f, 0.5f);
            var b = HeadBob.Offset(0.75f, 0.05f, 0.5f);
            Assert.AreEqual(0.05f, a.y, 1e-5f);
            Assert.AreEqual(0.025f, a.x, 1e-5f);
            Assert.AreEqual(a.y, b.y, 1e-5f);
            Assert.AreEqual(-a.x, b.x, 1e-5f);
            Assert.AreEqual(0f, a.z);
        }
    }
}
