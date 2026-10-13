using System.Collections.Generic;

namespace TycoWorld.WorldGen.Authoring
{
    public static class OsmTags
    {
        public static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

        /// <summary>Returns the tag value, or null when the tag is absent.</summary>
        public static string Tag(this IReadOnlyDictionary<string, string> tags, string key) =>
            tags != null && tags.TryGetValue(key, out var value) ? value : null;
    }

    public sealed class OsmNode
    {
        public readonly long Id;
        public readonly double Lat, Lon;
        public readonly IReadOnlyDictionary<string, string> Tags;

        public OsmNode(long id, double lat, double lon, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            Lat = lat;
            Lon = lon;
            Tags = tags ?? OsmTags.Empty;
        }
    }

    public sealed class OsmWay
    {
        public readonly long Id;
        public readonly List<long> NodeIds;
        public readonly IReadOnlyDictionary<string, string> Tags;

        public OsmWay(long id, List<long> nodeIds, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            NodeIds = nodeIds ?? new List<long>();
            Tags = tags ?? OsmTags.Empty;
        }

        public bool IsClosed => NodeIds.Count >= 4 && NodeIds[0] == NodeIds[NodeIds.Count - 1];
    }

    public sealed class OsmMember
    {
        public readonly OsmElementType Type;
        public readonly long Ref;
        public readonly string Role;

        public OsmMember(OsmElementType type, long reference, string role)
        {
            Type = type;
            Ref = reference;
            Role = role ?? "";
        }
    }

    public sealed class OsmRelation
    {
        public readonly long Id;
        public readonly List<OsmMember> Members;
        public readonly IReadOnlyDictionary<string, string> Tags;

        public OsmRelation(long id, List<OsmMember> members, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            Members = members ?? new List<OsmMember>();
            Tags = tags ?? OsmTags.Empty;
        }
    }

    public sealed class OsmData
    {
        public readonly Dictionary<long, OsmNode> Nodes = new Dictionary<long, OsmNode>();
        public readonly Dictionary<long, OsmWay> Ways = new Dictionary<long, OsmWay>();
        public readonly Dictionary<long, OsmRelation> Relations = new Dictionary<long, OsmRelation>();
    }
}
