using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsStoreTests
    {
        const string TestKey = "tyco.settings.test";

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(TestKey);

        [Test]
        public void Save_ThenLoad_RoundTrips()
        {
            var backend = new MemorySettingsBackend();
            var store = new SettingsStore(backend);
            var s = new SettingsData();
            QualityPresets.Apply(QualityPreset.High, s);
            s.fpsLimit = 0;
            s.mouseSensitivity = 1.5f;
            s.invertY = true;
            s.ambienceVolume = 40;
            s.showFps = true;
            store.Save(s);
            Assert.AreEqual(1, backend.Writes);
            var loaded = store.Load();
            Assert.AreEqual(QualityPreset.High, loaded.preset);
            Assert.AreEqual(500, loaded.viewDistance);
            Assert.AreEqual(0, loaded.fpsLimit);
            Assert.AreEqual(1.5f, loaded.mouseSensitivity);
            Assert.IsTrue(loaded.invertY);
            Assert.AreEqual(40, loaded.ambienceVolume);
            Assert.IsTrue(loaded.showFps);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_Empty_ReturnsDefaultsSilently()
        {
            var loaded = new SettingsStore(new MemorySettingsBackend()).Load();
            Assert.AreEqual(QualityPreset.Medium, loaded.preset);
            Assert.AreEqual(300, loaded.viewDistance);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_Garbage_ReturnsDefaultsWithOneWarning()
        {
            var backend = new MemorySettingsBackend { Json = "{ not json" };
            LogAssert.Expect(LogType.Warning, SettingsStore.UnreadableWarning);
            var loaded = new SettingsStore(backend).Load();
            Assert.AreEqual(300, loaded.viewDistance);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_UnknownVersion_ReturnsDefaultsWithOneWarning()
        {
            var backend = new MemorySettingsBackend { Json = "{\"version\":7,\"viewDistance\":500}" };
            LogAssert.Expect(LogType.Warning, SettingsStore.UnknownVersionWarningPrefix + "7; memakai nilai default.");
            var loaded = new SettingsStore(backend).Load();
            Assert.AreEqual(300, loaded.viewDistance);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_OutOfRange_ClampsWithOneWarning()
        {
            var backend = new MemorySettingsBackend { Json = "{\"version\":1,\"viewDistance\":5000,\"fieldOfView\":10}" };
            LogAssert.Expect(LogType.Warning, SettingsStore.ClampedWarning);
            var loaded = new SettingsStore(backend).Load();
            Assert.AreEqual(800, loaded.viewDistance);
            Assert.AreEqual(60, loaded.fieldOfView);
            Assert.AreEqual(QualityPreset.Custom, loaded.preset);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_MissingFields_KeepTheirDefaults()
        {
            var backend = new MemorySettingsBackend { Json = "{\"version\":1,\"viewDistance\":500}" };
            var loaded = new SettingsStore(backend).Load();
            Assert.AreEqual(500, loaded.viewDistance);
            Assert.AreEqual(85, loaded.renderScale);
            Assert.AreEqual(70, loaded.fieldOfView);
            Assert.AreEqual(QualityPreset.Custom, loaded.preset);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PlayerPrefsBackend_RoundTrips()
        {
            var backend = new PlayerPrefsBackend(TestKey);
            Assert.AreEqual("", backend.Read());
            backend.Write("{\"version\":1}");
            Assert.AreEqual("{\"version\":1}", backend.Read());
            Assert.AreEqual("tyco.settings", PlayerPrefsBackend.DefaultKey);
        }
    }
}
