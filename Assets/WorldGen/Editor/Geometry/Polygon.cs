using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Polygon on the ground plane (Vector2.x = east, Vector2.y = north). Rings have no closing duplicate.</summary>
    public sealed class Polygon
    {
        public readonly List<Vector2> Outer;
        public readonly List<List<Vector2>> Holes;

        public Polygon(List<Vector2> outer, List<List<Vector2>> holes = null)
        {
            Outer = outer ?? new List<Vector2>();
            Holes = holes ?? new List<List<Vector2>>();
        }

        public float Area
        {
            get
            {
                float area = Mathf.Abs(Polygon2D.SignedArea(Outer));
                foreach (var hole in Holes) area -= Mathf.Abs(Polygon2D.SignedArea(hole));
                return area;
            }
        }

        public Rect Bounds => Polygon2D.Bounds(Outer);

        public bool Contains(Vector2 p)
        {
            if (!Polygon2D.ContainsPoint(Outer, p)) return false;
            foreach (var hole in Holes)
                if (Polygon2D.ContainsPoint(hole, p)) return false;
            return true;
        }

        public float DistanceToBoundary(Vector2 p)
        {
            float d = Polygon2D.DistanceToRing(Outer, p);
            foreach (var hole in Holes) d = Mathf.Min(d, Polygon2D.DistanceToRing(hole, p));
            return d;
        }
    }

    public static class Polygon2D
    {
        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static float SignedArea(IReadOnlyList<Vector2> ring)
        {
            double sum = 0;
            for (int i = 0, n = ring.Count; i < n; i++)
            {
                Vector2 p = ring[i], q = ring[(i + 1) % n];
                sum += (double)p.x * q.y - (double)q.x * p.y;
            }
            return (float)(sum * 0.5);
        }

        public static bool IsCcw(IReadOnlyList<Vector2> ring) => SignedArea(ring) > 0f;

        public static void EnsureWinding(List<Vector2> ring, bool ccw)
        {
            if (ring.Count >= 3 && IsCcw(ring) != ccw) ring.Reverse();
        }

        public static Vector2 Centroid(IReadOnlyList<Vector2> ring)
        {
            double a = 0, cx = 0, cy = 0;
            for (int i = 0, n = ring.Count; i < n; i++)
            {
                Vector2 p = ring[i], q = ring[(i + 1) % n];
                double cross = (double)p.x * q.y - (double)q.x * p.y;
                a += cross;
                cx += (p.x + q.x) * cross;
                cy += (p.y + q.y) * cross;
            }
            if (Math.Abs(a) < 1e-9)
            {
                var sum = Vector2.zero;
                foreach (var p in ring) sum += p;
                return ring.Count > 0 ? sum / ring.Count : Vector2.zero;
            }
            return new Vector2((float)(cx / (3 * a)), (float)(cy / (3 * a)));
        }

        public static bool ContainsPoint(IReadOnlyList<Vector2> ring, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                Vector2 a = ring[i], b = ring[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public static Rect Bounds(IReadOnlyList<Vector2> points)
        {
            if (points.Count == 0) return Rect.zero;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-12f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return Vector2.Distance(p, a + ab * t);
        }

        public static float DistanceToRing(IReadOnlyList<Vector2> ring, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, n = ring.Count; i < n; i++)
                best = Mathf.Min(best, DistanceToSegment(p, ring[i], ring[(i + 1) % n]));
            return best;
        }

        /// <summary>Removes the closing duplicate, near-duplicate points and collinear points.</summary>
        public static List<Vector2> Clean(IReadOnlyList<Vector2> ring, float minSegment = 0.05f, float collinearTolerance = 0.01f)
        {
            var pts = new List<Vector2>(ring);
            float minSq = minSegment * minSegment;
            if (pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude < minSq) pts.RemoveAt(pts.Count - 1);
            bool changed = true;
            while (changed && pts.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < pts.Count && pts.Count >= 3; i++)
                {
                    Vector2 prev = pts[(i - 1 + pts.Count) % pts.Count], cur = pts[i], next = pts[(i + 1) % pts.Count];
                    if ((cur - prev).sqrMagnitude < minSq || DistanceToSegment(cur, prev, next) < collinearTolerance)
                    {
                        pts.RemoveAt(i);
                        changed = true;
                        i--;
                    }
                }
            }
            return pts;
        }

        public static bool IsSelfIntersecting(IReadOnlyList<Vector2> ring)
        {
            int n = ring.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % n];
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;
                    if (SegmentsCross(a, b, ring[j], ring[(j + 1) % n])) return true;
                }
            }
            return false;
        }

        /// <summary>True when the rings' areas touch: a vertex of one inside the other, or crossing edges.</summary>
        public static bool RingsOverlap(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            if (!Bounds(a).Overlaps(Bounds(b))) return false;
            foreach (var p in a) if (ContainsPoint(b, p)) return true;
            foreach (var p in b) if (ContainsPoint(a, p)) return true;
            for (int i = 0; i < a.Count; i++)
                for (int j = 0; j < b.Count; j++)
                    if (SegmentsCross(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
            return false;
        }

        /// <summary>Shortest distance between segments ab and cd (0 when they cross).</summary>
        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (SegmentsCross(a, b, c, d)) return 0f;
            return Mathf.Min(Mathf.Min(DistanceToSegment(a, c, d), DistanceToSegment(b, c, d)),
                Mathf.Min(DistanceToSegment(c, a, b), DistanceToSegment(d, a, b)));
        }

        static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(d - c, a - c), d2 = Cross(d - c, b - c);
            float d3 = Cross(b - a, c - a), d4 = Cross(b - a, d - a);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }
    }
}
