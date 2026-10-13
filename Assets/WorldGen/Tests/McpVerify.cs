using System.Collections.Generic;
using System.IO;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>Dev helper for Unity MCP verification scripts, whose compiler cannot reference System.dll types such as SortedDictionary.</summary>
    public static class McpVerify
    {
        public static Dictionary<string, string> LastChunkHashes()
        {
            var result = new Dictionary<string, string>();
            var report = WorldGenerationRunner.LastReport;
            if (report == null) return result;
            foreach (var kv in report.ChunkHashes) result[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>Writes "Chunk_x_z=hash" lines (ordinal order) of the last Generate to <paramref name="path"/>.</summary>
        public static string WriteChunkHashes(string path)
        {
            var report = WorldGenerationRunner.LastReport;
            if (report == null) return "NO REPORT";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var lines = new List<string>();
            foreach (var kv in report.ChunkHashes) lines.Add(kv.Key + "=" + kv.Value);
            File.WriteAllLines(path, lines);
            return "chunks=" + lines.Count;
        }

        /// <summary>Compares the last Generate's chunk hashes with a file written by <see cref="WriteChunkHashes"/>.</summary>
        public static string CompareChunkHashes(string path)
        {
            var report = WorldGenerationRunner.LastReport;
            if (report == null) return "NO REPORT";
            var before = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) before[line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            var diffs = new List<string>();
            foreach (var kv in before)
            {
                if (!report.ChunkHashes.TryGetValue(kv.Key, out var now)) diffs.Add("missing " + kv.Key);
                else if (now != kv.Value) diffs.Add("changed " + kv.Key);
            }
            foreach (var kv in report.ChunkHashes)
                if (!before.ContainsKey(kv.Key)) diffs.Add("new " + kv.Key);
            return diffs.Count == 0 ? "IDENTICAL chunks=" + before.Count : "DIFF " + string.Join(", ", diffs);
        }
    }
}
