using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>A door that does not open yet (interiors are a later sub-project).</summary>
    public sealed class LockedDoorInteractable : Interactable
    {
        public const string LockedMessage = "Pintunya terkunci.";

        [SerializeField] AudioClip rattleClip;

        public void Configure(AudioClip rattle) => rattleClip = rattle;

        public override string GetPrompt(in InteractionContext context) => "[E] Buka pintu";

        protected override void OnInteract(in InteractionContext context)
        {
            if (context.Feedback != null) context.Feedback.ShowMessage(LockedMessage);
            var output = Sound;
            if (output != null) output.Play(rattleClip, 1f, 1.1f);
        }
    }
}
