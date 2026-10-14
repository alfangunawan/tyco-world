using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleProfileTests
    {
        DayCycleProfile p;

        [SetUp]
        public void SetUp() => p = DayCycleProfile.CreateDefault();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(p);

        [Test]
        public void Defaults_MatchSpec()
        {
            Assert.AreEqual(5.75f, p.sunrise);
            Assert.AreEqual(17.75f, p.sunset);
            Assert.AreEqual(75f, p.sunMaxElevation);
            Assert.AreEqual(1.3f, p.sunIntensity);
            Assert.AreEqual(0.15f, p.moonIntensity);
            Assert.AreEqual(1.3f, p.skyExposureDay);
            Assert.AreEqual(0.8f, p.skyExposureDusk);
            Assert.AreEqual(0.08f, p.skyExposureNight);
            Assert.AreEqual(0.004f, p.fogDensityDay);
            Assert.AreEqual(0.008f, p.fogDensityDusk);
            Assert.AreEqual(0.02f, p.fogDensityNight);
            Assert.AreEqual(1.6f, p.horrorFogMultiplier);
            Assert.AreEqual(0.5f, p.horrorFadeHours);
            Assert.AreEqual(0.08f, p.flickerFraction);
            Assert.AreEqual(0.03f, p.deadFraction);
            Assert.AreEqual(5f, p.lampOnBelowElevation);
            Assert.AreEqual(3f, p.lampIntensity);
        }

        [Test]
        public void SunElevation_AtKeyTimes()
        {
            Assert.That(p.SunElevation(6f), Is.InRange(0f, 20f));
            Assert.AreEqual(75f, p.SunElevation(11.75f), 0.5f);
            Assert.Less(p.SunElevation(18.5f), 0f);
            Assert.Less(p.SunElevation(3f), -30f);
        }

        [Test]
        public void SunYaw_EastAtSunrise_WestAtSunset()
        {
            Assert.AreEqual(270f, p.SunYaw(6f), 15f);
            Assert.AreEqual(90f, p.SunYaw(17.5f), 15f);
        }

        [Test]
        public void SunAtNoon_MoonAtNight()
        {
            var noon = p.Evaluate(12f);
            Assert.IsTrue(noon.SunEnabled);
            Assert.IsFalse(noon.MoonEnabled);
            Assert.Greater(noon.SunIntensity, 1.2f);
            var night = p.Evaluate(22f);
            Assert.IsFalse(night.SunEnabled);
            Assert.IsTrue(night.MoonEnabled);
            Assert.AreEqual(0.15f, night.MoonIntensity, 1e-4f);
        }

        [Test]
        public void Fog_DayDuskNightAndHorror()
        {
            Assert.AreEqual(0.004f, p.Evaluate(12f).FogDensity, 1e-4f);
            Assert.AreEqual(0.032f, p.Evaluate(22f).FogDensity, 1e-4f);
            Assert.That(p.Evaluate(18.25f).FogDensity, Is.InRange(0.004f, 0.032f));
        }

        [Test]
        public void SkyExposure_DayAndNight()
        {
            Assert.AreEqual(1.3f, p.Evaluate(12f).SkyExposure, 1e-3f);
            Assert.AreEqual(0.08f, p.Evaluate(22f).SkyExposure, 1e-3f);
        }

        [Test]
        public void DayVolumeWeight_FullDayZeroNight()
        {
            Assert.AreEqual(1f, p.Evaluate(12f).DayVolumeWeight, 1e-4f);
            Assert.AreEqual(0f, p.Evaluate(22f).DayVolumeWeight, 1e-4f);
        }

        [TestCase(18.99f, 0f)]
        [TestCase(19.25f, 0.5f)]
        [TestCase(22f, 1f)]
        [TestCase(4.75f, 0.5f)]
        [TestCase(5f, 0f)]
        [TestCase(12f, 0f)]
        public void HorrorWeight_RampsOverHalfAnHour(float hours, float expected) =>
            Assert.AreEqual(expected, p.HorrorWeight(hours), 1e-3f);

        [Test]
        public void Lamps_OffByDay_OnAtNight_RisingAtDusk()
        {
            Assert.AreEqual(0f, p.Evaluate(12f).LampIntensity01, 1e-4f);
            Assert.AreEqual(0f, p.Evaluate(17f).LampIntensity01, 1e-4f);
            Assert.AreEqual(1f, p.Evaluate(22f).LampIntensity01, 1e-4f);
            float previous = 0f;
            for (float h = 17.5f; h <= 18.5f; h += 0.05f)
            {
                float v = p.Evaluate(h).LampIntensity01;
                Assert.GreaterOrEqual(v + 1e-5f, previous, h.ToString());
                previous = v;
            }
            Assert.AreEqual(1f, previous, 1e-4f);
        }

        [Test]
        public void Ambience_CrossfadesAtDuskAndDawn()
        {
            var noon = p.Evaluate(12f);
            Assert.AreEqual(1f, noon.DayAmbience, 1e-4f);
            Assert.AreEqual(0f, noon.NightAmbience, 1e-4f);
            var night = p.Evaluate(22f);
            Assert.AreEqual(0f, night.DayAmbience, 1e-4f);
            Assert.AreEqual(1f, night.NightAmbience, 1e-4f);
            Assert.AreEqual(1f, night.HorrorDrone, 1e-4f);
            Assert.AreEqual(0.5f, p.Evaluate(18f).DayAmbience, 1e-4f);
            Assert.AreEqual(0.5f, p.Evaluate(5.5f).DayAmbience, 1e-4f);
        }
    }
}
