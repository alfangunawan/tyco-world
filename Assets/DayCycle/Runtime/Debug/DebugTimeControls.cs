using System.Globalization;

namespace TycoWorld.DayCycle
{
    public struct DebugKeys
    {
        public bool JumpTime, SlowerSpeed, FasterSpeed, ToggleClock;
    }

    /// <summary>Debug time controls: T jumps 06:30 ↔ 19:30, [ / ] cycle the speed (x1, x10, x60), F3 toggles the HUD clock.</summary>
    public sealed class DebugTimeControls
    {
        static readonly float[] Speeds = { 1f, 10f, 60f };
        int speedIndex;

        public float Speed => Speeds[speedIndex];
        public bool ClockVisible { get; private set; }
        public string LastMessage { get; private set; } = "";

        public bool Apply(DebugKeys keys, WorldClock clock)
        {
            LastMessage = "";
            bool changed = false;
            if (keys.JumpTime)
            {
                bool daytime = clock.Hours >= DayTimes.DebugMorning && clock.Hours < DayTimes.DebugNight;
                clock.SetTime(daytime ? DayTimes.DebugNight : DayTimes.DebugMorning);
                LastMessage = "Lompat ke " + clock.Display;
                changed = true;
            }
            if (keys.FasterSpeed || keys.SlowerSpeed)
            {
                int step = keys.FasterSpeed ? 1 : -1;
                speedIndex = (speedIndex + step + Speeds.Length) % Speeds.Length;
                clock.SpeedMultiplier = Speed;
                LastMessage = "Kecepatan waktu x" + Speed.ToString("0", CultureInfo.InvariantCulture);
                changed = true;
            }
            if (keys.ToggleClock)
            {
                ClockVisible = !ClockVisible;
                changed = true;
            }
            return changed;
        }
    }
}
