using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class FootstepAudioTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            GroundProbe.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            GroundProbe.ClearCache();
        }

        AudioClip[] Clips(string prefix, int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++) clips[i] = fx.Track(AudioClip.Create(prefix + i, 441, 1, 44100, false));
            return clips;
        }

        FootstepBank Bank(out SurfaceClips grass, out SurfaceClips fallback)
        {
            var bank = fx.Track(ScriptableObject.CreateInstance<FootstepBank>());
            grass = new SurfaceClips { surface = SurfaceType.Grass, clips = Clips("grass", 3), volume = 0.5f, pitchMin = 0.9f, pitchMax = 1.1f };
            fallback = new SurfaceClips { surface = SurfaceType.Default, clips = Clips("default", 2), volume = 0.4f };
            bank.surfaces = new List<SurfaceClips> { fallback, grass };
            return bank;
        }

        (TestRig Rig, FootstepAudio Audio, RecordingSoundPlayer Sound, SurfaceClips Grass) GrassRig(Vector3 localFeet)
        {
            fx.Floor().AddComponent<GroundSurface>().Initialize(SurfaceType.Grass);
            fx.Sync();
            var rig = TestRig.Create(fx, localFeet);
            var audio = rig.Root.AddComponent<FootstepAudio>();
            audio.Configure(Bank(out var grass, out _), null);
            var sound = new RecordingSoundPlayer();
            audio.SetSoundPlayer(sound);
            rig.Init();
            return (rig, audio, sound, grass);
        }

        [Test]
        public void Bank_ResolvesSurface_AndFallsBackToDefault()
        {
            var bank = Bank(out var grass, out var fallback);
            Assert.AreSame(grass, bank.Resolve(SurfaceType.Grass));
            Assert.AreSame(fallback, bank.Resolve(SurfaceType.Asphalt));
            grass.clips = new AudioClip[0];
            Assert.AreSame(fallback, bank.Resolve(SurfaceType.Grass));
        }

        [Test]
        public void Walking_PlaysOneClipPerStep_FromProbedSurface()
        {
            var (rig, audio, sound, grass) = GrassRig(Vector3.zero);
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up }, 3f);
            rig.Run(3f);
            Assert.Greater(rig.Player.StepCount, 5);
            Assert.AreEqual(rig.Player.StepCount, sound.Calls.Count);
            Assert.AreEqual(sound.Calls.Count, audio.PlayedCount);
            Assert.AreEqual(SurfaceType.Grass, audio.LastSurface);
            foreach (var call in sound.Calls)
            {
                CollectionAssert.Contains(grass.clips, call.Clip);
                Assert.That(call.Pitch, Is.InRange(0.9f, 1.1f));
                Assert.AreEqual(0.5f, call.Volume, 1e-5f);
            }
            for (int i = 1; i < sound.Calls.Count; i++) Assert.AreNotSame(sound.Calls[i - 1].Clip, sound.Calls[i].Clip);
        }

        [Test]
        public void Landing_PlaysLouder()
        {
            var (rig, _, sound, _) = GrassRig(new Vector3(0f, 2f, 0f));
            rig.Run(1f);
            Assert.IsTrue(sound.Calls.Any(c => Mathf.Abs(c.Volume - 0.5f * FootstepAudio.LandingVolumeScale) < 1e-4f));
        }

        [Test]
        public void Crouched_StepsAreQuieter()
        {
            var (rig, _, sound, _) = GrassRig(Vector3.zero);
            rig.Intents.Press(new PlayerIntent { CrouchToggle = true });
            rig.Run(0.5f);
            sound.Calls.Clear();
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up }, 3f);
            rig.Run(3f);
            Assert.Greater(sound.Calls.Count, 0);
            foreach (var call in sound.Calls) Assert.AreEqual(0.5f * FootstepAudio.CrouchVolumeScale, call.Volume, 1e-5f);
        }

        [Test]
        public void WithoutBank_DoesNothing()
        {
            fx.Floor();
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero);
            var audio = rig.Root.AddComponent<FootstepAudio>();
            rig.Init();
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up }, 1f);
            Assert.DoesNotThrow(() => rig.Run(1f));
            Assert.AreEqual(0, audio.PlayedCount);
        }
    }
}
