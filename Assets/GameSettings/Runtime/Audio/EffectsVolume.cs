using System.Collections.Generic;
using TycoWorld.DayCycle;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// The "Efek" volume (spec #4 §5.5): scales every AudioSource except the ambience layers from its original volume.
    /// PlayOneShot multiplies by the source volume, so footsteps and interaction sounds follow without code changes.
    /// </summary>
    public sealed class EffectsVolume
    {
        readonly Dictionary<AudioSource, float> originals = new Dictionary<AudioSource, float>();

        public void Apply(IEnumerable<AudioSource> sources, float level)
        {
            if (sources == null) return;
            foreach (var source in sources)
            {
                if (source == null || source.GetComponentInParent<AmbienceAudio>(true) != null) continue;
                if (!originals.TryGetValue(source, out float original))
                {
                    original = source.volume;
                    originals[source] = original;
                }
                source.volume = original * level;
            }
        }

        public void Restore()
        {
            foreach (var pair in originals)
                if (pair.Key != null) pair.Key.volume = pair.Value;
            originals.Clear();
        }
    }
}
