using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Flat ribbon along a centerline: mitered joints, bevel when the miter exceeds MiterLimit × half width.</summary>
    public static class RoadMeshBuilder
    {
        public const float MiterLimit = 2f;

        public static void AddRibbon(MeshData mesh, int submesh, IReadOnlyList<Vector2> points, float width, float y,
            bool extendStart, bool extendEnd)
        {
            if (points == null || width <= 0f) return;
            var pts = new List<Vector2>(points.Count);
            foreach (var p in points)
                if (pts.Count == 0 || (p - pts[pts.Count - 1]).sqrMagnitude > 1e-4f) pts.Add(p);
            if (pts.Count < 2) return;

            float hw = width * 0.5f;
            int last = pts.Count - 1;
            if (extendStart) pts[0] -= (pts[1] - pts[0]).normalized * hw;
            if (extendEnd) pts[last] += (pts[last] - pts[last - 1]).normalized * hw;

            int segments = pts.Count - 1;
            var normal = new Vector2[segments];
            for (int s = 0; s < segments; s++)
            {
                var d = (pts[s + 1] - pts[s]).normalized;
                normal[s] = new Vector2(-d.y, d.x);
            }
            var dist = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) dist[i] = dist[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);

            var startL = new Vector2[segments];
            var startR = new Vector2[segments];
            var endL = new Vector2[segments];
            var endR = new Vector2[segments];
            for (int s = 0; s < segments; s++)
            {
                startL[s] = pts[s] + normal[s] * hw;
                startR[s] = pts[s] - normal[s] * hw;
                endL[s] = pts[s + 1] + normal[s] * hw;
                endR[s] = pts[s + 1] - normal[s] * hw;
            }

            for (int i = 1; i < last; i++)
            {
                Vector2 n0 = normal[i - 1], n1 = normal[i];
                Vector2 sum = n0 + n1;
                if (sum.sqrMagnitude > 1e-6f)
                {
                    Vector2 miter = sum.normalized;
                    float cosHalf = Vector2.Dot(miter, n1);
                    if (cosHalf > 1e-4f && 1f / cosHalf <= MiterLimit)
                    {
                        Vector2 offset = miter * (hw / cosHalf);
                        endL[i - 1] = startL[i] = pts[i] + offset;
                        endR[i - 1] = startR[i] = pts[i] - offset;
                        continue;
                    }
                }
                Vector2 d0 = pts[i] - pts[i - 1], d1 = pts[i + 1] - pts[i];
                bool leftTurn = d0.x * d1.y - d0.y * d1.x > 0f;
                Vector2 outer0 = leftTurn ? endR[i - 1] : endL[i - 1];
                Vector2 outer1 = leftTurn ? startR[i] : startL[i];
                var uv = new Vector2(hw, dist[i]);
                mesh.AddTriangle(submesh, Xz.At(pts[i], y), Xz.At(outer0, y), Xz.At(outer1, y), uv, uv, uv, Vector3.up);
            }

            for (int s = 0; s < segments; s++)
            {
                mesh.AddQuad(submesh,
                    Xz.At(startR[s], y), Xz.At(endR[s], y), Xz.At(endL[s], y), Xz.At(startL[s], y),
                    new Vector2(width, dist[s]), new Vector2(width, dist[s + 1]), new Vector2(0f, dist[s + 1]), new Vector2(0f, dist[s]),
                    Vector3.up);
            }
        }
    }
}
