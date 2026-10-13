using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class ScriptedIntentSourceTests
    {
        [Test]
        public void Press_FiresEdgesOnce()
        {
            var s = new ScriptedIntentSource();
            s.Press(new PlayerIntent { Interact = true, Jump = true });
            Assert.IsFalse(s.IsDone);
            var first = s.Read(0.016f);
            Assert.IsTrue(first.Interact);
            Assert.IsTrue(first.Jump);
            var second = s.Read(0.016f);
            Assert.IsFalse(second.Interact);
            Assert.IsFalse(second.Jump);
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void Hold_ExpiresByAccumulatedTime()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { Move = Vector2.up, Sprint = true }, 0.5f);
            int moving = 0;
            for (int i = 0; i < 5; i++)
                if (s.Read(0.2f).Move == Vector2.up) moving++;
            Assert.AreEqual(3, moving);
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void Hold_LookIsDegreesPerSecond()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { LookDegrees = new Vector2(90f, -10f) }, 1f);
            var intent = s.Read(0.5f);
            Assert.AreEqual(45f, intent.LookDegrees.x, 1e-4f);
            Assert.AreEqual(-5f, intent.LookDegrees.y, 1e-4f);
        }

        [Test]
        public void Hold_IgnoresEdgeFlags()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { Move = Vector2.up, Interact = true }, 1f);
            Assert.IsFalse(s.Read(0.1f).Interact);
        }

        [Test]
        public void Segments_RunInOrder_AndEdgesMergeWithHold()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { Move = Vector2.up }, 0.1f);
            s.Hold(new PlayerIntent { Move = Vector2.right }, 0.1f);
            s.Press(new PlayerIntent { Flashlight = true, LookDegrees = new Vector2(30f, 0f) });
            var a = s.Read(0.1f);
            Assert.AreEqual(Vector2.up, a.Move);
            Assert.IsTrue(a.Flashlight);
            Assert.AreEqual(30f, a.LookDegrees.x, 1e-4f);
            Assert.AreEqual(Vector2.right, s.Read(0.1f).Move);
            Assert.AreEqual(Vector2.zero, s.Read(0.1f).Move);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var s = new ScriptedIntentSource();
            s.Hold(new PlayerIntent { Move = Vector2.up }, 5f);
            s.Press(new PlayerIntent { Jump = true });
            s.Clear();
            Assert.IsTrue(s.IsDone);
            var intent = s.Read(0.1f);
            Assert.AreEqual(Vector2.zero, intent.Move);
            Assert.IsFalse(intent.Jump);
        }
    }
}
