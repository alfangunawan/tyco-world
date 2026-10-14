using System.Collections.Generic;
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>Records one-shot requests instead of playing them.</summary>
    internal sealed class RecordingSounds : ISoundPlayer
    {
        public readonly List<(AudioClip Clip, float Volume, float Pitch)> Calls = new List<(AudioClip, float, float)>();

        public void Play(AudioClip clip, float volume, float pitch) => Calls.Add((clip, volume, pitch));
    }
}
