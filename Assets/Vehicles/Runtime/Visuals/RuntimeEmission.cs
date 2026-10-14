using System;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Swaps one renderer material between two DontSave copies with low/high emission; the shared asset is never modified.</summary>
    public sealed class RuntimeEmission : IDisposable
    {
        const string EmissionKeyword = "_EMISSION";
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly Renderer renderer;
        Material[] original, lowSet, highSet;
        Material lowCopy, highCopy;
        int shown = -1;

        public RuntimeEmission(Renderer target, int materialIndex, float lowFactor, float highFactor)
        {
            renderer = target;
            if (renderer == null) return;
            var shared = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= shared.Length || shared[materialIndex] == null) return;
            var source = shared[materialIndex];
            var baseEmission = source.HasProperty(EmissionId) ? source.GetColor(EmissionId) : Color.white;
            lowCopy = Copy(source, baseEmission * lowFactor, " (Runtime Low)");
            highCopy = Copy(source, baseEmission * highFactor, " (Runtime High)");
            original = shared;
            lowSet = Replace(shared, materialIndex, lowCopy);
            highSet = Replace(shared, materialIndex, highCopy);
        }

        public bool IsHigh => shown == 1;
        public Material Low => lowCopy;
        public Material High => highCopy;

        public void Set(bool high)
        {
            if (original == null || renderer == null) return;
            int want = high ? 1 : 0;
            if (shown == want) return;
            renderer.sharedMaterials = high ? highSet : lowSet;
            shown = want;
        }

        public void Dispose()
        {
            if (renderer != null && original != null) renderer.sharedMaterials = original;
            DestroyCopy(ref lowCopy);
            DestroyCopy(ref highCopy);
            original = null;
            shown = -1;
        }

        static Material Copy(Material source, Color emission, string suffix)
        {
            var copy = new Material(source) { name = source.name + suffix, hideFlags = HideFlags.DontSave };
            copy.EnableKeyword(EmissionKeyword);
            copy.SetColor(EmissionId, emission);
            return copy;
        }

        static Material[] Replace(Material[] materials, int index, Material material)
        {
            var copy = (Material[])materials.Clone();
            copy[index] = material;
            return copy;
        }

        static void DestroyCopy(ref Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(material);
            else UnityEngine.Object.DestroyImmediate(material);
            material = null;
        }
    }
}
