using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class SmokeTests
    {
        [Test]
        public void ToAbsolute_ResolvesUnderProjectRoot()
        {
            string abs = WorldGenPaths.ToAbsolute("WorldGenData/osm_raw.json");
            StringAssert.EndsWith(Path.Combine("WorldGenData", "osm_raw.json"), abs);
            StringAssert.StartsWith(WorldGenPaths.ProjectRoot, abs);
        }

        [Test]
        public void Newtonsoft_IsReferenced()
        {
            Assert.AreEqual("ok", (string)JObject.Parse("{\"v\":\"ok\"}")["v"]);
        }
    }
}
