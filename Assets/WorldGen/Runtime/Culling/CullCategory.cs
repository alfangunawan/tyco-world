namespace TycoWorld.WorldGen
{
    /// <summary>
    /// Structures (roads, areas, water, buildings) stay until the view distance; props (trees, lamp posts) go sooner; details
    /// (property-line walls, road edges: low and fog-covered from afar) go soonest (spec frontage-polish §2.6).
    /// </summary>
    public enum CullCategory
    {
        Structure,
        Prop,
        Detail
    }
}
