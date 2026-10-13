using System.Globalization;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Builds the Overpass QL query from the spec (section 3).</summary>
    public static class OverpassQuery
    {
        const string Body =
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

        public static string Build(GeoBounds bounds, int timeoutSeconds)
        {
            var inv = CultureInfo.InvariantCulture;
            string bbox = string.Format(inv, "{0:0.0000######},{1:0.0000######},{2:0.0000######},{3:0.0000######}",
                bounds.South, bounds.West, bounds.North, bounds.East);
            return string.Format(inv, "[out:json][timeout:{0}][bbox:{1}];\n", timeoutSeconds, bbox) + Body;
        }
    }
}
