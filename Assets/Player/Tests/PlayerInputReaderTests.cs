using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Player.Tests
{
    public class PlayerInputReaderTests
    {
        [Test]
        public void LookFromPointer_Locked_ScalesPixelsToDegrees() =>
            Assert.AreEqual(new Vector2(1f, -0.5f), PlayerInputReader.LookFromPointer(new Vector2(10f, -5f), 0.1f, true));

        [Test]
        public void LookFromPointer_Unlocked_IsZero() =>
            Assert.AreEqual(Vector2.zero, PlayerInputReader.LookFromPointer(new Vector2(300f, 200f), 0.1f, false));

        [Test]
        public void Constructor_FindsActions_AndIdleReadIsEmpty()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerPaths.InputActionsAsset);
            var reader = new PlayerInputReader(asset, new LookTuning());
            var intent = reader.Read(0.016f);
            Assert.AreEqual(Vector2.zero, intent.Move);
            Assert.IsFalse(intent.Jump || intent.Interact || intent.Flashlight || intent.CrouchToggle || intent.Sprint);
        }

        [Test]
        public void ApplyLookSettings_ScalesAndInvertsVertical()
        {
            Assert.AreEqual(new Vector2(3f, 1.5f), PlayerInputReader.ApplyLookSettings(new Vector2(2f, 1f), 1.5f, false));
            Assert.AreEqual(new Vector2(2f, -1f), PlayerInputReader.ApplyLookSettings(new Vector2(2f, 1f), 1f, true));
        }
    }
}
