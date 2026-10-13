using System;
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Something the player can use with E. Interacted (with InteractionId) is the hook for the story system.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] string interactionId = "";
        [SerializeField] AudioSource audioSource;

        ISoundPlayer soundPlayer;

        public static event Action<Interactable> Interacted;

        /// <summary>Clears static subscribers; runs automatically before each Play session (domain reload is off).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics() => Interacted = null;

        public string InteractionId => interactionId;
        public AudioSource AudioOutput => audioSource;

        public void SetInteractionId(string id) => interactionId = id ?? "";

        public void SetAudioSource(AudioSource source)
        {
            audioSource = source;
            soundPlayer = null;
        }

        public void SetSoundPlayer(ISoundPlayer player) => soundPlayer = player;

        protected ISoundPlayer Sound
        {
            get
            {
                if (soundPlayer == null && audioSource != null) soundPlayer = new AudioSourceSoundPlayer(audioSource);
                return soundPlayer;
            }
        }

        public abstract string GetPrompt(in InteractionContext context);

        public virtual bool CanInteract(in InteractionContext context) => true;

        public bool Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return false;
            OnInteract(context);
            Interacted?.Invoke(this);
            return true;
        }

        protected abstract void OnInteract(in InteractionContext context);
    }
}
