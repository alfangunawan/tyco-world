using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>One-shot sound output; tests replace it with a recorder.</summary>
    public interface ISoundPlayer
    {
        void Play(AudioClip clip, float volume, float pitch);
    }
}
