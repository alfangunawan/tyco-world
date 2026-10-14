using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>A made-up house filling an empty lot beside a road (spec street-frontage §8).</summary>
    public sealed class InfillHouse
    {
        public long Id;
        public BuildingSpec Spec;
        public Vector2 Centroid;
        /// <summary>Ruko style: front at the property line, two floors, flat roof, no wall in front.</summary>
        public bool Flush;

        public BuildingFeature AsBuildingFeature() => new BuildingFeature { Id = Id, Spec = Spec, Centroid = Centroid, Name = "Infill" };
    }

    /// <summary>Row houses along pass-1 Vacant frontage, inside the 15 m corridor only (spec street-frontage §8).</summary>
    public static class InfillPlanner
    {
        public const string SkipBlocked = "infill:blocked", SkipCap = "infill:cap";

        /// <summary>Stable negative id from the chain key, side and start of the house along the chain.</summary>
        public static long InfillId(long chainKey, int side, float s)
        {
            var inv = CultureInfo.InvariantCulture;
            ulong h = SplitMix64.Hash(chainKey, "infill:" + side.ToString(inv) + ":" + Mathf.RoundToInt(s * 10f).ToString(inv));
            return -(long)(h & 0x3FFF_FFFF_FFFF_FFFFUL) - 1;
        }

        public static List<InfillHouse> Plan(IReadOnlyList<FrontageInterval> pass1, RoadNetwork net, StreetContext ctx, WorldGenSettings settings,
            GenerationReport report)
        {
            var houses = new List<InfillHouse>();
            var inf = settings.infill;
            if (inf == null || !inf.enabled) return houses;
            var near = new Dictionary<RoadChain, float>();
            var candidates = pass1.Where(x => x.Kind == FrontageKind.Vacant && x.Length >= inf.minLotLength)
                .OrderBy(x => x.Chain.Key).ThenBy(x => x.Chain.Index).ThenBy(x => x.Side).ThenBy(x => x.S0).ToList();
            foreach (var x in candidates) PlaceAlong(x, net, ctx, settings, near, houses, report);
            return houses;
        }

        static void PlaceAlong(FrontageInterval x, RoadNetwork net, StreetContext ctx, WorldGenSettings settings, Dictionary<RoadChain, float> near,
            List<InfillHouse> houses, GenerationReport report)
        {
            var inf = settings.infill;
            var inv = CultureInfo.InvariantCulture;
            var rng = SplitMix64.For(x.Chain.Key, "infill:" + x.Side.ToString(inv) + ":" + Mathf.RoundToInt(x.S0 * 10f).ToString(inv));
            float cursor = x.S0;
            while (x.S1 - cursor >= inf.minWidth)
            {
                // Every roll happens for every slot, accepted or not, so one blocked house never shifts its neighbours.
                float remaining = x.S1 - cursor;
                float width = Mathf.Min(rng.Range(inf.minWidth, inf.maxWidth), remaining);
                if (remaining - width < inf.minWidth) width = remaining;
                bool flush = rng.NextDouble() < inf.flushShare;
                float setback = flush ? inf.flushInset : rng.Range(inf.minSetback, inf.maxSetback);
                float depth = rng.Range(inf.minDepth, inf.maxDepth);
                bool twoStorey = flush || rng.NextDouble() < inf.twoStoreyShare;
                float gap = rng.Range(inf.minGap, inf.maxGap);
                long id = InfillId(x.Chain.Key, x.Side, cursor);
                if (houses.Count >= inf.maxInfill)
                {
                    report?.Skip(SkipCap, id);
                }
                else
                {
                    var house = TryPlace(x, cursor, cursor + width, setback, depth, flush, twoStorey, id, net, ctx, settings, near);
                    if (house == null)
                    {
                        report?.Skip(SkipBlocked, id);
                    }
                    else
                    {
                        houses.Add(house);
                        ctx.AddBuilding(new StreetBuilding { Id = house.Id, Footprint = house.Spec.Footprint, Centroid = house.Centroid, IsInfill = true });
                    }
                }
                cursor += width + gap;
            }
        }

        static InfillHouse TryPlace(FrontageInterval x, float a, float b, float setback, float depth, bool flush, bool twoStorey, long id,
            RoadNetwork net, StreetContext ctx, WorldGenSettings settings, Dictionary<RoadChain, float> near)
        {
            var f = settings.frontage;
            var inf = settings.infill;
            var chain = x.Chain;
            float offset = FrontagePlanner.LineOffset(chain, x.Profile, settings.roadEdges, f);
            Vector2 pa = chain.SidePoint(a, x.Side, offset), pb = chain.SidePoint(b, x.Side, offset);
            Vector2 u = pb - pa;
            float width = u.magnitude;
            if (width < 1e-3f) return null;
            u /= width;
            Vector2 outward = Polyline2D.LeftNormal(u) * x.Side;
            Vector2 front = pa + outward * setback;
            float maxDepth = Mathf.Min(depth, f.corridorDepth - setback);
            var depths = maxDepth > inf.minDepth + 1e-3f ? new[] { maxDepth, inf.minDepth } : new[] { maxDepth };
            float c = inf.clearance;
            foreach (float d in depths)
            {
                var ring = Rect(front, u, outward, width, d);
                var grown = Rect(front - u * c - outward * c, u, outward, width + 2f * c, d + 2f * c);
                if (!Fits(ring, grown, chain, offset, net, ctx, f, near)) continue;
                int levels = twoStorey ? 2 : 1;
                var spec = new BuildingSpec
                {
                    Id = id, Footprint = new Polygon(ring), Levels = levels, LevelHeight = settings.levelHeight,
                    WallHeight = Mathf.Max(levels * settings.levelHeight, settings.minWallHeight),
                    AllowGable = !flush, RoofOverride = flush ? RoofOverride.Flat : RoofOverride.Auto
                };
                return new InfillHouse { Id = id, Spec = spec, Centroid = Polygon2D.Centroid(ring), Flush = flush };
            }
            return null;
        }

        static List<Vector2> Rect(Vector2 front, Vector2 u, Vector2 outward, float width, float depth)
        {
            var ring = new List<Vector2> { front, front + u * width, front + u * width + outward * depth, front + outward * depth };
            Polygon2D.EnsureWinding(ring, true);
            return ring;
        }

        static bool Fits(List<Vector2> ring, List<Vector2> grown, RoadChain own, float ownOffset, RoadNetwork net, StreetContext ctx,
            FrontageSettings f, Dictionary<RoadChain, float> near)
        {
            foreach (var p in ring)
                if (!ctx.InBounds(p)) return false;
            if (ctx.OverlapsBuilding(grown) || ctx.OverlapsWater(grown) || ctx.OverlapsArea(grown) || ctx.NearKos(ring, f.kosExclusionRadius)) return false;
            foreach (var p in Boundary(ring, 0.5f))
            {
                net.Nearby(p, net.MaxReach + f.propertyLineGap, null, near);
                foreach (var kv in near)
                {
                    float limit = kv.Key == own ? ownOffset - 0.01f : kv.Key.HalfWidth + kv.Key.MaxEdgeWidth + f.propertyLineGap;
                    if (kv.Value < limit) return false;
                }
            }
            return true;
        }

        static IEnumerable<Vector2> Boundary(List<Vector2> ring, float step)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (int k = 0; k < n; k++) yield return Vector2.Lerp(a, b, k / (float)n);
            }
        }
    }
}
