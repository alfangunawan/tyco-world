using System.Collections.Generic;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Extracts the kos footprint, nearby road pieces and neighbouring footprints from classified OSM data.</summary>
    public static class KosSiteSource
    {
        public static bool TryBuild(ClassifiedWorld world, long kosId, float radius, out List<Vector2> footprint,
            out List<RoadLine> roads, out List<IReadOnlyList<Vector2>> neighbours)
        {
            footprint = null;
            roads = new List<RoadLine>();
            neighbours = new List<IReadOnlyList<Vector2>>();
            BuildingFeature kos = null;
            foreach (var b in world.Buildings)
                if (b.Id == kosId) { kos = b; break; }
            if (kos == null) return false;

            footprint = new List<Vector2>(kos.Spec.Footprint.Outer);
            var center = kos.Centroid;
            foreach (var road in world.Roads)
                foreach (var piece in road.Pieces)
                    if (Near(piece.Points, center, radius))
                        roads.Add(new RoadLine { Id = road.Id, Points = new List<Vector2>(piece.Points), HalfWidth = road.Width * 0.5f });
            foreach (var b in world.Buildings)
                if (b.Id != kosId && Vector2.Distance(b.Centroid, center) <= radius) neighbours.Add(b.Spec.Footprint.Outer);
            return true;
        }

        static bool Near(List<Vector2> points, Vector2 center, float radius)
        {
            for (int i = 0; i + 1 < points.Count; i++)
                if (Polygon2D.DistanceToSegment(center, points[i], points[i + 1]) <= radius) return true;
            return false;
        }
    }
}
