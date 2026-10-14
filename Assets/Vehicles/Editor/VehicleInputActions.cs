using System.IO;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Adds the "Vehicle" action map (spec #5 §5) to the project-wide actions asset once; other maps are left as they are.</summary>
    public static class VehicleInputActions
    {
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        /// <summary>True when the map was added and the file rewritten; false when it already existed.</summary>
        public static bool Ensure(string assetPath)
        {
            string fullPath = WorldGenPaths.ToAbsolute(assetPath);
            var asset = InputActionAsset.FromJson(File.ReadAllText(fullPath));
            try
            {
                if (asset.FindActionMap(VehicleInputReader.MapName) != null) return false;
                var map = asset.AddActionMap(VehicleInputReader.MapName);
                Axis(map, "Throttle", "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/upArrow", "<Keyboard>/downArrow");
                map.FindAction("Throttle").AddCompositeBinding("1DAxis")
                    .With("Positive", "<Gamepad>/rightTrigger", GamepadGroup)
                    .With("Negative", "<Gamepad>/leftTrigger", GamepadGroup);
                Axis(map, "Steer", "<Keyboard>/d", "<Keyboard>/a", "<Keyboard>/rightArrow", "<Keyboard>/leftArrow");
                map.FindAction("Steer").AddBinding("<Gamepad>/leftStick/x", groups: GamepadGroup);
                Button(map, "Brake", "<Keyboard>/space", "<Gamepad>/buttonSouth");
                Button(map, "Horn", "<Keyboard>/h", "<Gamepad>/leftStickPress");
                Button(map, "Headlight", "<Keyboard>/f", "<Gamepad>/dpad/up");
                Button(map, "Camera", "<Keyboard>/v", "<Gamepad>/buttonNorth");
                Button(map, "Exit", "<Keyboard>/e", "<Gamepad>/buttonWest");
                var look = map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
                look.AddBinding("<Pointer>/delta", groups: KeyboardGroup);
                look.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);
                File.WriteAllText(fullPath, asset.ToJson());
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        static void Axis(InputActionMap map, string name, string positive, string negative, string positiveAlt, string negativeAlt)
        {
            var action = map.AddAction(name, InputActionType.Value, expectedControlLayout: "Axis");
            action.AddCompositeBinding("1DAxis").With("Positive", positive, KeyboardGroup).With("Negative", negative, KeyboardGroup);
            action.AddCompositeBinding("1DAxis").With("Positive", positiveAlt, KeyboardGroup).With("Negative", negativeAlt, KeyboardGroup);
        }

        static void Button(InputActionMap map, string name, string key, string pad)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(key, groups: KeyboardGroup);
            action.AddBinding(pad, groups: GamepadGroup);
        }
    }
}
