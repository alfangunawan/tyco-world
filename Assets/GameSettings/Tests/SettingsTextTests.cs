using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsTextTests
    {
        [Test]
        public void Formats_Values()
        {
            Assert.AreEqual("300 m", SettingsText.Meters(300));
            Assert.AreEqual("85 %", SettingsText.Percent(85));
            Assert.AreEqual("1,5×", SettingsText.Multiplier(1.5f));
            Assert.AreEqual("1,0×", SettingsText.Multiplier(1f));
            Assert.AreEqual("70°", SettingsText.Degrees(70));
            Assert.AreEqual("16", SettingsText.LampLights(16));
            Assert.AreEqual("60 FPS", SettingsText.FpsLimit(60));
            Assert.AreEqual("Tanpa batas", SettingsText.FpsLimit(0));
            Assert.AreEqual("1920 × 1080", SettingsText.Resolution(new Vector2Int(1920, 1080)));
            Assert.AreEqual("Saat ini", SettingsText.Resolution(Vector2Int.zero));
            Assert.AreEqual("60 FPS", SettingsText.Fps(59.6f));
        }

        [Test]
        public void Names_AreIndonesian()
        {
            Assert.AreEqual("Rendah", SettingsText.PresetName(QualityPreset.Low));
            Assert.AreEqual("Sedang", SettingsText.PresetName(QualityPreset.Medium));
            Assert.AreEqual("Tinggi", SettingsText.PresetName(QualityPreset.High));
            Assert.AreEqual("Kustom", SettingsText.PresetName(QualityPreset.Custom));
            Assert.AreEqual("Mati", SettingsText.ShadowName(ShadowQuality.Off));
            Assert.AreEqual("Tinggi", SettingsText.ShadowName(ShadowQuality.High));
            Assert.AreEqual("Layar penuh", SettingsText.ScreenMode(true));
            Assert.AreEqual("Jendela", SettingsText.ScreenMode(false));
        }

        [Test]
        public void Choices_FollowTheValueOrder()
        {
            CollectionAssert.AreEqual(new[] { "Rendah", "Sedang", "Tinggi", "Kustom" }, SettingsText.PresetChoices());
            CollectionAssert.AreEqual(new[] { "Mati", "Rendah", "Sedang", "Tinggi" }, SettingsText.ShadowChoices());
            CollectionAssert.AreEqual(new[] { "8", "16", "32" }, SettingsText.LampLightChoices());
            CollectionAssert.AreEqual(new[] { "30 FPS", "60 FPS", "120 FPS", "Tanpa batas" }, SettingsText.FpsLimitChoices());
            CollectionAssert.AreEqual(new[] { "Layar penuh", "Jendela" }, SettingsText.ScreenModeChoices());
        }

        [Test]
        public void MenuTexts_MatchTheSpec()
        {
            Assert.AreEqual("Jeda", SettingsText.PauseTitle);
            Assert.AreEqual("Lanjutkan", SettingsText.Resume);
            Assert.AreEqual("Pengaturan", SettingsText.OpenSettings);
            Assert.AreEqual("Keluar", SettingsText.Quit);
            Assert.AreEqual("Keluar dari game?", SettingsText.QuitQuestion);
            Assert.AreEqual("Kembalikan default", SettingsText.ResetDefaults);
        }
    }
}
