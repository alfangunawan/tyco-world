using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class FrameStatsTests
    {
        // Powers of two keep the float sums exact.
        [Test]
        public void Sample_ReportsAverageWhenTheWindowFills()
        {
            var stats = new FrameStats(0.5f);
            for (int i = 0; i < 31; i++) Assert.IsFalse(stats.Sample(1f / 64f));
            Assert.IsTrue(stats.Sample(1f / 64f));
            Assert.IsTrue(stats.HasResult);
            Assert.AreEqual(64f, stats.AverageFps, 1e-3f);
            Assert.AreEqual(15.625f, stats.AverageMs, 1e-3f);
        }

        [Test]
        public void Sample_TracksTheWorstFrame()
        {
            var stats = new FrameStats(0.5f);
            for (int i = 0; i < 6; i++) stats.Sample(0.0625f);
            Assert.IsTrue(stats.Sample(0.125f));
            Assert.AreEqual(125f, stats.WorstMs, 1e-3f);
            Assert.AreEqual(14f, stats.AverageFps, 1e-3f);
        }

        [Test]
        public void Sample_IgnoresZeroAndNegativeFrames()
        {
            var stats = new FrameStats(0.5f);
            Assert.IsFalse(stats.Sample(0f));
            Assert.IsFalse(stats.Sample(-1f));
            Assert.IsFalse(stats.HasResult);
        }

        [Test]
        public void Clear_ForgetsTheResult()
        {
            var stats = new FrameStats(0.25f);
            stats.Sample(0.25f);
            stats.Clear();
            Assert.IsFalse(stats.HasResult);
            Assert.AreEqual(0f, stats.AverageFps);
        }
    }
}
