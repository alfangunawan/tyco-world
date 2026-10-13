using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Rejects Overpass responses that are HTML, broken JSON, runtime errors or empty.</summary>
    public static class OverpassResponseValidator
    {
        public static bool Validate(string body, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(body)) { error = "Respons kosong."; return false; }
            if (body.TrimStart()[0] != '{') { error = "Respons bukan JSON (kemungkinan halaman error HTML)."; return false; }

            JObject root;
            try { root = OsmParser.Load(body); }
            catch (JsonException ex) { error = "JSON rusak: " + ex.Message; return false; }

            var remark = (string)root["remark"];
            if (!string.IsNullOrEmpty(remark) && remark.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                error = "Overpass remark: " + remark;
                return false;
            }
            if (!(root["elements"] is JArray elements) || elements.Count == 0)
            {
                error = "Respons tidak berisi elements.";
                return false;
            }
            return true;
        }

        public static int CountElements(string body) =>
            OsmParser.Load(body)["elements"] is JArray elements ? elements.Count : 0;
    }
}
