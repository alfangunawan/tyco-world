using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Parses Overpass "out body" JSON into OsmData.</summary>
    public static class OsmParser
    {
        public static OsmData Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var root = Load(json);
            if (!(root["elements"] is JArray elements)) throw new FormatException("Overpass JSON tidak punya array 'elements'.");

            var data = new OsmData();
            foreach (var token in elements)
            {
                if (!(token is JObject e) || e["id"] == null) continue;
                long id = e["id"].Value<long>();
                var tags = ReadTags(e["tags"] as JObject);
                switch ((string)e["type"])
                {
                    case "node":
                        if (e["lat"] == null || e["lon"] == null) continue;
                        data.Nodes[id] = new OsmNode(id, e["lat"].Value<double>(), e["lon"].Value<double>(), tags);
                        break;
                    case "way":
                        var nodeIds = new List<long>();
                        if (e["nodes"] is JArray nodes)
                            foreach (var n in nodes) nodeIds.Add(n.Value<long>());
                        data.Ways[id] = new OsmWay(id, nodeIds, tags);
                        break;
                    case "relation":
                        var members = new List<OsmMember>();
                        if (e["members"] is JArray list)
                            foreach (var m in list)
                                if (m is JObject member && member["ref"] != null && TryParseType((string)member["type"], out var type))
                                    members.Add(new OsmMember(type, member["ref"].Value<long>(), (string)member["role"]));
                        data.Relations[id] = new OsmRelation(id, members, tags);
                        break;
                }
            }
            return data;
        }

        /// <summary>Loads JSON without converting date-like strings (tag values must stay verbatim).</summary>
        internal static JObject Load(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json))
                   {
                       DateParseHandling = DateParseHandling.None,
                       FloatParseHandling = FloatParseHandling.Double
                   })
            {
                return JObject.Load(reader);
            }
        }

        static IReadOnlyDictionary<string, string> ReadTags(JObject tags)
        {
            if (tags == null || !tags.HasValues) return OsmTags.Empty;
            var result = new Dictionary<string, string>();
            foreach (var property in tags.Properties())
                result[property.Name] = property.Value.Type == JTokenType.String
                    ? (string)property.Value
                    : property.Value.ToString(Formatting.None);
            return result;
        }

        static bool TryParseType(string text, out OsmElementType type)
        {
            switch (text)
            {
                case "node": type = OsmElementType.Node; return true;
                case "way": type = OsmElementType.Way; return true;
                case "relation": type = OsmElementType.Relation; return true;
                default: type = OsmElementType.Node; return false;
            }
        }
    }
}
