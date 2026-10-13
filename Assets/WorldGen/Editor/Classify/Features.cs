using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class LinePiece
    {
        public readonly List<Vector2> Points;
        public readonly bool ExtendStart;
        public readonly bool ExtendEnd;

        public LinePiece(List<Vector2> points, bool extendStart, bool extendEnd)
        {
            Points = points;
            ExtendStart = extendStart;
            ExtendEnd = extendEnd;
        }
    }

    public sealed class RoadFeature
    {
        public long Id;
        public string Name;
        public string Highway;
        public RoadRule Rule;
        public float Width;
        public float Y;
        public readonly List<LinePiece> Pieces = new List<LinePiece>();
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class BuildingFeature
    {
        public long Id;
        public OsmElementType ElementType;
        public string Name;
        public BuildingSpec Spec;
        public BuildingRule Rule;
        public bool IsCampus;
        public KeyBuilding Key;
        public Vector2 Centroid;
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class AreaFeature
    {
        public long Id;
        public OsmElementType ElementType;
        public string Name;
        public AreaRule Rule;
        public Polygon Shape;
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class WaterLineFeature
    {
        public long Id;
        public string Name;
        public float Width;
        public float Y;
        public readonly List<LinePiece> Pieces = new List<LinePiece>();
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class WaterAreaFeature
    {
        public long Id;
        public OsmElementType ElementType;
        public string Name;
        public Polygon Shape;
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class TreeFeature
    {
        public readonly long Id;
        public readonly Vector2 Position;

        public TreeFeature(long id, Vector2 position)
        {
            Id = id;
            Position = position;
        }
    }

    public sealed class ClassifiedWorld
    {
        public readonly Rect LocalBounds;
        public readonly List<RoadFeature> Roads = new List<RoadFeature>();
        public readonly List<BuildingFeature> Buildings = new List<BuildingFeature>();
        public readonly List<AreaFeature> Areas = new List<AreaFeature>();
        public readonly List<WaterLineFeature> WaterLines = new List<WaterLineFeature>();
        public readonly List<WaterAreaFeature> WaterAreas = new List<WaterAreaFeature>();
        public readonly List<TreeFeature> Trees = new List<TreeFeature>();

        public ClassifiedWorld(Rect localBounds) { LocalBounds = localBounds; }
    }
}
