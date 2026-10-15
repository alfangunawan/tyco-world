using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>
    /// Keeps roads clear of buildings (spec road-clearance §2). A plain house poking into a road is moved back out of
    /// it, or dropped when it sits too far in, across the centreline, between two roads, or onto another building once
    /// moved. A road running into a named, campus, key or large building is cut a clear distance short of its walls,
    /// leaving dead ends. Runs at the end of classification, so the world, the gameplay bake and the kos planner see
    /// the same roads and houses. Pure and deterministic: ids in order, no randomness.
    /// </summary>
    public static class RoadClearance
    {
        public const string Shifted = "ClearanceShifted", Dropped = "ClearanceDropped", RoadCuts = "ClearanceRoadCuts";
        const float GridCell = 40f;

        public static void Apply(ClassifiedWorld world, RoadClearanceSettings s, GenerationReport report)
        {
            if (world == null || s == null || !s.enabled) return;
            CutRoads(world, s, report);
            MoveHouses(world, s, report);
        }

        /// <summary>An unnamed, non-campus, non-key building small enough to be someone's house (open canopies are not).</summary>
        public static bool IsHouse(BuildingFeature b, RoadClearanceSettings s) =>
            !b.IsCampus && b.Key == null && string.IsNullOrEmpty(b.Name) && HasFootprint(b) && !b.Spec.RoofOnly && b.Spec.Footprint.Area < s.houseMaxArea;

        /// <summary>A roof on posts (building=roof): roads run under it, so it neither moves nor cuts them.</summary>
        static bool IsCanopy(BuildingFeature b) => b.Spec != null && b.Spec.RoofOnly;

        static bool HasFootprint(BuildingFeature b) => b.Spec != null && b.Spec.Footprint != null && b.Spec.Footprint.Outer.Count >= 3;

        static float Clear(RoadFeature r, RoadClearanceSettings s) => r.Width * 0.5f + s.margin;

        static Rect Grow(Rect r, float by) => new Rect(r.xMin - by, r.yMin - by, r.width + 2f * by, r.height + 2f * by);

        // ---------- roads through kept buildings ----------

        static void CutRoads(ClassifiedWorld world, RoadClearanceSettings s, GenerationReport report)
        {
            var solid = new SpatialGrid<BuildingFeature>(GridCell);
            int count = 0;
            foreach (var b in world.Buildings.OrderBy(b => b.Id))
            {
                if (!HasFootprint(b) || IsHouse(b, s) || IsCanopy(b)) continue;
                solid.Insert(b.Spec.Footprint.Bounds, b);
                count++;
            }
            if (count == 0) return;
            var candidates = new HashSet<BuildingFeature>();
            foreach (var road in world.Roads.OrderBy(r => r.Id))
            {
                float clear = Clear(road, s);
                var kept = new List<LinePiece>();
                bool changed = false;
                foreach (var piece in road.Pieces)
                {
                    var pts = piece.Points;
                    candidates.Clear();
                    solid.Query(Polygon2D.Bounds(pts), candidates);
                    var inside = candidates.Count == 0 ? null : Inside(pts, candidates.OrderBy(b => b.Id).ToList(), s.sampleStep);
                    if (inside == null || inside.Count == 0)
                    {
                        kept.Add(piece);
                        continue;
                    }
                    changed = true;
                    report.Count(RoadCuts, inside.Count);
                    var cum = Polyline2D.Cumulative(pts);
                    float length = cum[cum.Length - 1];
                    float start = 0f;
                    bool atStart = true;
                    foreach (var (a, b) in inside)
                    {
                        Keep(pts, cum, start, a - clear, atStart && piece.ExtendStart, false, s, kept);
                        start = b + clear;
                        atStart = false;
                    }
                    Keep(pts, cum, start, length, false, piece.ExtendEnd, s, kept);
                }
                if (!changed) continue;
                road.Pieces.Clear();
                road.Pieces.AddRange(kept);
            }
            world.Roads.RemoveAll(r => r.Pieces.Count == 0);
        }

        /// <summary>The stretches (arc length) of the line inside any of the buildings, merged, from samples every step.</summary>
        static List<(float, float)> Inside(List<Vector2> pts, List<BuildingFeature> buildings, float step)
        {
            var spans = new List<(float, float)>();
            var cum = Polyline2D.Cumulative(pts);
            float length = cum[cum.Length - 1];
            step = Mathf.Max(step, 0.05f);
            float spanStart = -1f;
            for (float d = 0f;; d += step)
            {
                bool last = d >= length;
                if (last) d = length;
                var p = Polyline2D.PointAt(pts, cum, d, out _);
                bool hit = false;
                foreach (var b in buildings)
                    if (b.Spec.Footprint.Bounds.Contains(p) && b.Spec.Footprint.Contains(p))
                    {
                        hit = true;
                        break;
                    }
                if (hit && spanStart < 0f) spanStart = Mathf.Max(0f, d - step * 0.5f);
                if (!hit && spanStart >= 0f)
                {
                    spans.Add((spanStart, d - step * 0.5f));
                    spanStart = -1f;
                }
                if (last) break;
            }
            if (spanStart >= 0f) spans.Add((spanStart, length));
            return spans;
        }

        static void Keep(List<Vector2> pts, float[] cum, float a, float b, bool extendStart, bool extendEnd, RoadClearanceSettings s,
            List<LinePiece> into)
        {
            if (b - a < s.minPieceLength) return;
            var slice = Polyline2D.Slice(pts, cum, a, b, new List<float>());
            if (slice.Count >= 2) into.Add(new LinePiece(slice, extendStart, extendEnd));
        }

        // ---------- houses ----------

        sealed class Segment
        {
            public RoadFeature Road;
            public Vector2 A, B;
        }

        static void MoveHouses(ClassifiedWorld world, RoadClearanceSettings s, GenerationReport report)
        {
            var segments = new SpatialGrid<Segment>(GridCell);
            float maxClear = 0f;
            foreach (var road in world.Roads)
            {
                maxClear = Mathf.Max(maxClear, Clear(road, s));
                foreach (var piece in road.Pieces)
                    for (int i = 1; i < piece.Points.Count; i++)
                    {
                        var seg = new Segment { Road = road, A = piece.Points[i - 1], B = piece.Points[i] };
                        segments.Insert(Polygon2D.Bounds(new[] { seg.A, seg.B }), seg);
                    }
            }
            var buildings = new SpatialGrid<BuildingFeature>(GridCell);
            foreach (var b in world.Buildings)
                if (HasFootprint(b))
                    buildings.Insert(Grow(b.Spec.Footprint.Bounds, s.maxShift + s.shiftExtra), b);

            var dropped = new HashSet<long>();
            var near = new HashSet<Segment>();
            var others = new HashSet<BuildingFeature>();
            foreach (var house in world.Buildings.Where(b => IsHouse(b, s)).OrderBy(b => b.Id).ToList())
            {
                var ring = house.Spec.Footprint.Outer;
                if (!Need(ring, segments, maxClear, s, near, out float need, out Vector2 away, out bool stuck)) continue;
                if (stuck || need > s.maxShift)
                {
                    dropped.Add(house.Id);
                    continue;
                }
                var offset = away * (need + s.shiftExtra);
                var moved = ring.Select(p => p + offset).ToList();
                bool clash = Need(moved, segments, maxClear, s, near, out _, out _, out _);
                if (!clash)
                {
                    others.Clear();
                    buildings.Query(Polygon2D.Bounds(moved), others);
                    foreach (var o in others.OrderBy(o => o.Id))
                        if (o != house && !dropped.Contains(o.Id) && Polygon2D.RingsOverlap(moved, o.Spec.Footprint.Outer))
                        {
                            clash = true;
                            break;
                        }
                }
                if (clash)
                {
                    dropped.Add(house.Id);
                    continue;
                }
                Move(house, offset);
                report.Count(Shifted);
            }
            if (dropped.Count == 0) return;
            world.Buildings.RemoveAll(b => dropped.Contains(b.Id));
            report.Count(Dropped, dropped.Count);
        }

        /// <summary>
        /// How far the ring sits inside the clear distance of the worst road, and which way is out; true when it does.
        /// Stuck when a road runs across or inside it, when it cannot say which way is out, or when two roads push it
        /// opposite ways.
        /// </summary>
        static bool Need(IReadOnlyList<Vector2> ring, SpatialGrid<Segment> segments, float maxClear, RoadClearanceSettings s,
            HashSet<Segment> near, out float need, out Vector2 away, out bool stuck)
        {
            need = 0f;
            away = Vector2.zero;
            stuck = false;
            near.Clear();
            segments.Query(Grow(Polygon2D.Bounds(ring), maxClear), near);
            if (near.Count == 0) return false;
            var perRoad = new Dictionary<RoadFeature, (float distance, Vector2 onRing, Vector2 onLine)>();
            foreach (var seg in near)
            {
                if (Polygon2D.ContainsPoint(ring, seg.A) || Polygon2D.ContainsPoint(ring, seg.B))
                {
                    stuck = true;
                    return true;
                }
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                    if (Polygon2D.SegmentDistance(a, b, seg.A, seg.B) <= 0f)
                    {
                        stuck = true;
                        return true;
                    }
                    float best = perRoad.TryGetValue(seg.Road, out var known) ? known.distance : float.PositiveInfinity;
                    var onRing = Vector2.zero;
                    var onLine = Vector2.zero;
                    float before = best;
                    Pair(a, b, seg.A, seg.B, ref best, ref onRing, ref onLine);
                    if (best < before) perRoad[seg.Road] = (best, onRing, onLine);
                }
            }
            var pushes = new List<Vector2>();
            foreach (var kv in perRoad.OrderBy(kv => kv.Key.Id))
            {
                float clear = Clear(kv.Key, s);
                var (distance, onRing, onLine) = kv.Value;
                if (distance >= clear) continue;
                var gap = onRing - onLine;
                if (gap.sqrMagnitude < 1e-10f)
                {
                    stuck = true;
                    return true;
                }
                var dir = gap.normalized;
                pushes.Add(dir);
                if (clear - distance > need)
                {
                    need = clear - distance;
                    away = dir;
                }
            }
            for (int i = 0; i < pushes.Count; i++)
                for (int j = i + 1; j < pushes.Count; j++)
                    if (Vector2.Dot(pushes[i], pushes[j]) < 0f)
                        stuck = true;
            return pushes.Count > 0;
        }

        /// <summary>Closest points of two non-crossing segments: the nearest endpoint of either onto the other.</summary>
        static void Pair(Vector2 a, Vector2 b, Vector2 c, Vector2 d, ref float best, ref Vector2 onRing, ref Vector2 onLine)
        {
            void Try(Vector2 r, Vector2 l, ref float bestRef, ref Vector2 ringRef, ref Vector2 lineRef)
            {
                float dist = Vector2.Distance(r, l);
                if (dist >= bestRef) return;
                bestRef = dist;
                ringRef = r;
                lineRef = l;
            }
            Try(a, OnSegment(a, c, d), ref best, ref onRing, ref onLine);
            Try(b, OnSegment(b, c, d), ref best, ref onRing, ref onLine);
            Try(OnSegment(c, a, b), c, ref best, ref onRing, ref onLine);
            Try(OnSegment(d, a, b), d, ref best, ref onRing, ref onLine);
        }

        static Vector2 OnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        static void Move(BuildingFeature b, Vector2 offset)
        {
            var f = b.Spec.Footprint;
            var outer = f.Outer.Select(p => p + offset).ToList();
            var holes = f.Holes.Select(h => h.Select(p => p + offset).ToList()).ToList();
            b.Spec.Footprint = new Polygon(outer, holes);
            b.Centroid += offset;
        }
    }
}
