using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles
{
    /// <summary>Reads the project-wide "Vehicle" action map (spec #5 §5); the map is only enabled while a bike is ridden.</summary>
    public sealed class VehicleInputReader : IVehicleIntentSource
    {
        public const string MapName = "Vehicle";

        readonly InputActionMap map;
        readonly InputAction throttle, steer, brake, horn, headlight, cameraToggle, exit, look;
        readonly float degreesPerPixel;

        public VehicleInputReader(InputActionAsset asset, float degreesPerPixel)
        {
            map = asset.FindActionMap(MapName, true);
            throttle = map.FindAction("Throttle", true);
            steer = map.FindAction("Steer", true);
            brake = map.FindAction("Brake", true);
            horn = map.FindAction("Horn", true);
            headlight = map.FindAction("Headlight", true);
            cameraToggle = map.FindAction("Camera", true);
            exit = map.FindAction("Exit", true);
            look = map.FindAction("Look", true);
            this.degreesPerPixel = degreesPerPixel;
        }

        /// <summary>Mouse look only applies while the cursor is locked (pause menu, alt-tab).</summary>
        public bool CursorLocked { get; set; } = true;

        public bool IsEnabled => map.enabled;

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public VehicleIntent Read(float dt) => new VehicleIntent
        {
            Throttle = Mathf.Clamp(throttle.ReadValue<float>(), -1f, 1f),
            Steer = Mathf.Clamp(steer.ReadValue<float>(), -1f, 1f),
            Brake = brake.IsPressed(),
            Horn = horn.WasPressedThisFrame(),
            ToggleHeadlight = headlight.WasPressedThisFrame(),
            ToggleCamera = cameraToggle.WasPressedThisFrame(),
            Exit = exit.WasPressedThisFrame(),
            LookDegrees = CursorLocked ? look.ReadValue<Vector2>() * degreesPerPixel : Vector2.zero
        };
    }
}
