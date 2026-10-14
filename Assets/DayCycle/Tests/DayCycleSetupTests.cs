using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleSetupTests
    {
        readonly List<Object> created = new List<Object>();
        readonly HashSet<GameObject> rootsBefore = new HashSet<GameObject>();
        Scene scratch;

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        VolumeProfile NewProfile() => Track(ScriptableObject.CreateInstance<VolumeProfile>());

        /// <summary>
        /// The untitled scene the test runner opens for EditMode tests stands in for World.unity, with what Setup Preview
        /// Scene leaves behind (Moon, Global Volume). Every root a test adds is destroyed in TearDown.
        /// </summary>
        Scene NewScratchWorld(out Light moon, out Volume night)
        {
            scratch = SceneManager.GetActiveScene();
            Assert.IsTrue(string.IsNullOrEmpty(scratch.path), "Tes setup hanya boleh memakai scene untitled, bukan " + scratch.path);
            rootsBefore.Clear();
            rootsBefore.UnionWith(scratch.GetRootGameObjects());
            moon = new GameObject(DayCycleSceneSetup.MoonName).AddComponent<Light>();
            moon.type = LightType.Directional;
            night = new GameObject(DayCycleSceneSetup.NightVolumeName).AddComponent<Volume>();
            night.isGlobal = true;
            night.sharedProfile = NewProfile();
            return scratch;
        }

        DayCycleAssets NewAssets() => new DayCycleAssets
        {
            Profile = Track(DayCycleProfile.CreateDefault()),
            DayVolume = NewProfile(),
            HorrorVolume = NewProfile(),
            Skybox = Track(DayCycleAssetsBuilder.CreateSkybox())
        };

        [TearDown]
        public void TearDown()
        {
            if (scratch.IsValid() && scratch.isLoaded)
                foreach (var go in scratch.GetRootGameObjects())
                    if (!rootsBefore.Contains(go)) Object.DestroyImmediate(go);
            scratch = default;
            rootsBefore.Clear();
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
        public void SceneSetup_FirstRun_WiresAndConfiguresEverything()
        {
            var world = NewScratchWorld(out var moon, out var night);
            var assets = NewAssets();
            var director = DayCycleSceneSetup.Wire(world, assets);
            Assert.AreSame(assets.Profile, director.Profile);
            Assert.AreSame(moon, director.Moon);
            Assert.AreSame(night, director.NightVolume);
            Assert.AreSame(assets.Skybox, director.SkyboxMaterial);
            Assert.IsNotNull(director.Ambience);
            Assert.AreEqual(DayCycleSceneSetup.SunName, director.Sun.name);
            Assert.AreEqual(LightType.Directional, director.Sun.type);
            Assert.AreEqual(LightShadows.Soft, director.Sun.shadows);
            Assert.IsFalse(director.Sun.enabled);
            Assert.AreEqual(1f, director.DayVolume.priority);
            Assert.AreSame(assets.DayVolume, director.DayVolume.sharedProfile);
            Assert.AreEqual(2f, director.HorrorVolume.priority);
            Assert.AreSame(assets.HorrorVolume, director.HorrorVolume.sharedProfile);
        }

        [Test]
        public void SceneSetup_Rerun_KeepsSceneChoices()
        {
            var world = NewScratchWorld(out _, out _);
            var assets = NewAssets();
            var director = DayCycleSceneSetup.Wire(world, assets);
            var sun = director.Sun;
            Volume day = director.DayVolume, horror = director.HorrorVolume;
            var myProfile = Track(DayCycleProfile.CreateDefault());
            var mySky = Track(DayCycleAssetsBuilder.CreateSkybox());
            VolumeProfile myDay = NewProfile(), myHorror = NewProfile();
            director.Configure(myProfile, sun, director.Moon, day, director.NightVolume, horror, mySky, director.Ambience, null);
            sun.shadows = LightShadows.None;
            day.priority = 5f;
            day.sharedProfile = myDay;
            horror.priority = 7f;
            horror.sharedProfile = myHorror;

            Assert.AreSame(director, DayCycleSceneSetup.Wire(world, assets));
            Assert.AreEqual(6, world.rootCount - rootsBefore.Count);
            Assert.AreSame(myProfile, director.Profile);
            Assert.AreSame(mySky, director.SkyboxMaterial);
            Assert.AreEqual(LightShadows.None, sun.shadows);
            Assert.AreEqual(5f, day.priority);
            Assert.AreSame(myDay, day.sharedProfile);
            Assert.AreEqual(7f, horror.priority);
            Assert.AreSame(myHorror, horror.sharedProfile);
        }

        [Test]
        public void SceneSetup_Rerun_RefillsMissingReferences()
        {
            var world = NewScratchWorld(out var moon, out var night);
            var assets = NewAssets();
            var director = DayCycleSceneSetup.Wire(world, assets);
            var sun = director.Sun;
            Volume day = director.DayVolume, horror = director.HorrorVolume;
            var ambience = director.Ambience;
            director.Configure(null, null, null, null, null, null, null, null, null);
            day.sharedProfile = null;

            DayCycleSceneSetup.Wire(world, assets);
            Assert.AreSame(assets.Profile, director.Profile);
            Assert.AreSame(sun, director.Sun);
            Assert.AreSame(moon, director.Moon);
            Assert.AreSame(day, director.DayVolume);
            Assert.AreSame(night, director.NightVolume);
            Assert.AreSame(horror, director.HorrorVolume);
            Assert.AreSame(assets.Skybox, director.SkyboxMaterial);
            Assert.AreSame(ambience, director.Ambience);
            Assert.AreSame(assets.DayVolume, day.sharedProfile);
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
