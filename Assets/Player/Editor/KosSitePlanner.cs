using System.Collections.Generic;
using System.Globalization;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.Player.Authoring
{
    public struct RoadLine
    {
        public long Id;
        public List<Vector2> Points;
        public float HalfWidth;
    }

    public sealed class KosSiteOptions
    {
        public float fenceRoadGap = 1f;
        public float minYard = 2.5f;
        public float maxYard = 6f;
        public float gateWidth = 2f;
        public float spawnOffset = 1.2f;
        public float minEdge = 3f;
        public float motorDepthRatio = 0.45f;
        public float sampleStep = 0.5f;
        /// <summary>Zero = pick the edge nearest a road; otherwise pick the road-facing edge whose outward normal is closest to this.</summary>
        public Vector2 preferredOutward = Vector2.zero;
    }

    /// <summary>Local 2D (x = east, y = north) layout of the kos front yard. Yaw follows Unity (0 = north, 90 = east).</summary>
    public struct KosSitePlan
    {
        public int EdgeIndex;
        public Vector2 WallA, WallB, Along, Outward, Door, Gate, Spawn, Motor;
        public float YardDepth, SpawnYaw, MotorYaw, RoadClearance, RoadDistance;
        public long RoadId;
    }

    /// <summary>Pure planner for the kos front yard (spec #2 §9): the wall facing the nearest road with an unobstructed yard.</summary>
    public static class KosSitePlanner
    {
        public static bool TryPlan(IReadOnlyList<Vector2> footprint, IReadOnlyList<RoadLine> roads, IReadOnlyList<IReadOnlyList<Vector2>> neighbours,
            KosSiteOptions options, out KosSitePlan plan, out string reason)
        {
            plan = default;
            reason = null;
            if (footprint == null || footprint.Count < 3) { reason = "Tapak kos tidak valid."; return false; }
            if (roads == null || roads.Count == 0) { reason = "Tidak ada jalan di sekitar kos."; return false; }
            var ring = new List<Vector2>(footprint);
            Polygon2D.EnsureWinding(ring, true);

            bool found = false, anyFacingRoad = false;
            float bestScore = float.MaxValue, bestLength = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                float length = Vector2.Distance(a, b);
                if (length < options.minEdge) continue;
                Vector2 along = (b - a) / length;
                Vector2 outward = new Vector2(along.y, -along.x);
                if (!NearestRoadInFront(a, along, length, outward, roads, options.sampleStep, out var roadPoint, out float distance, out var road)) continue;
                anyFacingRoad = true;
                float clearance = Vector2.Dot(roadPoint - a, outward) - road.HalfWidth;
                float yard = Mathf.Clamp(clearance - options.fenceRoadGap, options.minYard, options.maxYard);
                if (YardBlocked(a, b, along, outward, yard, neighbours)) continue;

                float score = options.preferredOutward.sqrMagnitude > 1e-6f ? -Vector2.Dot(outward, options.preferredOutward.normalized) : distance;
                bool better = !found || score < bestScore - 0.01f || (Mathf.Abs(score - bestScore) <= 0.01f && length > bestLength);
                if (!better) continue;
                plan = Build(i, a, along, outward, length, roadPoint, road, clearance, yard, distance, options);
                bestScore = score;
                bestLength = length;
                found = true;
            }
            if (found) return true;
            reason = anyFacingRoad
                ? "Semua sisi kos yang menghadap jalan terhalang bangunan tetangga."
                : "Tidak ada jalan di depan sisi mana pun dari kos.";
            return false;
        }

        public static float YawOf(Vector2 direction) => Mathf.Repeat(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg, 360f);

        public static string Describe(KosSitePlan p) => string.Format(CultureInfo.InvariantCulture,
            "edge={0} outward=({1:F2},{2:F2}) yard={3:F2} clearance={4:F2} road={5} door=({6:F2},{7:F2}) gate=({8:F2},{9:F2}) spawn=({10:F2},{11:F2}) yaw={12:F1} motor=({13:F2},{14:F2}) motorYaw={15:F1}",
            p.EdgeIndex, p.Outward.x, p.Outward.y, p.YardDepth, p.RoadClearance, p.RoadId, p.Door.x, p.Door.y, p.Gate.x, p.Gate.y,
            p.Spawn.x, p.Spawn.y, p.SpawnYaw, p.Motor.x, p.Motor.y, p.MotorYaw);

        static KosSitePlan Build(int index, Vector2 a, Vector2 along, Vector2 outward, float length, Vector2 roadPoint, RoadLine road,
            float clearance, float yard, float distance, KosSiteOptions o)
        {
            float halfGate = o.gateWidth * 0.5f, margin = halfGate + 0.5f;
            float s = Vector2.Dot(roadPoint - a, along);
            s = length >= 2f * margin ? Mathf.Clamp(s, margin, length - margin) : length * 0.5f;
            var door = a + along * s;
            float leftRoom = s - halfGate, rightRoom = length - s - halfGate;
            float motorAlong = rightRoom >= leftRoom ? s + halfGate + rightRoom * 0.5f : leftRoom * 0.5f;
            return new KosSitePlan
            {
                EdgeIndex = index, WallA = a, WallB = a + along * length, Along = along, Outward = outward,
                Door = door, Gate = door + outward * yard, Spawn = door + outward * o.spawnOffset,
                Motor = a + along * motorAlong + outward * (yard * o.motorDepthRatio),
                YardDepth = yard, SpawnYaw = YawOf(outward), MotorYaw = YawOf(along),
                RoadClearance = clearance, RoadDistance = distance, RoadId = road.Id
            };
        }

        /// <summary>Closest road point (over samples along the edge) that lies in front of the wall.</summary>
        static bool NearestRoadInFront(Vector2 a, Vector2 along, float length, Vector2 outward, IReadOnlyList<RoadLine> roads, float step,
            out Vector2 point, out float distance, out RoadLine road)
        {
            point = default;
            road = default;
            distance = float.MaxValue;
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / step));
            for (int k = 0; k <= samples; k++)
            {
                var e = a + along * (length * k / samples);
                foreach (var line in roads)
                {
                    if (line.Points == null) continue;
                    for (int j = 0; j + 1 < line.Points.Count; j++)
                    {
                        var r = ClosestOnSegment(e, line.Points[j], line.Points[j + 1]);
                        if (Vector2.Dot(r - e, outward) <= 0.1f) continue;
                        float d = Vector2.Distance(r, e);
                        if (d >= distance) continue;
                        distance = d;
                        point = r;
                        road = line;
                    }
                }
            }
            return distance < float.MaxValue;
        }

        static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        }

        static bool YardBlocked(Vector2 a, Vector2 b, Vector2 along, Vector2 outward, float yard, IReadOnlyList<IReadOnlyList<Vector2>> neighbours)
        {
            if (neighbours == null) return false;
            const float inset = 0.05f;
            var yardRing = new List<Vector2>
            {
                a + along * inset + outward * inset,
                b - along * inset + outward * inset,
                b - along * inset + outward * yard,
                a + along * inset + outward * yard
            };
            foreach (var other in neighbours)
            {
                if (other == null || other.Count < 3) continue;
                foreach (var p in other) if (Polygon2D.ContainsPoint(yardRing, p)) return true;
                foreach (var p in yardRing) if (Polygon2D.ContainsPoint(other, p)) return true;
                for (int i = 0; i < yardRing.Count; i++)
                for (int j = 0; j < other.Count; j++)
                    if (SegmentsCross(yardRing[i], yardRing[(i + 1) % yardRing.Count], other[j], other[(j + 1) % other.Count])) return true;
            }
            return false;
        }

        static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Polygon2D.Cross(p2 - p1, q1 - p1), d2 = Polygon2D.Cross(p2 - p1, q2 - p1);
            float d3 = Polygon2D.Cross(q2 - q1, p1 - q1), d4 = Polygon2D.Cross(q2 - q1, p2 - q1);
            return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f);
        }
    }
}
