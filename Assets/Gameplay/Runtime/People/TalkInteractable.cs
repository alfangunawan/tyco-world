using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>"[E] Sapa" on a passer-by: they stop, face you and say something about kampung life (spec #6 §2).</summary>
    public sealed class TalkInteractable : Interactable
    {
        public const string PromptText = "[E] Sapa";

        PedestrianSystem system;
        Pedestrian person;

        public Pedestrian Person => person;

        public void Configure(PedestrianSystem owner, Pedestrian who)
        {
            system = owner;
            person = who;
        }

        public override string GetPrompt(in InteractionContext context) => PromptText;

        public override bool CanInteract(in InteractionContext context) =>
            system != null && person != null && person.State != PedestrianState.Flee && person.State != PedestrianState.Chase;

        protected override void OnInteract(in InteractionContext context) => system.TalkTo(person);
    }
}
