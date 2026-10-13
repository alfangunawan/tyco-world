using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class PlayerTuningTests
    {
        [Test]
        public void Defaults_MatchSpec()
        {
            var t = ScriptableObject.CreateInstance<PlayerTuning>();
            try
            {
                Assert.AreEqual(1.8f, t.body.height);
                Assert.AreEqual(0.35f, t.body.radius);
                Assert.AreEqual(0.3f, t.body.stepOffset);
                Assert.AreEqual(0.05f, t.body.skinWidth);
                Assert.AreEqual(3f, t.movement.walkSpeed);
                Assert.AreEqual(5.5f, t.movement.sprintSpeed);
                Assert.AreEqual(1.5f, t.movement.crouchSpeed);
                Assert.AreEqual(12f, t.movement.acceleration);
                Assert.AreEqual(16f, t.movement.deceleration);
                Assert.AreEqual(0.3f, t.movement.airControl);
                Assert.AreEqual(0.8f, t.movement.jumpHeight);
                Assert.AreEqual(9.81f, t.movement.gravity);
                Assert.AreEqual(2f, t.movement.groundStick);
                Assert.AreEqual(0.1f, t.movement.coyoteTime);
                Assert.AreEqual(100f, t.stamina.max);
                Assert.AreEqual(18f, t.stamina.sprintDrainPerSecond);
                Assert.AreEqual(14f, t.stamina.regenPerSecond);
                Assert.AreEqual(1f, t.stamina.regenDelay);
                Assert.AreEqual(0.3f, t.stamina.recoverThreshold);
                Assert.AreEqual(8f, t.stamina.jumpCost);
                Assert.AreEqual(0.1f, t.look.degreesPerPixel);
                Assert.AreEqual(85f, t.look.pitchLimit);
                Assert.AreEqual(70f, t.look.fieldOfView);
                Assert.AreEqual(1f, t.crouch.crouchHeight);
                Assert.AreEqual(1.6f, t.crouch.standingEyeHeight);
                Assert.AreEqual(0.85f, t.crouch.crouchEyeHeight);
                Assert.AreEqual(0.75f, t.bob.walkStride);
                Assert.AreEqual(1f, t.bob.sprintStride);
                Assert.AreEqual(0.55f, t.bob.crouchStride);
                Assert.AreEqual(0.045f, t.bob.walkAmplitude);
                Assert.AreEqual(0.07f, t.bob.sprintAmplitude);
                Assert.AreEqual(0.025f, t.bob.crouchAmplitude);
                Assert.AreEqual(0.5f, t.bob.lateralRatio);
                Assert.AreEqual(2.2f, t.interaction.range);
                Assert.AreEqual(0.12f, t.interaction.sphereRadius);
                Assert.AreEqual(55f, t.flashlight.outerAngle);
                Assert.AreEqual(30f, t.flashlight.innerAngle);
                Assert.AreEqual(22f, t.flashlight.range);
                Assert.AreEqual(14f, t.flashlight.lagSpeed);
                Assert.IsFalse(t.flashlight.startOn);
                Assert.AreEqual(2.5f, t.hud.messageSeconds);
                Assert.AreEqual(0.8f, t.hud.staminaHideDelay);
                Assert.AreEqual(-20f, t.respawnBelowY);
                Assert.AreEqual(15f, t.worldWaitSeconds);
            }
            finally
            {
                Object.DestroyImmediate(t);
            }
        }
    }
}
