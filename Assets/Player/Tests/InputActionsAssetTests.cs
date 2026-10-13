using System.Linq;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEditor;
using UnityEngine.InputSystem;

namespace TycoWorld.Player.Tests
{
    public class InputActionsAssetTests
    {
        static InputActionAsset Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerPaths.InputActionsAsset);
            Assert.IsNotNull(asset);
            return asset;
        }

        [Test]
        public void PlayerMap_HasRequiredActions()
        {
            var map = Load().FindActionMap("Player", true);
            foreach (var name in new[] { "Move", "Look", "Sprint", "Jump", "Crouch", "Interact", "Flashlight", "Attack" })
                Assert.IsNotNull(map.FindAction(name), name);
        }

        [Test]
        public void Flashlight_IsButtonBoundToF()
        {
            var action = Load().FindAction("Player/Flashlight", true);
            Assert.AreEqual(InputActionType.Button, action.type);
            Assert.IsTrue(action.bindings.Any(b => b.path == "<Keyboard>/f" && b.groups.Contains("Keyboard&Mouse")));
        }

        [Test]
        public void Interact_IsTapNotHold()
        {
            var action = Load().FindAction("Player/Interact", true);
            Assert.IsTrue(string.IsNullOrEmpty(action.interactions), action.interactions);
            Assert.IsTrue(action.bindings.All(b => string.IsNullOrEmpty(b.interactions)));
            Assert.IsTrue(action.bindings.Any(b => b.path == "<Keyboard>/e"));
        }

        [Test]
        public void Crouch_IsBoundToC() =>
            Assert.IsTrue(Load().FindAction("Player/Crouch", true).bindings.Any(b => b.path == "<Keyboard>/c"));
    }
}
