using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class MotorbikeAudioTests
    {
        VehicleFixture fx;
        BikeTuning tuning;
        MotorbikeAudio audio;
        RecordingSounds sounds;
        AudioClip engine, starter, horn, bump;

        [SetUp]
        public void SetUp()
        {
            fx = new VehicleFixture();
            tuning = fx.Track(ScriptableObject.CreateInstance<BikeTuning>());
            engine = fx.Track(AudioClip.Create("engine", 4410, 1, 44100, false));
            starter = fx.Track(AudioClip.Create("starter", 441, 1, 44100, false));
            horn = fx.Track(AudioClip.Create("horn", 441, 1, 44100, false));
            bump = fx.Track(AudioClip.Create("bump", 441, 1, 44100, false));
            var go = fx.Track(new GameObject("BikeAudio"));
            audio = go.AddComponent<MotorbikeAudio>();
            audio.Configure(go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>(), engine, starter, horn, bump);
            sounds = new RecordingSounds();
            audio.OneShots = sounds;
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        [Test]
        public void EngineSound_FollowsSpeedAndThrottle()
        {
            Assert.AreEqual(0.8f, EngineSound.Pitch(0f, 0f, tuning), 1e-5f);
            Assert.AreEqual(2f, EngineSound.Pitch(1f, 0f, tuning), 1e-5f);
            Assert.AreEqual(2.1f, EngineSound.Pitch(1f, 1f, tuning), 1e-5f);
            Assert.AreEqual(2.1f, EngineSound.Pitch(3f, 5f, tuning), 1e-5f, "di-clamp");
            Assert.AreEqual(0.6f, EngineSound.Volume(0f, tuning), 1e-5f);
            Assert.AreEqual(0.9f, EngineSound.Volume(1f, tuning), 1e-5f);
        }

        [Test]
        public void StartEngine_PlaysTheStarterOnce_AndPreparesTheLoop()
        {
            audio.StartEngine();
            audio.StartEngine();
            Assert.IsTrue(audio.EngineRunning);
            Assert.AreEqual(1, sounds.Calls.Count);
            Assert.AreSame(starter, sounds.Calls[0].Clip);
            Assert.AreSame(engine, audio.EngineSource.clip);
            Assert.IsTrue(audio.EngineSource.loop);
            audio.StopEngine();
            Assert.IsFalse(audio.EngineRunning);
        }

        [Test]
        public void Tick_SetsPitchAndVolume_ScaledByLevel()
        {
            audio.Level = 0.5f;
            audio.StartEngine();
            audio.Tick(1f, 0f, 0.02f, tuning);
            Assert.AreEqual(2f, audio.EngineSource.pitch, 1e-4f);
            Assert.AreEqual(0.45f, audio.EngineSource.volume, 1e-4f);
        }

        [Test]
        public void Horn_HasACooldown()
        {
            audio.Level = 0.8f;
            Assert.IsTrue(audio.Horn(tuning));
            Assert.IsFalse(audio.Horn(tuning));
            audio.Tick(0f, 0f, 0.5f, tuning);
            Assert.IsTrue(audio.Horn(tuning));
            Assert.AreEqual(2, sounds.Calls.Count);
            Assert.AreSame(horn, sounds.Calls[0].Clip);
            Assert.AreEqual(0.8f, sounds.Calls[0].Volume, 1e-5f);
        }

        [Test]
        public void Bump_ScalesWithStrengthAndLevel()
        {
            audio.Level = 0.5f;
            audio.Bump(0.6f);
            Assert.AreSame(bump, sounds.Calls[0].Clip);
            Assert.AreEqual(0.3f, sounds.Calls[0].Volume, 1e-5f);
        }

        [Test]
        public void Level_IsClamped()
        {
            audio.Level = 3f;
            Assert.AreEqual(1f, audio.Level);
            audio.Level = -1f;
            Assert.AreEqual(0f, audio.Level);
        }
    }
}
