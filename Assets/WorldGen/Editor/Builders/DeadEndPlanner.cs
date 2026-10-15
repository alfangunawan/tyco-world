using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public enum DeadEndKind { Wall, Portal, Boundary }

    /// <summary>One barrier to place: where it stands, which way the road ran out, how wide it is and what kind.</summary>
    public readonly struct DeadEnd
    {
        public readonly Vector2 Position, Outward;
        public readonly float Width;
        public readonly DeadEndKind Kind;
        public readonly long RoadId;

        public DeadEnd(Vector2 position, Vector2 outward, float width, DeadEndKind kind, long roadId)
        {
            Position = position;
            Outward = outward;
            Width = width;
            Kind = kind;
            RoadId = roadId;
        }

        /// <summary>Across the road (left of the outward direction).</summary>
        public Vector2 Across => new Vector2(-Outward.y, Outward.x);
    }

    /// <summary>
    /// The road ends that need closing (spec road-clearance §3). An end is open when it reaches no other road's
    /// carriageway; it is skipped by the kos, inside a parking lot, and in front of a building that already closes it.
    /// What closes it: the road-closed barrier near the map edge, a portal on service roads and tracks, a low wall with
    /// pots elsewhere. Chains in id order, no randomness.
    /// </summary>
    public static class DeadEndPlanner
    {
        public const string Walls = "DeadEndWalls", Portals = "DeadEndPortals", Boundaries = "DeadEndBarriers", Skipped = "DeadEndSkipped";

        public static List<DeadEnd> Plan(RoadNetwork network, ClassifiedWorld world, StreetContext street, DeadEndSettings s, float kosRadius,
            GenerationReport report)
        {
            var ends = new List<DeadEnd>();
            if (network == null || world == null || s == null || !s.enabled) return ends;
            var parking = world.Areas.Where(a => a.Rule != null && a.Rule.category == AreaCategory.Parking && a.Shape != null).OrderBy(a => a.Id).ToList();
            var buildings = new SpatialGrid<Polygon>(40f);
            foreach (var b in world.Buildings.OrderBy(b => b.Id))
                if (b.Spec != null && b.Spec.Footprint != null && b.Spec.Footprint.Outer.Count >= 3)
                    buildings.Insert(b.Spec.Footprint.Bounds, b.Spec.Footprint);
            var near = new Dictionary<RoadChain, float>();
            var footprints = new HashSet<Polygon>();
            foreach (var chain in network.Chains.OrderBy(c => c.Id).ThenBy(c => c.Index))
            {
                if (chain.Closed || chain.Points.Count < 2) continue;
                var pts = chain.Points;
                Consider(chain, pts[0], (pts[0] - pts[1]).normalized);
                Consider(chain, pts[pts.Count - 1], (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized);
            }
            return ends;

            void Consider(RoadChain chain, Vector2 end, Vector2 outward)
            {
                network.Nearby(end, network.MaxHalfWidth + s.joinMargin, chain, near);
                foreach (var kv in near)
                    if (kv.Value <= kv.Key.HalfWidth + s.joinMargin) return;
                if (Skip(chain, end, outward))
                {
                    report.Count(Skipped);
                    return;
                }
                float width = Mathf.Max(s.minWidth, chain.Width + 2f * network.MaxEdgeWidth(chain.Id));
                var kind = ToBounds(end, world.LocalBounds) < s.boundaryDistance ? DeadEndKind.Boundary
                    : s.portalHighways != null && s.portalHighways.Contains(chain.Highway) ? DeadEndKind.Portal
                    : DeadEndKind.Wall;
                ends.Add(new DeadEnd(end - outward * s.endInset, outward, width, kind, chain.Id));
                report.Count(kind == DeadEndKind.Wall ? Walls : kind == DeadEndKind.Portal ? Portals : Boundaries);
            }

            bool Skip(RoadChain chain, Vector2 end, Vector2 outward)
            {
                if (street != null && street.DistanceToKos(end) < kosRadius) return true;
                foreach (var area in parking)
                    if (area.Shape.Contains(end)) return true;
                float reach = chain.HalfWidth + s.buildingAhead;
                var ahead = end + outward * reach;
                footprints.Clear();
                buildings.Query(new Rect(end.x - reach, end.y - reach, 2f * reach, 2f * reach), footprints);
                foreach (var f in footprints)
                    if (f.Contains(ahead) || f.Contains(end) || f.DistanceToBoundary(end) <= reach)
                        return true;
                return false;
            }
        }

        static float ToBounds(Vector2 p, Rect b) => Mathf.Min(Mathf.Min(p.x - b.xMin, b.xMax - p.x), Mathf.Min(p.y - b.yMin, b.yMax - p.y));
    }
}
