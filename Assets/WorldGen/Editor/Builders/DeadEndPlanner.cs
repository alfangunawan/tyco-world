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
    /// carriageway, nor its own road further along (a loop back onto its stem); it is skipped by the kos, inside a parking
    /// lot, and in front of a building that already closes it: one standing across the whole carriageway just ahead.
    /// What closes it: the road-closed barrier near the map edge, a portal on service roads and tracks, a low wall with
    /// pots elsewhere. Chains in id order, no randomness.
    /// </summary>
    public static class DeadEndPlanner
    {
        public const string Walls = "DeadEndWalls", Portals = "DeadEndPortals", Boundaries = "DeadEndBarriers", Skipped = "DeadEndSkipped";
        /// <summary>Rays across the carriageway are this far apart; a gap between buildings narrower than a person stays closed.</summary>
        const float RaySpacing = 0.25f, PassableGap = 0.7f;

        public static List<DeadEnd> Plan(RoadNetwork network, ClassifiedWorld world, StreetContext street, DeadEndSettings s, float kosRadius,
            GenerationReport report)
        {
            var ends = new List<DeadEnd>();
            if (network == null || world == null || s == null || !s.enabled) return ends;
            var parking = world.Areas.Where(a => a.Rule != null && a.Rule.category == AreaCategory.Parking && a.Shape != null).OrderBy(a => a.Id).ToList();
            // Open canopies (building=roof) are walked and driven under: they close nothing.
            var buildings = new SpatialGrid<Polygon>(40f);
            foreach (var b in world.Buildings.OrderBy(b => b.Id))
                if (b.Spec != null && !b.Spec.RoofOnly && b.Spec.Footprint != null && b.Spec.Footprint.Outer.Count >= 3)
                    buildings.Insert(b.Spec.Footprint.Bounds, b.Spec.Footprint);
            var near = new Dictionary<RoadChain, float>();
            var footprints = new HashSet<Polygon>();
            foreach (var chain in network.Chains.OrderBy(c => c.Id).ThenBy(c => c.Index))
            {
                if (chain.Closed || chain.Points.Count < 2) continue;
                var pts = chain.Points;
                Consider(chain, pts[0], (pts[0] - pts[1]).normalized, true);
                Consider(chain, pts[pts.Count - 1], (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized, false);
            }
            return ends;

            void Consider(RoadChain chain, Vector2 end, Vector2 outward, bool atStart)
            {
                network.Nearby(end, network.MaxHalfWidth + s.joinMargin, chain, near);
                foreach (var kv in near)
                    if (kv.Value <= kv.Key.HalfWidth + s.joinMargin) return;
                // Its own road further along, past the stretch the end itself lies on.
                float own = chain.HalfWidth + s.joinMargin, arc = 2f * own;
                float sMin = atStart ? -1f : chain.Length - arc, sMax = atStart ? arc : chain.Length + 1f;
                if (network.DistanceToChain(chain, end, own + 0.1f, sMin, sMax) <= own) return;
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
                return ClosedAhead(chain, end, outward);
            }

            // Rays straight ahead from across the carriageway: closed when buildings stop all of them, but for gaps
            // too narrow to walk through.
            bool ClosedAhead(RoadChain chain, Vector2 end, Vector2 outward)
            {
                float half = chain.HalfWidth, reach = half + s.buildingAhead, r = reach + half;
                footprints.Clear();
                buildings.Query(new Rect(end.x - r, end.y - r, 2f * r, 2f * r), footprints);
                if (footprints.Count == 0) return false;
                var across = new Vector2(-outward.y, outward.x);
                int rays = Mathf.Max(3, Mathf.CeilToInt(2f * half / RaySpacing) + 1);
                float step = 2f * half / (rays - 1), gap = 0f;
                for (int i = 0; i < rays; i++)
                {
                    var from = end + across * (-half + i * step);
                    if (RayHits(from, from + outward * reach)) gap = 0f;
                    else if ((gap += step) >= PassableGap) return false;
                }
                return true;
            }

            bool RayHits(Vector2 from, Vector2 to)
            {
                foreach (var f in footprints)
                {
                    if (f.Contains(from) || f.Contains(to)) return true;
                    var ring = f.Outer;
                    for (int i = 0; i < ring.Count; i++)
                        if (Polygon2D.SegmentDistance(from, to, ring[i], ring[(i + 1) % ring.Count]) < 1e-3f) return true;
                }
                return false;
            }
        }

        static float ToBounds(Vector2 p, Rect b) => Mathf.Min(Mathf.Min(p.x - b.xMin, b.xMax - p.x), Mathf.Min(p.y - b.yMin, b.yMax - p.y));
    }
}
