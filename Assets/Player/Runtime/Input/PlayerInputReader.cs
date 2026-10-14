using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Player
{
    /// <summary>Reads the project-wide "Player" action map into a PlayerIntent (keyboard + mouse).</summary>
    public sealed class PlayerInputReader : IPlayerIntentSource
    {
        readonly InputActionMap map;
        readonly InputAction move, look, sprint, jump, crouch, interact, flashlight, attack;
        readonly LookTuning tuning;

        public PlayerInputReader(InputActionAsset asset, LookTuning lookTuning)
        {
            map = asset.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            sprint = map.FindAction("Sprint", true);
            jump = map.FindAction("Jump", true);
            crouch = map.FindAction("Crouch", true);
            interact = map.FindAction("Interact", true);
            flashlight = map.FindAction("Flashlight", true);
            attack = map.FindAction("Attack", true);
            tuning = lookTuning;
        }

        /// <summary>Mouse look only applies while the cursor is locked (Esc / alt-tab must not spin the camera).</summary>
        public bool CursorLocked { get; set; } = true;

        public bool AttackPressed => attack.WasPressedThisFrame();

        public bool ReleaseCursorPressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public PlayerIntent Read(float dt) => new PlayerIntent
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f),
            LookDegrees = LookFromPointer(look.ReadValue<Vector2>(), tuning.degreesPerPixel, CursorLocked),
            Sprint = sprint.IsPressed(),
            Jump = jump.WasPressedThisFrame(),
            CrouchToggle = crouch.WasPressedThisFrame(),
            Interact = interact.WasPressedThisFrame(),
            Flashlight = flashlight.WasPressedThisFrame()
        };

        public static Vector2 LookFromPointer(Vector2 pixels, float degreesPerPixel, bool cursorLocked) =>
            cursorLocked ? pixels * degreesPerPixel : Vector2.zero;

        /// <summary>Applies the settings page's sensitivity multiplier and vertical inversion to a look delta in degrees (spec #4 §4).</summary>
        public static Vector2 ApplyLookSettings(Vector2 degrees, float sensitivity, bool invertY) =>
            new Vector2(degrees.x * sensitivity, (invertY ? -degrees.y : degrees.y) * sensitivity);
    }
}
