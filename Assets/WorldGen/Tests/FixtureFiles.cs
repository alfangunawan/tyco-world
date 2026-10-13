using System.IO;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    internal static class FixtureFiles
    {
        public static string Read(string fileName) =>
            File.ReadAllText(WorldGenPaths.ToAbsolute(WorldGenPaths.FixturesFolder + "/" + fileName));
    }
}
