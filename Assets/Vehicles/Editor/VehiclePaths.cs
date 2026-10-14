namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Project-relative paths of the vehicle sub-project (spec #5).</summary>
    public static class VehiclePaths
    {
        public const string Root = "Assets/Vehicles";
        public const string ConfigFolder = Root + "/Config";
        public const string TuningAsset = ConfigFolder + "/BikeTuning.asset";
        public const string MaterialsFolder = Root + "/Materials";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string MeshesFolder = PrefabsFolder + "/Meshes";
        public const string MotorbikePrefab = PrefabsFolder + "/Motorbike.prefab";
        public const string AudioFolder = Root + "/Audio";
        public const string AudioSourcesFile = AudioFolder + "/SOURCES.md";
    }
}
