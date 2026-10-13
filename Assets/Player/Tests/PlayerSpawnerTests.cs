using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.Player.Tests
{
    public class PlayerSpawnerTests
    {
        PhysicsFixture fx;

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            GroundProbe.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            GroundProbe.ClearCache();
        }

        (TestRig Rig, PlayerSpawner Spawner, PlayerSpawnPoint Point) Setup(bool worldReady)
        {
            fx.Floor(0.3f);
            var pointGo = fx.Track(new GameObject("PlayerSpawn_Kos"));
            pointGo.transform.SetPositionAndRotation(PhysicsFixture.Origin, Quaternion.Euler(0f, 180f, 0f));
            var point = pointGo.AddComponent<PlayerSpawnPoint>();
            point.Configure("kos", "Kos Raflesia 3");
            fx.Sync();
            var rig = TestRig.Create(fx, new Vector3(0f, 0.3f, -10f)).Init();
            var spawner = rig.Root.AddComponent<PlayerSpawner>();
            spawner.Configure(rig.Player, point);
            spawner.WorldReady = () => worldReady;
            spawner.Begin();
            return (rig, spawner, point);
        }

        [Test]
        public void Spawns_OnProbedFloor_FacingSpawnYaw()
        {
            var (rig, spawner, _) = Setup(true);
            spawner.Tick(0.016f);
            Assert.AreEqual(PlayerSpawner.SpawnState.Spawned, spawner.State);
            Assert.AreEqual(0.36f, rig.Local.y, 0.02f);
            Assert.AreEqual(0f, rig.Local.x, 1e-3f);
            Assert.AreEqual(0f, rig.Local.z, 1e-3f);
            Assert.AreEqual(180f, rig.Player.Yaw, 1e-3f);
            Assert.IsTrue(rig.Player.Locks.Allows(PlayerAbility.All));
            Assert.IsTrue(rig.Player.Hud.CrosshairVisible);
            Assert.AreEqual("Kos Raflesia 3", rig.Player.Hud.Message);
        }

        [Test]
        public void Waiting_KeepsPlayerFrozen()
        {
            var (rig, spawner, _) = Setup(false);
            rig.Intents.Hold(new PlayerIntent { Move = Vector2.up }, 1f);
            spawner.Tick(1f);
            rig.Run(1f);
            Assert.AreEqual(PlayerSpawner.SpawnState.WaitingForWorld, spawner.State);
            Assert.IsFalse(rig.Player.Locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(rig.Root.GetComponent<CharacterController>().enabled);
            Assert.AreEqual(-10f, rig.Local.z, 1e-3f);
            Assert.IsFalse(rig.Player.Hud.CrosshairVisible);
        }

        [Test]
        public void WorldMissing_AfterTimeout()
        {
            var (rig, spawner, _) = Setup(false);
            LogAssert.Expect(LogType.Error, new Regex("Dunia belum di-generate"));
            for (int i = 0; i < 16; i++) spawner.Tick(1f);
            Assert.AreEqual(PlayerSpawner.SpawnState.WorldMissing, spawner.State);
            StringAssert.Contains("belum di-generate", rig.Player.Hud.Message);
            Assert.IsFalse(rig.Player.Locks.Allows(PlayerAbility.Move));
        }

        [Test]
        public void Nudges_OutOfBlocker()
        {
            var (rig, spawner, _) = Setup(true);
            fx.Box("Lamp", new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 1.8f, 0.5f));
            fx.Sync();
            Assert.IsTrue(spawner.TrySpawnNow());
            float planar = new Vector2(rig.Local.x, rig.Local.z).magnitude;
            Assert.That(planar, Is.InRange(0.4f, 2.01f));
            var feet = rig.Root.transform.position;
            float r = rig.Tuning.body.radius;
            Assert.IsFalse(Physics.CheckCapsule(feet + Vector3.up * (r + 0.05f), feet + Vector3.up * (1.8f - r), r,
                PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore));
        }

        [Test]
        public void TryFindSpawn_AllBlocked_ReturnsFalse()
        {
            var (_, spawner, point) = Setup(true);
            fx.Box("Building", new Vector3(0f, 1.8f, 0f), new Vector3(6f, 3f, 6f));
            fx.Sync();
            Assert.IsFalse(spawner.TryFindSpawn(point.Feet, out var feet));
            Assert.AreEqual(point.Feet, feet);
        }

        [Test]
        public void AllBlocked_KeepsPlayerFrozenWithMessage()
        {
            var (rig, spawner, _) = Setup(true);
            fx.Box("Building", new Vector3(0f, 1.8f, 0f), new Vector3(6f, 3f, 6f));
            fx.Sync();
            LogAssert.Expect(LogType.Error, new Regex("terhalang"));
            Assert.IsFalse(spawner.TrySpawnNow());
            Assert.AreEqual(PlayerSpawner.SpawnState.Blocked, spawner.State);
            Assert.IsFalse(rig.Player.Locks.Allows(PlayerAbility.Move));
            Assert.AreEqual(-10f, rig.Local.z, 1e-3f, "player was not moved into the blocker");
            StringAssert.Contains("terhalang", rig.Player.Hud.Message);
        }

        GameObject Roof(Vector3 localCenter, Vector3 size)
        {
            var roof = fx.Box("Roof", localCenter, size);
            roof.AddComponent<TycoWorld.WorldGen.OsmFeature>().Initialize(1234, TycoWorld.WorldGen.OsmElementType.Way,
                TycoWorld.WorldGen.FeatureKind.Building, "kos", null);
            return roof;
        }

        [Test]
        public void CandidateUnderBuildingRoof_IsRejected()
        {
            var (_, spawner, point) = Setup(true);
            Roof(new Vector3(0f, 4f, 0f), new Vector3(1.2f, 0.2f, 1.2f));
            fx.Sync();
            Assert.IsTrue(spawner.TryFindSpawn(point.Feet, out var feet));
            Assert.GreaterOrEqual(new Vector2(feet.x - point.Feet.x, feet.z - point.Feet.z).magnitude, 0.9f);
        }

        [Test]
        public void SpawnInsideBuildingShell_IsBlocked()
        {
            var (_, spawner, point) = Setup(true);
            Roof(new Vector3(0f, 4f, 0f), new Vector3(8f, 0.2f, 8f));
            fx.Sync();
            Assert.IsFalse(spawner.TryFindSpawn(point.Feet, out _));
        }

        [Test]
        public void FellOutOfWorld_RespawnsAtPoint()
        {
            var (rig, spawner, _) = Setup(true);
            spawner.Tick(0.016f);
            rig.Player.Teleport(new Vector3(5000f, -30f, 5000f), 0f);
            rig.Player.Tick(1f / 60f);
            Assert.AreEqual(0.36f, rig.Local.y, 0.05f);
            Assert.AreEqual(PlayerSpawner.RespawnMessage, rig.Player.Hud.Message);
        }

        [Test]
        public void MissingSpawnPoint_ReportsWorldMissing()
        {
            fx.Floor();
            fx.Sync();
            var rig = TestRig.Create(fx, Vector3.zero).Init();
            var spawner = rig.Root.AddComponent<PlayerSpawner>();
            spawner.Configure(rig.Player, null);
            spawner.WorldReady = () => true;
            spawner.Begin();
            LogAssert.Expect(LogType.Error, new Regex("PlayerSpawnPoint"));
            spawner.Tick(0.016f);
            Assert.AreEqual(PlayerSpawner.SpawnState.WorldMissing, spawner.State);
        }
    }
}
