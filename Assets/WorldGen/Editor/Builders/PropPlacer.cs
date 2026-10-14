using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public struct PropPlacement
    {
        public Vector2 Position;
        public float YawDegrees;
        public float Scale;
        public int Variant;
    }

    /// <summary>A straight piece of wall or closed gate that lamps and trees keep clear of (spec street-frontage §9).</summary>
    public readonly struct WallSegment
    {
        public readonly Vector2 A, B;

        public WallSegment(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }
    }

    /// <summary>Deterministic placement of trees (OSM nodes + Poisson scatter) and street lamps.</summary>
    public sealed class PropPlacer
    {
        public const float MinTreeGap = 2f;
        public const float TreeBuildingBuffer = 1f;
        public const float TreeRoadClearance = 1f;
        public const float LampBuildingBuffer = 0.3f;
        public const float LampRoadClearance = 0.2f;
        public const float LampOwnRoadClearance = 0.05f;
        public const float TreeEdgeClearance = 0.5f;
        public const float LampWallClearance = 0.35f;
        public const float TreeWallClearance = 0.8f;

        readonly struct RoadSegment
        {
            public readonly Vector2 A, B;
            public readonly float HalfWidth, EdgeReach;
            public readonly long RoadId;

            public RoadSegment(Vector2 a, Vector2 b, float halfWidth, float edgeReach, long roadId)
            {
                A = a;
                B = b;
                HalfWidth = halfWidth;
                EdgeReach = edgeReach;
                RoadId = roadId;
            }
        }

        readonly SpatialGrid<BuildingFeature> buildingGrid = new SpatialGrid<BuildingFeature>(20f);
        readonly SpatialGrid<int> roadGrid = new SpatialGrid<int>(20f);
        readonly List<RoadSegment> segments = new List<RoadSegment>();
        readonly SpatialGrid<int> wallGrid = new SpatialGrid<int>(20f);
        readonly List<WallSegment> walls = new List<WallSegment>();
        readonly IEdgeLookup edges;
        readonly RoadEdgeSettings edgeSettings;
        readonly float maxEdgeReach;

        public PropPlacer(IReadOnlyList<BuildingFeature> buildings, IReadOnlyList<RoadFeature> roads, IEdgeLookup edges = null,
            RoadEdgeSettings edgeSettings = null)
        {
            this.edges = edges != null && edgeSettings != null ? edges : null;
            this.edgeSettings = edgeSettings;
            foreach (var b in buildings) buildingGrid.Insert(b.Spec.Footprint.Bounds, b);
            foreach (var road in roads)
            {
                float hw = road.Width * 0.5f;
                float reach = this.edges != null ? this.edges.MaxEdgeWidth(road.Id) : 0f;
                maxEdgeReach = Mathf.Max(maxEdgeReach, reach);
                foreach (var piece in road.Pieces)
                    for (int i = 0; i + 1 < piece.Points.Count; i++)
                    {
                        Vector2 a = piece.Points[i], b = piece.Points[i + 1];
                        var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - hw, Mathf.Min(a.y, b.y) - hw, Mathf.Max(a.x, b.x) + hw, Mathf.Max(a.y, b.y) + hw);
                        roadGrid.Insert(rect, segments.Count);
                        segments.Add(new RoadSegment(a, b, hw, reach, road.Id));
                    }
            }
        }

        static Rect Around(Vector2 p, float radius) => Rect.MinMaxRect(p.x - radius, p.y - radius, p.x + radius, p.y + radius);

        /// <summary>Extra footprints (infill houses) that trees and lamps must avoid like OSM buildings.</summary>
        public void AddBuildings(IEnumerable<BuildingFeature> extra)
        {
            foreach (var b in extra) buildingGrid.Insert(b.Spec.Footprint.Bounds, b);
        }

        public void AddWalls(IEnumerable<WallSegment> segmentsToAvoid)
        {
            foreach (var w in segmentsToAvoid)
            {
                wallGrid.Insert(Rect.MinMaxRect(Mathf.Min(w.A.x, w.B.x), Mathf.Min(w.A.y, w.B.y), Mathf.Max(w.A.x, w.B.x), Mathf.Max(w.A.y, w.B.y)), walls.Count);
                walls.Add(w);
            }
        }

        public bool IsNearWall(Vector2 p, float clearance)
        {
            if (walls.Count == 0) return false;
            var hits = new HashSet<int>();
            wallGrid.Query(Around(p, clearance), hits);
            foreach (int i in hits)
                if (Polygon2D.DistanceToSegment(p, walls[i].A, walls[i].B) < clearance) return true;
            return false;
        }

        public bool IsInsideBuilding(Vector2 p, float buffer)
        {
            var hits = new HashSet<BuildingFeature>();
            buildingGrid.Query(Around(p, buffer), hits);
            foreach (var b in hits)
            {
                var footprint = b.Spec.Footprint;
                if (footprint.Contains(p)) return true;
                if (buffer > 0f && footprint.DistanceToBoundary(p) < buffer) return true;
            }
            return false;
        }

        public bool IsOnRoad(Vector2 p, float clearance)
        {
            var hits = new HashSet<int>();
            roadGrid.Query(Around(p, clearance), hits);
            foreach (int i in hits)
            {
                var s = segments[i];
                if (Polygon2D.DistanceToSegment(p, s.A, s.B) < s.HalfWidth + clearance) return true;
            }
            return false;
        }

        /// <summary>Scatter trees keep hw + max(1, edge width + 0.5) from every road (spec street-frontage §9).</summary>
        bool TooCloseToRoadForTree(Vector2 p)
        {
            var hits = new HashSet<int>();
            roadGrid.Query(Around(p, Mathf.Max(TreeRoadClearance, maxEdgeReach + TreeEdgeClearance)), hits);
            foreach (int i in hits)
            {
                var s = segments[i];
                float clearance = Mathf.Max(TreeRoadClearance, s.EdgeReach + TreeEdgeClearance);
                if (Polygon2D.DistanceToSegment(p, s.A, s.B) < s.HalfWidth + clearance) return true;
            }
            return false;
        }

        /// <summary>A lamp may stand right beside its own asphalt (on the shoulder) but keeps 0.2 m off other roads.</summary>
        bool BlocksLamp(Vector2 p, long ownRoad)
        {
            var hits = new HashSet<int>();
            roadGrid.Query(Around(p, LampRoadClearance), hits);
            foreach (int i in hits)
            {
                var s = segments[i];
                float clearance = s.RoadId == ownRoad ? LampOwnRoadClearance : LampRoadClearance;
                if (Polygon2D.DistanceToSegment(p, s.A, s.B) < s.HalfWidth + clearance) return true;
            }
            return false;
        }

        public List<PropPlacement> PlaceTrees(IReadOnlyList<TreeFeature> osmTrees, IReadOnlyList<AreaFeature> areas,
            int maxTrees, int variantCount, GenerationReport report, IReadOnlyList<Polygon> noTreeZones = null)
        {
            var accepted = new List<Vector2>();
            var gapGrid = new SpatialGrid<int>(MinTreeGap * 2f);

            bool TryReserve(Vector2 p)
            {
                var near = new HashSet<int>();
                gapGrid.Query(Around(p, MinTreeGap), near);
                foreach (int i in near)
                    if ((accepted[i] - p).sqrMagnitude < MinTreeGap * MinTreeGap) return false;
                gapGrid.Insert(new Rect(p, Vector2.zero), accepted.Count);
                accepted.Add(p);
                return true;
            }

            var fromOsm = new List<PropPlacement>();
            foreach (var tree in osmTrees.OrderBy(t => t.Id))
            {
                if (IsInsideBuilding(tree.Position, 0f) || IsNearWall(tree.Position, TreeWallClearance) || !TryReserve(tree.Position)) continue;
                fromOsm.Add(Make(tree.Position, SplitMix64.For(tree.Id, "tree"), variantCount));
            }

            var blocked = new List<(Rect bounds, Polygon shape)>();
            foreach (var area in areas)
                if (area.Shape != null && (area.Rule == null || area.Rule.treeSpacing <= 0f)) blocked.Add((area.Shape.Bounds, area.Shape));
            if (noTreeZones != null)
                foreach (var zone in noTreeZones)
                    if (zone != null) blocked.Add((zone.Bounds, zone));

            bool InNoTreeZone(Vector2 p)
            {
                foreach (var (zoneBounds, shape) in blocked)
                    if (zoneBounds.Contains(p) && shape.Contains(p)) return true;
                return false;
            }

            var scatter = new List<(ulong key, PropPlacement placement)>();
            foreach (var area in areas.OrderBy(a => a.Id))
            {
                float spacing = area.Rule != null ? area.Rule.treeSpacing : 0f;
                if (spacing <= 0f) continue;
                var rng = SplitMix64.For(area.Id, "trees");
                foreach (var p in PoissonDisk.Sample(area.Shape.Bounds, spacing, rng))
                {
                    if (!area.Shape.Contains(p) || InNoTreeZone(p) || IsInsideBuilding(p, TreeBuildingBuffer) || TooCloseToRoadForTree(p) ||
                        IsNearWall(p, TreeWallClearance)) continue;
                    if (!TryReserve(p)) continue;
                    scatter.Add((rng.NextULong(), Make(p, rng, variantCount)));
                }
            }

            int room = Mathf.Max(0, maxTrees - fromOsm.Count);
            if (scatter.Count > room)
            {
                scatter.Sort((a, b) => a.key.CompareTo(b.key));
                report.TreesDroppedByCap = scatter.Count - room;
                scatter.RemoveRange(room, scatter.Count - room);
            }
            var result = new List<PropPlacement>(fromOsm);
            foreach (var s in scatter) result.Add(s.placement);
            report.TreesPlaced = result.Count;
            return result;
        }

        static PropPlacement Make(Vector2 p, SplitMix64 rng, int variantCount) => new PropPlacement
        {
            Position = p,
            YawDegrees = rng.Range(0f, 360f),
            Scale = rng.Range(0.8f, 1.2f),
            Variant = variantCount > 0 ? rng.RangeInclusive(0, variantCount - 1) : 0
        };

        public List<PropPlacement> PlaceLamps(IReadOnlyList<RoadFeature> roads, float minSpacing, float offset, GenerationReport report)
        {
            var result = new List<PropPlacement>();
            var grid = new SpatialGrid<int>(Mathf.Max(minSpacing, 1f) * 2f);
            foreach (var road in roads.OrderBy(r => r.Id))
            {
                float spacing = road.Rule != null ? road.Rule.lampSpacing : 0f;
                if (spacing <= 0f) continue;
                float hw = road.Width * 0.5f;
                int side = 1;
                foreach (var piece in road.Pieces)
                {
                    float next = spacing * 0.5f, travelled = 0f;
                    for (int i = 0; i + 1 < piece.Points.Count; i++)
                    {
                        Vector2 a = piece.Points[i], b = piece.Points[i + 1];
                        float length = Vector2.Distance(a, b);
                        if (length < 1e-4f) continue;
                        Vector2 dir = (b - a) / length;
                        var left = new Vector2(-dir.y, dir.x);
                        while (next <= travelled + length)
                        {
                            Vector2 center = a + dir * (next - travelled);
                            Vector2 sideDir = left * side;
                            Vector2 position = center + sideDir * (hw + LampOffset(road.Id, center, sideDir, offset));
                            Vector2 towardRoad = -sideDir;
                            if (!IsInsideBuilding(position, LampBuildingBuffer) && !BlocksLamp(position, road.Id) &&
                                !IsNearWall(position, LampWallClearance) && !NearOther(position, result, grid, minSpacing))
                            {
                                grid.Insert(new Rect(position, Vector2.zero), result.Count);
                                result.Add(new PropPlacement
                                {
                                    Position = position,
                                    YawDegrees = Mathf.Atan2(towardRoad.x, towardRoad.y) * Mathf.Rad2Deg,
                                    Scale = 1f,
                                    Variant = 0
                                });
                            }
                            side = -side;
                            next += spacing;
                        }
                        travelled += length;
                    }
                }
            }
            report.LampsPlaced = result.Count;
            return result;
        }

        /// <summary>Lamp distance beyond the asphalt edge: on the sidewalk, on the gutter shoulder, or the old fixed offset.</summary>
        float LampOffset(long roadId, Vector2 center, Vector2 sideDir, float fallback)
        {
            if (edges == null) return fallback;
            var profile = edges.ProfileAt(roadId, center, sideDir);
            if (profile == EdgeProfile.Sidewalk) return edgeSettings.sidewalkLampOffset;
            if (profile == EdgeProfile.Gutter) return edgeSettings.gutterLampOffset;
            return fallback;
        }

        static bool NearOther(Vector2 p, List<PropPlacement> placed, SpatialGrid<int> grid, float minSpacing)
        {
            var near = new HashSet<int>();
            grid.Query(Around(p, minSpacing), near);
            foreach (int i in near)
                if ((placed[i].Position - p).sqrMagnitude < minSpacing * minSpacing) return true;
            return false;
        }
    }
}
