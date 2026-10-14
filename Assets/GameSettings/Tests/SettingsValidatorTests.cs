using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsValidatorTests
    {
        [Test]
        public void Defaults_AreValid()
        {
            var s = new SettingsData();
            Assert.IsFalse(SettingsValidator.Sanitize(s));
            Assert.AreEqual(QualityPreset.Medium, s.preset);
        }

        [TestCase(5000, 800)]
        [TestCase(10, 100)]
        [TestCase(333, 350)]
        [TestCase(450, 450)]
        public void ViewDistance_IsClampedAndSnappedTo50(int input, int expected)
        {
            var s = new SettingsData { viewDistance = input };
            Assert.AreEqual(input != expected, SettingsValidator.Sanitize(s));
            Assert.AreEqual(expected, s.viewDistance);
        }

        [Test]
        public void OutOfRangeValues_AreClamped()
        {
            var s = new SettingsData
            {
                renderScale = 42, lampLights = 20, fpsLimit = 45, msaa = 8, smallMeshCulling = 50f,
                mouseSensitivity = 9f, fieldOfView = 10, masterVolume = -5, effectsVolume = 300, ambienceVolume = 101
            };
            Assert.IsTrue(SettingsValidator.Sanitize(s));
            Assert.AreEqual(50, s.renderScale);
            Assert.AreEqual(16, s.lampLights);
            Assert.AreEqual(60, s.fpsLimit);
            Assert.AreEqual(1, s.msaa);
            Assert.AreEqual(20f, s.smallMeshCulling);
            Assert.AreEqual(3f, s.mouseSensitivity);
            Assert.AreEqual(60, s.fieldOfView);
            Assert.AreEqual(0, s.masterVolume);
            Assert.AreEqual(100, s.effectsVolume);
            Assert.AreEqual(100, s.ambienceVolume);
        }

        [TestCase(int.MinValue + 8, 8)]
        [TestCase(int.MaxValue, 32)]
        public void LampLights_ExtremeValues_SnapWithoutOverflow(int input, int expected)
        {
            var s = new SettingsData { lampLights = input };
            Assert.DoesNotThrow(() => SettingsValidator.Sanitize(s));
            Assert.AreEqual(expected, s.lampLights);
        }

        [Test]
        public void NaNSensitivity_FallsBackToDefault()
        {
            var s = new SettingsData { mouseSensitivity = float.NaN };
            Assert.IsTrue(SettingsValidator.Sanitize(s));
            Assert.AreEqual(1f, s.mouseSensitivity);
        }

        [Test]
        public void UnknownShadowQuality_BecomesMedium()
        {
            var s = new SettingsData { shadows = (ShadowQuality)9 };
            Assert.IsTrue(SettingsValidator.Sanitize(s));
            Assert.AreEqual(ShadowQuality.Medium, s.shadows);
        }

        [TestCase(-1, 600)]
        [TestCase(1280, 0)]
        public void HalfSetResolution_MeansKeepCurrent(int w, int h)
        {
            var s = new SettingsData { resolutionWidth = w, resolutionHeight = h };
            Assert.IsTrue(SettingsValidator.Sanitize(s));
            Assert.AreEqual(0, s.resolutionWidth);
            Assert.AreEqual(0, s.resolutionHeight);
        }

        [Test]
        public void FullResolution_IsKept()
        {
            var s = new SettingsData { resolutionWidth = 1920, resolutionHeight = 1080 };
            Assert.IsFalse(SettingsValidator.Sanitize(s));
            Assert.AreEqual(1920, s.resolutionWidth);
        }

        [Test]
        public void MismatchedPreset_IsFixedSilently()
        {
            var s = new SettingsData { preset = QualityPreset.High };
            Assert.IsFalse(SettingsValidator.Sanitize(s));
            Assert.AreEqual(QualityPreset.Medium, s.preset);
        }

        [Test]
        public void Choices_MatchTheSpec()
        {
            CollectionAssert.AreEqual(new[] { 8, 16, 32 }, SettingsValidator.LampLightChoices());
            CollectionAssert.AreEqual(new[] { 30, 60, 120, 0 }, SettingsValidator.FpsLimitChoices());
        }
    }
}
