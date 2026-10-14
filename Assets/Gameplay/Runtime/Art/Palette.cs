using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Runtime colour copies of one URP Lit template material, cached per colour (spec #6 §2: primitive art). Copies are
    /// DontSave instances, so the template asset is never modified; one material per colour keeps the SRP batcher happy.
    /// </summary>
    public sealed class Palette
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color = Shader.PropertyToID("_Color");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        readonly Material template;
        readonly Dictionary<int, Material> cache = new Dictionary<int, Material>();
        readonly Dictionary<int, Material> glowing = new Dictionary<int, Material>();

        public Palette(Material template)
        {
            this.template = template;
            if (this.template == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                this.template = new Material(shader != null ? shader : Shader.Find("Standard")) { hideFlags = HideFlags.DontSave };
            }
        }

        public Material Get(Color32 colour)
        {
            int key = (colour.r << 24) | (colour.g << 16) | (colour.b << 8) | colour.a;
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(template) { hideFlags = HideFlags.DontSave, name = "Gameplay_" + key.ToString("X8") };
            m.SetColor(BaseColor, colour);
            m.SetColor(Color, colour);
            if (m.HasProperty(Smoothness)) m.SetFloat(Smoothness, 0.15f);
            cache[key] = m;
            return m;
        }

        /// <summary>A self-lit colour (signs, lamps, the ghostly figures at night).</summary>
        public Material Glow(Color32 colour, float intensity)
        {
            int key = (colour.r << 24) | (colour.g << 16) | (colour.b << 8) | Mathf.Clamp((int)(intensity * 16f), 0, 255);
            if (glowing.TryGetValue(key, out var m)) return m;
            m = new Material(Get(colour)) { hideFlags = HideFlags.DontSave };
            m.EnableKeyword("_EMISSION");
            m.SetColor(EmissionColor, (Color)colour * intensity);
            glowing[key] = m;
            return m;
        }

        public void Dispose()
        {
            foreach (var m in cache.Values) Release(m);
            foreach (var m in glowing.Values) Release(m);
            cache.Clear();
            glowing.Clear();
        }

        static void Release(Material m)
        {
            if (m == null) return;
            if (Application.isPlaying) Object.Destroy(m);
            else Object.DestroyImmediate(m);
        }
    }
}
