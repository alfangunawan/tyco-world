using System;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;

namespace TycoWorld.DayCycle.Tests
{
    public class AmbienceSynthTests
    {
        static float[] Make(string kind, float seconds) =>
            kind == "day" ? AmbienceSynth.Day(seconds) : kind == "night" ? AmbienceSynth.Night(seconds) : AmbienceSynth.Drone(seconds);

        [TestCase("day")]
        [TestCase("night")]
        [TestCase("drone")]
        public void IsDeterministic(string kind) => CollectionAssert.AreEqual(Make(kind, 3f), Make(kind, 3f));

        [TestCase("day")]
        [TestCase("night")]
        [TestCase("drone")]
        public void HasRequestedLength_AndHealthyLevel(string kind)
        {
            var s = Make(kind, 3f);
            Assert.AreEqual(3 * AmbienceSynth.SampleRate, s.Length);
            float peak = 0f;
            double sum = 0;
            foreach (var v in s)
            {
                peak = Math.Max(peak, Math.Abs(v));
                sum += v * v;
            }
            Assert.AreEqual(0.8f, peak, 1e-4f);
            Assert.Greater(Math.Sqrt(sum / s.Length), 0.02);
        }

        [TestCase("day")]
        [TestCase("night")]
        [TestCase("drone")]
        public void LoopSeam_IsNoBiggerThanAnInnerStep(string kind)
        {
            var s = Make(kind, 3f);
            float inner = 0f;
            for (int i = 1; i < s.Length; i++) inner = Math.Max(inner, Math.Abs(s[i] - s[i - 1]));
            Assert.LessOrEqual(Math.Abs(s[0] - s[s.Length - 1]), inner + 1e-4f);
        }

        [Test]
        public void Loop_CrossfadesTailIntoHead()
        {
            var loop = AmbienceSynth.Loop(new[] { 0f, 0f, 0f, 0f, 1f, 1f }, 2);
            CollectionAssert.AreEqual(new[] { 1f, 0.5f, 0f, 0f }, loop);
        }
    }
}
