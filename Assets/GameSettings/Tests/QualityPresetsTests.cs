using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class QualityPresetsTests
    {
        [Test]
        public void Defaults_AreTheMediumPreset()
        {
            var s = new SettingsData();
            Assert.AreEqual(QualityPreset.Medium, s.preset);
            Assert.AreEqual(QualityPreset.Medium, QualityPresets.Detect(s));
            Assert.AreEqual(SettingsData.CurrentVersion, s.version);
            Assert.AreEqual(60, s.fpsLimit);
            Assert.IsFalse(s.vSync);
        }

        [TestCase(QualityPreset.Low, 200, 70, ShadowQuality.Low, false, 8, 2f, 1)]
        [TestCase(QualityPreset.Medium, 300, 85, ShadowQuality.Medium, false, 16, 1f, 1)]
        [TestCase(QualityPreset.High, 500, 100, ShadowQuality.High, true, 32, 0f, 2)]
        public void Apply_FillsTheSpecTable(QualityPreset preset, int view, int scale, ShadowQuality shadows, bool ssao, int lamps,
            float smallMesh, int msaa)
        {
            var s = new SettingsData();
            QualityPresets.Apply(preset, s);
            Assert.AreEqual(preset, s.preset);
            Assert.AreEqual(view, s.viewDistance);
            Assert.AreEqual(scale, s.renderScale);
            Assert.AreEqual(shadows, s.shadows);
            Assert.AreEqual(ssao, s.ssao);
            Assert.IsFalse(s.gpuOcclusion);
            Assert.AreEqual(lamps, s.lampLights);
            Assert.AreEqual(smallMesh, s.smallMeshCulling, 1e-4f);
            Assert.AreEqual(msaa, s.msaa);
        }

        [Test]
        public void Detect_ChangedGraphicsValue_IsCustom()
        {
            var s = new SettingsData { viewDistance = 450 };
            Assert.AreEqual(QualityPreset.Custom, QualityPresets.Detect(s));
        }

        [Test]
        public void Detect_IgnoresEverythingOutsideThePreset()
        {
            var s = new SettingsData();
            QualityPresets.Apply(QualityPreset.High, s);
            s.fpsLimit = 0;
            s.vSync = true;
            s.fullscreen = false;
            s.resolutionWidth = 1280;
            s.resolutionHeight = 720;
            s.mouseSensitivity = 2f;
            s.fieldOfView = 90;
            s.masterVolume = 10;
            s.showFps = true;
            Assert.AreEqual(QualityPreset.High, QualityPresets.Detect(s));
        }

        [Test]
        public void Apply_Custom_OnlyMarksThePreset()
        {
            var s = new SettingsData { viewDistance = 450 };
            QualityPresets.Apply(QualityPreset.Custom, s);
            Assert.AreEqual(450, s.viewDistance);
            Assert.AreEqual(QualityPreset.Custom, s.preset);
        }

        [Test]
        public void ShadowTiers_MatchTheSpec()
        {
            var off = ShadowTiers.For(ShadowQuality.Off);
            Assert.IsFalse(off.Directional);
            Assert.Greater(off.Distance, 0f, "senter tetap butuh jarak bayangan");
            AssertTier(ShadowTiers.For(ShadowQuality.Low), 30f, 1, 1024, false);
            AssertTier(ShadowTiers.For(ShadowQuality.Medium), 50f, 2, 2048, true);
            AssertTier(ShadowTiers.For(ShadowQuality.High), 80f, 4, 2048, true);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = new SettingsData();
            var b = a.Clone();
            b.viewDistance = 800;
            Assert.AreEqual(300, a.viewDistance);
        }

        static void AssertTier(ShadowTier tier, float distance, int cascades, int resolution, bool soft)
        {
            Assert.IsTrue(tier.Directional);
            Assert.AreEqual(distance, tier.Distance);
            Assert.AreEqual(cascades, tier.Cascades);
            Assert.AreEqual(resolution, tier.Resolution);
            Assert.AreEqual(soft, tier.Soft);
        }
    }
}
