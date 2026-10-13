using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Ear-clipping triangulation with hole bridging (Eberly). Output triangles are CCW in 2D.</summary>
    public static class Triangulator
    {
        const float Epsilon = 1e-7f;

        public static bool Triangulate(Polygon polygon, List<Vector2> vertices, List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();
            if (polygon == null || polygon.Outer.Count < 3) return false;

            var merged = new List<Vector2>(polygon.Outer);
            Polygon2D.EnsureWinding(merged, true);
            var holes = new List<List<Vector2>>();
            foreach (var hole in polygon.Holes)
            {
                if (hole.Count < 3) continue;
                var h = new List<Vector2>(hole);
                Polygon2D.EnsureWinding(h, false);
                holes.Add(h);
            }
            holes.Sort((a, b) => MaxX(b).CompareTo(MaxX(a)));
            foreach (var hole in holes) merged = BridgeHole(merged, hole);

            vertices.AddRange(merged);
            return EarClip(vertices, triangles);
        }

        static float MaxX(List<Vector2> ring)
        {
            float max = float.MinValue;
            foreach (var p in ring) if (p.x > max) max = p.x;
            return max;
        }

        static List<Vector2> BridgeHole(List<Vector2> outer, List<Vector2> hole)
        {
            int mi = 0;
            for (int i = 1; i < hole.Count; i++) if (hole[i].x > hole[mi].x) mi = i;
            Vector2 m = hole[mi];

            int edge = -1;
            float bestX = float.MaxValue;
            for (int i = 0; i < outer.Count; i++)
            {
                Vector2 a = outer[i], b = outer[(i + 1) % outer.Count];
                if ((a.y > m.y) == (b.y > m.y)) continue;
                float x = a.x + (m.y - a.y) * (b.x - a.x) / (b.y - a.y);
                if (x >= m.x && x < bestX) { bestX = x; edge = i; }
            }
            if (edge < 0) return outer;

            var hit = new Vector2(bestX, m.y);
            int next = (edge + 1) % outer.Count;
            int candidate = outer[edge].x > outer[next].x ? edge : next;
            Vector2 p = outer[candidate];

            int chosen = candidate;
            if (Mathf.Abs(Polygon2D.Cross(hit - m, p - m)) > Epsilon)
            {
                float bestAngle = float.MaxValue, bestDistance = float.MaxValue;
                for (int i = 0; i < outer.Count; i++)
                {
                    if (i == candidate || !IsReflex(outer, i)) continue;
                    Vector2 v = outer[i];
                    if (!PointInTriangle(v, m, hit, p)) continue;
                    float angle = Mathf.Abs(Mathf.Atan2(v.y - m.y, v.x - m.x));
                    float distance = (v - m).sqrMagnitude;
                    if (angle < bestAngle - 1e-6f || (Mathf.Abs(angle - bestAngle) <= 1e-6f && distance < bestDistance))
                    {
                        bestAngle = angle;
                        bestDistance = distance;
                        chosen = i;
                    }
                }
            }

            var result = new List<Vector2>(outer.Count + hole.Count + 2);
            for (int i = 0; i <= chosen; i++) result.Add(outer[i]);
            for (int k = 0; k <= hole.Count; k++) result.Add(hole[(mi + k) % hole.Count]);
            for (int i = chosen; i < outer.Count; i++) result.Add(outer[i]);
            return result;
        }

        static bool IsReflex(List<Vector2> ring, int i)
        {
            Vector2 prev = ring[(i - 1 + ring.Count) % ring.Count], cur = ring[i], next = ring[(i + 1) % ring.Count];
            return Polygon2D.Cross(cur - prev, next - cur) < 0f;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Polygon2D.Cross(b - a, p - a), d2 = Polygon2D.Cross(c - b, p - b), d3 = Polygon2D.Cross(a - c, p - c);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        static bool EarClip(List<Vector2> pts, List<int> tris)
        {
            var idx = new List<int>(pts.Count);
            for (int i = 0; i < pts.Count; i++) idx.Add(i);

            while (idx.Count > 3)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[ia], b = pts[ib], c = pts[ic];
                    if (Polygon2D.Cross(b - a, c - b) <= Epsilon) continue;
                    if (ContainsOther(pts, idx, ia, ib, ic)) continue;
                    tris.Add(ia);
                    tris.Add(ib);
                    tris.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped && !RemoveDegenerate(pts, idx)) return false;
            }

            if (idx.Count == 3 && Polygon2D.Cross(pts[idx[1]] - pts[idx[0]], pts[idx[2]] - pts[idx[1]]) > Epsilon)
            {
                tris.Add(idx[0]);
                tris.Add(idx[1]);
                tris.Add(idx[2]);
            }
            return tris.Count > 0;
        }

        static bool ContainsOther(List<Vector2> pts, List<int> idx, int ia, int ib, int ic)
        {
            Vector2 a = pts[ia], b = pts[ib], c = pts[ic];
            foreach (int j in idx)
            {
                if (j == ia || j == ib || j == ic) continue;
                Vector2 p = pts[j];
                if (p == a || p == b || p == c) continue;
                if (PointInTriangle(p, a, b, c)) return true;
            }
            return false;
        }

        static bool RemoveDegenerate(List<Vector2> pts, List<int> idx)
        {
            for (int i = 0; i < idx.Count; i++)
            {
                Vector2 a = pts[idx[(i + idx.Count - 1) % idx.Count]], b = pts[idx[i]], c = pts[idx[(i + 1) % idx.Count]];
                if (a == b || b == c || Mathf.Abs(Polygon2D.Cross(b - a, c - b)) <= Epsilon)
                {
                    idx.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
