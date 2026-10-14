using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>"[E] Naik motor" on the parked bike (spec #5 §6).</summary>
    [RequireComponent(typeof(Motorbike))]
    public sealed class MotorbikeInteractable : Interactable
    {
        public const string PromptText = "[E] Naik motor";

        Motorbike Bike => GetComponent<Motorbike>();

        public override string GetPrompt(in InteractionContext context) => PromptText;

        public override bool CanInteract(in InteractionContext context)
        {
            var bike = Bike;
            return bike != null && !bike.IsMounted && context.Player != null;
        }

        protected override void OnInteract(in InteractionContext context) => Bike.Mount(context.Player);
    }
}
