using System;
using System.IO;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>Writes the synthesized ambience WAVs that are missing (never overwrites) and loads the three clips.</summary>
    public static class DayCycleAudioBuilder
    {
        public static AudioClip DayClip => Load(DayCyclePaths.DayAmbienceClip);
        public static AudioClip DroneClip => Load(DayCyclePaths.HorrorDroneClip);

        /// <summary>The CC0 cricket loop, or the synthesized night loop when the download is missing.</summary>
        public static AudioClip NightClip
        {
            get
            {
                var clip = Load(DayCyclePaths.NightAmbienceClip);
                return clip != null ? clip : Load(DayCyclePaths.NightAmbienceSynthClip);
            }
        }

        public static void WriteMissingClips()
        {
            AssetFolders.Ensure(DayCyclePaths.AudioFolder);
            WriteIfMissing(DayCyclePaths.DayAmbienceClip, () => AmbienceSynth.Day());
            WriteIfMissing(DayCyclePaths.HorrorDroneClip, () => AmbienceSynth.Drone());
            if (!File.Exists(WorldGenPaths.ToAbsolute(DayCyclePaths.NightAmbienceClip)))
                WriteIfMissing(DayCyclePaths.NightAmbienceSynthClip, () => AmbienceSynth.Night());
        }

        static void WriteIfMissing(string assetPath, Func<float[]> make)
        {
            string full = WorldGenPaths.ToAbsolute(assetPath);
            if (File.Exists(full)) return;
            File.WriteAllBytes(full, WavWriter.Encode(make(), AmbienceSynth.SampleRate));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        static AudioClip Load(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
