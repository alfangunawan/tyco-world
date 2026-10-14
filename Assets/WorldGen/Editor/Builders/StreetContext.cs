using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>A footprint the street builders must respect: an OSM building or an infill house.</summary>
    public sealed class StreetBuilding
    {
        public long Id;
        public Polygon Footprint;
        public Vector2 Centroid;
        /// <summary>Key building replaced by a hand-made prefab: no wall and no gate in front of it.</summary>
        public bool Excluded;
        public bool IsInfill;
    }

    /// <summary>Spatial lookups shared by the edge, frontage and infill builders (spec street-frontage §5, §7, §8).</summary>
    public sealed class StreetContext
    {
        public readonly Rect Bounds;
        /// <summary>Footprint of the player's kos (FrontageSettings.kosBuildingId), or null when it is not in the data.</summary>
        public readonly Polygon Kos;
        public readonly List<StreetBuilding> Buildings = new List<StreetBuilding>();

        readonly SpatialGrid<StreetBuilding> buildingGrid = new SpatialGrid<StreetBuilding>(20f);
        readonly HashSet<StreetBuilding> hits = new HashSet<StreetBuilding>();
        readonly List<Polygon> water = new List<Polygon>();
        readonly List<(Vector2 a, Vector2 b, float halfWidth)> waterSegs = new List<(Vector2 a, Vector2 b, float halfWidth)>();
        readonly SpatialGrid<int> waterGrid = new SpatialGrid<int>(20f);
        readonly HashSet<int> waterHits = new HashSet<int>();
        readonly List<AreaFeature> areas = new List<AreaFeature>();
        readonly float maxWaterHalfWidth;

        public StreetContext(Rect bounds, IEnumerable<StreetBuilding> buildings, IEnumerable<WaterAreaFeature> waterAreas,
            IEnumerable<WaterLineFeature> waterLines, IEnumerable<AreaFeature> areas, Polygon kos)
        {
            Bounds = bounds;
            Kos = kos;
            foreach (var b in buildings) AddBuilding(b);
            foreach (var w in waterAreas)
                if (w.Shape != null) water.Add(w.Shape);
            foreach (var line in waterLines)
            {
                float hw = line.Width * 0.5f;
                maxWaterHalfWidth = Mathf.Max(maxWaterHalfWidth, hw);
                foreach (var piece in line.Pieces)
                    for (int i = 0; i + 1 < piece.Points.Count; i++)
                    {
                        Vector2 a = piece.Points[i], b = piece.Points[i + 1];
                        waterGrid.Insert(Rect.MinMaxRect(Mathf.Min(a.x, b.x) - hw, Mathf.Min(a.y, b.y) - hw, Mathf.Max(a.x, b.x) + hw, Mathf.Max(a.y, b.y) + hw), waterSegs.Count);
                        waterSegs.Add((a, b, hw));
                    }
            }
            foreach (var a in areas)
                if (a.Shape != null && a.Rule != null) this.areas.Add(a);
        }

        public static StreetContext From(ClassifiedWorld world, WorldGenSettings settings)
        {
            var buildings = new List<StreetBuilding>();
            Polygon kos = null;
            foreach (var b in world.Buildings)
            {
                buildings.Add(new StreetBuilding
                {
                    Id = b.Id, Footprint = b.Spec.Footprint, Centroid = b.Centroid,
                    Excluded = b.Key != null && b.Key.prefab != null
                });
                if (b.Id == settings.frontage.kosBuildingId) kos = b.Spec.Footprint;
            }
            return new StreetContext(world.LocalBounds, buildings, world.WaterAreas, world.WaterLines, world.Areas, kos);
        }

        public void AddBuilding(StreetBuilding b)
        {
            Buildings.Add(b);
            buildingGrid.Insert(b.Footprint.Bounds, b);
        }

        public bool InBounds(Vector2 p) => Bounds.Contains(p);

        /// <summary>The building whose footprint contains p (smallest id when several do), or null.</summary>
        public StreetBuilding BuildingAt(Vector2 p)
        {
            hits.Clear();
            buildingGrid.Query(new Rect(p, Vector2.zero), hits);
            StreetBuilding found = null;
            foreach (var b in hits)
                if (b.Footprint.Contains(p) && (found == null || b.Id < found.Id)) found = b;
            return found;
        }

        public bool InsideBuilding(Vector2 p) => BuildingAt(p) != null;

        public bool InWater(Vector2 p)
        {
            foreach (var w in water)
                if (w.Bounds.Contains(p) && w.Contains(p)) return true;
            if (waterSegs.Count == 0) return false;
            waterHits.Clear();
            waterGrid.Query(new Rect(p, Vector2.zero), waterHits);
            foreach (int i in waterHits)
            {
                var (a, b, hw) = waterSegs[i];
                if (Polygon2D.DistanceToSegment(p, a, b) < hw) return true;
            }
            return false;
        }

        public bool InArea(Vector2 p, AreaCategory category)
        {
            foreach (var a in areas)
                if (a.Rule.category == category && a.Shape.Bounds.Contains(p) && a.Shape.Contains(p)) return true;
            return false;
        }

        public bool InAnyArea(Vector2 p)
        {
            foreach (var a in areas)
                if (a.Shape.Bounds.Contains(p) && a.Shape.Contains(p)) return true;
            return false;
        }

        public float DistanceToKos(Vector2 p) =>
            Kos == null ? float.PositiveInfinity : Kos.Contains(p) ? 0f : Kos.DistanceToBoundary(p);

        /// <summary>First building the ray (dir normalised) meets within maxDistance; distance 0 when origin is inside one; null when none.</summary>
        public StreetBuilding Raycast(Vector2 origin, Vector2 dir, float maxDistance, out float distance)
        {
            var inside = BuildingAt(origin);
            if (inside != null)
            {
                distance = 0f;
                return inside;
            }
            distance = float.PositiveInfinity;
            Vector2 end = origin + dir * maxDistance;
            hits.Clear();
            buildingGrid.Query(Rect.MinMaxRect(Mathf.Min(origin.x, end.x), Mathf.Min(origin.y, end.y), Mathf.Max(origin.x, end.x), Mathf.Max(origin.y, end.y)), hits);
            StreetBuilding best = null;
            foreach (var b in hits)
            {
                float d = Polyline2D.RayToRing(origin, dir, b.Footprint.Outer, maxDistance);
                if (d < distance || (d == distance && best != null && b.Id < best.Id))
                {
                    distance = d;
                    best = b;
                }
            }
            return best;
        }

        public bool OverlapsBuilding(IReadOnlyList<Vector2> ring)
        {
            hits.Clear();
            buildingGrid.Query(Polygon2D.Bounds(ring), hits);
            foreach (var b in hits)
                if (Polygon2D.RingsOverlap(ring, b.Footprint.Outer)) return true;
            return false;
        }

        public bool OverlapsWater(IReadOnlyList<Vector2> ring)
        {
            foreach (var w in water)
                if (Polygon2D.RingsOverlap(ring, w.Outer)) return true;
            if (waterSegs.Count == 0) return false;
            var b = Polygon2D.Bounds(ring);
            waterHits.Clear();
            waterGrid.Query(Rect.MinMaxRect(b.xMin - maxWaterHalfWidth, b.yMin - maxWaterHalfWidth, b.xMax + maxWaterHalfWidth, b.yMax + maxWaterHalfWidth), waterHits);
            foreach (int i in waterHits)
            {
                var (a, c, hw) = waterSegs[i];
                if (Polygon2D.ContainsPoint(ring, a) || Polygon2D.ContainsPoint(ring, c)) return true;
                for (int k = 0; k < ring.Count; k++)
                    if (Polygon2D.SegmentDistance(ring[k], ring[(k + 1) % ring.Count], a, c) < hw) return true;
            }
            return false;
        }

        public bool OverlapsArea(IReadOnlyList<Vector2> ring)
        {
            foreach (var a in areas)
                if (Polygon2D.RingsOverlap(ring, a.Shape.Outer)) return true;
            return false;
        }

        public bool NearKos(IReadOnlyList<Vector2> ring, float radius)
        {
            if (Kos == null) return false;
            if (Polygon2D.RingsOverlap(ring, Kos.Outer)) return true;
            foreach (var p in ring)
                if (Kos.DistanceToBoundary(p) < radius) return true;
            foreach (var p in Kos.Outer)
                if (Polygon2D.DistanceToRing(ring, p) < radius) return true;
            return false;
        }
    }
}
