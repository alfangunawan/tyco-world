using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// The baked map road lines turned into one connected network for GPS routes (spec #6 §2 "Penanda"). The lines come
    /// simplified and unjoined, so on load: roads that cross get a shared point, a road ending within
    /// <see cref="SnapDistance"/> of another is joined to it (T junctions), and points closer than
    /// <see cref="WeldDistance"/> become one node. Route is A* over that network. Pure; built once per director.
    /// </summary>
    public sealed class RoadGraph
    {
        public const float SnapDistance = 6f;
        public const float WeldDistance = 0.75f;
        /// <summary>How far from a road a point may be and still be routed (a kos yard, a shop counter).</summary>
        public const float JoinRadius = 80f;
        const float Cell = 40f;

        readonly List<Vector2> nodes = new List<Vector2>();
        readonly List<List<int>> neighbours = new List<List<int>>();
        readonly List<(int a, int b)> segments = new List<(int a, int b)>();
        readonly Dictionary<long, List<int>> segmentGrid = new Dictionary<long, List<int>>();

        public int NodeCount => nodes.Count;
        public int SegmentCount => segments.Count;
        public Vector2 Node(int i) => nodes[i];
        public IReadOnlyList<int> Neighbours(int i) => neighbours[i];

        public RoadGraph(Lane[] roads)
        {
            var lines = new List<List<Vector2>>();
            if (roads != null)
                foreach (var r in roads)
                    if (r != null && r.points != null && r.points.Length >= 2)
                        lines.Add(new List<Vector2>(r.points));
            var cuts = new Dictionary<long, List<(float t, Vector2 p)>>();
            SnapEnds(lines, cuts);
            CutCrossings(lines, cuts);
            for (int l = 0; l < lines.Count; l++) lines[l] = WithCuts(lines[l], l, cuts);
            Weld(lines);
        }

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
        static long SegmentKey(int line, int seg) => ((long)line << 20) | (uint)seg;

        static void AddCut(Dictionary<long, List<(float, Vector2)>> cuts, int line, int seg, float t, Vector2 p)
        {
            long k = SegmentKey(line, seg);
            if (!cuts.TryGetValue(k, out var list)) cuts[k] = list = new List<(float, Vector2)>();
            list.Add((t, p));
        }

        /// <summary>Segments of the original lines, indexed by grid cell (padded by the snap distance).</summary>
        static Dictionary<long, List<(int line, int seg)>> IndexSegments(List<List<Vector2>> lines)
        {
            var index = new Dictionary<long, List<(int, int)>>();
            for (int l = 0; l < lines.Count; l++)
                for (int k = 0; k + 1 < lines[l].Count; k++)
                {
                    Vector2 a = lines[l][k], b = lines[l][k + 1];
                    int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - SnapDistance) / Cell), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + SnapDistance) / Cell);
                    int y0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - SnapDistance) / Cell), y1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + SnapDistance) / Cell);
                    for (int x = x0; x <= x1; x++)
                        for (int y = y0; y <= y1; y++)
                        {
                            long key = Key(x, y);
                            if (!index.TryGetValue(key, out var list)) index[key] = list = new List<(int, int)>();
                            list.Add((l, k));
                        }
                }
            return index;
        }

        /// <summary>A line ending near another line's middle (a T junction) is extended onto it and cuts it there.</summary>
        static void SnapEnds(List<List<Vector2>> lines, Dictionary<long, List<(float, Vector2)>> cuts)
        {
            var index = IndexSegments(lines);
            var extendFirst = new Dictionary<int, Vector2>();
            var extendLast = new Dictionary<int, Vector2>();
            for (int l = 0; l < lines.Count; l++)
                for (int end = 0; end < 2; end++)
                {
                    var e = end == 0 ? lines[l][0] : lines[l][lines[l].Count - 1];
                    if (!index.TryGetValue(Key(Mathf.FloorToInt(e.x / Cell), Mathf.FloorToInt(e.y / Cell)), out var candidates)) continue;
                    float best = SnapDistance;
                    int bestLine = -1, bestSeg = -1;
                    float bestT = 0f;
                    Vector2 bestP = default;
                    foreach (var (ol, ok) in candidates)
                    {
                        if (ol == l) continue;
                        Vector2 a = lines[ol][ok], b = lines[ol][ok + 1];
                        var ab = b - a;
                        float len2 = ab.sqrMagnitude;
                        float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(e - a, ab) / len2) : 0f;
                        var p = a + ab * t;
                        float d = Vector2.Distance(e, p);
                        if (d >= best) continue;
                        best = d;
                        bestLine = ol;
                        bestSeg = ok;
                        bestT = t;
                        bestP = p;
                    }
                    if (bestLine < 0) continue;
                    if (bestT > 1e-4f && bestT < 1f - 1e-4f) AddCut(cuts, bestLine, bestSeg, bestT, bestP);
                    if (best > WeldDistance * 0.5f)
                    {
                        if (end == 0) extendFirst[l] = bestP;
                        else extendLast[l] = bestP;
                    }
                }
            foreach (var kv in extendFirst) lines[kv.Key].Insert(0, kv.Value);
            foreach (var kv in extendLast) lines[kv.Key].Add(kv.Value);
            // The inserted first points shift that line's segment numbers: move its cuts along.
            if (extendFirst.Count == 0) return;
            var moved = new Dictionary<long, List<(float, Vector2)>>();
            foreach (var kv in cuts)
            {
                int line = (int)(kv.Key >> 20), seg = (int)(kv.Key & 0xFFFFF);
                moved[extendFirst.ContainsKey(line) ? SegmentKey(line, seg + 1) : kv.Key] = kv.Value;
            }
            cuts.Clear();
            foreach (var kv in moved) cuts[kv.Key] = kv.Value;
        }

        /// <summary>Two lines that cross without a shared point get one where they cross.</summary>
        static void CutCrossings(List<List<Vector2>> lines, Dictionary<long, List<(float, Vector2)>> cuts)
        {
            var index = IndexSegments(lines);
            var done = new HashSet<(long, long)>();
            foreach (var cell in index.Values)
                for (int i = 0; i < cell.Count; i++)
                    for (int j = i + 1; j < cell.Count; j++)
                    {
                        var (la, ka) = cell[i];
                        var (lb, kb) = cell[j];
                        if (la == lb) continue;
                        long ida = SegmentKey(la, ka), idb = SegmentKey(lb, kb);
                        var pair = ida < idb ? (ida, idb) : (idb, ida);
                        if (!done.Add(pair)) continue;
                        Vector2 p = lines[la][ka], r = lines[la][ka + 1] - p;
                        Vector2 q = lines[lb][kb], s = lines[lb][kb + 1] - q;
                        float denom = r.x * s.y - r.y * s.x;
                        if (Mathf.Abs(denom) < 1e-6f) continue;
                        var qp = q - p;
                        float t = (qp.x * s.y - qp.y * s.x) / denom;
                        float u = (qp.x * r.y - qp.y * r.x) / denom;
                        if (t <= 1e-4f || t >= 1f - 1e-4f || u <= 1e-4f || u >= 1f - 1e-4f) continue;
                        var x = p + r * t;
                        AddCut(cuts, la, ka, t, x);
                        AddCut(cuts, lb, kb, u, x);
                    }
        }

        static List<Vector2> WithCuts(List<Vector2> line, int index, Dictionary<long, List<(float t, Vector2 p)>> cuts)
        {
            var result = new List<Vector2>(line.Count);
            for (int k = 0; k < line.Count; k++)
            {
                result.Add(line[k]);
                if (k + 1 >= line.Count || !cuts.TryGetValue(SegmentKey(index, k), out var list)) continue;
                list.Sort((a, b) => a.t.CompareTo(b.t));
                foreach (var (_, p) in list) result.Add(p);
            }
            return result;
        }

        void Weld(List<List<Vector2>> lines)
        {
            var weldGrid = new Dictionary<long, List<int>>();
            int NodeAt(Vector2 p)
            {
                int cx = Mathf.FloorToInt(p.x / WeldDistance), cy = Mathf.FloorToInt(p.y / WeldDistance);
                for (int x = cx - 1; x <= cx + 1; x++)
                    for (int y = cy - 1; y <= cy + 1; y++)
                        if (weldGrid.TryGetValue(Key(x, y), out var list))
                            foreach (int n in list)
                                if ((nodes[n] - p).sqrMagnitude <= WeldDistance * WeldDistance) return n;
                nodes.Add(p);
                neighbours.Add(new List<int>());
                long k = Key(cx, cy);
                if (!weldGrid.TryGetValue(k, out var cell)) weldGrid[k] = cell = new List<int>();
                cell.Add(nodes.Count - 1);
                return nodes.Count - 1;
            }
            foreach (var line in lines)
            {
                int previous = NodeAt(line[0]);
                for (int i = 1; i < line.Count; i++)
                {
                    int current = NodeAt(line[i]);
                    if (current != previous && !neighbours[previous].Contains(current))
                    {
                        neighbours[previous].Add(current);
                        neighbours[current].Add(previous);
                        IndexSegment(segments.Count, nodes[previous], nodes[current]);
                        segments.Add((previous, current));
                    }
                    previous = current;
                }
            }
        }

        void IndexSegment(int id, Vector2 a, Vector2 b)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / Cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / Cell);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / Cell), y1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / Cell);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    long k = Key(x, y);
                    if (!segmentGrid.TryGetValue(k, out var list)) segmentGrid[k] = list = new List<int>();
                    list.Add(id);
                }
        }

        /// <summary>The nearest road segment to p within <see cref="JoinRadius"/>, and the point on it; -1 when none.</summary>
        public int Nearest(Vector2 p, out Vector2 onRoad)
        {
            onRoad = p;
            int best = -1;
            float bestDistance = JoinRadius;
            int r = Mathf.CeilToInt(JoinRadius / Cell);
            int cx = Mathf.FloorToInt(p.x / Cell), cy = Mathf.FloorToInt(p.y / Cell);
            var seen = new HashSet<int>();
            for (int x = cx - r; x <= cx + r; x++)
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (!segmentGrid.TryGetValue(Key(x, y), out var list)) continue;
                    foreach (int s in list)
                    {
                        if (!seen.Add(s)) continue;
                        Vector2 a = nodes[segments[s].a], b = nodes[segments[s].b];
                        var ab = b - a;
                        float len2 = ab.sqrMagnitude;
                        float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                        var q = a + ab * t;
                        float d = Vector2.Distance(p, q);
                        if (d >= bestDistance) continue;
                        bestDistance = d;
                        best = s;
                        onRoad = q;
                    }
                }
            return best;
        }

        /// <summary>
        /// The shortest way along the roads from <paramref name="from"/> to <paramref name="to"/>: the point on the
        /// nearest road to each, the junctions between, and <paramref name="to"/> itself last. Null when either is far
        /// from any road or no road connects them.
        /// </summary>
        public List<Vector2> Route(Vector2 from, Vector2 to)
        {
            int start = Nearest(from, out var startOn);
            int goal = Nearest(to, out var goalOn);
            if (start < 0 || goal < 0) return null;
            var route = new List<Vector2>();
            void Add(Vector2 q)
            {
                if (route.Count == 0 || (route[route.Count - 1] - q).sqrMagnitude > 1e-6f) route.Add(q);
            }
            if (start == goal)
            {
                Add(startOn);
                Add(goalOn);
                Add(to);
                return route;
            }
            var (sa, sb) = segments[start];
            var (ga, gb) = segments[goal];
            var cost = new Dictionary<int, float>();
            var came = new Dictionary<int, int>();
            var open = new MinHeap();
            void Seed(int n, float g)
            {
                if (cost.TryGetValue(n, out float old) && old <= g) return;
                cost[n] = g;
                came[n] = -1;
                open.Push(n, g + Vector2.Distance(nodes[n], to));
            }
            Seed(sa, Vector2.Distance(startOn, nodes[sa]));
            Seed(sb, Vector2.Distance(startOn, nodes[sb]));
            int bestEnd = -1;
            float bestTotal = float.PositiveInfinity;
            var closed = new HashSet<int>();
            while (open.Count > 0)
            {
                int n = open.Pop(out float priority);
                if (priority >= bestTotal) break;
                if (!closed.Add(n)) continue;
                float g = cost[n];
                if (n == ga || n == gb)
                {
                    float total = g + Vector2.Distance(nodes[n], goalOn);
                    if (total < bestTotal)
                    {
                        bestTotal = total;
                        bestEnd = n;
                    }
                }
                foreach (int m in neighbours[n])
                {
                    if (closed.Contains(m)) continue;
                    float ng = g + Vector2.Distance(nodes[n], nodes[m]);
                    if (cost.TryGetValue(m, out float known) && known <= ng) continue;
                    cost[m] = ng;
                    came[m] = n;
                    open.Push(m, ng + Vector2.Distance(nodes[m], to));
                }
            }
            if (bestEnd < 0) return null;
            var path = new List<int>();
            for (int n = bestEnd; n >= 0; n = came[n]) path.Add(n);
            path.Reverse();
            Add(startOn);
            foreach (int n in path) Add(nodes[n]);
            Add(goalOn);
            Add(to);
            return route;
        }

        public static float Length(IReadOnlyList<Vector2> route)
        {
            float total = 0f;
            for (int i = 1; i < route.Count; i++) total += Vector2.Distance(route[i - 1], route[i]);
            return total;
        }

        /// <summary>A small binary min-heap of (node, priority).</summary>
        sealed class MinHeap
        {
            readonly List<(int node, float priority)> items = new List<(int, float)>();

            public int Count => items.Count;

            public void Push(int node, float priority)
            {
                items.Add((node, priority));
                int i = items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (items[parent].priority <= items[i].priority) break;
                    (items[parent], items[i]) = (items[i], items[parent]);
                    i = parent;
                }
            }

            public int Pop(out float priority)
            {
                var top = items[0];
                int last = items.Count - 1;
                items[0] = items[last];
                items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1, right = left + 1, smallest = i;
                    if (left < items.Count && items[left].priority < items[smallest].priority) smallest = left;
                    if (right < items.Count && items[right].priority < items[smallest].priority) smallest = right;
                    if (smallest == i) break;
                    (items[smallest], items[i]) = (items[i], items[smallest]);
                    i = smallest;
                }
                priority = top.priority;
                return top.node;
            }
        }
    }
}
