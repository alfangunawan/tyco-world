using System;
using TycoWorld.Player;

namespace TycoWorld.Gameplay
{
    /// <summary>An E target whose prompt and effect a mission supplies (hand over the galon, pick up the cat...).</summary>
    public sealed class ActionInteractable : Interactable
    {
        Func<string> prompt;
        Func<bool> can;
        Action action;

        public void Configure(Func<string> promptText, Action onInteract, Func<bool> canInteract = null)
        {
            prompt = promptText;
            action = onInteract;
            can = canInteract;
        }

        public override string GetPrompt(in InteractionContext context) => prompt != null ? prompt() ?? "" : "";

        public override bool CanInteract(in InteractionContext context) => action != null && (can == null || can());

        protected override void OnInteract(in InteractionContext context) => action();
    }
}
