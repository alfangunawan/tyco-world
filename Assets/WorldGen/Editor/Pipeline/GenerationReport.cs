using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TycoWorld.WorldGen.Authoring
{
    public static class SkipReasons
    {
        public const string BuildingUnclosed = "building:unclosed";
        public const string BuildingMissingNodes = "building:missing-nodes";
        public const string BuildingTooFewPoints = "building:too-few-points";
        public const string BuildingTooSmall = "building:too-small";
        public const string BuildingSelfIntersecting = "building:self-intersecting";
        public const string RelationUnclosedRing = "relation:unclosed-ring";
        public const string RoadMissingNodes = "road:missing-nodes";
        public const string RoadEmptyMesh = "road:empty-mesh";
        public const string WaterMissingNodes = "water:missing-nodes";
        public const string WaterEmptyMesh = "water:empty-mesh";
        public const string AreaUnclosed = "area:unclosed";
        public const string AreaMissingNodes = "area:missing-nodes";
        public const string AreaDegenerate = "area:degenerate";
        public const string AreaTriangulationFailed = "area:triangulation-failed";
        public const string RoofTriangulationFailed = "roof:triangulation-failed";
    }

    public static class CullReasons
    {
        public const string BuildingOutside = "building:outside-bounds";
        public const string RoadOutside = "road:outside-bounds";
        public const string WaterOutside = "water:outside-bounds";
    }

    /// <summary>Counts, skips and timings of one generation run.</summary>
    public sealed class GenerationReport
    {
        public readonly SortedDictionary<string, int> Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, List<long>> Skipped = new SortedDictionary<string, List<long>>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, int> Culled = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, string> ChunkHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public readonly List<string> Warnings = new List<string>();
        public readonly List<KeyValuePair<string, double>> StageMillis = new List<KeyValuePair<string, double>>();
        public int Ignored;
        public int TreesPlaced;
        public int TreesDroppedByCap;
        public int LampsPlaced;
        public double TotalMillis;

        public void Count(string kind, int amount = 1)
        {
            Counts.TryGetValue(kind, out int n);
            Counts[kind] = n + amount;
        }

        public int GetCount(string kind) => Counts.TryGetValue(kind, out int n) ? n : 0;

        public void Skip(string reason, long id)
        {
            if (!Skipped.TryGetValue(reason, out var ids))
            {
                ids = new List<long>();
                Skipped[reason] = ids;
            }
            ids.Add(id);
        }

        public int SkippedTotal(string reasonPrefix = "") =>
            Skipped.Where(kv => kv.Key.StartsWith(reasonPrefix, StringComparison.Ordinal)).Sum(kv => kv.Value.Count);

        public void Cull(string reason)
        {
            Culled.TryGetValue(reason, out int n);
            Culled[reason] = n + 1;
        }

        public int GetCulled(string reason) => Culled.TryGetValue(reason, out int n) ? n : 0;

        public void Warn(string message)
        {
            if (!Warnings.Contains(message)) Warnings.Add(message);
        }

        public void Stage(string name, double millis) => StageMillis.Add(new KeyValuePair<string, double>(name, millis));

        public string Summary()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("Features: " + Join(Counts.Select(kv => kv.Key + "=" + kv.Value.ToString(inv))));
            sb.AppendLine(string.Format(inv, "Trees: {0} (dropped by cap {1}), Lamps: {2}", TreesPlaced, TreesDroppedByCap, LampsPlaced));
            sb.AppendLine("Skipped: " + Join(Skipped.Select(kv =>
                kv.Key + "=" + kv.Value.Count.ToString(inv) + " [" +
                string.Join(",", kv.Value.Take(10).Select(id => id.ToString(inv))) + (kv.Value.Count > 10 ? ",..." : "") + "]")));
            sb.AppendLine("Culled: " + Join(Culled.Select(kv => kv.Key + "=" + kv.Value.ToString(inv))));
            sb.AppendLine("Ignored: " + Ignored.ToString(inv));
            sb.AppendLine("Chunks: " + ChunkHashes.Count.ToString(inv));
            sb.AppendLine("Stages: " + Join(StageMillis.Select(kv => kv.Key + " " + kv.Value.ToString("0", inv) + " ms")) +
                          string.Format(inv, " | total {0:0} ms", TotalMillis));
            foreach (var warning in Warnings) sb.AppendLine("WARNING: " + warning);
            return sb.ToString();
        }

        static string Join(IEnumerable<string> parts)
        {
            string s = string.Join(", ", parts);
            return s.Length == 0 ? "-" : s;
        }
    }
}
