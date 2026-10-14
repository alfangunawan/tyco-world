using System.Linq;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles.Tests
{
    public class VehicleInputActionsTests
    {
        static InputActionAsset Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerPaths.InputActionsAsset);
            Assert.IsNotNull(asset);
            return asset;
        }

        static bool HasPath(InputAction action, string path) => action.bindings.Any(b => b.path == path);

        [Test]
        public void VehicleMap_HasEveryAction()
        {
            var map = Load().FindActionMap(VehicleInputReader.MapName, true);
            foreach (var name in new[] { "Throttle", "Steer", "Brake", "Horn", "Headlight", "Camera", "Exit", "Look" })
                Assert.IsNotNull(map.FindAction(name), name);
        }

        [Test]
        public void KeyboardBindings_MatchTheSpec()
        {
            var map = Load().FindActionMap(VehicleInputReader.MapName, true);
            Assert.IsTrue(HasPath(map.FindAction("Throttle"), "<Keyboard>/w"));
            Assert.IsTrue(HasPath(map.FindAction("Throttle"), "<Keyboard>/s"));
            Assert.IsTrue(HasPath(map.FindAction("Steer"), "<Keyboard>/a"));
            Assert.IsTrue(HasPath(map.FindAction("Steer"), "<Keyboard>/d"));
            Assert.IsTrue(HasPath(map.FindAction("Brake"), "<Keyboard>/space"));
            Assert.IsTrue(HasPath(map.FindAction("Horn"), "<Keyboard>/h"));
            Assert.IsTrue(HasPath(map.FindAction("Headlight"), "<Keyboard>/f"));
            Assert.IsTrue(HasPath(map.FindAction("Camera"), "<Keyboard>/v"));
            Assert.IsTrue(HasPath(map.FindAction("Exit"), "<Keyboard>/e"));
            Assert.IsTrue(HasPath(map.FindAction("Look"), "<Pointer>/delta"));
            Assert.AreEqual(InputActionType.Button, map.FindAction("Exit").type);
            Assert.AreEqual(InputActionType.Value, map.FindAction("Throttle").type);
        }

        [Test]
        public void PlayerMap_IsUntouched()
        {
            var player = Load().FindActionMap("Player", true);
            foreach (var name in new[] { "Move", "Look", "Sprint", "Jump", "Crouch", "Interact", "Flashlight", "Attack" })
                Assert.IsNotNull(player.FindAction(name), name);
            Assert.IsTrue(HasPath(player.FindAction("Interact"), "<Keyboard>/e"));
            Assert.IsNotNull(Load().FindActionMap("UI"));
        }

        [Test]
        public void Ensure_IsIdempotent() => Assert.IsFalse(VehicleInputActions.Ensure(PlayerPaths.InputActionsAsset));
    }
}
