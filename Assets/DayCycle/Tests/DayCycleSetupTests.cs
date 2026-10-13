using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleSetupTests
    {
        readonly List<Object> created = new List<Object>();

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        VolumeProfile NewProfile() => Track(ScriptableObject.CreateInstance<VolumeProfile>());

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                if (obj is VolumeProfile profile)
                    foreach (var component in profile.components) Object.DestroyImmediate(component);
                if (obj != null) Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        [Test]
        public void DayVolume_OverridesEveryNightParameter()
        {
            var night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(WorldGenPaths.VolumeProfileAsset);
            Assert.IsNotNull(night, WorldGenPaths.VolumeProfileAsset);
            var day = NewProfile();
            DayCycleAssetsBuilder.EnsureDayVolume(day);
            foreach (var n in night.components)
            {
                Assert.IsTrue(day.TryGet(n.GetType(), out VolumeComponent d), n.GetType().Name);
                for (int i = 0; i < n.parameters.Count; i++)
                    if (n.parameters[i].overrideState)
                        Assert.IsTrue(d.parameters[i].overrideState, n.GetType().Name + " parameter " + i);
            }
        }

        [Test]
        public void DayVolume_MatchesSpec()
        {
            var day = NewProfile();
            DayCycleAssetsBuilder.EnsureDayVolume(day);
            Assert.IsTrue(day.TryGet(out FilmGrain grain));
            Assert.AreEqual(0.15f, grain.intensity.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out Vignette vignette));
            Assert.AreEqual(0.15f, vignette.intensity.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out ColorAdjustments color));
            Assert.AreEqual(5f, color.saturation.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out Bloom bloom));
            Assert.AreEqual(0.3f, bloom.intensity.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out ChromaticAberration ca));
            Assert.AreEqual(0f, ca.intensity.value, 1e-4f);
            Assert.IsTrue(day.TryGet(out Tonemapping tonemapping));
            Assert.AreEqual(TonemappingMode.ACES, tonemapping.mode.value);
        }

        [Test]
        public void HorrorVolume_MatchesSpec()
        {
            var horror = NewProfile();
            DayCycleAssetsBuilder.EnsureHorrorVolume(horror);
            Assert.IsTrue(horror.TryGet(out FilmGrain grain));
            Assert.AreEqual(0.8f, grain.intensity.value, 1e-4f);
            Assert.IsTrue(horror.TryGet(out Vignette vignette));
            Assert.AreEqual(0.5f, vignette.intensity.value, 1e-4f);
            Assert.IsTrue(horror.TryGet(out ChromaticAberration ca));
            Assert.AreEqual(0.3f, ca.intensity.value, 1e-4f);
            Assert.IsTrue(horror.TryGet(out ColorAdjustments color));
            Assert.AreEqual(-45f, color.saturation.value, 1e-4f);
            Assert.AreEqual(20f, color.contrast.value, 1e-4f);
        }

        [Test]
        public void EnsureDayVolume_KeepsExisting()
        {
            var day = NewProfile();
            Assert.IsTrue(DayCycleAssetsBuilder.EnsureDayVolume(day));
            day.TryGet(out FilmGrain grain);
            grain.intensity.Override(0.42f);
            Assert.IsFalse(DayCycleAssetsBuilder.EnsureDayVolume(day));
            Assert.AreEqual(0.42f, grain.intensity.value, 1e-5f);
            Assert.AreEqual(6, day.components.Count);
        }

        [Test]
        public void EnsureHorrorVolume_KeepsExisting()
        {
            var horror = NewProfile();
            Assert.IsTrue(DayCycleAssetsBuilder.EnsureHorrorVolume(horror));
            horror.TryGet(out Vignette vignette);
            vignette.intensity.Override(0.7f);
            Assert.IsFalse(DayCycleAssetsBuilder.EnsureHorrorVolume(horror));
            Assert.AreEqual(0.7f, vignette.intensity.value, 1e-5f);
            Assert.AreEqual(4, horror.components.Count);
        }

        [Test]
        public void EnsureClips_KeepsExisting()
        {
            var profile = Track(DayCycleProfile.CreateDefault());
            var mine = Track(AudioClip.Create("mine", 100, 1, 22050, false));
            var day = Track(AudioClip.Create("day", 100, 1, 22050, false));
            var night = Track(AudioClip.Create("night", 100, 1, 22050, false));
            var drone = Track(AudioClip.Create("drone", 100, 1, 22050, false));
            profile.dayClip = mine;
            Assert.IsTrue(DayCycleAssetsBuilder.EnsureClips(profile, day, night, drone));
            Assert.AreSame(mine, profile.dayClip);
            Assert.AreSame(night, profile.nightClip);
            Assert.AreSame(drone, profile.horrorDroneClip);
            Assert.IsFalse(DayCycleAssetsBuilder.EnsureClips(profile, day, night, drone));
        }

        [Test]
        public void Skybox_IsProceduralWithDayExposure()
        {
            var sky = Track(DayCycleAssetsBuilder.CreateSkybox());
            Assert.AreEqual("Skybox/Procedural", sky.shader.name);
            Assert.AreEqual(1.3f, sky.GetFloat("_Exposure"), 1e-4f);
        }

        [Test]
        public void PreviewSetup_SeesDirectorAsLightingOwner()
        {
            var owner = Track(new GameObject("DayCycle"));
            owner.AddComponent<DayCycleDirector>();
            var plain = Track(new GameObject("Plain"));
            Assert.IsTrue(PreviewSceneSetup.HasLightingOwner(new[] { plain, owner }));
            Assert.IsFalse(PreviewSceneSetup.HasLightingOwner(new[] { plain }));
        }
    }
}
