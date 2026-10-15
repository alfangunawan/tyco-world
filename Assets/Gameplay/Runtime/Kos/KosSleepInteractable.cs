using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>"[E] Tidur di kos" on the kos door (replaces the locked door at runtime; spec #6 §2).</summary>
    public sealed class KosSleepInteractable : Interactable
    {
        public const string PromptText = "[E] Tidur di kos";

        [SerializeField] KampungDirector director;

        public void Configure(KampungDirector owner) => director = owner;

        public override string GetPrompt(in InteractionContext context) => PromptText;

        public override bool CanInteract(in InteractionContext context) => director != null && !director.IsModalOpen && !director.IsFading;

        protected override void OnInteract(in InteractionContext context) => director.Sleep();
    }
}
