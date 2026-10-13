using System.IO;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleAudioAssetsTests
    {
        [Test]
        public void AmbienceClips_Exist_AsCompressedLoops()
        {
            foreach (var clip in new[] { DayCycleAudioBuilder.DayClip, DayCycleAudioBuilder.NightClip, DayCycleAudioBuilder.DroneClip })
            {
                Assert.IsNotNull(clip, "Jalankan DayCycleAudioBuilder.WriteMissingClips (lewat Day Cycle Setup).");
                Assert.GreaterOrEqual(clip.length, 10f, clip.name);
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Assert.AreEqual(AudioClipLoadType.CompressedInMemory, importer.defaultSampleSettings.loadType, clip.name);
                Assert.AreEqual(AudioCompressionFormat.Vorbis, importer.defaultSampleSettings.compressionFormat, clip.name);
            }
        }

        [Test]
        public void Sources_ListEveryClipAndLicense()
        {
            string path = WorldGenPaths.ToAbsolute(DayCyclePaths.AudioSourcesFile);
            Assert.IsTrue(File.Exists(path), DayCyclePaths.AudioSourcesFile);
            string text = File.ReadAllText(path);
            foreach (var clip in new[] { DayCycleAudioBuilder.DayClip, DayCycleAudioBuilder.NightClip, DayCycleAudioBuilder.DroneClip })
                StringAssert.Contains(Path.GetFileName(AssetDatabase.GetAssetPath(clip)), text);
            StringAssert.Contains("CC0", text);
        }
    }
}
