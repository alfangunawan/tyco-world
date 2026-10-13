using UnityEngine;

namespace TycoWorld.Player
{
    public struct MoveInput
    {
        public Vector2 Move;
        public Vector3 Forward, Right;
        public bool Grounded, Sprinting, Crouched, JumpRequested;
    }

    /// <summary>Pure character motion: speed per mode, acceleration, air control, jump with coyote time, gravity.</summary>
    public sealed class MovementModel
    {
        readonly MovementTuning t;
        float sinceGrounded = float.PositiveInfinity;

        public Vector3 Velocity;

        public bool JumpedThisStep { get; private set; }

        public MovementModel(MovementTuning tuning) { t = tuning; }

        public float TargetSpeed(bool sprinting, bool crouched) => crouched ? t.crouchSpeed : sprinting ? t.sprintSpeed : t.walkSpeed;

        public bool CanJump(bool groundedNow, bool crouched) => !crouched && (groundedNow || sinceGrounded <= t.coyoteTime);

        /// <summary>Advances velocity by <paramref name="dt"/> and returns the displacement to feed CharacterController.Move.</summary>
        public Vector3 Step(in MoveInput input, float dt)
        {
            JumpedThisStep = false;
            var move = Vector2.ClampMagnitude(input.Move, 1f);
            var wish = (input.Forward * move.y + input.Right * move.x) * TargetSpeed(input.Sprinting, input.Crouched);
            wish.y = 0f;
            var horizontal = new Vector3(Velocity.x, 0f, Velocity.z);
            float rate = wish.sqrMagnitude > 1e-6f ? t.acceleration : t.deceleration;
            if (!input.Grounded) rate *= t.airControl;
            horizontal = Vector3.MoveTowards(horizontal, wish, rate * dt);

            if (input.Grounded) sinceGrounded = 0f;
            else sinceGrounded += dt;

            float vy = Velocity.y;
            if (input.JumpRequested && CanJump(input.Grounded, input.Crouched))
            {
                vy = Mathf.Sqrt(2f * t.gravity * t.jumpHeight);
                JumpedThisStep = true;
                sinceGrounded = float.PositiveInfinity;
            }
            else if (input.Grounded && vy <= 0f) vy = -t.groundStick;
            else vy -= t.gravity * dt;

            Velocity = new Vector3(horizontal.x, vy, horizontal.z);
            return Velocity * dt;
        }

        public void HitCeiling()
        {
            if (Velocity.y > 0f) Velocity.y = 0f;
        }

        public void ResetMotion()
        {
            Velocity = Vector3.zero;
            sinceGrounded = float.PositiveInfinity;
        }
    }
}
