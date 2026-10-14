namespace TycoWorld.Vehicles
{
    /// <summary>Where a ridden bike's input comes from: the keyboard/gamepad reader or a scripted source (tests, MCP).</summary>
    public interface IVehicleIntentSource
    {
        VehicleIntent Read(float dt);
    }
}
