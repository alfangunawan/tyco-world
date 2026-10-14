using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleDirectorTests
    {
        DirectorFixture fx;

        [SetUp]
        public void SetUp() => fx = new DirectorFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        static void AreClose(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f);
            Assert.AreEqual(expected.g, actual.g, 1e-3f);
            Assert.AreEqual(expected.b, actual.b, 1e-3f);
        }

        [Test]
        public void Messages_AreExact()
        {
            Assert.AreEqual("Malam semakin larut\u2026", DayCycleDirector.HorrorStartMessage);
            Assert.AreEqual("Subuh. Malam sudah lewat.", DayCycleDirector.HorrorEndMessage);
        }

        [Test]
        public void Initialize_StartsAt0630_WithoutEvents()
        {
            fx.Director.Initialize();
            Assert.AreEqual("06:30", fx.Director.Clock.Display);
            Assert.AreEqual(1, fx.Director.Clock.Day);
            Assert.AreEqual(DayPhaseKind.Day, fx.Director.Phase);
            Assert.IsFalse(fx.Director.IsHorror);
            Assert.AreEqual(0, fx.Started + fx.Ended);
            Assert.IsTrue(fx.Sun.enabled);
            Assert.AreSame(fx.Sun, RenderSettings.sun);
        }

        [Test]
        public void Noon_AppliesDaylight()
        {
            fx.Director.Initialize(12f);
            Assert.IsTrue(fx.Sun.enabled);
            Assert.IsFalse(fx.Moon.enabled);
            Assert.AreEqual(fx.Profile.sunIntensity, fx.Sun.intensity, 1e-3f);
            Assert.AreSame(fx.Sun, RenderSettings.sun);
            Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
            AreClose(fx.Profile.ambientSkyDay, RenderSettings.ambientSkyColor);
            AreClose(fx.Profile.ambientGroundDay, RenderSettings.ambientGroundColor);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(FogMode.Exponential, RenderSettings.fogMode);
            Assert.AreEqual(0.004f, RenderSettings.fogDensity, 1e-4f);
            Assert.AreEqual(1f, fx.Day.weight, 1e-4f);
            Assert.AreEqual(1f, fx.Night.weight, 1e-4f);
            Assert.AreEqual(0f, fx.Horror.weight, 1e-4f);
        }

        [Test]
        public void Night_AppliesMoonFogAndHorrorVolume()
        {
            fx.Director.Initialize(22f);
            Assert.IsFalse(fx.Sun.enabled);
            Assert.IsTrue(fx.Moon.enabled);
            Assert.AreEqual(0.15f, fx.Moon.intensity, 1e-4f);
            Assert.AreSame(fx.Moon, RenderSettings.sun);
            AreClose(fx.Profile.ambientSkyNight, RenderSettings.ambientSkyColor);
            Assert.AreEqual(0.032f, RenderSettings.fogDensity, 1e-4f);
            Assert.AreEqual(0f, fx.Day.weight, 1e-4f);
            Assert.AreEqual(1f, fx.Night.weight, 1e-4f);
            Assert.AreEqual(1f, fx.Horror.weight, 1e-4f);
            Assert.IsTrue(fx.Director.IsHorror);
            Assert.AreEqual(0, fx.Started);
        }

        [Test]
        public void Tick_LongFrameAcross1900_FiresOnce()
        {
            fx.Director.Initialize(18.9f);
            fx.Director.Tick(60f);
            Assert.IsTrue(fx.Director.IsHorror);
            Assert.AreEqual(1, fx.Started);
            fx.Director.Tick(1f);
            fx.Director.Tick(60f);
            Assert.AreEqual(1, fx.Started);
            Assert.AreEqual(0, fx.Ended);
        }

        [Test]
        public void Tick_Across0500_FiresEndedOnce()
        {
            fx.Director.Initialize(4.5f);
            fx.Director.Tick(60f);
            Assert.IsFalse(fx.Director.IsHorror);
            Assert.AreEqual(DayPhaseKind.Dawn, fx.Director.Phase);
            Assert.AreEqual(1, fx.Ended);
            Assert.AreEqual(0, fx.Started);
        }

        [Test]
        public void HorrorMessages_GoToPlayerHud()
        {
            var player = fx.AddPlayer();
            fx.Director.Initialize(18.9f);
            fx.Director.Tick(60f);
            Assert.AreEqual(DayCycleDirector.HorrorStartMessage, player.Hud.Message);
            fx.Director.Clock.SetTime(4.9f);
            fx.Director.Tick(12f);
            Assert.AreEqual(DayCycleDirector.HorrorEndMessage, player.Hud.Message);
        }

        [Test]
        public void ShowClock_WritesClockToHud()
        {
            var player = fx.AddPlayer();
            fx.Director.Initialize(12f);
            fx.Director.Tick(0.01f);
            Assert.IsFalse(player.Hud.ClockVisible);
            fx.Director.ShowClock = true;
            fx.Director.Tick(0.01f);
            Assert.IsTrue(player.Hud.ClockVisible);
            Assert.AreEqual(fx.Director.Clock.Display, player.Hud.Clock);
        }

        [Test]
        public void WithoutPlayer_RunsWithoutErrors()
        {
            fx.Director.Configure(fx.Profile, null, null, null, null, null, null, null, null);
            fx.Director.Initialize(18.9f);
            Assert.DoesNotThrow(() => fx.Director.Tick(60f));
            Assert.IsTrue(fx.Director.IsHorror);
            Assert.DoesNotThrow(() => fx.Director.ShowMessage("x"));
        }

        [Test]
        public void MissingProfile_UsesDefault_WithOneWarning()
        {
            fx.Director.Configure(null, fx.Sun, fx.Moon, fx.Day, fx.Night, fx.Horror, fx.Sky, fx.Ambience, null);
            LogAssert.Expect(LogType.Warning, DayCycleDirector.MissingProfileWarning);
            fx.Director.Initialize(12f);
            Assert.IsNotNull(fx.Director.Profile);
            Assert.IsTrue(fx.Sun.enabled);
        }

        [Test]
        public void Skybox_UsesRuntimeCopy()
        {
            fx.Director.Initialize(12f);
            var copy = fx.Director.SkyboxInstance;
            Assert.IsNotNull(copy);
            Assert.AreNotSame(fx.Sky, copy);
            Assert.AreSame(copy, RenderSettings.skybox);
            Assert.AreEqual(1.3f, copy.GetFloat("_Exposure"), 1e-3f);
            Assert.AreEqual(9f, fx.Sky.GetFloat("_Exposure"), 1e-5f);
            fx.Director.Shutdown();
            Assert.AreSame(fx.Sky, RenderSettings.skybox);
        }

        [TestCase(17.6f, 18.4f)]
        [TestCase(5.2f, 6.0f)]
        public void SkyLight_DirectionIsContinuous_AcrossTheHorizon(float from, float to)
        {
            fx.Moon.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            fx.Director.Initialize(from);
            var previous = RenderSettings.sun.transform.forward;
            while (fx.Director.Clock.Hours < to)
            {
                fx.Director.Tick(0.6f);
                var now = RenderSettings.sun.transform.forward;
                Assert.Less(Vector3.Angle(previous, now), 5f, fx.Director.Clock.Display);
                previous = now;
            }
        }

        [Test]
        public void DeepNight_MoonKeepsItsSceneRotation()
        {
            var pose = Quaternion.Euler(50f, -30f, 0f);
            fx.Moon.transform.rotation = pose;
            fx.Director.Initialize(22f);
            Assert.Less(Quaternion.Angle(pose, fx.Moon.transform.rotation), 0.01f);
        }

        [Test]
        public void HorrorStarted_SeesLightingAlreadyApplied()
        {
            float weightAtEvent = -1f, fogAtEvent = -1f;
            fx.Director.HorrorStarted += () =>
            {
                weightAtEvent = fx.Director.State.HorrorWeight;
                fogAtEvent = RenderSettings.fogDensity;
            };
            fx.Director.Initialize(18.9f);
            fx.Director.Tick(60f);
            Assert.AreEqual(1f, weightAtEvent, 1e-3f);
            Assert.AreEqual(0.032f, fogAtEvent, 1e-4f);
        }

        [Test]
        public void Lamps_AreCollectedOnTheFrameTheWorldAppears()
        {
            var rootGo = fx.Track(new GameObject("World_Generated_Test"));
            var root = rootGo.AddComponent<GeneratedRoot>();
            LampFixture.Create(rootGo.transform, LampFixture.PositionWithRole(LampRole.Normal, fx.Profile));
            bool worldLoaded = false;
            fx.Director.FindWorldRoot = () => worldLoaded ? root : null;
            fx.Director.Initialize(22f);
            fx.Director.Tick(0.016f);
            worldLoaded = true;
            fx.Director.Tick(0.016f);
            Assert.AreEqual(1, fx.Director.Lamps.Count);
        }

        [Test]
        public void NoStreetLamps_WarnsOnce()
        {
            var root = fx.Track(new GameObject("World_Generated_Test")).AddComponent<GeneratedRoot>();
            fx.Director.FindWorldRoot = () => root;
            fx.Director.Initialize(22f);
            LogAssert.Expect(LogType.Warning, DayCycleDirector.NoLampsWarning);
            for (int i = 0; i < 5; i++) fx.Director.Tick(1f);
            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(fx.Director.Lamps.Collected);
            Assert.AreEqual(0, fx.Director.Lamps.Count);
        }

        [Test]
        public void StreetLamps_UnderWorldRoot_AreLitAtNightOnly()
        {
            var rootGo = fx.Track(new GameObject("World_Generated_Test"));
            var root = rootGo.AddComponent<GeneratedRoot>();
            var lamp = LampFixture.Create(rootGo.transform, LampFixture.PositionWithRole(LampRole.Normal, fx.Profile));
            fx.Director.FindWorldRoot = () => root;
            fx.Director.Initialize(22f);
            fx.Director.Tick(0.1f);
            Assert.AreEqual(1, fx.Director.Lamps.Count);
            Assert.IsTrue(lamp.Light.enabled);
            Assert.AreEqual(fx.Profile.lampIntensity, lamp.Light.intensity, 1e-4f);
            fx.Director.Clock.SetTime(12f);
            fx.Director.Tick(0.1f);
            Assert.IsFalse(lamp.Light.enabled);
        }
    }
}
