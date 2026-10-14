using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Walking lanes as a graph (spec #6 §3): arc-length lookups along a lane, the next lane at a node, and a grid for
    /// "lanes near this point". Pure; built once from KampungData.
    /// </summary>
    public sealed class LaneGraph
    {
        const float CellSize = 20f;

        readonly Lane[] lanes;
        readonly LaneNode[] nodes;
        readonly float[][] cum;
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

        public LaneGraph(Lane[] lanes, LaneNode[] nodes)
        {
            this.lanes = lanes ?? new Lane[0];
            this.nodes = nodes ?? new LaneNode[0];
            cum = new float[this.lanes.Length][];
            for (int i = 0; i < this.lanes.Length; i++)
            {
                var pts = this.lanes[i].points;
                var c = new float[pts.Length];
                for (int j = 1; j < pts.Length; j++) c[j] = c[j - 1] + Vector2.Distance(pts[j - 1], pts[j]);
                cum[i] = c;
                for (int j = 0; j + 1 < pts.Length; j++) Index(i, pts[j], pts[j + 1]);
            }
        }

        public int LaneCount => lanes.Length;
        public Lane Lane(int i) => lanes[i];
        public LaneNode Node(int i) => nodes[i];
        public float Length(int lane) => cum[lane].Length > 0 ? cum[lane][cum[lane].Length - 1] : 0f;

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        void Index(int lane, Vector2 a, Vector2 b)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / CellSize), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / CellSize);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / CellSize), y1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / CellSize);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    long k = Key(x, y);
                    if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<int>();
                    if (list.Count == 0 || list[list.Count - 1] != lane) list.Add(lane);
                }
        }

        /// <summary>Point and direction at arc length s along a lane (clamped).</summary>
        public Vector2 PointAt(int lane, float s, out Vector2 direction)
        {
            var pts = lanes[lane].points;
            var c = cum[lane];
            int n = pts.Length;
            if (n == 1)
            {
                direction = Vector2.up;
                return pts[0];
            }
            s = Mathf.Clamp(s, 0f, c[n - 1]);
            int i = 0;
            int lo = 0, hi = n - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (c[mid] <= s) lo = mid;
                else hi = mid - 1;
            }
            i = lo;
            float len = c[i + 1] - c[i];
            var d = pts[i + 1] - pts[i];
            direction = len > 1e-5f ? d / len : Vector2.up;
            float t = len > 1e-5f ? (s - c[i]) / len : 0f;
            return pts[i] + d * t;
        }

        /// <summary>
        /// The lane to walk next from <paramref name="node"/>, coming off <paramref name="fromLane"/>: any other lane at the
        /// node (picked by <paramref name="rng"/>), or back along the same lane at a dead end.
        /// </summary>
        public int NextLane(int node, int fromLane, ref SeededRandom rng)
        {
            if (node < 0 || node >= nodes.Length) return fromLane;
            var options = nodes[node].lanes;
            int others = 0;
            foreach (int l in options)
                if (l != fromLane) others++;
            if (others == 0) return fromLane;
            int pick = rng.Range(0, others);
            foreach (int l in options)
            {
                if (l == fromLane) continue;
                if (pick-- == 0) return l;
            }
            return fromLane;
        }

        /// <summary>Lanes with a segment in the grid cells around p within radius (candidates, not exact).</summary>
        public void Near(Vector2 p, float radius, List<int> result)
        {
            result.Clear();
            int x0 = Mathf.FloorToInt((p.x - radius) / CellSize), x1 = Mathf.FloorToInt((p.x + radius) / CellSize);
            int y0 = Mathf.FloorToInt((p.y - radius) / CellSize), y1 = Mathf.FloorToInt((p.y + radius) / CellSize);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (grid.TryGetValue(Key(x, y), out var list))
                        foreach (int l in list)
                            if (!result.Contains(l)) result.Add(l);
            result.Sort();
        }

        /// <summary>Arc length on <paramref name="lane"/> of the point closest to p; distance receives how far p is.</summary>
        public float Project(int lane, Vector2 p, out float distance)
        {
            var pts = lanes[lane].points;
            var c = cum[lane];
            float best = 0f;
            distance = float.PositiveInfinity;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], ab = pts[i + 1] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-10f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                float d = Vector2.Distance(p, a + ab * t);
                if (d >= distance) continue;
                distance = d;
                best = c[i] + t * (c[i + 1] - c[i]);
            }
            return best;
        }
    }
}
