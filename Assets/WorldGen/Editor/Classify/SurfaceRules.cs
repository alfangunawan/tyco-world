using System;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Maps what the generator draws to the walkable surface it sounds like (spec #2 §5).</summary>
    public static class SurfaceRules
    {
        public const SurfaceType Ground = SurfaceType.Dirt;

        public static SurfaceType ForMaterialName(string name)
        {
            switch (name)
            {
                case "Road_Asphalt": return SurfaceType.Asphalt;
                case "Path_Paving":
                case "Area_Campus": return SurfaceType.Paving;
                case "Area_Parking":
                case "Area_Hardground": return SurfaceType.Concrete;
                case "Area_Grass":
                case "Area_Farmland":
                case "Area_Pitch": return SurfaceType.Grass;
                case "Ground_Dirt":
                case "Area_Cemetery": return SurfaceType.Dirt;
                case "Water": return SurfaceType.Water;
                case "Edge_Sidewalk": return SurfaceType.Paving;
                case "Edge_Concrete":
                case "Edge_Gutter": return SurfaceType.Concrete;
                default: return SurfaceType.Default;
            }
        }

        public static SurfaceType ForRoad(string highway) => ForMaterialName(ArtAssetBuilder.RoadMaterialName(highway));

        public static SurfaceType ForArea(AreaCategory category) => ForMaterialName(ArtAssetBuilder.AreaMaterialName(category));

        /// <summary>Drawn material first; when it is unknown or missing, the feature kind and category decide.</summary>
        public static SurfaceType For(BuiltFeature feature)
        {
            if (feature.Materials != null && feature.Materials.Length > 0 && feature.Materials[0] != null)
            {
                var byMaterial = ForMaterialName(feature.Materials[0].name);
                if (byMaterial != SurfaceType.Default) return byMaterial;
            }
            switch (feature.Kind)
            {
                case FeatureKind.Road: return ForRoad(feature.Category);
                case FeatureKind.Area:
                    return Enum.TryParse(feature.Category, out AreaCategory category) ? ForArea(category) : SurfaceType.Default;
                case FeatureKind.WaterArea:
                case FeatureKind.WaterLine: return SurfaceType.Water;
                case FeatureKind.RoadEdge: return feature.Name == RoadEdgeBuilder.SidewalkName ? SurfaceType.Paving : SurfaceType.Concrete;
                default: return SurfaceType.Default;
            }
        }
    }
}
