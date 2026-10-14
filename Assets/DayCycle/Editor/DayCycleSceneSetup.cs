using TycoWorld.Player;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>"Tyco World/Day Cycle Setup": wires the day cycle into World.unity; safe to re-run (spec #3 §6).</summary>
    public static class DayCycleSceneSetup
    {
        public const string RootName = "DayCycle";
        public const string SunName = "Sun";
        public const string MoonName = "Moon";
        public const string NightVolumeName = "Global Volume";
        public const string DayVolumeName = "Volume_Day";
        public const string HorrorVolumeName = "Volume_Horror";
        public const string PlayerName = "Player";
        public const string CameraWarning = "[DayCycle] Kamera pemain belum memakai Skybox. Jalankan \"Tyco World/Player Setup\" ulang.";
        public const string NoPlayerWarning = "[DayCycle] Player tidak ada di World.unity; jam dan pesan HUD tidak tampil. Jalankan \"Tyco World/Player Setup\".";

        [MenuItem("Tyco World/Day Cycle Setup")]
        static void SetupMenu()
        {
            if (!Run(true, out string error)) EditorUtility.DisplayDialog("Day Cycle Setup gagal", error, "OK");
        }

        public static bool Run(bool interactive, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var world = WorldScenes.EnsureWorldSceneOpen();
            var moonGo = FindRoot(world, MoonName);
            var nightGo = FindRoot(world, NightVolumeName);
            if (moonGo == null || moonGo.GetComponent<Light>() == null || nightGo == null || nightGo.GetComponent<Volume>() == null)
            {
                error = "Moon atau Global Volume tidak ada di World.unity. Jalankan \"Tyco World/World Generator/Setup Preview Scene\" dulu.";
                return false;
            }
            var assets = DayCycleAssetsBuilder.BuildAll();
            SceneManager.SetActiveScene(world);
            var director = Wire(world, assets);
            RenderSettings.skybox = director.SkyboxMaterial;
            var player = director.Player;
            if (player == null) Debug.LogWarning(NoPlayerWarning);
            else if (!UsesSkybox(player)) Debug.LogWarning(CameraWarning);

            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            return true;
        }

        /// <summary>
        /// Creates or completes the sun, the day and horror volumes and the DayCycle root in <paramref name="world"/>
        /// (which must already hold Moon and Global Volume) and wires the director. A re-run keeps what is already
        /// there: director references, volume profiles, volume priorities and sun shadows are only filled in when
        /// missing or set when the object is first created.
        /// </summary>
        public static DayCycleDirector Wire(Scene world, DayCycleAssets assets)
        {
            var moonGo = FindRoot(world, MoonName);
            var nightGo = FindRoot(world, NightVolumeName);
            var sun = GetOrAdd<Light>(FindOrCreate(world, SunName), out bool newSun);
            sun.type = LightType.Directional;
            sun.enabled = false;
            if (newSun)
            {
                sun.shadows = LightShadows.Soft;
                sun.color = assets.Profile.sunNoonColor;
                sun.intensity = assets.Profile.sunIntensity;
            }

            var day = EnsureVolume(world, DayVolumeName, 1f, assets.DayVolume);
            var horror = EnsureVolume(world, HorrorVolumeName, 2f, assets.HorrorVolume);

            var root = FindOrCreate(world, RootName);
            var ambience = GetOrAdd<AmbienceAudio>(root, out _);
            var director = GetOrAdd<DayCycleDirector>(root, out _);
            GetOrAdd<DayCycleDebug>(root, out _);
            var playerGo = FindRoot(world, PlayerName);
            var player = playerGo != null ? playerGo.GetComponent<FirstPersonPlayer>() : null;
            director.Configure(Keep(director.Profile, assets.Profile), Keep(director.Sun, sun), Keep(director.Moon, moonGo.GetComponent<Light>()),
                Keep(director.DayVolume, day), Keep(director.NightVolume, nightGo.GetComponent<Volume>()), Keep(director.HorrorVolume, horror),
                Keep(director.SkyboxMaterial, assets.Skybox), Keep(director.Ambience, ambience), Keep(director.Player, player));
            EditorUtility.SetDirty(director);
            return director;
        }

        /// <summary>The reference already in the scene, or <paramref name="fallback"/> when it is empty or missing.</summary>
        static T Keep<T>(T current, T fallback) where T : Object => current != null ? current : fallback;

        static bool UsesSkybox(FirstPersonPlayer player)
        {
            var cam = player.View != null ? player.View.GetComponent<Camera>() : null;
            return cam != null && cam.clearFlags == CameraClearFlags.Skybox;
        }

        static Volume EnsureVolume(Scene world, string name, float priority, VolumeProfile profile)
        {
            var volume = GetOrAdd<Volume>(FindOrCreate(world, name), out bool added);
            volume.isGlobal = true;
            volume.weight = 0f;
            if (added) volume.priority = priority;
            if (volume.sharedProfile == null) volume.sharedProfile = profile;
            return volume;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        static GameObject FindOrCreate(Scene scene, string name)
        {
            var go = FindRoot(scene, name);
            if (go != null) return go;
            go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        static T GetOrAdd<T>(GameObject go, out bool added) where T : Component
        {
            var component = go.GetComponent<T>();
            added = component == null;
            if (added) component = go.AddComponent<T>();
            return component;
        }
    }
}
