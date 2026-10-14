using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Open polylines on the ground plane. "s" is arc length along the polyline (spec street-frontage §5).</summary>
    public static class Polyline2D
    {
        public static float[] Cumulative(IReadOnlyList<Vector2> pts)
        {
            var cum = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
            return cum;
        }

        public static Vector2 LeftNormal(Vector2 dir) => new Vector2(-dir.y, dir.x);

        /// <summary>Index i of the segment pts[i]..pts[i+1] holding arc length s (clamped to the first/last segment).</summary>
        public static int SegmentAt(float[] cum, float s)
        {
            int last = cum.Length - 2;
            if (last <= 0) return 0;
            int lo = 0, hi = last;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cum[mid] <= s) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        public static Vector2 PointAt(IReadOnlyList<Vector2> pts, float[] cum, float s, out Vector2 tangent)
        {
            int i = SegmentAt(cum, s);
            Vector2 a = pts[i], b = pts[i + 1];
            float length = cum[i + 1] - cum[i];
            tangent = length > 1e-6f ? (b - a) / length : Vector2.right;
            float t = length > 1e-6f ? Mathf.Clamp01((s - cum[i]) / length) : 0f;
            return Vector2.Lerp(a, b, t);
        }

        /// <summary>Sub-polyline from s0 to s1: both cut points plus every vertex strictly between; along receives each point's s.</summary>
        public static List<Vector2> Slice(IReadOnlyList<Vector2> pts, float[] cum, float s0, float s1, List<float> along)
        {
            along.Clear();
            var result = new List<Vector2> { PointAt(pts, cum, s0, out _) };
            along.Add(s0);
            for (int i = 0; i < pts.Count; i++)
            {
                if (cum[i] <= s0 + 1e-4f || cum[i] >= s1 - 1e-4f) continue;
                result.Add(pts[i]);
                along.Add(cum[i]);
            }
            result.Add(PointAt(pts, cum, s1, out _));
            along.Add(s1);
            return result;
        }

        /// <summary>
        /// Mitered offset; positive = left of travel. A joint whose miter would be longer than RoadMeshBuilder.MiterLimit
        /// times the offset becomes a bevel (two points). The decision depends only on the turn angle, so every offset of one
        /// polyline has the same point count. along receives the centreline s of each output point.
        /// </summary>
        public static List<Vector2> Offset(IReadOnlyList<Vector2> pts, IReadOnlyList<float> s, float offset, List<float> along)
        {
            int n = pts.Count;
            along.Clear();
            var result = new List<Vector2>(n + 4);
            var normals = new Vector2[n - 1];
            for (int i = 0; i + 1 < n; i++) normals[i] = LeftNormal((pts[i + 1] - pts[i]).normalized);
            result.Add(pts[0] + normals[0] * offset);
            along.Add(s[0]);
            for (int i = 1; i + 1 < n; i++)
            {
                Vector2 n0 = normals[i - 1], n1 = normals[i];
                Vector2 sum = n0 + n1;
                float cosHalf = sum.sqrMagnitude > 1e-6f ? Vector2.Dot(sum.normalized, n1) : 0f;
                if (cosHalf > 1e-4f && 1f / cosHalf <= RoadMeshBuilder.MiterLimit)
                {
                    result.Add(pts[i] + sum.normalized * (offset / cosHalf));
                    along.Add(s[i]);
                }
                else
                {
                    result.Add(pts[i] + n0 * offset);
                    along.Add(s[i]);
                    result.Add(pts[i] + n1 * offset);
                    along.Add(s[i]);
                }
            }
            result.Add(pts[n - 1] + normals[n - 2] * offset);
            along.Add(s[n - 1]);
            return result;
        }

        /// <summary>Arc length of the polyline point closest to p; distance receives how far p is from it.</summary>
        public static float Project(IReadOnlyList<Vector2> pts, float[] cum, Vector2 p, out float distance)
        {
            float best = 0f;
            distance = float.PositiveInfinity;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i], ab = pts[i + 1] - a;
                float lengthSq = ab.sqrMagnitude;
                float t = lengthSq > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
                float d = Vector2.Distance(p, a + ab * t);
                if (d >= distance) continue;
                distance = d;
                best = cum[i] + t * (cum[i + 1] - cum[i]);
            }
            return best;
        }

        public static float DistanceTo(IReadOnlyList<Vector2> pts, Vector2 p)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i + 1 < pts.Count; i++) best = Mathf.Min(best, Polygon2D.DistanceToSegment(p, pts[i], pts[i + 1]));
            return best;
        }

        /// <summary>Distance along the ray (dir normalised) to the first ring edge it crosses; +∞ when none within maxDistance.</summary>
        public static float RayToRing(Vector2 origin, Vector2 dir, IReadOnlyList<Vector2> ring, float maxDistance)
        {
            float best = float.PositiveInfinity;
            for (int i = 0, n = ring.Count; i < n; i++)
            {
                Vector2 a = ring[i], e = ring[(i + 1) % n] - a;
                float denom = Polygon2D.Cross(dir, e);
                if (Mathf.Abs(denom) < 1e-9f) continue;
                Vector2 ao = a - origin;
                float t = Polygon2D.Cross(ao, e) / denom;
                float u = Polygon2D.Cross(ao, dir) / denom;
                if (t >= 0f && t <= maxDistance && u >= 0f && u <= 1f && t < best) best = t;
            }
            return best;
        }

        /// <summary>Moves from keep (passes) toward drop (fails) by halving; returns the last value that still passes.</summary>
        public static float Bisect(float keep, float drop, Func<float, bool> passes, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                float mid = (keep + drop) * 0.5f;
                if (passes(mid)) keep = mid;
                else drop = mid;
            }
            return keep;
        }
    }
}
