using System.Globalization;
using System.Text;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.GameSettings.Tests
{
    /// <summary>
    /// Dev helper for MCP performance measurements in Play mode (not a test). Keeps no static state: the sampler and the
    /// player lock live on the "PerfProbe" GameObject. Viewpoints: "kos" (yard, facing the lane), "road" (Jalan
    /// Sukabirus, looking north-east along it) and "high" (30 m above the road, facing the campus).
    /// Place turns Application.runInBackground on so sampling continues while the editor is unfocused; Release turns it
    /// off again, because in the editor it mirrors PlayerSettings "Run In Background" (would be saved into
    /// ProjectSettings.asset), and the project's committed value is off (runInBackground: 0).
    /// </summary>
    public static class McpPerfProbe
    {
        public const string ProbeName = "PerfProbe";

        public static string Status()
        {
            if (!EditorApplication.isPlaying) return "NOT PLAYING";
            var player = Object.FindAnyObjectByType<FirstPersonPlayer>();
            if (player == null || !player.IsInitialized) return "NO PLAYER";
            if (Object.FindAnyObjectByType<GeneratedRoot>() == null) return "NO WORLD";
            return "READY t=" + Clock() + " pos=" + Vec(player.transform.position) + " paused=" + EditorApplication.isPaused;
        }

        public static string Place(string viewpoint)
        {
            Application.runInBackground = true;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            var player = Object.FindAnyObjectByType<FirstPersonPlayer>();
            if (player == null || !player.IsInitialized) return "NO PLAYER";
            float x, z, yaw, lift = 0f;
            switch (viewpoint)
            {
                case "kos": x = -321.3f; z = -612.6f; yaw = 180f; break;
                case "road": x = -287.7f; z = -633.3f; yaw = 57f; break;
                case "high": x = -287.7f; z = -633.3f; yaw = 24f; lift = 30f; break;
                default: return "UNKNOWN VIEWPOINT " + viewpoint;
            }
            if (!TryGround(x, z, out var ground)) return "NO GROUND at " + viewpoint;
            // Locking Move turns the CharacterController off, so the player hangs at the lifted viewpoint without falling.
            player.Locks.Acquire(Sampler(), PlayerAbility.All);
            player.Teleport(ground + Vector3.up * lift, yaw);
            return "placed " + viewpoint + " feet=" + Vec(player.transform.position) + " yaw=" + yaw.ToString("F0", CultureInfo.InvariantCulture);
        }

        public static string SetTime(float hours)
        {
            var director = Object.FindAnyObjectByType<DayCycleDirector>();
            if (director == null || !director.IsInitialized) return "NO DIRECTOR";
            director.Clock.SetTime(hours);
            return "t=" + director.Clock.Display;
        }

        public static string Begin(float seconds)
        {
            Sampler().Begin(seconds);
            return "sampling " + seconds.ToString("F1", CultureInfo.InvariantCulture) + " s after a 1 s warm-up";
        }

        public static string Report(string label)
        {
            var sampler = Sampler();
            if (!sampler.IsDone) return "PENDING";
            var s = sampler.Stats;
            var inv = CultureInfo.InvariantCulture;
            int lights = 0;
            foreach (var light in Object.FindObjectsByType<Light>())
                if (light.enabled) lights++;
            int renderers = 0, total = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                total++;
                if (r.enabled) renderers++;
            }
            var cam = Camera.main;
            var sb = new StringBuilder();
            sb.Append(label)
                .Append(" fps=").Append(s.AverageFps.ToString("F1", inv))
                .Append(" ms=").Append(s.AverageMs.ToString("F2", inv))
                .Append(" worst=").Append(s.WorstMs.ToString("F1", inv))
                .Append(" batches=").Append(UnityStats.drawCalls)
                .Append(" setpass=").Append(UnityStats.setPassCalls)
                .Append(" tris=").Append(UnityStats.triangles)
                .Append(" lights=").Append(lights)
                .Append(" renderers=").Append(renderers).Append('/').Append(total)
                .Append(" far=").Append(cam != null ? cam.farClipPlane.ToString("F0", inv) : "-")
                .Append(" fog=").Append(RenderSettings.fogDensity.ToString("F4", inv))
                .Append(" t=").Append(Clock());
            return sb.ToString();
        }

        public static string Release()
        {
            Application.runInBackground = false;
            var go = GameObject.Find(ProbeName);
            if (go == null) return "NO PROBE";
            var player = Object.FindAnyObjectByType<FirstPersonPlayer>();
            if (player != null && player.IsInitialized) player.Locks.Release(go.GetComponent<FrameStatsSampler>());
            Object.Destroy(go);
            return "released";
        }

        static FrameStatsSampler Sampler()
        {
            var go = GameObject.Find(ProbeName);
            if (go == null) go = new GameObject(ProbeName);
            var sampler = go.GetComponent<FrameStatsSampler>();
            if (sampler == null) sampler = go.AddComponent<FrameStatsSampler>();
            return sampler;
        }

        /// <summary>The highest walkable surface below 5 m under (x, z): a GroundSurface or the Ground quad, never a roof or tree.</summary>
        static bool TryGround(float x, float z, out Vector3 point)
        {
            point = default;
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore))
            {
                bool walkable = hit.collider.GetComponentInParent<GroundSurface>() != null || hit.collider.name == "Ground";
                if (!walkable || hit.point.y > 5f || hit.point.y <= best) continue;
                best = hit.point.y;
                point = hit.point;
            }
            return best > float.NegativeInfinity;
        }

        static string Clock()
        {
            var director = Object.FindAnyObjectByType<DayCycleDirector>();
            return director != null && director.IsInitialized ? director.Clock.Display : "-";
        }

        static string Vec(Vector3 v)
        {
            var inv = CultureInfo.InvariantCulture;
            return "(" + v.x.ToString("F1", inv) + "," + v.y.ToString("F1", inv) + "," + v.z.ToString("F1", inv) + ")";
        }
    }
}
