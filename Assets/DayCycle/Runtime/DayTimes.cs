namespace TycoWorld.DayCycle
{
    /// <summary>Clock constants from spec #3 §1 (hours are 0–24 game hours).</summary>
    public static class DayTimes
    {
        public const float StartHour = 6.5f;
        public const float DawnStart = 5f;
        public const float DayStart = 6.5f;
        public const float DuskStart = 17.5f;
        public const float NightStart = 19f;
        public const float HorrorStart = 19f;
        public const float HorrorEnd = 5f;
        public const float DebugMorning = 6.5f;
        public const float DebugNight = 19.5f;
        /// <summary>One real minute is one game hour (a day lasts 24 real minutes).</summary>
        public const float RealSecondsPerGameHour = 60f;
    }
}
