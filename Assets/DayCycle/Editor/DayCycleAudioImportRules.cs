using System;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>Ambience loops: Vorbis, compressed in memory, loaded in the background (long looping clips).</summary>
    public sealed class DayCycleAudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(DayCyclePaths.AudioFolder + "/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
        }
    }
}
