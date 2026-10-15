using System;
using System.Collections.Generic;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Paints a model vehicle (spec vehicles-3a §4): its listed body materials are swapped for tinted copies, one copy per
    /// (source material, colour), so the SRP Batcher keeps batching. paintStrength below 1 lerps from white so an atlas's
    /// glass and lights do not turn the paint colour.
    /// </summary>
    public sealed class VehicleTint : IDisposable
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        // glTFast's own shader graph (the downloaded models) names its colour baseColorFactor.
        static readonly int BaseColorFactor = Shader.PropertyToID("baseColorFactor");
        readonly Dictionary<(Material, int), Material> cache = new Dictionary<(Material, int), Material>();

        public void Apply(GameObject vehicle, VehicleCatalog.Entry entry, Color32 colour)
        {
            if (vehicle == null || entry == null || entry.paintMaterials == null || entry.paintMaterials.Length == 0) return;
            var tintColour = entry.paintStrength >= 1f ? (Color)colour : Color.Lerp(Color.white, colour, Mathf.Clamp01(entry.paintStrength));
            int key = ((Color32)tintColour).GetHashCode();
            foreach (var r in vehicle.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = Source(slots[i]);
                    if (source == null || Array.IndexOf(entry.paintMaterials, source.name) < 0) continue;
                    if (!cache.TryGetValue((source, key), out var copy))
                    {
                        copy = new Material(source) { name = source.name, hideFlags = HideFlags.DontSave };
                        if (copy.HasProperty(BaseColor)) copy.SetColor(BaseColor, tintColour);
                        if (copy.HasProperty(BaseColorFactor)) copy.SetColor(BaseColorFactor, tintColour);
                        cache[(source, key)] = copy;
                        sources[copy] = source;
                    }
                    slots[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = slots;
            }
        }

        readonly Dictionary<Material, Material> sources = new Dictionary<Material, Material>();

        /// <summary>The imported material behind a slot that may already hold one of our copies (repaint).</summary>
        Material Source(Material slot)
        {
            if (slot == null) return null;
            return sources.TryGetValue(slot, out var s) ? s : slot;
        }

        public void Dispose()
        {
            foreach (var m in cache.Values)
                if (m != null) UnityEngine.Object.DestroyImmediate(m);
            cache.Clear();
            sources.Clear();
        }
    }
}
