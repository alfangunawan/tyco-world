using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Edge profile and edge width of a road, as seen by props placed along one OSM way.</summary>
    public interface IEdgeLookup
    {
        /// <summary>Profile of the road side that <paramref name="sideDirection"/> points to, near centreline point p.</summary>
        EdgeProfile ProfileAt(long roadId, Vector2 p, Vector2 sideDirection);

        /// <summary>Widest edge on either side of the road; 0 when it has none.</summary>
        float MaxEdgeWidth(long roadId);
    }

    public sealed class ChainMember
    {
        public RoadFeature Road;
        public float S0, S1;
    }

    /// <summary>OSM way pieces of one kind of road joined end to end (spec street-frontage §5).</summary>
    public sealed class RoadChain
    {
        public int Index;
        public long Id;
        /// <summary>Stable seed: smallest way id plus the quantised first point (pieces of one clipped way differ).</summary>
        public long Key;
        public string Highway = "";
        public int Rank;
        public float Width;
        public float Y;
        public EdgeProfile BaseProfile;
        public bool CampusCandidate;
        public float MaxEdgeWidth;
        public List<Vector2> Points = new List<Vector2>();
        public float[] Cum = new float[0];
        public readonly List<ChainMember> Members = new List<ChainMember>();
        /// <summary>Profile per sample (s = i × step) on the left (+1) and right (-1) side, after the campus rule and smoothing.</summary>
        public EdgeProfile[] Left = new EdgeProfile[0];
        public EdgeProfile[] Right = new EdgeProfile[0];

        public float HalfWidth => Width * 0.5f;
        public float Length => Cum.Length > 0 ? Cum[Cum.Length - 1] : 0f;
        public EdgeProfile[] Profiles(int side) => side > 0 ? Left : Right;

        /// <summary>Arc length of sample i; the last sample sits on the chain end.</summary>
        public float SampleS(int i, float step) => Mathf.Min(i * step, Length);

        public EdgeProfile ProfileAt(int side, float s, float step)
        {
            var p = Profiles(side);
            return p.Length == 0 ? EdgeProfile.None : p[Mathf.Clamp(Mathf.RoundToInt(s / step), 0, p.Length - 1)];
        }

        public Vector2 PointAt(float s, out Vector2 tangent) => Polyline2D.PointAt(Points, Cum, s, out tangent);

        /// <summary>Point at arc length s, <paramref name="offset"/> metres to the given side (+1 left) of the centreline.</summary>
        public Vector2 SidePoint(float s, int side, float offset)
        {
            var c = PointAt(s, out var t);
            return c + Polyline2D.LeftNormal(t) * (side * offset);
        }
    }

    /// <summary>Road chains, per-side edge profiles and a segment grid for distance queries (spec street-frontage §4–§5).</summary>
    public sealed class RoadNetwork : IEdgeLookup
    {
        public static readonly int[] Sides = { 1, -1 };

        public readonly List<RoadChain> Chains = new List<RoadChain>();
        public float MaxHalfWidth { get; private set; }
        /// <summary>Largest half width + edge width of any chain, plus 1 m: a safe query radius for band tests.</summary>
        public float MaxReach { get; private set; }

        readonly RoadEdgeSettings settings;
        readonly Dictionary<long, List<RoadChain>> chainsOfRoad = new Dictionary<long, List<RoadChain>>();
        readonly List<Seg> segs = new List<Seg>();
        readonly SpatialGrid<int> grid = new SpatialGrid<int>(20f);
        readonly HashSet<int> hits = new HashSet<int>();

        readonly struct Seg
        {
            public readonly int Chain;
            public readonly Vector2 A, B;
            public readonly float S0, S1;

            public Seg(int chain, Vector2 a, Vector2 b, float s0, float s1)
            {
                Chain = chain;
                A = a;
                B = b;
                S0 = s0;
                S1 = s1;
            }
        }

        sealed class Unit
        {
            public RoadFeature Road;
            public List<Vector2> Points;
            public string MergeKey;
            public int StartLink = -1, EndLink = -1;
            public bool StartLinkAtStart, EndLinkAtStart;
        }

        public RoadNetwork(IReadOnlyList<RoadFeature> roads, IReadOnlyList<Polygon> campus, RoadEdgeSettings settings)
        {
            this.settings = settings;
            BuildChains(roads);
            foreach (var chain in Chains) IndexSegments(chain);
            foreach (var chain in Chains)
            {
                chain.Left = ComputeProfiles(chain, 1, campus);
                chain.Right = ComputeProfiles(chain, -1, campus);
                float max = 0f;
                foreach (var p in chain.Left) max = Mathf.Max(max, settings.Width(p));
                foreach (var p in chain.Right) max = Mathf.Max(max, settings.Width(p));
                chain.MaxEdgeWidth = max;
                MaxHalfWidth = Mathf.Max(MaxHalfWidth, chain.HalfWidth);
                MaxReach = Mathf.Max(MaxReach, chain.HalfWidth + max + 1f);
            }
        }

        public static EdgeProfile BaseProfileOf(RoadFeature road, RoadEdgeSettings s)
        {
            string highway = road.Highway ?? "";
            if (Contains(s.sidewalkHighways, highway)) return EdgeProfile.Sidewalk;
            if (!Contains(s.gutterHighways, highway)) return EdgeProfile.None;
            if (highway == "service" && road.Tags != null && road.Tags.TryGetValue("service", out var value) && Contains(s.noEdgeServiceValues, value))
                return EdgeProfile.None;
            return EdgeProfile.Gutter;
        }

        public static bool Contains(string[] values, string value)
        {
            if (values == null) return false;
            foreach (var v in values)
                if (v == value) return true;
            return false;
        }

        /// <summary>Junction priority: higher rank, then wider, then smaller id, then lower index (spec street-frontage §5).</summary>
        public static bool Beats(RoadChain a, RoadChain b)
        {
            if (a.Rank != b.Rank) return a.Rank > b.Rank;
            if (Mathf.Abs(a.Width - b.Width) > 1e-3f) return a.Width > b.Width;
            if (a.Id != b.Id) return a.Id < b.Id;
            return a.Index < b.Index;
        }

        /// <summary>Closest distance from p to each chain (except <paramref name="except"/>) whose centreline is within radius.</summary>
        public void Nearby(Vector2 p, float radius, RoadChain except, Dictionary<RoadChain, float> result)
        {
            result.Clear();
            hits.Clear();
            grid.Query(Around(p, radius), hits);
            foreach (int i in hits)
            {
                var seg = segs[i];
                var chain = Chains[seg.Chain];
                if (chain == except) continue;
                float d = Polygon2D.DistanceToSegment(p, seg.A, seg.B);
                if (d > radius) continue;
                if (!result.TryGetValue(chain, out float old) || d < old) result[chain] = d;
            }
        }

        /// <summary>
        /// Distance from p to the chain's own centreline, ignoring segments whose arc range overlaps [sMin, sMax] (the stretch
        /// p was offset from). Returns <paramref name="radius"/> when nothing is closer.
        /// </summary>
        public float DistanceToChain(RoadChain chain, Vector2 p, float radius, float sMin, float sMax)
        {
            hits.Clear();
            grid.Query(Around(p, radius), hits);
            float best = radius;
            foreach (int i in hits)
            {
                var seg = segs[i];
                if (seg.Chain != chain.Index || (seg.S1 > sMin && seg.S0 < sMax)) continue;
                best = Mathf.Min(best, Polygon2D.DistanceToSegment(p, seg.A, seg.B));
            }
            return best;
        }

        public EdgeProfile ProfileAt(long roadId, Vector2 p, Vector2 sideDirection)
        {
            var chain = Closest(roadId, p, out float s);
            if (chain == null) return EdgeProfile.None;
            chain.PointAt(s, out var t);
            int side = Vector2.Dot(sideDirection, Polyline2D.LeftNormal(t)) >= 0f ? 1 : -1;
            return chain.ProfileAt(side, s, settings.sampleStep);
        }

        public float MaxEdgeWidth(long roadId)
        {
            if (!chainsOfRoad.TryGetValue(roadId, out var list)) return 0f;
            float max = 0f;
            foreach (var c in list) max = Mathf.Max(max, c.MaxEdgeWidth);
            return max;
        }

        /// <summary>Runs shorter than minSamples take the profile of their longer neighbour (spec street-frontage §4).</summary>
        public static void Smooth(EdgeProfile[] profiles, int minSamples)
        {
            while (true)
            {
                var runs = Runs(profiles);
                if (runs.Count < 2) return;
                int pick = -1;
                for (int r = 0; r < runs.Count; r++)
                    if (runs[r].count < minSamples && (pick < 0 || runs[r].count < runs[pick].count)) pick = r;
                if (pick < 0) return;
                int neighbour = pick == 0 ? 1
                    : pick == runs.Count - 1 ? pick - 1
                    : runs[pick - 1].count >= runs[pick + 1].count ? pick - 1 : pick + 1;
                for (int i = runs[pick].start; i < runs[pick].start + runs[pick].count; i++) profiles[i] = runs[neighbour].profile;
            }
        }

        static List<(EdgeProfile profile, int start, int count)> Runs(EdgeProfile[] profiles)
        {
            var runs = new List<(EdgeProfile profile, int start, int count)>();
            int start = 0;
            for (int i = 1; i <= profiles.Length; i++)
            {
                if (i < profiles.Length && profiles[i] == profiles[start]) continue;
                runs.Add((profiles[start], start, i - start));
                start = i;
            }
            return runs;
        }

        RoadChain Closest(long roadId, Vector2 p, out float s)
        {
            s = 0f;
            if (!chainsOfRoad.TryGetValue(roadId, out var list)) return null;
            RoadChain best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var c in list)
            {
                float cs = Polyline2D.Project(c.Points, c.Cum, p, out float d);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = c;
                s = cs;
            }
            return best;
        }

        EdgeProfile[] ComputeProfiles(RoadChain chain, int side, IReadOnlyList<Polygon> campus)
        {
            float step = settings.sampleStep;
            int n = Mathf.Max(1, Mathf.CeilToInt(chain.Length / step - 1e-4f) + 1);
            var result = new EdgeProfile[n];
            float probe = chain.HalfWidth + settings.sidewalkWidth * 0.5f;
            float cap = settings.campusSidewalkDistance + 1f;
            for (int i = 0; i < n; i++)
            {
                var profile = chain.BaseProfile;
                if (profile == EdgeProfile.Gutter && chain.CampusCandidate && campus != null && campus.Count > 0)
                {
                    float s = chain.SampleS(i, step);
                    float here = CampusDistance(campus, chain.SidePoint(s, side, probe), cap);
                    float there = CampusDistance(campus, chain.SidePoint(s, -side, probe), cap);
                    bool facing = here <= settings.campusSidewalkDistance && (here < there - 0.5f || (here == 0f && there == 0f));
                    if (facing) profile = EdgeProfile.Sidewalk;
                }
                result[i] = profile;
            }
            Smooth(result, Mathf.RoundToInt(settings.minProfileRun / step));
            return result;
        }

        static float CampusDistance(IReadOnlyList<Polygon> polygons, Vector2 p, float cap)
        {
            float best = cap;
            foreach (var poly in polygons)
            {
                var b = poly.Bounds;
                if (p.x < b.xMin - cap || p.x > b.xMax + cap || p.y < b.yMin - cap || p.y > b.yMax + cap) continue;
                if (poly.Contains(p)) return 0f;
                best = Mathf.Min(best, poly.DistanceToBoundary(p));
            }
            return best;
        }

        void BuildChains(IReadOnlyList<RoadFeature> roads)
        {
            var units = new List<Unit>();
            foreach (var road in roads)
                foreach (var piece in road.Pieces)
                {
                    var pts = Dedup(piece.Points);
                    if (pts.Count < 2) continue;
                    units.Add(new Unit { Road = road, Points = pts, MergeKey = MergeKeyOf(road) });
                }

            var vertexUse = new Dictionary<long, int>();
            var endpoints = new Dictionary<long, List<(int unit, bool atStart)>>();
            for (int u = 0; u < units.Count; u++)
            {
                var pts = units[u].Points;
                foreach (var p in pts)
                {
                    long k = Quantize(p);
                    vertexUse[k] = vertexUse.TryGetValue(k, out int c) ? c + 1 : 1;
                }
                AddEndpoint(endpoints, Quantize(pts[0]), u, true);
                AddEndpoint(endpoints, Quantize(pts[pts.Count - 1]), u, false);
            }
            // Each link is symmetric and every unit end sits under exactly one key, so iteration order does not matter.
            foreach (var kv in endpoints)
            {
                var ends = kv.Value;
                if (ends.Count != 2 || vertexUse[kv.Key] != 2) continue;
                var (a, aStart) = ends[0];
                var (b, bStart) = ends[1];
                if (a == b || units[a].MergeKey != units[b].MergeKey) continue;
                Link(units[a], aStart, b, bStart);
                Link(units[b], bStart, a, aStart);
            }

            var visited = new bool[units.Count];
            for (int u = 0; u < units.Count; u++)
            {
                if (visited[u] || (units[u].StartLink >= 0 && units[u].EndLink >= 0)) continue;
                Walk(units, visited, u, units[u].StartLink < 0);
            }
            for (int u = 0; u < units.Count; u++)
                if (!visited[u]) Walk(units, visited, u, true);
        }

        void Walk(List<Unit> units, bool[] visited, int first, bool forward)
        {
            var chain = new RoadChain { Index = Chains.Count };
            var ranges = new List<(RoadFeature road, int start, int end)>();
            int u = first;
            bool fwd = forward;
            while (u >= 0 && !visited[u])
            {
                visited[u] = true;
                var unit = units[u];
                int start = chain.Points.Count == 0 ? 0 : chain.Points.Count - 1;
                for (int i = 0; i < unit.Points.Count; i++)
                {
                    if (i == 0 && chain.Points.Count > 0) continue;
                    chain.Points.Add(fwd ? unit.Points[i] : unit.Points[unit.Points.Count - 1 - i]);
                }
                ranges.Add((unit.Road, start, chain.Points.Count - 1));
                bool enterAtStart = fwd ? unit.EndLinkAtStart : unit.StartLinkAtStart;
                u = fwd ? unit.EndLink : unit.StartLink;
                fwd = enterAtStart;
            }
            chain.Cum = Polyline2D.Cumulative(chain.Points);
            var head = ranges[0].road;
            chain.Highway = head.Highway ?? "";
            chain.Rank = head.Rule != null ? head.Rule.rank : 0;
            chain.Width = head.Width;
            chain.Y = head.Y;
            chain.BaseProfile = BaseProfileOf(head, settings);
            chain.CampusCandidate = Contains(settings.campusSidewalkHighways, chain.Highway);
            long id = long.MaxValue;
            foreach (var (road, start, end) in ranges)
            {
                chain.Members.Add(new ChainMember { Road = road, S0 = chain.Cum[start], S1 = chain.Cum[end] });
                if (road.Id < id) id = road.Id;
                if (!chainsOfRoad.TryGetValue(road.Id, out var list)) chainsOfRoad[road.Id] = list = new List<RoadChain>();
                if (!list.Contains(chain)) list.Add(chain);
            }
            chain.Id = id;
            chain.Key = unchecked((long)SplitMix64.Hash(id, "chain:" + Quantize(chain.Points[0]).ToString(CultureInfo.InvariantCulture)));
            Chains.Add(chain);
        }

        void IndexSegments(RoadChain chain)
        {
            var pts = chain.Points;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                grid.Insert(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)), segs.Count);
                segs.Add(new Seg(chain.Index, a, b, chain.Cum[i], chain.Cum[i + 1]));
            }
        }

        string MergeKeyOf(RoadFeature road) =>
            (road.Highway ?? "") + "|" + Mathf.RoundToInt(road.Width * 100f).ToString(CultureInfo.InvariantCulture) + "|" + BaseProfileOf(road, settings);

        static void AddEndpoint(Dictionary<long, List<(int unit, bool atStart)>> map, long key, int unit, bool atStart)
        {
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<(int unit, bool atStart)>();
            list.Add((unit, atStart));
        }

        static void Link(Unit unit, bool atStart, int other, bool otherAtStart)
        {
            if (atStart)
            {
                unit.StartLink = other;
                unit.StartLinkAtStart = otherAtStart;
            }
            else
            {
                unit.EndLink = other;
                unit.EndLinkAtStart = otherAtStart;
            }
        }

        static List<Vector2> Dedup(IReadOnlyList<Vector2> points)
        {
            var result = new List<Vector2>(points.Count);
            foreach (var p in points)
                if (result.Count == 0 || (p - result[result.Count - 1]).sqrMagnitude > 1e-4f) result.Add(p);
            return result;
        }

        static long Quantize(Vector2 p) => ((long)Mathf.RoundToInt(p.x * 100f) << 32) ^ (uint)Mathf.RoundToInt(p.y * 100f);

        static Rect Around(Vector2 p, float r) => Rect.MinMaxRect(p.x - r, p.y - r, p.x + r, p.y + r);
    }
}
