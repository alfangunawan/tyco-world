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

            var sun = GetOrAdd<Light>(FindOrCreate(world, SunName));
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.color = assets.Profile.sunNoonColor;
            sun.intensity = assets.Profile.sunIntensity;
            sun.enabled = false;

            var day = EnsureVolume(world, DayVolumeName, 1f, assets.DayVolume);
            var horror = EnsureVolume(world, HorrorVolumeName, 2f, assets.HorrorVolume);

            var root = FindOrCreate(world, RootName);
            var ambience = GetOrAdd<AmbienceAudio>(root);
            var director = GetOrAdd<DayCycleDirector>(root);
            GetOrAdd<DayCycleDebug>(root);
            var playerGo = FindRoot(world, PlayerName);
            var player = playerGo != null ? playerGo.GetComponent<FirstPersonPlayer>() : null;
            director.Configure(assets.Profile, sun, moonGo.GetComponent<Light>(), day, nightGo.GetComponent<Volume>(), horror,
                assets.Skybox, ambience, player);
            EditorUtility.SetDirty(director);
            RenderSettings.skybox = assets.Skybox;
            if (player == null) Debug.LogWarning(NoPlayerWarning);
            else if (!UsesSkybox(player)) Debug.LogWarning(CameraWarning);

            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            return true;
        }

        static bool UsesSkybox(FirstPersonPlayer player)
        {
            var cam = player.View != null ? player.View.GetComponent<Camera>() : null;
            return cam != null && cam.clearFlags == CameraClearFlags.Skybox;
        }

        static Volume EnsureVolume(Scene world, string name, float priority, VolumeProfile profile)
        {
            var volume = GetOrAdd<Volume>(FindOrCreate(world, name));
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0f;
            volume.sharedProfile = profile;
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

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }
    }
}
