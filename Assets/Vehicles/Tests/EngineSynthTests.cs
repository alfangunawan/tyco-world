using System;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;

namespace TycoWorld.Vehicles.Tests
{
    public class EngineSynthTests
    {
        static float Rms(float[] s, int from, int to)
        {
            double sum = 0;
            for (int i = from; i < to; i++) sum += s[i] * s[i];
            return (float)Math.Sqrt(sum / Math.Max(1, to - from));
        }

        static float Peak(float[] s)
        {
            float max = 0f;
            foreach (var v in s) max = Math.Max(max, Math.Abs(v));
            return max;
        }

        [Test]
        public void EngineLoop_IsDeterministicSeamlessAndCentred()
        {
            var a = EngineSynth.EngineLoop();
            var b = EngineSynth.EngineLoop();
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(EngineSynth.SampleRate, a.Length);
            Assert.Less(Math.Abs(a[0] - a[a.Length - 1]), 0.2f, "sambungan loop halus");
            Assert.That(Peak(a), Is.InRange(0.5f, 0.8001f));
            double mean = 0;
            foreach (var v in a) mean += v;
            Assert.Less(Math.Abs(mean / a.Length), 0.02, "tanpa DC offset");
        }

        [Test]
        public void Starter_RampsUpAndFadesOut()
        {
            var s = EngineSynth.Starter();
            Assert.AreEqual((int)(0.9f * EngineSynth.SampleRate), s.Length);
            Assert.Less(Rms(s, 0, s.Length / 20), Rms(s, s.Length / 2, s.Length * 3 / 4));
            Assert.Less(Math.Abs(s[s.Length - 1]), 0.05f);
        }

        [Test]
        public void Horn_HasTwoBeepsWithASilentGap()
        {
            var s = EngineSynth.Horn();
            int beep = (int)(0.22f * EngineSynth.SampleRate);
            int gap = (int)(0.08f * EngineSynth.SampleRate);
            Assert.Greater(Rms(s, beep / 4, beep * 3 / 4), 0.1f);
            Assert.Less(Rms(s, beep + gap / 4, beep + gap * 3 / 4), 0.01f);
            Assert.Greater(Rms(s, beep + gap + beep / 4, beep + gap + beep * 3 / 4), 0.1f);
        }

        [Test]
        public void Bump_DecaysQuickly()
        {
            var s = EngineSynth.Bump();
            int tenth = s.Length / 10;
            Assert.Less(Rms(s, s.Length - tenth, s.Length), Rms(s, 0, tenth) * 0.2f);
        }
    }
}
