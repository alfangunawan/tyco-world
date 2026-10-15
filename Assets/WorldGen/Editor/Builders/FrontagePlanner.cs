using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public enum FrontageKind { Blocked, Empty, Flush, Lot, Vacant, Campus, Cemetery, Excluded }

    /// <summary>A stretch of one chain side's property line with one classification (spec street-frontage §7).</summary>
    public sealed class FrontageInterval
    {
        public RoadChain Chain;
        public int Side;
        public EdgeProfile Profile;
        public float S0, S1;
        public FrontageKind Kind;
        /// <summary>The building behind a Lot; null for every other kind.</summary>
        public StreetBuilding Building;
        /// <summary>
        /// On a loop, the interval on the other side of the seam that continues this one (same kind, building and profile):
        /// the two are one lot, so they share one wall look and get one opening (spec frontage-polish §2.1).
        /// </summary>
        public FrontageInterval SeamPartner;
        public float Length => S1 - S0;
    }

    public sealed class WallOpening
    {
        public float S;
        public float Width;
        public bool IsGate;
    }

    /// <summary>A wall or fence along one interval, with its look and any gate/door opening.</summary>
    public sealed class WallRun
    {
        public RoadChain Chain;
        public int Side;
        public EdgeProfile Profile;
        public float S0, S1;
        public WallStyleRule Style;
        public int Band;
        public float Height;
        public readonly List<WallOpening> Openings = new List<WallOpening>();
    }

    /// <summary>Classifies the property line beside every edged road and turns it into wall runs (spec street-frontage §7).</summary>
    public static class FrontagePlanner
    {
        /// <summary>Distance from the chain centreline to the property line (the road-side face of the wall).</summary>
        public static float LineOffset(RoadChain chain, EdgeProfile profile, RoadEdgeSettings e, FrontageSettings f) =>
            chain.HalfWidth + e.Width(profile) + f.propertyLineGap;

        public static List<FrontageInterval> Plan(RoadNetwork net, StreetContext ctx, WorldGenSettings settings)
        {
            var e = settings.roadEdges;
            var f = settings.frontage;
            var result = new List<FrontageInterval>();
            var near = new Dictionary<RoadChain, float>();
            foreach (var chain in net.Chains)
            {
                if (chain.BaseProfile == EdgeProfile.None || chain.Points.Count < 2) continue;
                foreach (int side in RoadNetwork.Sides)
                {
                    var profiles = chain.Profiles(side);
                    int n = profiles.Length;
                    var kinds = new FrontageKind[n];
                    var owners = new StreetBuilding[n];
                    for (int i = 0; i < n; i++)
                        kinds[i] = profiles[i] == EdgeProfile.None
                            ? FrontageKind.Blocked
                            : Classify(chain, side, chain.SampleS(i, e.SafeSampleStep), profiles[i], net, ctx, e, f, near, out owners[i]);
                    var list = new List<FrontageInterval>();
                    int start = 0;
                    for (int i = 1; i <= n; i++)
                    {
                        if (i < n && kinds[i] == kinds[start] && owners[i] == owners[start] && profiles[i] == profiles[start]) continue;
                        float s0 = start == 0 ? 0f : 0.5f * (chain.SampleS(start - 1, e.SafeSampleStep) + chain.SampleS(start, e.SafeSampleStep));
                        float s1 = i == n ? chain.Length : 0.5f * (chain.SampleS(i - 1, e.SafeSampleStep) + chain.SampleS(i, e.SafeSampleStep));
                        list.Add(new FrontageInterval
                        {
                            Chain = chain, Side = side, Profile = profiles[start], S0 = s0, S1 = s1, Kind = kinds[start], Building = owners[start]
                        });
                        start = i;
                    }
                    MergeShort(list, f.minInterval);
                    PairAcrossSeam(chain, list);
                    foreach (var x in list)
                        if (x.Kind != FrontageKind.Blocked && x.Length > 1e-3f) result.Add(x);
                }
            }
            return result;
        }

        static void PairAcrossSeam(RoadChain chain, List<FrontageInterval> list)
        {
            if (!chain.Closed || list.Count < 2) return;
            var first = list[0];
            var last = list[list.Count - 1];
            if (first.S0 > 1e-3f || last.S1 < chain.Length - 1e-3f || first.Kind == FrontageKind.Blocked) return;
            if (first.Kind != last.Kind || first.Building != last.Building || first.Profile != last.Profile) return;
            first.SeamPartner = last;
            last.SeamPartner = first;
        }

        /// <summary>Where a lot starts: across a loop seam, the start of the partner that runs into the seam.</summary>
        static float LotStart(FrontageInterval x) => x.SeamPartner != null && x.S0 < x.SeamPartner.S0 ? x.SeamPartner.S0 : x.S0;

        /// <summary>Of two seam partners only the longer (the later one on a tie) carries the lot's opening.</summary>
        static bool CarriesOpening(FrontageInterval x)
        {
            var other = x.SeamPartner;
            if (other == null) return true;
            return x.Length > other.Length + 1e-4f || (Mathf.Abs(x.Length - other.Length) <= 1e-4f && x.S0 > other.S0);
        }

        /// <summary>Classification of the property-line sample at arc length s (spec street-frontage §7, rules in order).</summary>
        public static FrontageKind Classify(RoadChain chain, int side, float s, EdgeProfile profile, RoadNetwork net, StreetContext ctx,
            RoadEdgeSettings e, FrontageSettings f, Dictionary<RoadChain, float> near, out StreetBuilding owner)
        {
            owner = null;
            var c = chain.PointAt(s, out var t);
            var outward = Polyline2D.LeftNormal(t) * side;
            float offset = LineOffset(chain, profile, e, f);
            var p = c + outward * offset;
            if (!ctx.InBounds(p)) return FrontageKind.Blocked;
            if (net.DistanceToChain(chain, p, offset, s - offset - 1f, s + offset + 1f) < offset - 0.05f) return FrontageKind.Blocked;
            net.Nearby(p, net.MaxReach + f.propertyLineGap + f.wallThickness, chain, near);
            foreach (var kv in near)
                if (kv.Value < kv.Key.HalfWidth + kv.Key.MaxEdgeWidth + f.propertyLineGap + f.wallThickness) return FrontageKind.Blocked;

            var probe = p + outward * f.contextProbe;
            if (ctx.InWater(p) || ctx.InWater(probe)) return FrontageKind.Empty;
            if (ctx.InArea(probe, AreaCategory.Campus)) return ctx.InArea(c, AreaCategory.Campus) ? FrontageKind.Empty : FrontageKind.Campus;
            if (ctx.InArea(probe, AreaCategory.Cemetery)) return ctx.InArea(c, AreaCategory.Cemetery) ? FrontageKind.Empty : FrontageKind.Cemetery;
            if (ctx.InAnyArea(probe)) return FrontageKind.Empty;
            if (ctx.DistanceToKos(p) < f.kosExclusionRadius) return FrontageKind.Excluded;

            var hit = ctx.Raycast(p, outward, f.corridorDepth, out float distance);
            if (hit == null) return FrontageKind.Vacant;
            if (hit.Excluded) return FrontageKind.Excluded;
            if (distance < f.flushDistance) return FrontageKind.Flush;
            owner = hit;
            return FrontageKind.Lot;
        }

        /// <summary>
        /// Intervals shorter than <paramref name="min"/> join their longer touching neighbour (never a Blocked one), then equal
        /// neighbours coalesce. A short interval with no eligible neighbour stays.
        /// </summary>
        public static void MergeShort(List<FrontageInterval> list, float min)
        {
            while (true)
            {
                int pick = -1, into = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    var x = list[i];
                    if (x.Kind == FrontageKind.Blocked || x.Length >= min) continue;
                    int target = LongerNeighbour(list, i);
                    if (target < 0) continue;
                    if (pick < 0 || x.Length < list[pick].Length)
                    {
                        pick = i;
                        into = target;
                    }
                }
                if (pick < 0) return;
                var small = list[pick];
                var host = list[into];
                if (into < pick) host.S1 = small.S1;
                else host.S0 = small.S0;
                list.RemoveAt(pick);
                Coalesce(list);
            }
        }

        static int LongerNeighbour(List<FrontageInterval> list, int i)
        {
            int best = -1;
            for (int j = i - 1; j <= i + 1; j += 2)
            {
                if (j < 0 || j >= list.Count || list[j].Kind == FrontageKind.Blocked) continue;
                float gap = j < i ? list[i].S0 - list[j].S1 : list[j].S0 - list[i].S1;
                if (Mathf.Abs(gap) > 1e-3f) continue;
                if (best < 0 || list[j].Length > list[best].Length) best = j;
            }
            return best;
        }

        static void Coalesce(List<FrontageInterval> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                var a = list[i - 1];
                var b = list[i];
                if (a.Kind != b.Kind || a.Building != b.Building || a.Profile != b.Profile || Mathf.Abs(b.S0 - a.S1) > 1e-3f) continue;
                a.S1 = b.S1;
                list.RemoveAt(i);
            }
        }

        /// <summary>Wall runs for Lot, Vacant, Campus and Cemetery intervals; styles, heights and bands are seeded by stable ids.</summary>
        public static List<WallRun> Walls(IReadOnlyList<FrontageInterval> intervals, FrontageSettings f)
        {
            var inv = CultureInfo.InvariantCulture;
            var runs = new List<WallRun>();
            foreach (var x in intervals)
            {
                WallStyleRule style;
                SplitMix64 rng;
                switch (x.Kind)
                {
                    case FrontageKind.Lot:
                        rng = SplitMix64.For(x.Building.Id, "wall");
                        style = f.PickResidential(rng.NextDouble());
                        break;
                    case FrontageKind.Vacant:
                        rng = SplitMix64.For(x.Chain.Key, "wall:" + x.Side.ToString(inv) + ":" + Mathf.RoundToInt(LotStart(x) * 10f).ToString(inv));
                        style = f.PickResidential(rng.NextDouble());
                        break;
                    case FrontageKind.Campus:
                    case FrontageKind.Cemetery:
                        rng = SplitMix64.For(x.Chain.Key, "wall:" + x.Side.ToString(inv) + ":" + Mathf.RoundToInt(LotStart(x) * 10f).ToString(inv));
                        style = f.Style(x.Kind == FrontageKind.Campus ? WallStyle.Campus : WallStyle.Cemetery);
                        break;
                    default:
                        continue;
                }
                if (style == null) continue;
                var run = new WallRun
                {
                    Chain = x.Chain, Side = x.Side, Profile = x.Profile, S0 = x.S0, S1 = x.S1, Style = style,
                    Height = rng.Range(style.minHeight, style.maxHeight),
                    Band = style.firstBand + rng.RangeInclusive(0, Mathf.Max(0, style.bandCount - 1))
                };
                if (x.Kind == FrontageKind.Lot && CarriesOpening(x)) AddOpening(run, x, f);
                runs.Add(run);
            }
            return runs;
        }

        static void AddOpening(WallRun run, FrontageInterval x, FrontageSettings f)
        {
            float width = x.Length >= f.gateMinLot ? f.gateWidth : x.Length >= f.doorMinLot ? f.doorWidth : 0f;
            if (width <= 0f) return;
            float lo = x.S0 + f.gateEndMargin + width * 0.5f, hi = x.S1 - f.gateEndMargin - width * 0.5f;
            if (lo > hi) return;
            float centre = Mathf.Clamp(Polyline2D.Project(x.Chain.Points, x.Chain.Cum, x.Building.Centroid, out _), lo, hi);
            run.Openings.Add(new WallOpening { S = Straightest(x.Chain, centre, lo, hi, width * 0.5f + f.pilasterSize + 0.05f, f.gateMaxTurnDegrees), Width = width, IsGate = width >= f.gateWidth });
        }

        const float GateSlideStep = 0.1f;

        /// <summary>
        /// The arc length nearest <paramref name="centre"/> within [lo, hi] whose ±reach stretch turns at most maxTurn degrees,
        /// so the gate and its posts stand on a straight part of the wall line; <paramref name="centre"/> when none does.
        /// </summary>
        static float Straightest(RoadChain chain, float centre, float lo, float hi, float reach, float maxTurn)
        {
            int steps = Mathf.CeilToInt((hi - lo) / GateSlideStep) + 1;
            for (int k = 0; k <= steps; k++)
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    if (k == 0 && sign < 0) continue;
                    float s = centre + sign * k * GateSlideStep;
                    if (s < lo - 1e-4f || s > hi + 1e-4f) continue;
                    if (TurnWithin(chain, s - reach, s + reach) <= maxTurn) return s;
                }
            return centre;
        }

        /// <summary>Sum of the centreline's turn angles at the vertices strictly inside (a, b).</summary>
        static float TurnWithin(RoadChain chain, float a, float b)
        {
            var pts = chain.Points;
            float total = 0f;
            for (int i = Mathf.Max(1, Polyline2D.SegmentAt(chain.Cum, a)); i + 1 < pts.Count && chain.Cum[i] < b; i++)
            {
                if (chain.Cum[i] <= a) continue;
                total += Vector2.Angle(pts[i] - pts[i - 1], pts[i + 1] - pts[i]);
            }
            return total;
        }
    }
}
