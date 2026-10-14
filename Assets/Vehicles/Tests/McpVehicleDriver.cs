using System.Globalization;
using TycoWorld.Player;
using TycoWorld.Player.Tests;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>
    /// Dev helper for MCP checks of the motorbike in Play mode (not a test). Keeps no static state: the scripted input
    /// lives on the bike (IntentSourceOverride) and the test track is a scene object destroyed by Release or by
    /// leaving Play.
    /// </summary>
    public static class McpVehicleDriver
    {
        public const string TrackName = "VehicleTestTrack";
        /// <summary>Flat test track far from the world (same spot as the EditMode fixture); its wall face is 150 m ahead (+z).</summary>
        public static Vector3 TrackStart => new Vector3(7000f, 1000f, 7000f);
        public const float TrackWallZ = 7150f;

        static Motorbike Bike => Object.FindAnyObjectByType<Motorbike>();
        static FirstPersonPlayer Player => Object.FindAnyObjectByType<FirstPersonPlayer>();

        public static string Status()
        {
            if (!EditorApplication.isPlaying) return "NOT PLAYING";
            var bike = Bike;
            if (bike == null) return "NO BIKE";
            var inv = CultureInfo.InvariantCulture;
            var script = bike.IntentSourceOverride as ScriptedVehicleIntentSource;
            var map = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Vehicle") : null;
            var cam = Camera.main;
            var s = "mounted=" + bike.IsMounted
                    + " kmh=" + bike.SpeedKmh.ToString("F1", inv)
                    + " pos=" + Vec(bike.transform.position)
                    + " yaw=" + bike.Yaw.ToString("F1", inv)
                    + " bumps=" + bike.Bumps
                    + " headlight=" + bike.HeadlightOn
                    + " cam=" + (bike.CameraState != null ? bike.CameraState.Mode.ToString() : "-")
                    + " engine=" + (bike.Sound != null && bike.Sound.EngineRunning)
                    + " level=" + (bike.Sound != null ? bike.Sound.Level.ToString("F2", inv) : "-")
                    + " script=" + (script == null ? "none" : script.IsDone ? "done" : "busy")
                    + " timeScale=" + Time.timeScale.ToString("F2", inv)
                    + " vehicleMap=" + (map != null ? map.enabled.ToString() : "-")
                    + " camDist=" + (cam != null
                        ? Vector3.Distance(cam.transform.position, bike.transform.position + Vector3.up * bike.Tuning.lookHeight).ToString("F2", inv)
                        : "-");
            var player = Player;
            if (player == null || !player.IsInitialized) return s + " player=-";
            return s + " seated=" + player.IsSeated
                     + " lookFree=" + player.Locks.Allows(PlayerAbility.Look)
                     + " moveFree=" + player.Locks.Allows(PlayerAbility.Move)
                     + " feet=" + Vec(player.transform.position)
                     + " hudVehicle=" + player.Hud.VehicleVisible
                     + " hudKmh=" + player.Hud.VehicleSpeedKmh
                     + " hudExit=" + player.Hud.VehicleCanExit
                     + " crosshair=" + player.Hud.CrosshairVisible
                     + " msg=\"" + player.Hud.Message + "\"";
        }

        /// <summary>Gives the bike a scripted input source (until Release).</summary>
        public static string Install()
        {
            var bike = Bike;
            if (bike == null) return "NO BIKE";
            bike.SetIntentSource(new ScriptedVehicleIntentSource());
            return Status();
        }

        /// <summary>Back to keyboard/gamepad input; removes the test track.</summary>
        public static string Release()
        {
            var bike = Bike;
            if (bike != null) bike.SetIntentSource(null);
            var track = GameObject.Find(TrackName);
            if (track != null) Object.Destroy(track);
            return Status();
        }

        public static string Hold(float throttle, float steer, bool brake, float seconds)
        {
            var script = Script();
            if (script == null) return "NOT INSTALLED";
            script.Hold(new VehicleIntent { Throttle = throttle, Steer = steer, Brake = brake }, seconds);
            return Status();
        }

        public static string Press(bool horn, bool headlight, bool camera, bool exit)
        {
            var script = Script();
            if (script == null) return "NOT INSTALLED";
            script.Press(new VehicleIntent { Horn = horn, ToggleHeadlight = headlight, ToggleCamera = camera, Exit = exit });
            return Status();
        }

        /// <summary>Puts the player where they would get off (left, else right), facing the bike; then McpPlayerDriver.Face + Press(interact).</summary>
        public static string StandBeside()
        {
            var bike = Bike;
            var player = Player;
            if (bike == null || player == null || !player.IsInitialized) return "NO BIKE OR PLAYER";
            var stance = player.Tuning.body;
            if (!DismountFinder.TryFind(bike.transform, bike.Tuning.dismountSideOffset, stance.radius, stance.height, out var feet))
                return "NO ROOM";
            var toBike = bike.transform.position - feet;
            player.Teleport(feet, Mathf.Atan2(toBike.x, toBike.z) * Mathf.Rad2Deg);
            return "feet=" + Vec(feet) + " face=" + Vec(bike.transform.position + Vector3.up * 0.8f);
        }

        /// <summary>Opens the kos gate as if the player standing at the bike pressed E on it.</summary>
        public static string OpenGate()
        {
            var gate = Object.FindAnyObjectByType<GateInteractable>();
            var player = Player;
            if (gate == null || player == null) return "NO GATE OR PLAYER";
            bool done = gate.Interact(new InteractionContext(player, player.Hud, player.View.position));
            return "gate interact=" + done + " state=" + gate.State;
        }

        /// <summary>Turns the bike on the spot towards the gate (a verification shortcut) and returns the gate position.</summary>
        public static string AimAtGate()
        {
            var gate = Object.FindAnyObjectByType<GateInteractable>();
            var bike = Bike;
            if (gate == null || bike == null) return "NO GATE OR BIKE";
            var to = gate.transform.position - bike.transform.position;
            bike.Teleport(bike.transform.position, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            return "gate=" + Vec(gate.transform.position) + " dist=" + new Vector2(to.x, to.z).magnitude.ToString("F2", CultureInfo.InvariantCulture) + " " + Status();
        }

        /// <summary>Drops the bike (and its rider) on the ground at x, z.</summary>
        public static string PlaceAt(float x, float z, float yaw)
        {
            var bike = Bike;
            if (bike == null) return "NO BIKE";
            if (!Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out var hit, 600f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                return "NO GROUND";
            bike.Teleport(hit.point, yaw);
            return Status();
        }

        /// <summary>40 x 400 m floor with a wall whose face is at z = TrackWallZ; puts the bike at TrackStart facing +z.</summary>
        public static string TestTrack()
        {
            var bike = Bike;
            if (bike == null) return "NO BIKE";
            if (GameObject.Find(TrackName) == null)
            {
                var track = new GameObject(TrackName);
                var start = TrackStart;
                Block(track.transform, "Floor", start + new Vector3(0f, -0.5f, 150f), new Vector3(40f, 1f, 400f));
                Block(track.transform, "Wall", new Vector3(start.x, start.y + 1.5f, TrackWallZ + 0.25f), new Vector3(20f, 3f, 0.5f));
                Physics.SyncTransforms();
            }
            bike.Teleport(TrackStart, 0f);
            return Status();
        }

        public static string Shot(string path) => McpRender.SavePng(Camera.main, path, 1280, 720);

        public static string Luminance() => McpRender.CenterLuminance(Camera.main).ToString("F4", CultureInfo.InvariantCulture);

        static ScriptedVehicleIntentSource Script()
        {
            var bike = Bike;
            return bike != null ? bike.IntentSourceOverride as ScriptedVehicleIntentSource : null;
        }

        static void Block(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
        }

        static string Vec(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2},{2:F2})", v.x, v.y, v.z);
    }
}
