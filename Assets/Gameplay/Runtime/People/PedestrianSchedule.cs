namespace TycoWorld.Gameplay
{
    /// <summary>How many people are out around the player at a given hour (spec #6 §2: busy by day, empty at night).</summary>
    public static class PedestrianSchedule
    {
        public const int Day = 26;
        public const int Evening = 14;
        public const int Night = 3;
        public const int Dawn = 6;

        public static int TargetCount(float hours)
        {
            float h = ((hours % 24f) + 24f) % 24f;
            if (h >= 6.5f && h < 18f) return Day;
            if (h >= 18f && h < 21f) return Evening;
            if (h >= 5f && h < 6.5f) return Dawn;
            return Night;
        }

        /// <summary>The pale figures by the road: only deep in the horror night.</summary>
        public static int SosokCount(float hours)
        {
            float h = ((hours % 24f) + 24f) % 24f;
            return h >= 22f || h < 4f ? 1 : 0;
        }
    }
}
