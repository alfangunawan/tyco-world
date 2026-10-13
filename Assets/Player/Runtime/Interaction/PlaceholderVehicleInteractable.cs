using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>The parked motorbike until the vehicle sub-project: it only answers that riding is not possible yet.</summary>
    public sealed class PlaceholderVehicleInteractable : Interactable
    {
        public const string NotYetMessage = "Belum bisa naik motor.";

        [SerializeField] AudioClip keysClip;

        public void Configure(AudioClip keys) => keysClip = keys;

        public override string GetPrompt(in InteractionContext context) => "[E] Naik motor";

        protected override void OnInteract(in InteractionContext context)
        {
            if (context.Feedback != null) context.Feedback.ShowMessage(NotYetMessage);
            var output = Sound;
            if (output != null) output.Play(keysClip, 0.8f, 1f);
        }
    }
}
