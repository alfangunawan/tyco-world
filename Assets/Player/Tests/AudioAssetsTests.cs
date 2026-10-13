using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class AudioAssetsTests
    {
        [Test]
        public void FootstepSets_CoverEverySurface()
        {
            foreach (SurfaceType surface in Enum.GetValues(typeof(SurfaceType)))
            {
                var set = PlayerAudioMap.Footsteps.SingleOrDefault(f => f.Surface == surface);
                Assert.IsNotNull(set, surface.ToString());
                Assert.Greater(set.Files.Length, 0, surface.ToString());
                Assert.LessOrEqual(set.PitchMin, set.PitchMax);
            }
        }

        [Test]
        public void MappedFiles_AreImportedAsMonoDecompressedClips()
        {
            var paths = PlayerAudioMap.AllFootstepFiles.Select(PlayerAudioMap.FootstepPath)
                .Concat(PlayerAudioMap.InteractionFiles.Select(PlayerAudioMap.InteractionPath));
            foreach (var path in paths)
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>(path), path);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.IsTrue(importer.forceToMono, path);
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType, path);
            }
        }

        [Test]
        public void Sources_ListEveryFileAndLicense()
        {
            string text = File.ReadAllText(WorldGenPaths.ToAbsolute(PlayerPaths.AudioSourcesFile));
            StringAssert.Contains("CC0", text);
            StringAssert.Contains("https://kenney.nl/assets/impact-sounds", text);
            StringAssert.Contains("https://kenney.nl/assets/rpg-audio", text);
            foreach (var file in PlayerAudioMap.AllFootstepFiles.Concat(PlayerAudioMap.InteractionFiles))
                StringAssert.Contains(file, text);
        }
    }
}
