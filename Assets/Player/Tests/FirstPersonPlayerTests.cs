using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class FirstPersonPlayerTests
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

        TestRig FloorRig(float yaw = 0f)
        {
            fx.Floor();
            fx.Sync();
            return TestRig.Create(fx, Vector3.zero, yaw).Init();
        }

        static PlayerIntent Forward(bool sprint = false) => new PlayerIntent { Move = Vector2.up, Sprint = sprint };

        GameObject Ribbon(float y, float zStart, float zEnd)
        {
            var mesh = fx.Track(new UnityEngine.Mesh());
            mesh.vertices = new[] { new Vector3(-5f, y, zStart), new Vector3(5f, y, zStart), new Vector3(5f, y, zEnd), new Vector3(-5f, y, zEnd) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            var go = fx.Track(new GameObject("Ribbon"));
            go.transform.position = PhysicsFixture.Origin;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        [Test]
        public void CharacterController_MoveInEditMode_IsBlockedByWall()
        {
            fx.Floor();
            fx.Box("Wall", new Vector3(0f, 1.5f, 3f), new Vector3(10f, 3f, 0.2f));
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Intents.Hold(Forward(), 3f);
            rig.Run(3f);
            Assert.Greater(rig.Local.z, 2f, "moved");
            Assert.Less(rig.Local.z, 2.61f, "blocked by wall face at z = 2.9");
        }

        [Test]
        public void Tick_WalkForward_MovesExpectedDistance()
        {
            var rig = FloorRig();
            rig.Intents.Hold(Forward(), 2f);
            rig.Run(2f);
            Assert.AreEqual(5.625f, rig.Local.z, 0.3f);
            Assert.AreEqual(0f, rig.Local.x, 0.05f);
            Assert.IsTrue(rig.Player.IsGrounded);
        }

        [Test]
        public void Tick_LargeDt_DoesNotPassThroughWall()
        {
            fx.Floor();
            fx.Box("Wall", new Vector3(0f, 1.5f, 3f), new Vector3(10f, 3f, 0.2f));
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Intents.Hold(Forward(true), 3f);
            rig.Run(3f, 0.33f);
            Assert.Less(rig.Local.z, 2.61f);
        }

        [Test]
        public void Tick_StepsOntoThinRibbon()
        {
            fx.Floor();
            Ribbon(0.08f, 2f, 12f);
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Intents.Hold(Forward(), 2.5f);
            rig.Run(2.5f);
            Assert.Greater(rig.Local.z, 3f);
            Assert.That(rig.Local.y, Is.InRange(0.06f, 0.16f), "on the 0.08 m ribbon (within skin width)");
            Assert.IsTrue(rig.Player.IsGrounded);
        }

        [Test]
        public void Tick_StepsOntoKerb()
        {
            // Street-frontage spec §4: from residential asphalt (y 0.08) up a kerb onto a sidewalk whose top is 0.21.
            fx.Floor(0.08f);
            fx.Box("Sidewalk", new Vector3(0f, 0.21f - 0.5f, 2f + 5f), new Vector3(10f, 1f, 10f));
            fx.Sync();
            var rig = TestRig.Create(fx, new Vector3(0f, 0.08f, 0f)).Init();
            rig.Intents.Hold(Forward(), 2.5f);
            rig.Run(2.5f);
            Assert.Greater(rig.Local.z, 3f);
            Assert.That(rig.Local.y, Is.InRange(0.19f, 0.29f), "on the 0.21 m sidewalk (within skin width)");
            Assert.IsTrue(rig.Player.IsGrounded);
        }

        [Test]
        public void Tick_WalkingOffRibbon_NoLanding()
        {
            fx.Floor();
            Ribbon(0.08f, -2f, 1f);
            fx.Sync();
            var rig = TestRig.Create(fx, new Vector3(0f, 0.1f, 0f)).Init();
            rig.Intents.Hold(Forward(), 2f);
            bool landed = false;
            for (int i = 0; i < 120; i++)
            {
                rig.Player.Tick(1f / 60f);
                landed |= rig.Player.LandedThisFrame;
            }
            Assert.Greater(rig.Local.z, 2f);
            Assert.That(rig.Local.y, Is.InRange(-0.01f, 0.07f), "back on the floor (within skin width)");
            Assert.IsFalse(landed);
        }

        [Test]
        public void Crouch_LowersBodyAndEye()
        {
            var rig = FloorRig();
            rig.Intents.Press(new PlayerIntent { CrouchToggle = true });
            rig.Run(0.5f);
            Assert.IsTrue(rig.Player.IsCrouched);
            Assert.AreEqual(1f, rig.Root.GetComponent<CharacterController>().height, 1e-3f);
            Assert.AreEqual(0.85f, rig.Pivot.localPosition.y, 1e-3f);
        }

        [Test]
        public void Crouch_UnderLowCeiling_CannotStand()
        {
            fx.Floor();
            fx.Box("Ceiling", new Vector3(0f, 1.55f, 5.5f), new Vector3(10f, 0.5f, 5f));
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Intents.Press(new PlayerIntent { CrouchToggle = true });
            rig.Run(0.5f);
            rig.Intents.Hold(Forward(), 3f);
            rig.Run(3f);
            Assert.Greater(rig.Local.z, 3.5f, "walked under the ceiling");
            rig.Intents.Press(new PlayerIntent { CrouchToggle = true });
            rig.Run(0.5f);
            Assert.IsTrue(rig.Player.IsCrouched);
            Assert.AreEqual(1f, rig.Root.GetComponent<CharacterController>().height, 1e-3f);
        }

        [Test]
        public void Teleport_SurvivesNextMove()
        {
            var rig = FloorRig();
            rig.Player.Teleport(PhysicsFixture.Origin + new Vector3(10f, 0f, 10f), 90f);
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(10f, rig.Local.x, 0.05f);
            Assert.AreEqual(10f, rig.Local.z, 0.05f);
            Assert.AreEqual(90f, rig.Player.Yaw, 1e-3f);
        }

        [Test]
        public void MoveLock_DisablesBodyAndFreezesPosition()
        {
            var rig = FloorRig();
            var owner = new object();
            rig.Player.Locks.Acquire(owner, PlayerAbility.Move);
            var body = rig.Root.GetComponent<CharacterController>();
            Assert.IsFalse(body.enabled);
            rig.Intents.Hold(Forward(), 1f);
            rig.Run(1f);
            Assert.AreEqual(0f, rig.Local.z, 1e-3f);
            rig.Player.Locks.Release(owner);
            Assert.IsTrue(body.enabled);
        }

        [Test]
        public void Jump_LeavesGroundAndSpendsStamina()
        {
            var rig = FloorRig();
            rig.Run(0.2f);
            rig.Intents.Press(new PlayerIntent { Jump = true });
            rig.Run(0.25f);
            Assert.Greater(rig.Local.y, 0.4f);
            Assert.AreEqual(0.92f, rig.Player.Stamina.Normalized, 0.01f);
        }

        [Test]
        public void Sprint_DrainsStaminaAndIsFaster()
        {
            var rig = FloorRig();
            rig.Intents.Hold(Forward(true), 1f);
            rig.Run(1f);
            Assert.AreEqual(0.82f, rig.Player.Stamina.Normalized, 0.02f);
            Assert.Greater(new Vector3(rig.Player.Velocity.x, 0f, rig.Player.Velocity.z).magnitude, 5f);
            Assert.IsTrue(rig.Player.Hud.StaminaVisible);
        }

        [Test]
        public void Look_RotatesBodyYawAndPivotPitch()
        {
            var rig = FloorRig();
            rig.Intents.Press(new PlayerIntent { LookDegrees = new Vector2(30f, 10f) });
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(30f, rig.Player.Yaw, 1e-3f);
            Assert.AreEqual(10f, rig.Player.Pitch, 1e-3f);
            Assert.AreEqual(30f, rig.Root.transform.eulerAngles.y, 1e-2f);
            Assert.AreEqual(350f, rig.Pivot.localEulerAngles.x, 1e-2f);
        }

        [Test]
        public void Walking_CountsStepsAndBobs_ThenBobFades()
        {
            var rig = FloorRig();
            rig.Intents.Hold(Forward(), 3f);
            float maxBob = 0f;
            for (int i = 0; i < 180; i++)
            {
                rig.Player.Tick(1f / 60f);
                maxBob = Mathf.Max(maxBob, rig.View.localPosition.y);
            }
            Assert.That(rig.Player.StepCount, Is.InRange(10, 12));
            Assert.Greater(maxBob, 0.02f);
            rig.Run(1f);
            Assert.AreEqual(0f, rig.View.localPosition.magnitude, 1e-3f);
        }

        [Test]
        public void FellOutOfWorld_RaisedBelowThreshold()
        {
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            int fell = 0;
            rig.Player.FellOutOfWorld += () => fell++;
            rig.Player.Teleport(new Vector3(5000f, -25f, 5000f), 0f);
            rig.Player.Tick(1f / 60f);
            Assert.GreaterOrEqual(fell, 1);
        }

        [Test]
        public void Ground_IsProbedEveryTick()
        {
            var floor = fx.Floor();
            floor.AddComponent<TycoWorld.WorldGen.GroundSurface>().Initialize(TycoWorld.WorldGen.SurfaceType.Concrete);
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            rig.Player.Tick(1f / 60f);
            Assert.IsTrue(rig.Player.Ground.Hit);
            Assert.AreEqual(TycoWorld.WorldGen.SurfaceType.Concrete, rig.Player.Ground.Surface);
        }

        [Test]
        public void LookSettings_DoNotTouchScriptedLook()
        {
            var rig = FloorRig();
            rig.Player.LookSensitivity = 3f;
            rig.Player.InvertY = true;
            rig.Intents.Press(new PlayerIntent { LookDegrees = new Vector2(30f, 10f) });
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(30f, rig.Player.Yaw, 1e-3f);
            Assert.AreEqual(10f, rig.Player.Pitch, 1e-3f);
        }

        [Test]
        public void SettingsKnobs_DefaultToTheOldBehaviour()
        {
            var rig = FloorRig();
            Assert.AreEqual(1f, rig.Player.LookSensitivity);
            Assert.IsFalse(rig.Player.InvertY);
            Assert.IsTrue(rig.Player.EscapeUnlocksCursor);
        }

        Transform NewSeat(Vector3 local, float yaw)
        {
            var seat = fx.Track(new GameObject("Seat")).transform;
            seat.SetPositionAndRotation(PhysicsFixture.Origin + local, Quaternion.Euler(0f, yaw, 0f));
            return seat;
        }

        static void AssertNear(Vector3 expected, Vector3 actual) => Assert.Less(Vector3.Distance(expected, actual), 1e-3f, actual.ToString("F3"));

        [Test]
        public void Seat_FollowsTheSeat_WithTheBodyOff()
        {
            var rig = FloorRig();
            var seat = NewSeat(new Vector3(3f, 0.85f, 2f), 90f);
            rig.Player.Seat(seat);
            Assert.IsTrue(rig.Player.IsSeated);
            Assert.AreSame(seat, rig.Player.CurrentSeat);
            Assert.IsFalse(rig.Root.GetComponent<CharacterController>().enabled);
            seat.SetPositionAndRotation(seat.position + new Vector3(5f, 0f, 0f), Quaternion.Euler(0f, 120f, 0f));
            rig.Player.Tick(1f / 60f);
            AssertNear(seat.position, rig.Root.transform.position);
            Assert.AreEqual(120f, rig.Player.Yaw, 1e-3f);
        }

        [Test]
        public void Seated_IgnoresMoveAndLook_AndLeavesTheCameraAlone()
        {
            var rig = FloorRig();
            var seat = NewSeat(new Vector3(0f, 0.85f, 0f), 0f);
            rig.Player.Seat(seat);
            var cameraPose = PhysicsFixture.Origin + new Vector3(0f, 3f, -4f);
            rig.View.position = cameraPose;
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up, LookDegrees = new Vector2(90f, 30f) }, 1f);
            rig.Run(0.5f);
            AssertNear(cameraPose, rig.View.position);
            AssertNear(seat.position, rig.Root.transform.position);
            Assert.AreEqual(0, rig.Player.StepCount);
        }

        [Test]
        public void Seated_BelowTheWorld_DoesNotRespawn()
        {
            var rig = FloorRig();
            int fell = 0;
            rig.Player.FellOutOfWorld += () => fell++;
            rig.Player.Seat(NewSeat(new Vector3(0f, -1100f, 0f), 0f));
            rig.Run(0.1f);
            Assert.AreEqual(0, fell);
        }

        [Test]
        public void Unseat_RestoresControlAndTheCameraRig()
        {
            var rig = FloorRig();
            rig.Player.Seat(NewSeat(new Vector3(0f, 0.85f, 0f), 0f));
            rig.View.localPosition = new Vector3(1f, 2f, -3f);
            rig.View.localRotation = Quaternion.Euler(10f, 20f, 0f);
            int events = 0;
            rig.Player.Unseated += () => events++;
            rig.Player.Unseat(PhysicsFixture.Origin + new Vector3(2f, 0f, 0f), 45f);
            Assert.IsFalse(rig.Player.IsSeated);
            Assert.IsTrue(rig.Root.GetComponent<CharacterController>().enabled);
            Assert.AreEqual(Vector3.zero, rig.View.localPosition);
            Assert.AreEqual(Quaternion.identity, rig.View.localRotation);
            Assert.AreEqual(45f, rig.Player.Yaw, 1e-3f);
            AssertNear(PhysicsFixture.Origin + new Vector3(2f, 0f, 0f), rig.Root.transform.position);
            Assert.AreEqual(1, events);
            rig.Player.Unseat(PhysicsFixture.Origin, 0f);
            Assert.AreEqual(1, events, "Unseat tanpa duduk tidak memicu event");
        }

        [Test]
        public void Teleport_WhileSeated_UnseatsAndRaisesTheEvent()
        {
            var rig = FloorRig();
            rig.Player.Seat(NewSeat(new Vector3(0f, 0.85f, 0f), 0f));
            int events = 0;
            rig.Player.Unseated += () => events++;
            rig.Player.Teleport(PhysicsFixture.Origin + new Vector3(4f, 0f, 0f), 0f);
            Assert.IsFalse(rig.Player.IsSeated);
            Assert.AreEqual(1, events);
            rig.Player.Teleport(PhysicsFixture.Origin, 0f);
            Assert.AreEqual(1, events, "teleport biasa tidak memicu event");
        }

        [Test]
        public void LockChanges_WhileSeated_KeepTheBodyOff()
        {
            var rig = FloorRig();
            rig.Player.Seat(NewSeat(new Vector3(0f, 0.85f, 0f), 0f));
            var owner = new object();
            rig.Player.Locks.Acquire(owner, PlayerAbility.Flashlight);
            rig.Player.Locks.Release(owner);
            Assert.IsFalse(rig.Root.GetComponent<CharacterController>().enabled);
        }

        [Test]
        public void SeatDestroyed_WhileSeated_GetsOffInPlaceOnTheNextTick()
        {
            var rig = FloorRig();
            var seat = NewSeat(new Vector3(0f, 0.85f, 0f), 30f);
            rig.Player.Seat(seat);
            rig.View.localPosition = new Vector3(1f, 2f, -3f);
            rig.View.localRotation = Quaternion.Euler(10f, 20f, 0f);
            int events = 0;
            rig.Player.Unseated += () => events++;
            Object.DestroyImmediate(seat.gameObject);
            rig.Player.Tick(1f / 60f);
            Assert.IsFalse(rig.Player.IsSeated);
            Assert.IsTrue(ReferenceEquals(rig.Player.CurrentSeat, null), "kursi yang sudah hancur dilepas");
            Assert.AreEqual(1, events);
            Assert.AreEqual(Vector3.zero, rig.View.localPosition);
            Assert.AreEqual(Quaternion.identity, rig.View.localRotation);
            Assert.IsTrue(rig.Player.Locks.Allows(PlayerAbility.All));
            Assert.IsTrue(rig.Root.GetComponent<CharacterController>().enabled);
            Assert.AreEqual(30f, rig.Player.Yaw, 1e-3f);
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(1, events, "event hanya sekali");
        }
    }
}
