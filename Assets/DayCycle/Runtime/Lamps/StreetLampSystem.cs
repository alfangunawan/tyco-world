using System;
using System.Collections.Generic;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>
    /// Switches the generated street lamps with the day cycle: light on/off and intensity, head emission through two
    /// runtime material copies (the shared M_Lamp_Head asset is never modified), and horror flicker/dead lamps.
    /// With a light budget only the nearest lamps to <see cref="Viewer"/> emit light and the rest keep only their lit
    /// heads (spec #4 §5.3). Light components are only touched when their value changes.
    /// </summary>
    public sealed class StreetLampSystem : IDisposable
    {
        public const float SelectIntervalSeconds = 0.25f;
        public const float FadeSeconds = 0.5f;
        public const float ViewerJumpMeters = 50f;

        sealed class Entry
        {
            public StreetLamp Lamp;
            public uint Seed;
            public LampRole Role;
            public Material[] Original, On, Off;
            public int Shown = -1;
            public bool Selected = true;
            public float Fade = 1f;
            public bool LightOn;
            public float LightIntensity;
        }

        struct Candidate
        {
            public float SqrDistance;
            public int Index;
        }

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly DayCycleProfile profile;
        readonly List<Entry> entries = new List<Entry>();
        readonly List<Candidate> candidates = new List<Candidate>();
        Material headOn, headOff;
        Color baseEmission;
        int lightBudget = int.MaxValue;
        float maxLightDistance = float.PositiveInfinity;
        bool selectionDirty = true;
        bool snapNextApply = true;
        float selectTimer;
        Vector3? lastViewer;

        public StreetLampSystem(DayCycleProfile profile) => this.profile = profile;

        public bool Collected { get; private set; }
        public int Count => entries.Count;
        /// <summary>Lamps that are on (lit head), whether or not their light is within the budget.</summary>
        public int LitCount { get; private set; }
        /// <summary>Lamps whose Light component is enabled: at most the budget plus lamps still fading out.</summary>
        public int LightsOn { get; private set; }

        /// <summary>How many lamps may emit light; int.MaxValue (default) keeps every lit lamp's light on.</summary>
        public int LightBudget
        {
            get => lightBudget;
            set
            {
                lightBudget = Mathf.Max(0, value);
                selectionDirty = true;
            }
        }

        /// <summary>Lamps farther than this from the viewer never emit light; infinity (default) = no limit.</summary>
        public float MaxLightDistance
        {
            get => maxLightDistance;
            set
            {
                maxLightDistance = Mathf.Max(0f, value);
                selectionDirty = true;
            }
        }

        /// <summary>Where the budget is measured from (the active camera); null applies no budget.</summary>
        public Vector3? Viewer { get; set; }

        bool Budgeted => lightBudget < int.MaxValue || !float.IsPositiveInfinity(maxLightDistance);

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
                var entry = new Entry
                {
                    Lamp = lamp,
                    Seed = LampFlicker.Seed(lamp.transform.position),
                    LightOn = lamp.Light.enabled,
                    LightIntensity = lamp.Light.intensity
                };
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
            selectionDirty = true;
            snapNextApply = true;
        }

        /// <param name="dt">Seconds since the last call; drives the budget fades (0 = snap).</param>
        public void Apply(float intensity01, float horrorWeight, float timeSeconds, float dt = 0f)
        {
            if (headOn != null) headOn.SetColor(EmissionId, baseEmission * intensity01);
            bool horror = horrorWeight > 0f;
            Select(intensity01, horror, dt);
            bool snap = dt <= 0f || snapNextApply;
            snapNextApply = false;
            float full = profile.lampIntensity * intensity01;
            int lit = 0, lightsOn = 0;
            foreach (var e in entries)
            {
                if (e.Lamp == null) continue;
                bool on = Eligible(e, intensity01, horror);
                if (on && horror && e.Role == LampRole.Flicker) on = LampFlicker.IsLit(e.Seed, timeSeconds);
                float target = e.Selected ? 1f : 0f;
                e.Fade = snap ? target : Mathf.MoveTowards(e.Fade, target, dt / FadeSeconds);
                var light = e.Lamp.Light;
                if (light != null)
                {
                    bool enabled = on && e.Fade > 0f;
                    if (enabled != e.LightOn)
                    {
                        light.enabled = enabled;
                        e.LightOn = enabled;
                    }
                    float intensity = full * e.Fade;
                    if (enabled && intensity != e.LightIntensity)
                    {
                        light.intensity = intensity;
                        e.LightIntensity = intensity;
                    }
                    if (enabled) lightsOn++;
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
            LightsOn = lightsOn;
        }

        public void Dispose()
        {
            foreach (var e in entries)
                if (e.Lamp != null && e.Original != null && e.Lamp.HeadRenderer != null)
                    e.Lamp.HeadRenderer.sharedMaterials = e.Original;
            entries.Clear();
            LitCount = 0;
            LightsOn = 0;
            DestroyCopy(ref headOn);
            DestroyCopy(ref headOff);
        }

        static bool Eligible(Entry e, float intensity01, bool horror) => intensity01 > 0f && !(horror && e.Role == LampRole.Dead);

        void Select(float intensity01, bool horror, float dt)
        {
            if (!Budgeted || !Viewer.HasValue)
            {
                foreach (var e in entries) e.Selected = true;
                lastViewer = null;
                return;
            }
            var viewer = Viewer.Value;
            selectTimer += Mathf.Max(0f, dt);
            bool jumped = !lastViewer.HasValue || (viewer - lastViewer.Value).sqrMagnitude > ViewerJumpMeters * ViewerJumpMeters;
            if (!selectionDirty && !jumped && selectTimer < SelectIntervalSeconds) return;
            selectTimer = 0f;
            selectionDirty = false;
            lastViewer = viewer;
            float maxSqr = float.IsPositiveInfinity(maxLightDistance) ? float.PositiveInfinity : maxLightDistance * maxLightDistance;
            candidates.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                e.Selected = false;
                if (e.Lamp == null || !Eligible(e, intensity01, horror)) continue;
                float sqr = (e.Lamp.transform.position - viewer).sqrMagnitude;
                if (sqr <= maxSqr) candidates.Add(new Candidate { SqrDistance = sqr, Index = i });
            }
            candidates.Sort(ByDistanceComparison);
            int count = Mathf.Min(lightBudget, candidates.Count);
            for (int k = 0; k < count; k++) entries[candidates[k].Index].Selected = true;
        }

        static int ByDistance(Candidate a, Candidate b) => a.SqrDistance.CompareTo(b.SqrDistance);

        /// <summary>Cached so the periodic re-selection does not allocate a delegate (immutable, safe without domain reload).</summary>
        static readonly Comparison<Candidate> ByDistanceComparison = ByDistance;

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
