using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Bridson Poisson-disk sampling inside a rectangle, driven by SplitMix64.</summary>
    public static class PoissonDisk
    {
        public const int MaxGridCells = 2000000;

        public static List<Vector2> Sample(Rect bounds, float minDistance, SplitMix64 rng, int attempts = 20)
        {
            var points = new List<Vector2>();
            if (minDistance <= 0f || bounds.width <= 0f || bounds.height <= 0f) return points;
            float cell = minDistance / Mathf.Sqrt(2f);
            int gw = Mathf.CeilToInt(bounds.width / cell), gh = Mathf.CeilToInt(bounds.height / cell);
            if ((long)gw * gh > MaxGridCells) return points;

            var grid = new int[gw * gh];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;
            var active = new List<int>();
            float minSq = minDistance * minDistance;

            int CellX(Vector2 p) => Mathf.Clamp((int)((p.x - bounds.xMin) / cell), 0, gw - 1);
            int CellY(Vector2 p) => Mathf.Clamp((int)((p.y - bounds.yMin) / cell), 0, gh - 1);

            void Accept(Vector2 p)
            {
                grid[CellY(p) * gw + CellX(p)] = points.Count;
                active.Add(points.Count);
                points.Add(p);
            }

            bool IsFar(Vector2 c)
            {
                int cx = CellX(c), cy = CellY(c);
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= gw || y >= gh) continue;
                        int j = grid[y * gw + x];
                        if (j >= 0 && (points[j] - c).sqrMagnitude < minSq) return false;
                    }
                return true;
            }

            Accept(new Vector2(rng.Range(bounds.xMin, bounds.xMax), rng.Range(bounds.yMin, bounds.yMax)));
            while (active.Count > 0)
            {
                int index = (int)(rng.NextULong() % (ulong)active.Count);
                Vector2 origin = points[active[index]];
                bool found = false;
                for (int k = 0; k < attempts; k++)
                {
                    float angle = rng.Range(0f, 2f * Mathf.PI);
                    float radius = rng.Range(minDistance, 2f * minDistance);
                    var candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!bounds.Contains(candidate) || !IsFar(candidate)) continue;
                    Accept(candidate);
                    found = true;
                    break;
                }
                if (!found) active.RemoveAt(index);
            }
            return points;
        }
    }
}
