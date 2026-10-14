namespace TycoWorld.WorldGen
{
    /// <summary>Structures (roads, areas, water, buildings) stay until the view distance; props (trees, lamp posts) go sooner.</summary>
    public enum CullCategory
    {
        Structure,
        Prop
    }
}
