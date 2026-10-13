using System.Collections.Generic;
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
    }
}
