using System;
using System.Collections.Generic;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player
{
    [Serializable]
    public sealed class SurfaceClips
    {
        public SurfaceType surface;
        public AudioClip[] clips = new AudioClip[0];
        public float volume = 0.5f;
        public float pitchMin = 0.95f;
        public float pitchMax = 1.05f;
    }

    /// <summary>Footstep clips per surface; the Default entry is the fallback for surfaces without clips.</summary>
    [CreateAssetMenu(menuName = "Tyco World/Footstep Bank", fileName = "FootstepBank")]
    public sealed class FootstepBank : ScriptableObject
    {
        public List<SurfaceClips> surfaces = new List<SurfaceClips>();

        public SurfaceClips Resolve(SurfaceType surface)
        {
            SurfaceClips fallback = null;
            foreach (var entry in surfaces)
            {
                if (entry == null || entry.clips == null || entry.clips.Length == 0) continue;
                if (entry.surface == surface) return entry;
                if (entry.surface == SurfaceType.Default) fallback = entry;
            }
            return fallback;
        }
    }
}
