using NUnit.Framework;
using TycoWorld.DayCycle;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.GameSettings.Tests
{
    public class EffectsVolumeTests
    {
        GameFixture fx;
        EffectsVolume effects;

        [SetUp]
        public void SetUp()
        {
            fx = new GameFixture();
            effects = new EffectsVolume();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        AudioSource Source(float volume)
        {
            var source = fx.Track(new GameObject("Sfx")).AddComponent<AudioSource>();
            source.volume = volume;
            return source;
        }

        [Test]
        public void Apply_ScalesFromTheOriginalVolume()
        {
            var source = Source(0.8f);
            effects.Apply(new[] { source }, 0.5f);
            Assert.AreEqual(0.4f, source.volume, 1e-5f);
            effects.Apply(new[] { source }, 0.5f);
            Assert.AreEqual(0.4f, source.volume, 1e-5f, "tidak mengecil berulang");
            effects.Apply(new[] { source }, 1f);
            Assert.AreEqual(0.8f, source.volume, 1e-5f);
        }

        [Test]
        public void Apply_SkipsAmbienceLayers()
        {
            var root = fx.Track(new GameObject("Ambience"));
            root.AddComponent<AmbienceAudio>();
            var layer = new GameObject("Ambience_Day");
            layer.transform.SetParent(root.transform, false);
            var source = layer.AddComponent<AudioSource>();
            source.volume = 0.3f;
            effects.Apply(new[] { source }, 0.5f);
            Assert.AreEqual(0.3f, source.volume, 1e-5f);
        }

        [Test]
        public void Apply_SkipsMotorbikeSources()
        {
            var bike = fx.Track(new GameObject("Motorbike"));
            bike.AddComponent<MotorbikeAudio>();
            var audio = new GameObject("Audio");
            audio.transform.SetParent(bike.transform, false);
            var source = audio.AddComponent<AudioSource>();
            source.volume = 0.7f;
            effects.Apply(new[] { source }, 0.5f);
            Assert.AreEqual(0.7f, source.volume, 1e-5f, "volume mesin diatur MotorbikeAudio.Level");
        }

        [Test]
        public void Restore_PutsOriginalsBack()
        {
            var source = Source(0.8f);
            effects.Apply(new[] { source }, 0.25f);
            effects.Restore();
            Assert.AreEqual(0.8f, source.volume, 1e-5f);
        }

        [Test]
        public void Apply_IgnoresNullAndDestroyedSources()
        {
            var source = Source(0.8f);
            Object.DestroyImmediate(source);
            Assert.DoesNotThrow(() => effects.Apply(new AudioSource[] { null, source }, 0.5f));
            Assert.DoesNotThrow(() => effects.Apply(null, 0.5f));
        }
    }
}
