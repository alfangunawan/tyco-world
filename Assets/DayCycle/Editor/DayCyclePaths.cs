namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>Project-relative paths of the day/night cycle sub-project.</summary>
    public static class DayCyclePaths
    {
        public const string Root = "Assets/DayCycle";
        public const string ConfigFolder = Root + "/Config";
        public const string ProfileAsset = ConfigFolder + "/DayCycleProfile.asset";
        public const string DayVolumeAsset = ConfigFolder + "/Volume_Day.asset";
        public const string HorrorVolumeAsset = ConfigFolder + "/Volume_Horror.asset";
        public const string SkyboxMaterial = ConfigFolder + "/Sky_Procedural.mat";
        public const string AudioFolder = Root + "/Audio";
        public const string DayAmbienceClip = AudioFolder + "/ambience_day.wav";
        public const string NightAmbienceClip = AudioFolder + "/ambience_night_crickets.mp3";
        public const string HorrorDroneClip = AudioFolder + "/ambience_horror_drone.wav";
        public const string AudioSourcesFile = AudioFolder + "/SOURCES.md";
    }
}
