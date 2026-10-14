using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class ViewDistanceRulesTests
    {
        [TestCase(100f, 105f)]
        [TestCase(300f, 315f)]
        [TestCase(800f, 840f)]
        public void FarClip_Is105Percent(float view, float expected) => Assert.AreEqual(expected, ViewDistanceRules.FarClip(view), 1e-3f);

        [TestCase(300f, 0.0076667f)]
        [TestCase(500f, 0.0046f)]
        [TestCase(800f, 0.002875f)]
        public void MinFogDensity_Is2Point3OverDistance(float view, float expected) =>
            Assert.AreEqual(expected, ViewDistanceRules.MinFogDensity(view), 1e-6f);

        [TestCase(100f, 80f)]
        [TestCase(300f, 180f)]
        [TestCase(800f, 480f)]
        public void PropDistance_Is60PercentWith80Minimum(float view, float expected) =>
            Assert.AreEqual(expected, ViewDistanceRules.PropDistance(view), 1e-3f);

        [Test]
        public void SliderRange_MatchesTheSpec()
        {
            Assert.AreEqual(100, ViewDistanceRules.Min);
            Assert.AreEqual(800, ViewDistanceRules.Max);
            Assert.AreEqual(50, ViewDistanceRules.Step);
        }
    }
}
