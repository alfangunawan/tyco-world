using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class MovementModelTests
    {
        const float Dt = 1f / 60f;

        static MoveInput Input(Vector2 move, bool grounded = true, bool sprint = false, bool crouch = false, bool jump = false) =>
            new MoveInput { Move = move, Forward = Vector3.forward, Right = Vector3.right, Grounded = grounded, Sprinting = sprint, Crouched = crouch, JumpRequested = jump };

        static float Horizontal(MovementModel m) => new Vector3(m.Velocity.x, 0f, m.Velocity.z).magnitude;

        static MovementModel Run(MoveInput input, int steps)
        {
            var m = new MovementModel(new MovementTuning());
            for (int i = 0; i < steps; i++) m.Step(input, Dt);
            return m;
        }

        [Test]
        public void Walk_ReachesWalkSpeed() => Assert.AreEqual(3f, Horizontal(Run(Input(Vector2.up), 60)), 1e-3f);

        [Test]
        public void Sprint_ReachesSprintSpeed() => Assert.AreEqual(5.5f, Horizontal(Run(Input(Vector2.up, sprint: true), 60)), 1e-3f);

        [Test]
        public void Crouch_UsesCrouchSpeed_EvenWhenSprintHeld() =>
            Assert.AreEqual(1.5f, Horizontal(Run(Input(Vector2.up, sprint: true, crouch: true), 60)), 1e-3f);

        [Test]
        public void Diagonal_IsNotFaster() => Assert.AreEqual(3f, Horizontal(Run(Input(new Vector2(1f, 1f)), 60)), 1e-3f);

        [Test]
        public void Move_FollowsBodyAxes()
        {
            var m = Run(Input(Vector2.right), 60);
            Assert.AreEqual(3f, m.Velocity.x, 1e-3f);
            Assert.AreEqual(0f, m.Velocity.z, 1e-3f);
        }

        [Test]
        public void NoInput_DeceleratesToRest()
        {
            var m = Run(Input(Vector2.up), 60);
            for (int i = 0; i < 30; i++) m.Step(Input(Vector2.zero), Dt);
            Assert.AreEqual(0f, Horizontal(m), 1e-4f);
        }

        [Test]
        public void Grounded_KeepsDownwardStick()
        {
            var m = Run(Input(Vector2.zero), 1);
            Assert.AreEqual(-2f, m.Velocity.y, 1e-5f);
        }

        [Test]
        public void Jump_ApexMatchesJumpHeight()
        {
            var m = new MovementModel(new MovementTuning());
            m.Step(Input(Vector2.zero), Dt);
            Assert.IsTrue(m.CanJump(true, false));
            float y = m.Step(Input(Vector2.zero, jump: true), Dt).y;
            Assert.IsTrue(m.JumpedThisStep);
            float apex = y;
            for (int i = 0; i < 120 && y >= 0f; i++)
            {
                y += m.Step(Input(Vector2.zero, grounded: false), Dt).y;
                apex = Mathf.Max(apex, y);
            }
            Assert.That(apex, Is.InRange(0.78f, 0.9f));
        }

        [Test]
        public void CanJump_RespectsCoyoteTime()
        {
            var m = new MovementModel(new MovementTuning());
            m.Step(Input(Vector2.zero), Dt);
            m.Step(Input(Vector2.zero, grounded: false), 0.05f);
            Assert.IsTrue(m.CanJump(false, false));
            m.Step(Input(Vector2.zero, grounded: false), 0.1f);
            Assert.IsFalse(m.CanJump(false, false));
        }

        [Test]
        public void CanJump_FalseWhileCrouched() => Assert.IsFalse(Run(Input(Vector2.zero), 1).CanJump(true, true));

        [Test]
        public void AirControl_ReducesAcceleration()
        {
            var m = new MovementModel(new MovementTuning());
            for (int i = 0; i < 6; i++) m.Step(Input(Vector2.up, grounded: false), Dt);
            Assert.AreEqual(12f * 0.3f * 0.1f, Horizontal(m), 0.01f);
        }

        [Test]
        public void HitCeiling_StopsUpwardVelocity()
        {
            var m = Run(Input(Vector2.zero), 1);
            m.Step(Input(Vector2.zero, jump: true), Dt);
            Assert.Greater(m.Velocity.y, 0f);
            m.HitCeiling();
            Assert.AreEqual(0f, m.Velocity.y);
        }

        [Test]
        public void ResetMotion_ClearsVelocityAndCoyote()
        {
            var m = Run(Input(Vector2.up), 30);
            m.ResetMotion();
            Assert.AreEqual(Vector3.zero, m.Velocity);
            Assert.IsFalse(m.CanJump(false, false));
        }
    }
}
