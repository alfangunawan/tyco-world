using System.Globalization;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class OverpassQueryTests
    {
        const string Expected =
            "[out:json][timeout:90][bbox:-6.9850,107.6255,-6.9675,107.6380];\n" +
            "(\n" +
            "  way[\"highway\"];\n" +
            "  way[\"building\"];\n" +
            "  relation[\"building\"];\n" +
            "  way[\"landuse\"];\n" +
            "  relation[\"landuse\"];\n" +
            "  way[\"leisure\"];\n" +
            "  way[\"natural\"];\n" +
            "  relation[\"natural\"=\"water\"];\n" +
            "  way[\"waterway\"];\n" +
            "  way[\"amenity\"~\"^(university|parking|grave_yard)$\"];\n" +
            "  node[\"natural\"=\"tree\"];\n" +
            ");\n" +
            "(._;>;);\n" +
            "out body;\n";

        static readonly GeoBounds SpecBounds = new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380);

        [Test]
        public void Build_MatchesSpecQueryExactly()
        {
            Assert.AreEqual(Expected, OverpassQuery.Build(SpecBounds, 90));
        }

        [Test]
        public void Build_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                Assert.AreEqual(Expected, OverpassQuery.Build(SpecBounds, 90));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
