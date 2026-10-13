using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class SpawnNudgeTests
    {
        [Test]
        public void Candidates_StartAtOrigin_AndGrowOutward()
        {
            var origin = new Vector3(3f, 1f, -2f);
            var list = SpawnNudge.Candidates(origin);
            Assert.AreEqual(33, list.Count);
            Assert.AreEqual(origin, list[0]);
            float previous = 0f;
            foreach (var c in list)
            {
                float d = Vector3.Distance(origin, c);
                Assert.GreaterOrEqual(d + 1e-4f, previous);
                Assert.LessOrEqual(d, 2f + 1e-4f);
                Assert.AreEqual(origin.y, c.y);
                previous = d;
            }
        }
    }
}
