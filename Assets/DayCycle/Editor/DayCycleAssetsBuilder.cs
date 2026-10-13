using System;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.DayCycle.Authoring
{
    public sealed class DayCycleAssets
    {
        public DayCycleProfile Profile;
        public VolumeProfile DayVolume, HorrorVolume;
        public Material Skybox;
    }

    /// <summary>Creates the day cycle assets once; re-running never overwrites what the user tuned (spec #3 §6).</summary>
    public static class DayCycleAssetsBuilder
    {
        public static DayCycleAssets BuildAll()
        {
            AssetFolders.Ensure(DayCyclePaths.ConfigFolder);
            DayCycleAudioBuilder.WriteMissingClips();
            var assets = new DayCycleAssets
            {
                Profile = LoadOrCreate(DayCyclePaths.ProfileAsset, DayCycleProfile.CreateDefault),
                DayVolume = LoadOrCreate(DayCyclePaths.DayVolumeAsset, ScriptableObject.CreateInstance<VolumeProfile>),
                HorrorVolume = LoadOrCreate(DayCyclePaths.HorrorVolumeAsset, ScriptableObject.CreateInstance<VolumeProfile>),
                Skybox = LoadOrCreate(DayCyclePaths.SkyboxMaterial, CreateSkybox)
            };
            if (EnsureClips(assets.Profile, DayCycleAudioBuilder.DayClip, DayCycleAudioBuilder.NightClip, DayCycleAudioBuilder.DroneClip))
                EditorUtility.SetDirty(assets.Profile);
            if (EnsureDayVolume(assets.DayVolume)) EditorUtility.SetDirty(assets.DayVolume);
            if (EnsureHorrorVolume(assets.HorrorVolume)) EditorUtility.SetDirty(assets.HorrorVolume);
            AssetDatabase.SaveAssets();
            return assets;
        }

        /// <summary>Fills only the clip slots that are still empty.</summary>
        public static bool EnsureClips(DayCycleProfile profile, AudioClip day, AudioClip night, AudioClip drone)
        {
            bool changed = false;
            if (profile.dayClip == null && day != null) { profile.dayClip = day; changed = true; }
            if (profile.nightClip == null && night != null) { profile.nightClip = night; changed = true; }
            if (profile.horrorDroneClip == null && drone != null) { profile.horrorDroneClip = drone; changed = true; }
            return changed;
        }

        /// <summary>Day look; overrides every parameter the night VHS profile overrides, so its weight is a true blend.</summary>
        public static bool EnsureDayVolume(VolumeProfile profile)
        {
            bool changed = false;
            if (TryAdd(profile, out Tonemapping tonemapping)) { tonemapping.mode.Override(TonemappingMode.ACES); changed = true; }
            if (TryAdd(profile, out ColorAdjustments color)) { color.saturation.Override(5f); color.contrast.Override(0f); changed = true; }
            if (TryAdd(profile, out FilmGrain grain)) { grain.intensity.Override(0.15f); changed = true; }
            if (TryAdd(profile, out Vignette vignette)) { vignette.intensity.Override(0.15f); changed = true; }
            if (TryAdd(profile, out ChromaticAberration ca)) { ca.intensity.Override(0f); changed = true; }
            if (TryAdd(profile, out Bloom bloom)) { bloom.intensity.Override(0.3f); bloom.threshold.Override(1f); changed = true; }
            return changed;
        }

        public static bool EnsureHorrorVolume(VolumeProfile profile)
        {
            bool changed = false;
            if (TryAdd(profile, out ColorAdjustments color)) { color.saturation.Override(-45f); color.contrast.Override(20f); changed = true; }
            if (TryAdd(profile, out FilmGrain grain)) { grain.intensity.Override(0.8f); changed = true; }
            if (TryAdd(profile, out Vignette vignette)) { vignette.intensity.Override(0.5f); changed = true; }
            if (TryAdd(profile, out ChromaticAberration ca)) { ca.intensity.Override(0.3f); changed = true; }
            return changed;
        }

        public static Material CreateSkybox()
        {
            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Sky_Procedural" };
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 1f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.37f, 0.35f, 0.34f));
            sky.SetFloat("_Exposure", 1.3f);
            return sky;
        }

        static T LoadOrCreate<T>(string path, Func<T> create) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static bool TryAdd<T>(VolumeProfile profile, out T component) where T : VolumeComponent
        {
            if (profile.TryGet(out component)) return false;
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            if (EditorUtility.IsPersistent(profile)) AssetDatabase.AddObjectToAsset(component, profile);
            return true;
        }
    }
}
