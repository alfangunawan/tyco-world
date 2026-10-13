using System;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class PlayerAssetsTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp() => fx = new PhysicsFixture();

        [TearDown]
        public void TearDown() => fx.Dispose();

        GameObject Rig(out PlayerTuning tuning, out FootstepBank bank)
        {
            tuning = fx.Track(ScriptableObject.CreateInstance<PlayerTuning>());
            bank = fx.Track(ScriptableObject.CreateInstance<FootstepBank>());
            return fx.Track(PlayerRigBuilder.CreateRig(tuning, bank, null, null));
        }

        [Test]
        public void Rig_HasOneCameraAndListener_AtEyeHeight_OnPlayerLayer()
        {
            var rig = Rig(out var tuning, out var bank);
            Assert.AreEqual(1, rig.GetComponentsInChildren<Camera>(true).Length);
            Assert.AreEqual(1, rig.GetComponentsInChildren<AudioListener>(true).Length);
            Assert.AreEqual(PlayerLayers.Player, rig.layer);
            var cam = rig.GetComponentInChildren<Camera>(true);
            Assert.IsTrue(cam.CompareTag("MainCamera"));
            Assert.AreEqual(70f, cam.fieldOfView, 1e-3f);
            Assert.AreEqual(1.6f, cam.transform.parent.localPosition.y, 1e-4f);
            var cc = rig.GetComponent<CharacterController>();
            Assert.AreEqual(1.8f, cc.height, 1e-4f);
            Assert.AreEqual(0.9f, cc.center.y, 1e-4f);
            var player = rig.GetComponent<FirstPersonPlayer>();
            Assert.AreSame(tuning, player.Tuning);
            Assert.AreSame(cam.transform.parent, player.CameraPivot);
            Assert.AreSame(cam.transform, player.View);
            Assert.AreSame(bank, rig.GetComponent<FootstepAudio>().Bank);
            Assert.IsNotNull(cam.GetComponent<Interactor>());
            Assert.IsNotNull(rig.GetComponent<PlayerHud>());
            Assert.IsNotNull(rig.GetComponent<PlayerSpawner>());
            var light = rig.GetComponentInChildren<Light>(true);
            Assert.AreEqual(LightType.Spot, light.type);
            Assert.AreEqual(LightShadows.Soft, light.shadows);
            Assert.IsFalse(light.enabled);
        }

        [Test]
        public void Rig_InitializesModulesInOrder()
        {
            var rig = Rig(out _, out _);
            var player = rig.GetComponent<FirstPersonPlayer>();
            player.Initialize();
            CollectionAssert.AreEqual(new[] { 10, 20, 30, 40 }, player.Modules.Select(m => m.Order).ToArray());
        }

        [Test]
        public void Gate_HasHingedLeafTallerThanJumpReach()
        {
            var gate = fx.Track(PlaceholderPropFactory.CreateGate(null, null, null, null));
            var interactable = gate.GetComponent<GateInteractable>();
            Assert.IsNotNull(interactable);
            var hinge = gate.transform.Find("Hinge");
            Assert.IsNotNull(hinge);
            Assert.AreEqual(-PlaceholderPropFactory.GateWidth * 0.5f, hinge.localPosition.x, 1e-4f);
            var leaf = hinge.Find("Leaf").GetComponent<BoxCollider>();
            Assert.GreaterOrEqual(leaf.size.y, 1.7f);
            Assert.AreEqual(PlaceholderPropFactory.GateWidth, leaf.size.x, 1e-4f);
            Assert.Greater(leaf.center.x, 0f, "leaf extends along +x from the hinge");
            Assert.IsNotNull(gate.GetComponent<AudioSource>());
        }

        [Test]
        public void Door_And_Motor_AreInteractablesWithColliders()
        {
            var door = fx.Track(PlaceholderPropFactory.CreateDoor(null, null, null));
            Assert.IsNotNull(door.GetComponent<LockedDoorInteractable>());
            Assert.IsNotNull(door.GetComponentInChildren<BoxCollider>());
            var motor = fx.Track(PlaceholderPropFactory.CreateMotor(null, null, null));
            Assert.IsNotNull(motor.GetComponent<PlaceholderVehicleInteractable>());
            var box = motor.GetComponent<BoxCollider>();
            Assert.IsNotNull(box);
            Assert.Greater(box.size.z, box.size.x, "motor is longer than wide (local +z forward)");
        }

        [Test]
        public void Block_SpansFromTo_WithHeight()
        {
            var unit = fx.Track(PlaceholderPropFactory.UnitBoxMesh());
            var block = fx.Track(PlaceholderPropFactory.CreateBlock("Wall_Test", null, new Vector3(5000f, 0f, 5000f), new Vector3(5004f, 0f, 5000f), 1.8f, 0.15f, null, unit));
            Physics.SyncTransforms();
            var bounds = block.GetComponent<BoxCollider>().bounds;
            Assert.AreEqual(4f, bounds.size.x, 1e-3f);
            Assert.AreEqual(1.8f, bounds.size.y, 1e-3f);
            Assert.AreEqual(0f, bounds.min.y, 1e-3f);
            Assert.AreEqual(5002f, bounds.center.x, 1e-3f);
        }

        [Test]
        public void EnsureFootsteps_KeepsUserTuning()
        {
            var bank = fx.Track(ScriptableObject.CreateInstance<FootstepBank>());
            PlayerAssetsBuilder.FillFootsteps(bank);
            var grass = bank.surfaces.Single(e => e.surface == SurfaceType.Grass);
            grass.volume = 0.9f;
            Assert.IsFalse(PlayerAssetsBuilder.EnsureFootsteps(bank));
            Assert.AreEqual(0.9f, bank.surfaces.Single(e => e.surface == SurfaceType.Grass).volume);
        }

        [Test]
        public void EnsureFootsteps_FillsEmptyBank()
        {
            var bank = fx.Track(ScriptableObject.CreateInstance<FootstepBank>());
            Assert.IsTrue(PlayerAssetsBuilder.EnsureFootsteps(bank));
            Assert.AreEqual(Enum.GetValues(typeof(SurfaceType)).Length, bank.surfaces.Count);
        }

        [Test]
        public void FillFootsteps_CoversEverySurfaceWithClips()
        {
            var bank = fx.Track(ScriptableObject.CreateInstance<FootstepBank>());
            PlayerAssetsBuilder.FillFootsteps(bank);
            foreach (SurfaceType surface in Enum.GetValues(typeof(SurfaceType)))
            {
                var entry = bank.surfaces.Single(s => s.surface == surface);
                Assert.Greater(entry.clips.Length, 0, surface.ToString());
                Assert.IsTrue(entry.clips.All(c => c != null), surface.ToString());
            }
        }
    }
}
