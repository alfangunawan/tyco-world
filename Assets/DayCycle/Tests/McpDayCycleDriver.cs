using System.Globalization;
using System.Text;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    /// <summary>Dev helper for MCP verification in Play mode (not a test). Install() resets its counters per director.</summary>
    public static class McpDayCycleDriver
    {
        static DayCycleDirector hooked;
        static int started, ended;
        static string lastEvent = "";

        static DayCycleDirector Director => Object.FindAnyObjectByType<DayCycleDirector>();

        public static string Install()
        {
            Application.runInBackground = true;
            var d = Director;
            if (d == null) return "NO DIRECTOR";
            if (hooked != d)
            {
                started = 0;
                ended = 0;
                lastEvent = "";
                d.HorrorStarted += () => { started++; lastEvent = HudMessage(d); };
                d.HorrorEnded += () => { ended++; lastEvent = HudMessage(d); };
                hooked = d;
            }
            return Status();
        }

        public static string SetTime(float hours)
        {
            var d = Director;
            if (d == null || !d.IsInitialized) return "NOT READY";
            d.Clock.SetTime(hours);
            return Status();
        }

        public static string Keys(bool jump, bool slower, bool faster, bool clock)
        {
            var debug = Object.FindAnyObjectByType<DayCycleDebug>();
            if (debug == null) return "NO DEBUG";
            bool applied = debug.ApplyKeys(new DebugKeys { JumpTime = jump, SlowerSpeed = slower, FasterSpeed = faster, ToggleClock = clock });
            return "applied=" + applied + " " + Status();
        }

        public static string Status()
        {
            var d = Director;
            if (d == null || !d.IsInitialized) return "NOT READY";
            var s = d.State;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("t=").Append(d.Clock.Display).Append(" day=").Append(d.Clock.Day)
                .Append(" speed=x").Append(d.Clock.SpeedMultiplier.ToString("0", inv))
                .Append(" phase=").Append(d.Phase).Append(" horror=").Append(d.IsHorror)
                .Append(" started=").Append(started).Append(" ended=").Append(ended)
                .Append(" event='").Append(lastEvent).Append('\'')
                .Append(" sunElev=").Append(s.SunElevation.ToString("F1", inv))
                .Append(" sun=").Append(d.Sun != null && d.Sun.enabled)
                .Append(" moon=").Append(d.Moon != null && d.Moon.enabled)
                .Append(" fog=").Append(RenderSettings.fogDensity.ToString("F4", inv))
                .Append(" dayW=").Append(s.DayVolumeWeight.ToString("F2", inv))
                .Append(" horrorW=").Append(s.HorrorWeight.ToString("F2", inv))
                .Append(" lamps=").Append(d.Lamps.Count).Append(" lit=").Append(d.Lamps.LitCount)
                .Append(" flicker=").Append(d.Lamps.RoleCount(LampRole.Flicker))
                .Append(" dead=").Append(d.Lamps.RoleCount(LampRole.Dead));
            if (d.Ambience != null && d.Ambience.Day != null)
                sb.Append(" amb=").Append(d.Ambience.Day.volume.ToString("F2", inv))
                    .Append('/').Append(d.Ambience.Night.volume.ToString("F2", inv))
                    .Append('/').Append(d.Ambience.Drone.volume.ToString("F2", inv));
            var player = d.Player;
            if (player != null && player.IsInitialized)
                sb.Append(" msg='").Append(player.Hud.Message).Append("' clock='")
                    .Append(player.Hud.ClockVisible ? player.Hud.Clock : "").Append('\'');
            sb.Append(" fps=").Append((1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f)).ToString("F0", inv));
            return sb.ToString();
        }

        /// <summary>The HUD message right after an event fired (messages fade after 2.5 s, MCP polls are slower).</summary>
        static string HudMessage(DayCycleDirector d) => d.Player != null && d.Player.IsInitialized ? d.Player.Hud.Message : "";
    }
}
