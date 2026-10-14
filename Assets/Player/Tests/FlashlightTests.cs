using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class FlashlightTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp() => fx = new PhysicsFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        (TestRig Rig, Flashlight Light, RecordingSoundPlayer Sound) Rig()
        {
            fx.Floor();
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero);
            var go = new GameObject("Flashlight");
            go.transform.SetParent(rig.View, false);
            var flashlight = go.AddComponent<Flashlight>();
            var sound = new RecordingSoundPlayer();
            flashlight.Configure(null, fx.Track(AudioClip.Create("click", 441, 1, 44100, false)));
            flashlight.SetSoundPlayer(sound);
            rig.Init();
            return (rig, flashlight, sound);
        }

        [Test]
        public void Initialize_AppliesTuning_StartsOff()
        {
            var (_, flashlight, _) = Rig();
            var light = flashlight.GetComponent<Light>();
            Assert.AreEqual(LightType.Spot, light.type);
            Assert.AreEqual(55f, light.spotAngle, 1e-3f);
            Assert.AreEqual(30f, light.innerSpotAngle, 1e-3f);
            Assert.AreEqual(22f, light.range, 1e-3f);
            Assert.AreEqual(LightShadows.Soft, light.shadows);
            Assert.IsFalse(flashlight.IsOn);
        }

        [Test]
        public void Press_TogglesAndClicks()
        {
            var (rig, flashlight, sound) = Rig();
            rig.Intents.Press(new PlayerIntent { Flashlight = true });
            rig.Player.Tick(1f / 60f);
            Assert.IsTrue(flashlight.IsOn);
            rig.Intents.Press(new PlayerIntent { Flashlight = true });
            rig.Player.Tick(1f / 60f);
            Assert.IsFalse(flashlight.IsOn);
            Assert.AreEqual(2, sound.Calls.Count);
        }

        [Test]
        public void Lock_BlocksToggle()
        {
            var (rig, flashlight, _) = Rig();
            rig.Player.Locks.Acquire(this, PlayerAbility.Flashlight);
            rig.Intents.Press(new PlayerIntent { Flashlight = true });
            rig.Player.Tick(1f / 60f);
            Assert.IsFalse(flashlight.IsOn);
        }

        [Test]
        public void Beam_LagsThenFollowsView()
        {
            var (rig, flashlight, _) = Rig();
            rig.Intents.Press(new PlayerIntent { LookDegrees = new Vector2(90f, 0f) });
            rig.Player.Tick(1f / 60f);
            Assert.Greater(Quaternion.Angle(flashlight.transform.rotation, rig.View.rotation), 5f);
            rig.Run(0.5f);
            Assert.Less(Quaternion.Angle(flashlight.transform.rotation, rig.View.rotation), 1f);
        }

        [Test]
        public void TurnOff_SwitchesOffWithoutAClick()
        {
            var (_, flashlight, sound) = Rig();
            flashlight.Toggle();
            Assert.IsTrue(flashlight.IsOn);
            flashlight.TurnOff();
            Assert.IsFalse(flashlight.IsOn);
            Assert.AreEqual(1, sound.Calls.Count, "hanya klik saat menyalakan");
            flashlight.TurnOff();
            Assert.IsFalse(flashlight.IsOn);
        }
    }
}
