using System.IO;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;

namespace TycoWorld.Vehicles.Tests
{
    public class VehicleAudioAssetsTests
    {
        static readonly string[] Names =
            { VehicleAudioBuilder.EngineName, VehicleAudioBuilder.StarterName, VehicleAudioBuilder.HornName, VehicleAudioBuilder.BumpName };

        [Test]
        public void EveryClipResolves_AsMono()
        {
            foreach (var name in Names)
            {
                var clip = VehicleAudioBuilder.Resolve(name);
                Assert.IsNotNull(clip, name);
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Assert.IsTrue(importer.forceToMono, name);
                Assert.AreEqual(UnityEngine.AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType, name);
            }
        }

        [Test]
        public void SourcesFile_ListsEveryClip()
        {
            string text = File.ReadAllText(WorldGenPaths.ToAbsolute(VehiclePaths.AudioSourcesFile));
            foreach (var name in Names) StringAssert.Contains(name, text);
        }

        [Test]
        public void WriteMissingClips_DoesNotOverwrite()
        {
            string path = AssetDatabase.GetAssetPath(VehicleAudioBuilder.Resolve(VehicleAudioBuilder.EngineName));
            var before = File.GetLastWriteTimeUtc(WorldGenPaths.ToAbsolute(path));
            VehicleAudioBuilder.WriteMissingClips();
            Assert.AreEqual(before, File.GetLastWriteTimeUtc(WorldGenPaths.ToAbsolute(path)));
        }
    }
}
