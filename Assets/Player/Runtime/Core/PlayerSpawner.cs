using System;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Keeps the player frozen until the generated world exists, then places it at the spawn point; respawns after a fall.</summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        public const string WorldMissingMessage = "Dunia belum di-generate. Buka Tyco World > World Generator.";
        public const string RespawnMessage = "Kamu kembali ke kos.";

        public enum SpawnState { WaitingForWorld, Spawned, WorldMissing }

        [SerializeField] FirstPersonPlayer player;
        [SerializeField] PlayerSpawnPoint spawnPoint;

        public Func<bool> WorldReady;
        public SpawnState State { get; private set; } = SpawnState.WaitingForWorld;
        public float WaitedSeconds { get; private set; }
        public PlayerSpawnPoint SpawnPoint => spawnPoint;

        public void Configure(FirstPersonPlayer target, PlayerSpawnPoint point)
        {
            player = target;
            spawnPoint = point;
        }

        void Start()
        {
            if (Application.isPlaying) Begin();
        }

        void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime);
        }

        void OnDestroy()
        {
            if (player != null) player.FellOutOfWorld -= OnFell;
        }

        public void Begin()
        {
            if (player == null) player = GetComponent<FirstPersonPlayer>();
            player.Initialize();
            player.Locks.Acquire(this, PlayerAbility.All);
            player.Hud.CrosshairVisible = false;
            State = SpawnState.WaitingForWorld;
            WaitedSeconds = 0f;
            player.FellOutOfWorld -= OnFell;
            player.FellOutOfWorld += OnFell;
        }

        public void Tick(float dt)
        {
            if (State != SpawnState.WaitingForWorld) return;
            WaitedSeconds += dt;
            if (IsWorldReady() && TrySpawnNow()) return;
            if (State == SpawnState.WaitingForWorld && WaitedSeconds >= player.Tuning.worldWaitSeconds) Fail(WorldMissingMessage);
        }

        public bool TrySpawnNow()
        {
            if (spawnPoint == null)
            {
                Fail("PlayerSpawnPoint belum di-set di PlayerSpawner. Jalankan Tyco World > Player Setup.");
                return false;
            }
            if (!TryFindSpawn(spawnPoint.Feet, out var feet))
                Debug.LogWarning("[Player] Titik spawn terhalang; pemain tetap ditaruh di " + spawnPoint.name + ".");
            player.Teleport(feet, spawnPoint.Yaw);
            player.Locks.Release(this);
            player.Hud.CrosshairVisible = true;
            player.Hud.ShowMessage(spawnPoint.ArrivalMessage);
            player.LockCursor();
            State = SpawnState.Spawned;
            return true;
        }

        /// <summary>Probes the floor under each nudge candidate and returns the first one where the standing capsule fits.</summary>
        public bool TryFindSpawn(Vector3 desiredFeet, out Vector3 feet)
        {
            Physics.SyncTransforms();
            float r = player.Tuning.body.radius, h = player.Tuning.body.height;
            foreach (var candidate in SpawnNudge.Candidates(desiredFeet))
            {
                // Start 1 m up: a ray that starts inside a blocker (lamp, wall) ignores it and finds the real floor,
                // so the capsule test below rejects the spot instead of spawning on top of the blocker.
                if (!Physics.Raycast(candidate + Vector3.up, Vector3.down, out var hit, 3f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                    continue;
                var f = hit.point + Vector3.up * 0.06f;
                if (Physics.CheckCapsule(f + Vector3.up * (r + 0.05f), f + Vector3.up * (h - r), r, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                    continue;
                feet = f;
                return true;
            }
            feet = desiredFeet;
            return false;
        }

        bool IsWorldReady() => WorldReady != null ? WorldReady() : UnityEngine.Object.FindAnyObjectByType<GeneratedRoot>() != null;

        void Fail(string message)
        {
            State = SpawnState.WorldMissing;
            player.Hud.ShowMessage(message, 3600f);
            Debug.LogError("[Player] " + message);
        }

        void OnFell()
        {
            if (State != SpawnState.Spawned || spawnPoint == null) return;
            TryFindSpawn(spawnPoint.Feet, out var feet);
            player.Teleport(feet, spawnPoint.Yaw);
            player.Hud.ShowMessage(RespawnMessage);
        }
    }
}
