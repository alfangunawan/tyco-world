using NUnit.Framework;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using TycoWorld.Vehicles;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsApplierTests
    {
        GameFixture fx;
        FirstPersonPlayer player;
        DayCycleDirector director;
        SettingsApplier applier;
        DistanceCuller culler;
        MemorySettingsBackend backend;
        RenderPipelineAsset pipelineBefore;
        int frameRateBefore, vSyncBefore;
        float volumeBefore;

        [SetUp]
        public void SetUp()
        {
            pipelineBefore = QualitySettings.renderPipeline;
            frameRateBefore = Application.targetFrameRate;
            vSyncBefore = QualitySettings.vSyncCount;
            volumeBefore = AudioListener.volume;
            fx = new GameFixture();
            player = fx.CreatePlayer();
            director = fx.CreateDirector(player);
            var systems = fx.Track(new GameObject("GameSystems"));
            culler = systems.AddComponent<DistanceCuller>();
            culler.FindWorldRoot = () => null;
            culler.FindViewer = () => null;
            applier = systems.AddComponent<SettingsApplier>();
            backend = new MemorySettingsBackend();
            applier.Backend = backend;
            applier.FindPlayer = () => player;
            applier.FindDirector = () => director;
        }

        [TearDown]
        public void TearDown()
        {
            applier.Shutdown();
            fx.Dispose();
            if (QualitySettings.renderPipeline != pipelineBefore) QualitySettings.renderPipeline = pipelineBefore;
            if (QualitySettings.vSyncCount != vSyncBefore) QualitySettings.vSyncCount = vSyncBefore;
            Application.targetFrameRate = frameRateBefore;
            AudioListener.volume = volumeBefore;
        }

        Camera Cam => player.View.GetComponent<Camera>();

        [Test]
        public void Initialize_LoadsTheStoredSettings()
        {
            backend.Json = "{\"version\":1,\"viewDistance\":500}";
            applier.Initialize();
            Assert.AreEqual(500, applier.Current.viewDistance);
        }

        [Test]
        public void Apply_DrivesCameraPlayerCullerAndDirector()
        {
            applier.Initialize();
            applier.Apply(new SettingsData
            {
                viewDistance = 300, fieldOfView = 80, mouseSensitivity = 1.5f, invertY = true, lampLights = 8, ambienceVolume = 50,
                showClock = true
            });
            Assert.AreEqual(315f, Cam.farClipPlane, 1e-3f);
            Assert.AreEqual(80f, Cam.fieldOfView, 1e-3f);
            Assert.AreEqual(1.5f, player.LookSensitivity, 1e-5f);
            Assert.IsTrue(player.InvertY);
            Assert.AreEqual(300f, culler.ViewDistance);
            Assert.AreEqual(180f, culler.PropDistance, 1e-3f);
            Assert.AreEqual(2.3f / 300f, director.MinFogDensity, 1e-6f);
            Assert.AreEqual(8, director.Lamps.LightBudget);
            Assert.AreEqual(180f, director.Lamps.MaxLightDistance, 1e-3f);
            Assert.AreEqual(0.5f, director.Ambience.Level, 1e-5f);
            Assert.IsTrue(director.ShowClock);
        }

        [Test]
        public void Apply_ShadowQualityReachesSunAndMoon()
        {
            applier.Initialize();
            applier.Apply(new SettingsData { shadows = ShadowQuality.Low });
            Assert.AreEqual(LightShadows.Hard, director.Sun.shadows);
            Assert.AreEqual(LightShadows.Hard, director.Moon.shadows);
        }

        [Test]
        public void Apply_SetsMasterAndEffectsVolumes_LeavesAmbience()
        {
            applier.Initialize();
            var source = fx.Track(new GameObject("Sfx")).AddComponent<AudioSource>();
            source.volume = 0.8f;
            float dayBefore = director.Ambience.Day.volume;
            applier.Apply(new SettingsData { masterVolume = 40, effectsVolume = 50 });
            Assert.AreEqual(0.4f, AudioListener.volume, 1e-5f);
            Assert.AreEqual(0.4f, source.volume, 1e-5f);
            applier.Apply(new SettingsData { masterVolume = 40, effectsVolume = 50 });
            Assert.AreEqual(0.4f, source.volume, 1e-5f, "tidak mengecil berulang");
            Assert.AreEqual(dayBefore, director.Ambience.Day.volume, 1e-5f, "ambience diatur lewat Level, bukan Efek");
        }

        [Test]
        public void Apply_SetsTheMotorbikeLevel_FromEffects()
        {
            applier.Initialize();
            var bike = fx.Track(new GameObject("Motorbike")).AddComponent<MotorbikeAudio>();
            applier.Apply(new SettingsData { effectsVolume = 40 });
            Assert.AreEqual(0.4f, bike.Level, 1e-5f);
            applier.Apply(new SettingsData { effectsVolume = 100 });
            Assert.AreEqual(1f, bike.Level, 1e-5f);
        }

        [Test]
        public void Apply_SanitizesInput()
        {
            applier.Initialize();
            applier.Apply(new SettingsData { viewDistance = 5000 });
            Assert.AreEqual(800, applier.Current.viewDistance);
            Assert.AreEqual(840f, Cam.farClipPlane, 1e-3f);
        }

        [Test]
        public void Apply_KeepsItsOwnCopy()
        {
            applier.Initialize();
            var settings = new SettingsData { viewDistance = 450 };
            applier.Apply(settings);
            settings.viewDistance = 100;
            Assert.AreEqual(450, applier.Current.viewDistance);
        }

        [Test]
        public void Save_WritesTheBackendOnlyWhenAsked()
        {
            applier.Initialize();
            applier.Apply(new SettingsData { viewDistance = 450 });
            Assert.AreEqual(0, backend.Writes);
            applier.Save();
            Assert.AreEqual(1, backend.Writes);
            StringAssert.Contains("\"viewDistance\":450", backend.Json);
        }

        [Test]
        public void ApplyAll_RaisesApplied()
        {
            int calls = 0;
            applier.Applied += _ => calls++;
            applier.Initialize();
            applier.ApplyAll();
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Shutdown_RestoresTheRenderPipeline()
        {
            applier.Initialize();
            Assert.AreNotSame(pipelineBefore, QualitySettings.renderPipeline);
            applier.Shutdown();
            Assert.AreSame(pipelineBefore, QualitySettings.renderPipeline);
            Assert.IsFalse(applier.IsInitialized);
        }

        [Test]
        public void Shutdown_RestoresTheMasterVolume()
        {
            AudioListener.volume = 0.7f;
            applier.Initialize();
            applier.Apply(new SettingsData { masterVolume = 40 });
            Assert.AreEqual(0.4f, AudioListener.volume, 1e-5f);
            applier.Shutdown();
            Assert.AreEqual(0.7f, AudioListener.volume, 1e-5f);
        }

        [Test]
        public void WithoutPlayerOrDirector_NoErrors()
        {
            applier.FindPlayer = () => null;
            applier.FindDirector = () => null;
            applier.Initialize();
            Assert.DoesNotThrow(() => applier.Apply(new SettingsData()));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
