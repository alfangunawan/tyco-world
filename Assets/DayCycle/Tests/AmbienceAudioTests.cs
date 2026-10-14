using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class AmbienceAudioTests
    {
        GameObject go;
        DayCycleProfile profile;
        AmbienceAudio ambience;

        [SetUp]
        public void SetUp()
        {
            profile = DayCycleProfile.CreateDefault();
            profile.dayClip = AudioClip.Create("day", 2205, 1, 22050, false);
            profile.nightClip = AudioClip.Create("night", 2205, 1, 22050, false);
            profile.horrorDroneClip = AudioClip.Create("drone", 2205, 1, 22050, false);
            go = new GameObject("Ambience");
            ambience = go.AddComponent<AmbienceAudio>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(profile.dayClip);
            Object.DestroyImmediate(profile.nightClip);
            Object.DestroyImmediate(profile.horrorDroneClip);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Initialize_CreatesThreeLooping2DSources()
        {
            ambience.Initialize(profile);
            foreach (var s in new[] { ambience.Day, ambience.Night, ambience.Drone })
            {
                Assert.IsNotNull(s);
                Assert.IsTrue(s.loop);
                Assert.IsFalse(s.playOnAwake);
                Assert.AreEqual(0f, s.spatialBlend);
                Assert.IsTrue(s.transform.IsChildOf(go.transform));
            }
            Assert.AreSame(profile.dayClip, ambience.Day.clip);
            Assert.AreSame(profile.nightClip, ambience.Night.clip);
            Assert.AreSame(profile.horrorDroneClip, ambience.Drone.clip);
        }

        [Test]
        public void Initialize_Twice_ReusesSources()
        {
            ambience.Initialize(profile);
            ambience.Initialize(profile);
            Assert.AreEqual(3, go.GetComponentsInChildren<AudioSource>(true).Length);
        }

        [Test]
        public void Volumes_FollowTheClock()
        {
            ambience.Initialize(profile);
            var noon = profile.Evaluate(12f);
            ambience.Apply(noon.DayAmbience, noon.NightAmbience, noon.HorrorDrone);
            Assert.AreEqual(0.35f, ambience.Day.volume, 1e-4f);
            Assert.AreEqual(0f, ambience.Night.volume, 1e-4f);
            Assert.AreEqual(0f, ambience.Drone.volume, 1e-4f);
            var night = profile.Evaluate(22f);
            ambience.Apply(night.DayAmbience, night.NightAmbience, night.HorrorDrone);
            Assert.AreEqual(0f, ambience.Day.volume, 1e-4f);
            Assert.AreEqual(0.4f, ambience.Night.volume, 1e-4f);
            Assert.AreEqual(0.3f, ambience.Drone.volume, 1e-4f);
            var dusk = profile.Evaluate(18f);
            ambience.Apply(dusk.DayAmbience, dusk.NightAmbience, dusk.HorrorDrone);
            Assert.AreEqual(0.175f, ambience.Day.volume, 1e-4f);
            Assert.AreEqual(0.2f, ambience.Night.volume, 1e-4f);
        }

        [Test]
        public void MissingClips_DoNotThrow()
        {
            var empty = DayCycleProfile.CreateDefault();
            try
            {
                ambience.Initialize(empty);
                Assert.IsNull(ambience.Day.clip);
                Assert.DoesNotThrow(() => ambience.Apply(1f, 1f, 1f));
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void Apply_BeforeInitialize_DoesNothing() => Assert.DoesNotThrow(() => ambience.Apply(1f, 1f, 1f));
    }
}
