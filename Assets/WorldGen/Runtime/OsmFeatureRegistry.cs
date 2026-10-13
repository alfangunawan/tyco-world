using System;
using System.Collections.Generic;

namespace TycoWorld.WorldGen
{
    /// <summary>Runtime lookup of generated features by OSM id or display name.</summary>
    public static class OsmFeatureRegistry
    {
        static readonly Dictionary<(OsmElementType, long), OsmFeature> features =
            new Dictionary<(OsmElementType, long), OsmFeature>();

        public static int Count => features.Count;

        public static void Register(OsmFeature feature)
        {
            if (feature == null) return;
            features[(feature.ElementType, feature.OsmId)] = feature;
        }

        public static void Unregister(OsmFeature feature)
        {
            if (feature == null) return;
            var key = (feature.ElementType, feature.OsmId);
            if (features.TryGetValue(key, out var current) && current == feature) features.Remove(key);
        }

        public static bool TryGet(OsmElementType type, long osmId, out OsmFeature feature) =>
            features.TryGetValue((type, osmId), out feature) && feature != null;

        public static bool TryGet(long osmId, out OsmFeature feature) =>
            TryGet(OsmElementType.Way, osmId, out feature)
            || TryGet(OsmElementType.Relation, osmId, out feature)
            || TryGet(OsmElementType.Node, osmId, out feature);

        public static List<OsmFeature> FindByName(string name)
        {
            var result = new List<OsmFeature>();
            if (string.IsNullOrEmpty(name)) return result;
            foreach (var feature in features.Values)
                if (feature != null && string.Equals(feature.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                    result.Add(feature);
            result.Sort((a, b) => a.OsmId.CompareTo(b.OsmId));
            return result;
        }

        public static void Clear() => features.Clear();
    }
}
