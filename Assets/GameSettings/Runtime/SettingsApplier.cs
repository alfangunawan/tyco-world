using System;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using TycoWorld.Vehicles;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Owns the current settings (spec #4 §3): loads them before the first frame, applies them to the camera, player,
    /// culler, day cycle, quality, audio and screen whenever they change, saves on request, and gives the quality
    /// globals and AudioListener.volume back on shutdown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsApplier : MonoBehaviour
    {
        /// <summary>Re-apply every frame for at most this long until the player and director are initialized.</summary>
        public const float TargetRetrySeconds = 10f;

        readonly EffectsVolume effects = new EffectsVolume();
        SettingsStore store;
        QualityApplier quality;
        float retryTime;
        float originalVolume;
        bool targetsReady;
        bool screenApplied, lastFullscreen;
        int lastWidth, lastHeight;

        public event Action<SettingsData> Applied;

        public SettingsData Current { get; private set; } = new SettingsData();
        public bool IsInitialized { get; private set; }
        public QualityApplier Quality => quality;
        /// <summary>Settings storage; null (default) = PlayerPrefs. Tests set a memory backend before Initialize.</summary>
        public ISettingsBackend Backend { get; set; }
        public Func<FirstPersonPlayer> FindPlayer { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>();
        public Func<DayCycleDirector> FindDirector { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<DayCycleDirector>();

        void Awake()
        {
            if (Application.isPlaying) Initialize();
        }

        void Start()
        {
            if (Application.isPlaying) ApplyAll();
        }

        void Update()
        {
            if (!Application.isPlaying || targetsReady) return;
            retryTime += Time.unscaledDeltaTime;
            if (retryTime > TargetRetrySeconds)
            {
                targetsReady = true;
                return;
            }
            ApplyAll();
        }

        void OnDestroy() => Shutdown();

        public void Initialize()
        {
            if (IsInitialized) return;
            originalVolume = AudioListener.volume;
            store = new SettingsStore(Backend != null ? Backend : new PlayerPrefsBackend());
            Current = store.Load();
            quality = new QualityApplier();
            SceneManager.sceneLoaded += OnSceneLoaded;
            IsInitialized = true;
        }

        /// <summary>Validates a copy of <paramref name="settings"/>, makes it current and applies it (live, not saved).</summary>
        public void Apply(SettingsData settings)
        {
            if (!IsInitialized) Initialize();
            var copy = settings.Clone();
            SettingsValidator.Sanitize(copy);
            Current = copy;
            ApplyAll();
        }

        public void Save()
        {
            if (store != null) store.Save(Current);
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            IsInitialized = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (quality != null) quality.Dispose();
            quality = null;
            AudioListener.volume = originalVolume;
        }

        public void ApplyAll()
        {
            if (!IsInitialized) Initialize();
            var s = Current;
            float view = s.viewDistance;
            float props = ViewDistanceRules.PropDistance(view);

            var player = FindPlayer != null ? FindPlayer() : null;
            var camera = player != null && player.View != null ? player.View.GetComponent<Camera>() : null;
            if (camera != null)
            {
                camera.farClipPlane = ViewDistanceRules.FarClip(view);
                camera.fieldOfView = s.fieldOfView;
            }
            if (player != null)
            {
                player.LookSensitivity = s.mouseSensitivity;
                player.InvertY = s.invertY;
            }

            var culler = GetComponent<DistanceCuller>();
            if (culler == null) culler = UnityEngine.Object.FindAnyObjectByType<DistanceCuller>();
            if (culler != null)
            {
                culler.ViewDistance = view;
                culler.PropDistance = props;
            }

            var director = FindDirector != null ? FindDirector() : null;
            Light sun = null, moon = null;
            if (director != null)
            {
                director.MinFogDensity = ViewDistanceRules.MinFogDensity(view);
                director.ShowClock = s.showClock;
                if (director.Lamps != null)
                {
                    director.Lamps.LightBudget = s.lampLights;
                    director.Lamps.MaxLightDistance = props;
                }
                if (director.Ambience != null) director.Ambience.Level = s.ambienceVolume / 100f;
                sun = director.Sun;
                moon = director.Moon;
            }

            quality.Apply(s, camera, new[] { sun, moon });
            AudioListener.volume = s.masterVolume / 100f;
            effects.Apply(UnityEngine.Object.FindObjectsByType<AudioSource>(), s.effectsVolume / 100f);
            foreach (var bike in UnityEngine.Object.FindObjectsByType<MotorbikeAudio>()) bike.Level = s.effectsVolume / 100f;
            ApplyScreen(s);
            targetsReady = player != null && player.IsInitialized && (director == null || director.IsInitialized);
            Applied?.Invoke(s);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAll();

        void ApplyScreen(SettingsData s)
        {
            // Screen.SetResolution has no effect in the editor's Game view; screen settings are checked by hand in a build.
            if (Application.isEditor) return;
            if (screenApplied && lastFullscreen == s.fullscreen && lastWidth == s.resolutionWidth && lastHeight == s.resolutionHeight) return;
            bool first = !screenApplied;
            screenApplied = true;
            lastFullscreen = s.fullscreen;
            lastWidth = s.resolutionWidth;
            lastHeight = s.resolutionHeight;
            bool currentlyFullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (first && s.resolutionWidth == 0 && s.fullscreen == currentlyFullscreen) return;
            var mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int width = s.resolutionWidth > 0 ? s.resolutionWidth : s.fullscreen ? Screen.currentResolution.width : Screen.width;
            int height = s.resolutionHeight > 0 ? s.resolutionHeight : s.fullscreen ? Screen.currentResolution.height : Screen.height;
            Screen.SetResolution(width, height, mode);
        }
    }
}
