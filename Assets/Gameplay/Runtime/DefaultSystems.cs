namespace TycoWorld.Gameplay
{
    /// <summary>The systems a live game runs (spec #6 §6); each phase of the sub-project adds its own here.</summary>
    public static class DefaultSystems
    {
        public static void AddTo(KampungDirector director)
        {
            director.AddSystem(new PedestrianSystem());
            director.AddSystem(new VehicleSystem());
            director.AddSystem(new MissionSystem());
            director.AddSystem(new RiderBodySystem());
        }
    }
}
