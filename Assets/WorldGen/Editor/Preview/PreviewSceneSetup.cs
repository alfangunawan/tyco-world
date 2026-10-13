using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Fills World.unity with night lighting (unless a runtime lighting owner exists), fog, a VHS volume and a street-level preview camera (spec section 13).</summary>
    public static class PreviewSceneSetup
    {
        public const float EyeHeight = 1.6f;
        public const long SukabirusWayId = 95529338;

        [MenuItem("Tyco World/World Generator/Setup Preview Scene")]
        static void Menu()
        {
            if (!Run(DefaultSettingsFactory.LoadOrCreateAsset(), true, out string error))
                EditorUtility.DisplayDialog("Setup Preview gagal", error, "OK");
        }

        public static bool TryComputeStreetPose(OsmData data, GeoProjection projection, long wayId, out Vector3 position, out float yawDegrees)
        {
            position = new Vector3(0f, EyeHeight, 0f);
            yawDegrees = 0f;
            if (!data.Ways.TryGetValue(wayId, out var way)) return false;
            var pts = new List<Vector2>();
            foreach (long id in way.NodeIds)
            {
                if (!data.Nodes.TryGetValue(id, out var node)) return false;
                pts.Add(projection.ToLocal(node.Lat, node.Lon));
            }
            if (pts.Count < 2) return false;

            float total = 0f;
            for (int i = 0; i + 1 < pts.Count; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            float half = total * 0.5f, travelled = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float length = Vector2.Distance(pts[i], pts[i + 1]);
                if (length <= 0f || travelled + length < half)
                {
                    travelled += length;
                    continue;
                }
                var mid = Vector2.Lerp(pts[i], pts[i + 1], (half - travelled) / length);
                var dir = (pts[i + 1] - pts[i]) / length;
                position = new Vector3(mid.x, EyeHeight, mid.y);
                yawDegrees = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                return true;
            }
            return false;
        }

        public static bool Run(WorldGenSettings settings, bool interactive, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var world = WorldScenes.EnsureWorldSceneOpen();

            if (HasLightingOwner(world.GetRootGameObjects()))
                Debug.Log("[WorldGen] Pencahayaan dikelola siklus pagi/malam; Moon dan RenderSettings tidak diubah.");
            else
                ApplyNightLighting(world);

            var camGo = FindOrCreate(world, "PreviewCam");
            camGo.tag = "MainCamera";
            var cam = GetOrAdd<Camera>(camGo);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("0E1218");
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            if (TryLoadPose(settings, out var position, out float yaw))
            {
                camGo.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            }
            else
            {
                camGo.transform.SetPositionAndRotation(new Vector3(0f, EyeHeight, 0f), Quaternion.identity);
                Debug.LogWarning("[WorldGen] Jl. Sukabirus tidak ditemukan di cache OSM; PreviewCam diletakkan di titik nol.");
            }

            var volume = GetOrAdd<Volume>(FindOrCreate(world, "Global Volume"));
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = LoadOrCreateProfile();

            GetOrAdd<WorldSceneLoader>(FindOrCreate(world, "WorldSceneLoader"));

            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            return true;
        }

        /// <summary>True when a root object (or a child) drives scene lighting at runtime, e.g. the day cycle director.</summary>
        public static bool HasLightingOwner(IEnumerable<GameObject> roots)
        {
            foreach (var go in roots)
                if (go != null && go.GetComponentInChildren<ISceneLightingOwner>(true) != null) return true;
            return false;
        }

        static void ApplyNightLighting(Scene world)
        {
            var moonGo = FindOrCreate(world, "Moon");
            moonGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var moon = GetOrAdd<Light>(moonGo);
            moon.type = LightType.Directional;
            moon.intensity = 0.15f;
            moon.color = Hex("9DB4FF");
            moon.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("121826");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = Hex("0E1218");
            RenderSettings.skybox = null;
            RenderSettings.sun = moon;
        }

        static bool TryLoadPose(WorldGenSettings settings, out Vector3 position, out float yaw)
        {
            position = default;
            yaw = 0f;
            string raw = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            if (!File.Exists(raw)) return false;
            return TryComputeStreetPose(OsmParser.Parse(File.ReadAllText(raw)), settings.CreateProjection(), SukabirusWayId, out position, out yaw);
        }

        static GameObject FindOrCreate(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }

        static VolumeProfile LoadOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(WorldGenPaths.VolumeProfileAsset);
            if (profile == null)
            {
                AssetFolders.Ensure(WorldGenPaths.ConfigFolder);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, WorldGenPaths.VolumeProfileAsset);
            }
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var color = Get<ColorAdjustments>(profile);
            color.saturation.Override(-25f);
            color.contrast.Override(10f);
            Get<FilmGrain>(profile).intensity.Override(0.5f);
            Get<Vignette>(profile).intensity.Override(0.35f);
            Get<ChromaticAberration>(profile).intensity.Override(0.15f);
            var bloom = Get<Bloom>(profile);
            bloom.intensity.Override(0.6f);
            bloom.threshold.Override(1f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
