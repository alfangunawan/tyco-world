using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class LookModelTests
    {
        [Test]
        public void Pitch_ClampsAtLimit()
        {
            Assert.AreEqual(85f, LookModel.Apply(Vector2.zero, new Vector2(0f, 200f), 85f).y);
            Assert.AreEqual(-85f, LookModel.Apply(Vector2.zero, new Vector2(0f, -200f), 85f).y);
        }

        [Test]
        public void Yaw_WrapsIntoZeroTo360()
        {
            Assert.AreEqual(10f, LookModel.Apply(new Vector2(350f, 0f), new Vector2(20f, 0f), 85f).x, 1e-4f);
            Assert.AreEqual(350f, LookModel.Apply(new Vector2(10f, 0f), new Vector2(-20f, 0f), 85f).x, 1e-4f);
        }

        [Test]
        public void PitchRotation_PositiveLooksUp()
        {
            Assert.Greater((LookModel.PitchRotation(30f) * Vector3.forward).y, 0.4f);
            Assert.Less((LookModel.PitchRotation(-30f) * Vector3.forward).y, -0.4f);
        }
    }
}
