using System;
using System.IO;
using TycoWorld.DayCycle.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>
    /// Motorbike clips (spec #5 §7): a CC0 recording named after the clip (.ogg, .wav or .mp3) wins; otherwise a
    /// synthesized "&lt;name&gt;_synth.wav" is written once (never overwritten).
    /// </summary>
    public static class VehicleAudioBuilder
    {
        public const string EngineName = "motor_engine_loop";
        public const string StarterName = "motor_starter";
        public const string HornName = "motor_horn";
        public const string BumpName = "motor_bump";
        const string SynthSuffix = "_synth.wav";

        static string[] RecordingExtensions() => new[] { ".ogg", ".wav", ".mp3" };

        public static AudioClip Resolve(string baseName)
        {
            foreach (var extension in RecordingExtensions())
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VehiclePaths.AudioFolder + "/" + baseName + extension);
                if (clip != null) return clip;
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(SynthPath(baseName));
        }

        public static bool HasRecording(string baseName)
        {
            foreach (var extension in RecordingExtensions())
                if (File.Exists(WorldGenPaths.ToAbsolute(VehiclePaths.AudioFolder + "/" + baseName + extension))) return true;
            return false;
        }

        public static void WriteMissingClips()
        {
            AssetFolders.Ensure(VehiclePaths.AudioFolder);
            WriteIfMissing(EngineName, () => EngineSynth.EngineLoop());
            WriteIfMissing(StarterName, () => EngineSynth.Starter());
            WriteIfMissing(HornName, EngineSynth.Horn);
            WriteIfMissing(BumpName, () => EngineSynth.Bump());
        }

        static string SynthPath(string baseName) => VehiclePaths.AudioFolder + "/" + baseName + SynthSuffix;

        static void WriteIfMissing(string baseName, Func<float[]> make)
        {
            if (HasRecording(baseName)) return;
            string path = SynthPath(baseName);
            string full = WorldGenPaths.ToAbsolute(path);
            if (File.Exists(full)) return;
            File.WriteAllBytes(full, WavWriter.Encode(make(), EngineSynth.SampleRate));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
