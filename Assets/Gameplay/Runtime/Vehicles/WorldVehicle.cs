using TycoWorld.Player;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    public enum VehicleKind
    {
        Bike,
        Car
    }

    /// <summary>A warga's parked bike or car (spec #6 §2): where it belongs, and what the player has done to it.</summary>
    public sealed class WorldVehicle
    {
        public readonly VehicleKind Kind;
        public readonly Motorbike Body;
        public readonly StealVehicleInteractable Lock;
        /// <summary>Index of its parking spot in KampungData.spots.</summary>
        public int Spot = -1;
        /// <summary>The player broke the lock: E now simply gets on.</summary>
        public bool Unlocked;
        /// <summary>Taken from its spot (stays where the player leaves it until it is far away).</summary>
        public bool Moved;
        /// <summary>Warga saw it stolen and it has not been repainted.</summary>
        public bool Hot;
        public int Colour;

        public WorldVehicle(VehicleKind kind, Motorbike body, VehicleSystem system)
        {
            Kind = kind;
            Body = body;
            Lock = body.gameObject.AddComponent<StealVehicleInteractable>();
            Lock.Configure(system, this);
            Lock.SetInteractionId(kind == VehicleKind.Bike ? "warga-bike" : "warga-car");
        }

        public GameObject Root => Body.gameObject;
        public Vector3 Position => Body.transform.position;
        public bool Active => Body != null && Body.gameObject.activeSelf;
        public string Noun => Kind == VehicleKind.Bike ? "motor" : "mobil";
    }

    /// <summary>"[E] Bobol kunci motor/mobil" on a warga's vehicle, "[E] Naik motor/Masuk mobil" once the lock is broken.</summary>
    public sealed class StealVehicleInteractable : Interactable
    {
        VehicleSystem system;
        WorldVehicle vehicle;

        public WorldVehicle Vehicle => vehicle;

        public void Configure(VehicleSystem owner, WorldVehicle target)
        {
            system = owner;
            vehicle = target;
        }

        public override string GetPrompt(in InteractionContext context)
        {
            if (vehicle == null) return "";
            if (vehicle.Unlocked) return vehicle.Kind == VehicleKind.Bike ? "[E] Naik motor" : "[E] Masuk mobil";
            return "[E] Bobol kunci " + vehicle.Noun;
        }

        public override bool CanInteract(in InteractionContext context) =>
            system != null && vehicle != null && context.Player != null && system.CanUse(vehicle);

        protected override void OnInteract(in InteractionContext context) => system.Use(vehicle);
    }
}
