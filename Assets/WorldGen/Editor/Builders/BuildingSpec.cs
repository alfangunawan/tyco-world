namespace TycoWorld.WorldGen.Authoring
{
    public enum RoofOverride { Auto, Flat, Gable }

    public enum RoofKind { Flat, Gable, RoofOnly }

    /// <summary>Everything the mesh builder needs to know about one building.</summary>
    public sealed class BuildingSpec
    {
        public long Id;
        public Polygon Footprint;
        public float WallHeight;
        public int Levels = 1;
        public float LevelHeight = 3.2f;
        public bool AllowGable;
        public bool RoofOnly;
        public RoofOverride RoofOverride;
    }

    public struct BuildingMeshParams
    {
        public float FacadeTileWidth, ParapetHeight, GableMaxArea, GableMinRectangularity, GablePitchRatio, GableOverhang;

        public static BuildingMeshParams Default => new BuildingMeshParams
        {
            FacadeTileWidth = 3f, ParapetHeight = 0.6f, GableMaxArea = 200f,
            GableMinRectangularity = 0.85f, GablePitchRatio = 0.25f, GableOverhang = 0.3f
        };
    }

    public struct BuildingBuildInfo
    {
        public RoofKind Roof;
        public Obb Obb;
        public bool RoofFailed;
    }
}
