using TycoWorld.Vehicles;

namespace TycoWorld.Gameplay
{
    /// <summary>A ridden bike's input while a gameplay window (the phone) is open: brake held, nothing else.</summary>
    public sealed class HoldBrakeIntentSource : IVehicleIntentSource
    {
        public VehicleIntent Read(float dt) => new VehicleIntent { Brake = true };
    }
}
