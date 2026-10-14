using System.IO;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Project-relative paths used by the world generator.</summary>
    public static class WorldGenPaths
    {
        public const string Root = "Assets/WorldGen";
        public const string ConfigFolder = Root + "/Config";
        public const string SettingsAsset = ConfigFolder + "/WorldGenSettings.asset";
        public const string VolumeProfileAsset = ConfigFolder + "/Preview_NightVHS.asset";
        public const string GeneratedFolder = Root + "/Generated";
        public const string GeneratedMeshesFolder = GeneratedFolder + "/Meshes";
        public const string GeneratedSceneName = "World_Generated";
        public const string GeneratedScenePath = GeneratedFolder + "/" + GeneratedSceneName + ".unity";
        public const string WorldScenePath = "Assets/Scenes/World.unity";
        public const string MaterialsFolder = Root + "/Materials";
        public const string TexturesFolder = Root + "/Textures";
        public const string Cc0TexturesFolder = TexturesFolder + "/CC0";
        public const string FacadeTexturesFolder = TexturesFolder + "/Facade";
        public const string StreetTexturesFolder = TexturesFolder + "/Street";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string FixturesFolder = Root + "/Tests/Fixtures";

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static string ToAbsolute(string projectRelativePath) =>
            Path.GetFullPath(Path.Combine(ProjectRoot, projectRelativePath));
    }
}
