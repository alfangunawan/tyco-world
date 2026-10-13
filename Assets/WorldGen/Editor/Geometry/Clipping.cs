using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class PolylinePiece
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        public bool StartsAtOriginalStart;
        public bool EndsAtOriginalEnd;
    }

    /// <summary>Liang–Barsky line clipping and Sutherland–Hodgman polygon clipping against a rectangle.</summary>
    public static class Clipping
    {
        public static List<PolylinePiece> ClipPolyline(IReadOnlyList<Vector2> points, Rect rect)
        {
            var pieces = new List<PolylinePiece>();
            PolylinePiece current = null;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                if (!ClipSegment(rect, ref a, ref b, out bool aClipped, out bool bClipped))
                {
                    current = null;
                    continue;
                }
                if (current == null || aClipped)
                {
                    current = new PolylinePiece { StartsAtOriginalStart = i == 0 && !aClipped };
                    current.Points.Add(a);
                    pieces.Add(current);
                }
                current.Points.Add(b);
                current.EndsAtOriginalEnd = i + 1 == points.Count - 1 && !bClipped;
                if (bClipped) current = null;
            }
            return pieces;
        }

        public static bool ClipSegment(Rect rect, ref Vector2 a, ref Vector2 b, out bool aClipped, out bool bClipped)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            if (!Clip(-d.x, a.x - rect.xMin, ref t0, ref t1) || !Clip(d.x, rect.xMax - a.x, ref t0, ref t1) ||
                !Clip(-d.y, a.y - rect.yMin, ref t0, ref t1) || !Clip(d.y, rect.yMax - a.y, ref t0, ref t1))
            {
                aClipped = bClipped = true;
                return false;
            }
            aClipped = t0 > 0f;
            bClipped = t1 < 1f;
            Vector2 start = a + d * t0, end = a + d * t1;
            a = start;
            b = end;
            return true;
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (p == 0f) return q >= 0f;
            float t = q / p;
            if (p < 0f)
            {
                if (t > t1) return false;
                if (t > t0) t0 = t;
            }
            else
            {
                if (t < t0) return false;
                if (t < t1) t1 = t;
            }
            return true;
        }

        public static List<Vector2> ClipPolygon(IReadOnlyList<Vector2> ring, Rect rect)
        {
            var output = new List<Vector2>(ring);
            output = ClipEdge(output, p => p.x >= rect.xMin, (a, b) => AtX(a, b, rect.xMin));
            output = ClipEdge(output, p => p.x <= rect.xMax, (a, b) => AtX(a, b, rect.xMax));
            output = ClipEdge(output, p => p.y >= rect.yMin, (a, b) => AtY(a, b, rect.yMin));
            output = ClipEdge(output, p => p.y <= rect.yMax, (a, b) => AtY(a, b, rect.yMax));
            return output;
        }

        static List<Vector2> ClipEdge(List<Vector2> input, Func<Vector2, bool> inside, Func<Vector2, Vector2, Vector2> intersect)
        {
            var output = new List<Vector2>();
            if (input.Count == 0) return output;
            Vector2 prev = input[input.Count - 1];
            foreach (var cur in input)
            {
                bool curIn = inside(cur), prevIn = inside(prev);
                if (curIn)
                {
                    if (!prevIn) output.Add(intersect(prev, cur));
                    output.Add(cur);
                }
                else if (prevIn)
                {
                    output.Add(intersect(prev, cur));
                }
                prev = cur;
            }
            return output;
        }

        static Vector2 AtX(Vector2 a, Vector2 b, float x) => new Vector2(x, a.y + (b.y - a.y) * (x - a.x) / (b.x - a.x));

        static Vector2 AtY(Vector2 a, Vector2 b, float y) => new Vector2(a.x + (b.x - a.x) * (y - a.y) / (b.y - a.y), y);
    }
}
