using System.Globalization;
using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>Game clock: one real minute is one game hour, times SpeedMultiplier (debug).</summary>
    public sealed class WorldClock
    {
        public WorldClock(float startHours) => SetTime(startHours);

        public float Hours { get; private set; }
        public int Day { get; private set; } = 1;
        public float SpeedMultiplier { get; set; } = 1f;
        public int Hour => Mathf.FloorToInt(Hours);
        public int Minute => Mathf.Clamp(Mathf.FloorToInt((Hours - Hour) * 60f + 1e-3f), 0, 59);
        public string Display => Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + Minute.ToString("00", CultureInfo.InvariantCulture);

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            Hours += dt / DayTimes.RealSecondsPerGameHour * SpeedMultiplier;
            while (Hours >= 24f)
            {
                Hours -= 24f;
                Day++;
            }
        }

        public void SetTime(float hours) => Hours = Mathf.Repeat(hours, 24f);
    }
}
