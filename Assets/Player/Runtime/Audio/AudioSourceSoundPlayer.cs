using UnityEngine;

namespace TycoWorld.Player
{
    public sealed class AudioSourceSoundPlayer : ISoundPlayer
    {
        readonly AudioSource source;

        public AudioSourceSoundPlayer(AudioSource source) { this.source = source; }

        public void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || source == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
