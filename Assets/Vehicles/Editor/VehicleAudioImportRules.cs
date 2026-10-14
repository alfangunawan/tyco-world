using System;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Motorbike clips are short 3D sounds: mono, decompressed on load, Vorbis.</summary>
    public sealed class VehicleAudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(VehiclePaths.AudioFolder + "/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
