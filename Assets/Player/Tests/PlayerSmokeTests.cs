using NUnit.Framework;
using TycoWorld.Player.Authoring;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class PlayerSmokeTests
    {
        [Test]
        public void Paths_AreUnderPlayerRoot()
        {
            Assert.AreEqual("Assets/Player", PlayerPaths.Root);
            StringAssert.StartsWith(PlayerPaths.Root + "/", PlayerPaths.PlayerPrefab);
            StringAssert.StartsWith(PlayerPaths.Root + "/", PlayerPaths.FootstepAudioFolder);
            Assert.AreEqual("Assets/InputSystem_Actions.inputactions", PlayerPaths.InputActionsAsset);
        }

        [Test]
        public void InputSystem_IsReferenced() => Assert.IsNotNull(typeof(UnityEngine.InputSystem.InputAction));

        [Test]
        public void PlayerLayer_IsBuiltInIgnoreRaycast()
        {
            Assert.AreEqual(LayerMask.NameToLayer("Ignore Raycast"), PlayerLayers.Player);
            Assert.AreEqual(0, PlayerLayers.QueryMask & (1 << PlayerLayers.Player));
            Assert.AreNotEqual(0, PlayerLayers.QueryMask & 1);
        }

        [Test]
        public void PhysicsFixture_RaycastHitsFloorTop()
        {
            using (var fx = new PhysicsFixture())
            {
                fx.Floor(0.3f);
                fx.Sync();
                bool hit = Physics.Raycast(PhysicsFixture.Origin + Vector3.up * 3f, Vector3.down, out var info, 10f,
                    PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
                Assert.IsTrue(hit);
                Assert.AreEqual(PhysicsFixture.Origin.y + 0.3f, info.point.y, 1e-3f);
            }
        }
    }
}
