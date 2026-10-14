using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles.Tests
{
    public class VehicleInputReaderTests
    {
        [Test]
        public void Constructor_FindsActions_IdleReadIsEmpty()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerPaths.InputActionsAsset);
            var reader = new VehicleInputReader(asset, 0.1f);
            Assert.IsFalse(reader.IsEnabled);
            var intent = reader.Read(0.016f);
            Assert.AreEqual(0f, intent.Throttle);
            Assert.AreEqual(0f, intent.Steer);
            Assert.AreEqual(Vector2.zero, intent.LookDegrees);
            Assert.IsFalse(intent.Brake || intent.Horn || intent.ToggleHeadlight || intent.ToggleCamera || intent.Exit);
        }
    }
}
