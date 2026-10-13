using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Oriented bounding box. AxisU is the long axis; AxisV is AxisU rotated +90 degrees.</summary>
    public struct Obb
    {
        public Vector2 Center, AxisU, AxisV;
        public float HalfU, HalfV;

        public float Area => 4f * HalfU * HalfV;

        public Vector2 Corner(int signU, int signV) => Center + AxisU * (HalfU * signU) + AxisV * (HalfV * signV);
    }

    public static class ObbCalculator
    {
        /// <summary>Minimum-area OBB via rotating calipers over the convex hull.</summary>
        public static Obb Compute(IReadOnlyList<Vector2> points)
        {
            var hull = ConvexHull(points);
            if (hull.Count < 3)
            {
                var b = Polygon2D.Bounds(points);
                return Normalize(new Obb { Center = b.center, AxisU = Vector2.right, AxisV = Vector2.up, HalfU = b.width / 2f, HalfV = b.height / 2f });
            }

            var best = new Obb();
            float bestArea = float.MaxValue;
            for (int i = 0; i < hull.Count; i++)
            {
                var edge = hull[(i + 1) % hull.Count] - hull[i];
                if (edge.sqrMagnitude < 1e-10f) continue;
                var u = edge.normalized;
                var v = new Vector2(-u.y, u.x);
                float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
                foreach (var p in hull)
                {
                    float pu = Vector2.Dot(p, u), pv = Vector2.Dot(p, v);
                    if (pu < minU) minU = pu;
                    if (pu > maxU) maxU = pu;
                    if (pv < minV) minV = pv;
                    if (pv > maxV) maxV = pv;
                }
                float area = (maxU - minU) * (maxV - minV);
                if (area < bestArea - 1e-4f)
                {
                    bestArea = area;
                    best = new Obb
                    {
                        AxisU = u, AxisV = v,
                        HalfU = (maxU - minU) / 2f, HalfV = (maxV - minV) / 2f,
                        Center = u * ((minU + maxU) / 2f) + v * ((minV + maxV) / 2f)
                    };
                }
            }
            return Normalize(best);
        }

        static Obb Normalize(Obb obb)
        {
            if (obb.HalfV <= obb.HalfU) return obb;
            return new Obb { Center = obb.Center, AxisU = obb.AxisV, AxisV = -obb.AxisU, HalfU = obb.HalfV, HalfV = obb.HalfU };
        }

        /// <summary>Andrew's monotone chain. Returns a CCW hull without duplicate end point.</summary>
        public static List<Vector2> ConvexHull(IReadOnlyList<Vector2> points)
        {
            var pts = new List<Vector2>(points);
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            if (pts.Count < 3) return pts;
            var hull = new List<Vector2>(pts.Count * 2);
            foreach (var p in pts)
            {
                while (hull.Count >= 2 && Polygon2D.Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 2]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lower = hull.Count + 1;
            for (int i = pts.Count - 2; i >= 0; i--)
            {
                var p = pts[i];
                while (hull.Count >= lower && Polygon2D.Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 2]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        /// <summary>Yaw (degrees, Unity Y rotation) that turns local +X onto the given ground-plane axis.</summary>
        public static float YawDegreesForLocalX(Vector2 axis) => Mathf.Atan2(-axis.y, axis.x) * Mathf.Rad2Deg;
    }
}
