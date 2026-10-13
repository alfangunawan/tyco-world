using System;
using UnityEngine;

namespace TycoWorld.Player
{
    [Serializable]
    public sealed class BodyTuning
    {
        public float height = 1.8f;
        public float radius = 0.35f;
        public float stepOffset = 0.3f;
        public float skinWidth = 0.05f;
        public float slopeLimit = 50f;
    }

    [Serializable]
    public sealed class MovementTuning
    {
        public float walkSpeed = 3f;
        public float sprintSpeed = 5.5f;
        public float crouchSpeed = 1.5f;
        public float acceleration = 12f;
        public float deceleration = 16f;
        public float airControl = 0.3f;
        public float jumpHeight = 0.8f;
        public float gravity = 9.81f;
        public float groundStick = 2f;
        public float coyoteTime = 0.1f;
    }

    [Serializable]
    public sealed class StaminaTuning
    {
        public float max = 100f;
        public float sprintDrainPerSecond = 18f;
        public float regenPerSecond = 14f;
        public float regenDelay = 1f;
        public float recoverThreshold = 0.3f;
        public float jumpCost = 8f;
    }

    [Serializable]
    public sealed class LookTuning
    {
        public float degreesPerPixel = 0.1f;
        public float pitchLimit = 85f;
        public float fieldOfView = 70f;
    }

    [Serializable]
    public sealed class CrouchTuning
    {
        public float crouchHeight = 1f;
        public float standingEyeHeight = 1.6f;
        public float crouchEyeHeight = 0.85f;
        public float transitionSpeed = 4f;
    }

    [Serializable]
    public sealed class BobTuning
    {
        public float walkStride = 0.75f;
        public float sprintStride = 1f;
        public float crouchStride = 0.55f;
        public float walkAmplitude = 0.045f;
        public float sprintAmplitude = 0.07f;
        public float crouchAmplitude = 0.025f;
        public float lateralRatio = 0.5f;
        public float fadeSpeed = 4f;
        public float landingMinSpeed = 3f;
    }

    [Serializable]
    public sealed class InteractionTuning
    {
        public float range = 2.2f;
        public float sphereRadius = 0.12f;
    }

    [Serializable]
    public sealed class FlashlightTuning
    {
        public float outerAngle = 55f;
        public float innerAngle = 30f;
        public float range = 22f;
        public float intensity = 3f;
        public Color color = new Color(1f, 0.95f, 0.85f);
        public float lagSpeed = 14f;
        public bool startOn;
    }

    [Serializable]
    public sealed class HudTuning
    {
        public float messageSeconds = 2.5f;
        public float staminaHideDelay = 0.8f;
    }

    /// <summary>All player tuning in one asset; field initialisers are the spec defaults (spec #2 §4).</summary>
    [CreateAssetMenu(menuName = "Tyco World/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        public BodyTuning body = new BodyTuning();
        public MovementTuning movement = new MovementTuning();
        public StaminaTuning stamina = new StaminaTuning();
        public LookTuning look = new LookTuning();
        public CrouchTuning crouch = new CrouchTuning();
        public BobTuning bob = new BobTuning();
        public InteractionTuning interaction = new InteractionTuning();
        public FlashlightTuning flashlight = new FlashlightTuning();
        public HudTuning hud = new HudTuning();
        public float respawnBelowY = -20f;
        public float worldWaitSeconds = 15f;
    }
}
