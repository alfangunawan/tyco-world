using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class InteractionTests
    {
        PhysicsFixture fx;
        static Ray Eye => new Ray(PhysicsFixture.Origin + Vector3.up * 1.6f, Vector3.forward);

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            Interactable.ResetStatics();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            Interactable.ResetStatics();
        }

        AudioClip Clip(string name) => fx.Track(AudioClip.Create(name, 441, 1, 44100, false));

        LockedDoorInteractable Door(float z, out RecordingSoundPlayer sound, out AudioClip rattle)
        {
            var go = fx.Box("Door", new Vector3(0f, 1.6f, z), new Vector3(1f, 2f, 0.1f));
            var door = go.AddComponent<LockedDoorInteractable>();
            rattle = Clip("rattle");
            door.Configure(rattle);
            sound = new RecordingSoundPlayer();
            door.SetSoundPlayer(sound);
            fx.Sync();
            return door;
        }

        InteractionContext Context(IPlayerFeedback feedback) => new InteractionContext(null, feedback, PhysicsFixture.Origin + Vector3.up * 1.6f);

        [Test]
        public void FindFocus_WithinRangeOnly()
        {
            var near = Door(2f, out _, out _);
            Assert.AreSame(near, Interactor.FindFocus(Eye, 2.2f, 0.12f));
            Object.DestroyImmediate(near.gameObject);
            Door(2.5f, out _, out _);
            Assert.IsNull(Interactor.FindFocus(Eye, 2.2f, 0.12f));
        }

        [Test]
        public void FindFocus_BlockedByWall()
        {
            Door(2f, out _, out _);
            fx.Box("Wall", new Vector3(0f, 1.6f, 1f), new Vector3(3f, 3f, 0.1f));
            fx.Sync();
            Assert.IsNull(Interactor.FindFocus(Eye, 2.2f, 0.12f));
        }

        [Test]
        public void FindFocus_ChildColliderResolvesParent()
        {
            var parent = fx.Track(new GameObject("Motor"));
            var vehicle = parent.AddComponent<PlaceholderVehicleInteractable>();
            var child = fx.Box("Body", new Vector3(0f, 1.6f, 1.5f), new Vector3(0.6f, 1f, 1.8f));
            child.transform.SetParent(parent.transform, true);
            fx.Sync();
            Assert.AreSame(vehicle, Interactor.FindFocus(Eye, 2.2f, 0.12f));
        }

        [Test]
        public void FindFocus_SphereCatchesNearMiss()
        {
            var go = fx.Box("Handle", new Vector3(0.15f, 1.6f, 1.5f), new Vector3(0.1f, 0.1f, 0.1f));
            var door = go.AddComponent<LockedDoorInteractable>();
            fx.Sync();
            Assert.AreSame(door, Interactor.FindFocus(Eye, 2.2f, 0.12f));
        }

        [Test]
        public void LockedDoor_ShowsMessageAndRattles()
        {
            var door = Door(2f, out var sound, out var rattle);
            var hud = new HudModel(new HudTuning());
            var ctx = Context(hud);
            Assert.AreEqual("[E] Buka pintu", door.GetPrompt(ctx));
            Assert.IsTrue(door.Interact(ctx));
            Assert.AreEqual("Pintunya terkunci.", hud.Message);
            Assert.AreEqual(1, sound.Calls.Count);
            Assert.AreSame(rattle, sound.Calls[0].Clip);
        }

        [Test]
        public void Vehicle_PromptMessageAndKeys()
        {
            var go = fx.Box("Motor", new Vector3(0f, 0.5f, 2f), new Vector3(0.7f, 1f, 1.8f));
            var vehicle = go.AddComponent<PlaceholderVehicleInteractable>();
            var keys = Clip("keys");
            vehicle.Configure(keys);
            var sound = new RecordingSoundPlayer();
            vehicle.SetSoundPlayer(sound);
            var hud = new HudModel(new HudTuning());
            var ctx = Context(hud);
            Assert.AreEqual("[E] Naik motor", vehicle.GetPrompt(ctx));
            vehicle.Interact(ctx);
            Assert.AreEqual("Belum bisa naik motor.", hud.Message);
            Assert.AreSame(keys, sound.Calls[0].Clip);
        }

        [Test]
        public void Gate_TogglesSwingsAwayAndPlaysSounds()
        {
            var root = fx.Track(new GameObject("Gate"));
            root.transform.position = PhysicsFixture.Origin + new Vector3(0f, 0f, 2f);
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(root.transform, false);
            var gate = root.AddComponent<GateInteractable>();
            AudioClip unlatch = Clip("unlatch"), creak = Clip("creak"), latch = Clip("latch");
            gate.Configure(leaf, unlatch, creak, latch);
            var sound = new RecordingSoundPlayer();
            gate.SetSoundPlayer(sound);
            var ctx = Context(new HudModel(new HudTuning()));

            Assert.AreEqual("[E] Buka pagar", gate.GetPrompt(ctx));
            Assert.IsTrue(gate.Interact(ctx));
            gate.Tick(0.05f);
            Assert.AreSame(unlatch, sound.Calls[0].Clip);
            Assert.AreSame(creak, sound.Calls[1].Clip);
            Assert.IsFalse(gate.Interact(ctx), "ignored while swinging");
            gate.Tick(1f);
            Assert.AreEqual(GateSwing.State.Open, gate.State);
            Assert.AreEqual(-100f, gate.AngleDeg, 1e-3f, "viewer at -z → leaf swings toward +z");
            Assert.AreEqual(260f, leaf.localEulerAngles.y, 0.01f);
            Assert.AreEqual("[E] Tutup pagar", gate.GetPrompt(ctx));

            Assert.IsTrue(gate.Interact(ctx));
            gate.Tick(1f);
            Assert.AreEqual(GateSwing.State.Closed, gate.State);
            Assert.AreSame(latch, sound.Calls[sound.Calls.Count - 1].Clip);
        }

        GateInteractable OpenGate(out Transform root)
        {
            var go = fx.Track(new GameObject("Gate"));
            go.transform.position = PhysicsFixture.Origin + new Vector3(0f, 0f, 2f);
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(go.transform, false);
            var gate = go.AddComponent<GateInteractable>();
            gate.Configure(leaf, null, null, null);
            gate.Interact(Context(null));
            gate.Tick(1f);
            root = go.transform;
            return gate;
        }

        [Test]
        public void Gate_RefusesToCloseWhilePlayerStandsInItsArc()
        {
            var gate = OpenGate(out var root);
            Assert.AreEqual(GateSwing.State.Open, gate.State);
            var inArc = new InteractionContext(null, null, root.TransformPoint(new Vector3(0.8f, 1.6f, 0.8f)));
            Assert.IsFalse(gate.CanInteract(inArc));
            Assert.IsFalse(gate.Interact(inArc));
            Assert.AreEqual(GateSwing.State.Open, gate.State);
            var clear = new InteractionContext(null, null, root.TransformPoint(new Vector3(0.8f, 1.6f, -1.5f)));
            Assert.IsTrue(gate.Interact(clear));
            Assert.AreEqual(GateSwing.State.Closing, gate.State);
        }

        [TestCase(0.5f, 0.6f, true)]
        [TestCase(1.9f, 0.2f, true)]
        [TestCase(1.0f, -0.6f, false)]
        [TestCase(3.0f, 3.0f, false)]
        public void Gate_IsInSweep(float x, float z, bool expected) =>
            Assert.AreEqual(expected, GateInteractable.IsInSweep(new Vector2(x, z), -100f, 0f, 2f, GateInteractable.SweepClearance));

        [Test]
        public void Interacted_EventCarriesId_AndResetClearsSubscribers()
        {
            var door = Door(2f, out _, out _);
            door.SetInteractionId("kos-door");
            Interactable received = null;
            Interactable.Interacted += i => received = i;
            door.Interact(Context(null));
            Assert.AreSame(door, received);
            Assert.AreEqual("kos-door", received.InteractionId);

            received = null;
            Interactable.ResetStatics();
            door.Interact(Context(null));
            Assert.IsNull(received);
        }

        [Test]
        public void Interactor_SetsPrompt_InteractsOnPress_AndRespectsLock()
        {
            fx.Floor();
            Door(1.5f, out _, out _);
            var rig = TestRig.Create(fx, Vector3.zero);
            var interactor = rig.View.gameObject.AddComponent<Interactor>();
            rig.Init();
            rig.Player.Tick(1f / 60f);
            Assert.IsNotNull(interactor.Focus);
            Assert.AreEqual("[E] Buka pintu", rig.Player.Hud.Prompt);

            rig.Intents.Press(new PlayerIntent { Interact = true });
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual("Pintunya terkunci.", rig.Player.Hud.Message);

            rig.Player.Locks.Acquire(this, PlayerAbility.Interact);
            rig.Player.Tick(1f / 60f);
            Assert.IsNull(interactor.Focus);
            Assert.AreEqual("", rig.Player.Hud.Prompt);
        }
    }
}
