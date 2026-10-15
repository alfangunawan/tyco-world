using System.IO;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Gameplay.Authoring
{
    /// <summary>"Tyco World/Gameplay/Bake Kampung Data": rebuilds Assets/Gameplay/Data/KampungData.asset from the OSM data (spec #6 §3).</summary>
    public static class KampungDataBakeMenu
    {
        [MenuItem("Tyco World/Gameplay/Bake Kampung Data")]
        static void Menu()
        {
            if (Bake(out string summary, out string error)) Debug.Log("[Gameplay] KampungData di-bake: " + summary);
            else Debug.LogError("[Gameplay] " + error);
        }

        public static bool Bake(out string summary, out string error)
        {
            summary = "";
            error = "";
            var settings = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            if (settings == null)
            {
                error = "WorldGenSettings tidak ditemukan. Jalankan Tyco World > World Generator dulu.";
                return false;
            }
            string raw = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            if (!File.Exists(raw))
            {
                error = "Data OSM belum diunduh: " + settings.rawDataPath;
                return false;
            }
            var report = new GenerationReport();
            var world = WorldMeshPipeline.Classify(File.ReadAllText(raw), settings, report);
            var baked = KampungBaker.Bake(world, settings, report);
            summary = baked.summary;
            Save(baked, GameplayPaths.KampungDataAsset);
            return true;
        }

        /// <summary>Writes into the existing asset (keeps its GUID and scene references) or creates it.</summary>
        public static KampungData Save(KampungData baked, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            baked.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<KampungData>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(baked, path);
                AssetDatabase.SaveAssets();
                return baked;
            }
            EditorUtility.CopySerialized(baked, existing);
            Object.DestroyImmediate(baked);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }
    }
}
