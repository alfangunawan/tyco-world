using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Atomic cache writes for WorldGenData/osm_raw.json and its info file.</summary>
    public static class CacheWriter
    {
        public static void WriteAtomic(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        public static string InfoPath(string rawPath) =>
            Path.Combine(Path.GetDirectoryName(rawPath), Path.GetFileNameWithoutExtension(rawPath) + ".info.json");

        public static void WriteInfo(string rawPath, string endpoint, GeoBounds bounds, int elementCount, DateTime utcNow)
        {
            var info = new JObject
            {
                ["downloadedAtUtc"] = utcNow.ToString("o", CultureInfo.InvariantCulture),
                ["endpoint"] = endpoint,
                ["south"] = bounds.South,
                ["west"] = bounds.West,
                ["north"] = bounds.North,
                ["east"] = bounds.East,
                ["elementCount"] = elementCount
            };
            WriteAtomic(InfoPath(rawPath), info.ToString(Formatting.Indented));
        }

        public static string ReadInfoSummary(string rawPath)
        {
            string infoPath = InfoPath(rawPath);
            if (!File.Exists(rawPath) || !File.Exists(infoPath)) return "Belum ada data. Klik Download Data.";
            try
            {
                var info = OsmParser.Load(File.ReadAllText(infoPath));
                return string.Format(CultureInfo.InvariantCulture, "{0} elemen, diunduh {1} dari {2}",
                    (int)info["elementCount"], (string)info["downloadedAtUtc"], (string)info["endpoint"]);
            }
            catch (Exception ex)
            {
                return "Info cache rusak: " + ex.Message;
            }
        }
    }
}
