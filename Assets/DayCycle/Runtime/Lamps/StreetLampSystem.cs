using System;
using System.Collections.Generic;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>
    /// Switches the generated street lamps with the day cycle: light on/off and intensity, head emission through two
    /// runtime material copies (the shared M_Lamp_Head asset is never modified), and horror flicker/dead lamps.
    /// </summary>
    public sealed class StreetLampSystem : IDisposable
    {
        sealed class Entry
        {
            public StreetLamp Lamp;
            public uint Seed;
            public LampRole Role;
            public Material[] Original, On, Off;
            public int Shown = -1;
        }

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly DayCycleProfile profile;
        readonly List<Entry> entries = new List<Entry>();
        Material headOn, headOff;
        Color baseEmission;

        public StreetLampSystem(DayCycleProfile profile) => this.profile = profile;

        public bool Collected { get; private set; }
        public int Count => entries.Count;
        public int LitCount { get; private set; }

        public int RoleCount(LampRole role)
        {
            int n = 0;
            foreach (var e in entries)
                if (e.Role == role) n++;
            return n;
        }

        public void Collect(IReadOnlyList<StreetLamp> lamps)
        {
            Collected = true;
            foreach (var lamp in lamps)
            {
                if (lamp == null || lamp.Light == null) continue;
                var entry = new Entry { Lamp = lamp, Seed = LampFlicker.Seed(lamp.transform.position) };
                entry.Role = LampFlicker.RoleFor(entry.Seed, profile.flickerFraction, profile.deadFraction);
                var renderer = lamp.HeadRenderer;
                int index = lamp.HeadMaterialIndex;
                if (renderer != null && index >= 0 && index < renderer.sharedMaterials.Length && renderer.sharedMaterials[index] != null)
                {
                    entry.Original = renderer.sharedMaterials;
                    EnsureCopies(entry.Original[index]);
                    entry.On = Replace(entry.Original, index, headOn);
                    entry.Off = Replace(entry.Original, index, headOff);
                }
                entries.Add(entry);
            }
        }

        public void Apply(float intensity01, float horrorWeight, float timeSeconds)
        {
            if (headOn != null) headOn.SetColor(EmissionId, baseEmission * intensity01);
            bool horror = horrorWeight > 0f;
            int lit = 0;
            foreach (var e in entries)
            {
                if (e.Lamp == null) continue;
                bool on = intensity01 > 0f;
                if (on && horror && e.Role == LampRole.Dead) on = false;
                if (on && horror && e.Role == LampRole.Flicker) on = LampFlicker.IsLit(e.Seed, timeSeconds);
                var light = e.Lamp.Light;
                if (light != null)
                {
                    light.enabled = on;
                    light.intensity = profile.lampIntensity * intensity01;
                }
                int shown = on ? 1 : 0;
                if (e.On != null && e.Shown != shown && e.Lamp.HeadRenderer != null)
                {
                    e.Lamp.HeadRenderer.sharedMaterials = on ? e.On : e.Off;
                    e.Shown = shown;
                }
                if (on) lit++;
            }
            LitCount = lit;
        }

        public void Dispose()
        {
            foreach (var e in entries)
                if (e.Lamp != null && e.Original != null && e.Lamp.HeadRenderer != null)
                    e.Lamp.HeadRenderer.sharedMaterials = e.Original;
            entries.Clear();
            LitCount = 0;
            DestroyCopy(ref headOn);
            DestroyCopy(ref headOff);
        }

        void EnsureCopies(Material source)
        {
            if (headOn != null) return;
            baseEmission = source.HasProperty(EmissionId) ? source.GetColor(EmissionId) : Color.black;
            headOn = new Material(source) { name = source.name + " (Runtime On)", hideFlags = HideFlags.DontSave };
            headOff = new Material(source) { name = source.name + " (Runtime Off)", hideFlags = HideFlags.DontSave };
            headOff.SetColor(EmissionId, Color.black);
        }

        static Material[] Replace(Material[] original, int index, Material material)
        {
            var copy = (Material[])original.Clone();
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
