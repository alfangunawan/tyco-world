using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    internal sealed class RecordingSoundPlayer : ISoundPlayer
    {
        public readonly List<(AudioClip Clip, float Volume, float Pitch)> Calls = new List<(AudioClip, float, float)>();

        public void Play(AudioClip clip, float volume, float pitch) => Calls.Add((clip, volume, pitch));
    }
}
