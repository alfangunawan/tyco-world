using System.Collections.Generic;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>Builds OsmData from local-meter coordinates around the spec origin.</summary>
    internal sealed class TestOsm
    {
        public readonly OsmData Data = new OsmData();
        readonly GeoProjection projection = new GeoProjection(-6.97265, 107.63400);
        long nextNodeId = 9_000_000;

        public static IReadOnlyDictionary<string, string> Tags(params (string Key, string Value)[] tags)
        {
            var d = new Dictionary<string, string>();
            foreach (var (k, v) in tags)
                if (v != null) d[k] = v;
            return d;
        }

        public long Node(Vector2 local, params (string, string)[] tags)
        {
            projection.ToGeo(local, out double lat, out double lon);
            long id = nextNodeId++;
            Data.Nodes[id] = new OsmNode(id, lat, lon, Tags(tags));
            return id;
        }

        public OsmWay Way(long id, IList<Vector2> points, bool closed, params (string, string)[] tags)
        {
            var ids = new List<long>();
            foreach (var p in points) ids.Add(Node(p));
            if (closed) ids.Add(ids[0]);
            var way = new OsmWay(id, ids, Tags(tags));
            Data.Ways[id] = way;
            return way;
        }

        public OsmWay Rect(long id, float x, float z, float w, float d, params (string, string)[] tags) =>
            Way(id, new[] { new Vector2(x, z), new Vector2(x + w, z), new Vector2(x + w, z + d), new Vector2(x, z + d) }, true, tags);

        public OsmRelation Relation(long id, (long wayId, string role)[] members, params (string, string)[] tags)
        {
            var list = new List<OsmMember>();
            foreach (var (wayId, role) in members) list.Add(new OsmMember(TycoWorld.WorldGen.OsmElementType.Way, wayId, role));
            var relation = new OsmRelation(id, list, Tags(tags));
            Data.Relations[id] = relation;
            return relation;
        }
    }
}
