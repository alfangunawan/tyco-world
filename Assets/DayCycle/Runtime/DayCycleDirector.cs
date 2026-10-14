using System;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.DayCycle
{
    /// <summary>
    /// The single per-frame driver of the day cycle (spec #3 §3): clock → profile → sun/moon, RenderSettings,
    /// skybox copy, three volumes, street lamps, ambience and the player HUD → phase events (subscribers see this
    /// frame's lighting).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayCycleDirector : MonoBehaviour, ISceneLightingOwner
    {
        public const string HorrorStartMessage = "Malam semakin larut\u2026";
        public const string HorrorEndMessage = "Subuh. Malam sudah lewat.";
        public const string MissingProfileWarning = "[DayCycle] DayCycleProfile kosong; memakai nilai default.";
        public const string NoLampsWarning = "[DayCycle] Dunia belum punya penanda StreetLamp. Generate ulang untuk lampu dinamis.";

        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");

        [SerializeField] DayCycleProfile profile;
        [SerializeField] Light sun;
        [SerializeField] Light moon;
        [SerializeField] Volume dayVolume;
        [SerializeField] Volume nightVolume;
        [SerializeField] Volume horrorVolume;
        [SerializeField] Material skyboxMaterial;
        [SerializeField] AmbienceAudio ambience;
        [SerializeField] FirstPersonPlayer player;

        DayPhase phase;
        Material skyCopy;
        bool ownsProfile;
        float elapsed;
        Quaternion moonPose;

        public event Action HorrorStarted;
        public event Action HorrorEnded;

        public DayCycleProfile Profile => profile;
        public Light Sun => sun;
        public Light Moon => moon;
        public Volume DayVolume => dayVolume;
        public Volume NightVolume => nightVolume;
        public Volume HorrorVolume => horrorVolume;
        public Material SkyboxMaterial => skyboxMaterial;
        public AmbienceAudio Ambience => ambience;
        public FirstPersonPlayer Player => player;

        public WorldClock Clock { get; private set; }
        public DayPhaseKind Phase => phase != null ? phase.Phase : DayPhase.PhaseAt(DayTimes.StartHour);
        public bool IsHorror => phase != null && phase.IsHorror;
        public bool IsInitialized { get; private set; }
        public bool ShowClock { get; set; }
        public LightingState State { get; private set; }
        public StreetLampSystem Lamps { get; private set; }
        public Material SkyboxInstance => skyCopy;

        /// <summary>Finds the generated world (another scene, so it cannot be serialized); tests replace it.</summary>
        public Func<GeneratedRoot> FindWorldRoot { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<GeneratedRoot>();

        /// <summary>Where lamp lights are budgeted from: the main camera; tests replace it.</summary>
        public Func<Vector3?> FindViewer { get; set; } = MainCameraPosition;

        /// <summary>The fog is never thinner than this (view distance setting, spec #4 §5.2); 0 = profile fog only.</summary>
        public float MinFogDensity { get; set; }

        HudModel Hud => player != null && player.IsInitialized ? player.Hud : null;

        public void Configure(DayCycleProfile cycleProfile, Light sunLight, Light moonLight, Volume day, Volume night, Volume horror,
            Material skybox, AmbienceAudio ambienceAudio, FirstPersonPlayer firstPerson)
        {
            profile = cycleProfile;
            sun = sunLight;
            moon = moonLight;
            dayVolume = day;
            nightVolume = night;
            horrorVolume = horror;
            skyboxMaterial = skybox;
            ambience = ambienceAudio;
            player = firstPerson;
        }

        void Awake() => Initialize();

        void Update()
        {
            if (!Application.isPlaying) return;
            Tick(Time.deltaTime);
        }

        void OnDestroy() => Shutdown();

        public void Initialize() => Initialize(DayTimes.StartHour);

        public void Initialize(float startHours)
        {
            if (IsInitialized) return;
            if (profile == null)
            {
                Debug.LogWarning(MissingProfileWarning);
                profile = DayCycleProfile.CreateDefault();
                profile.hideFlags = HideFlags.DontSave;
                ownsProfile = true;
            }
            Clock = new WorldClock(startHours);
            phase = new DayPhase();
            phase.HorrorStarted += OnHorrorStarted;
            phase.HorrorEnded += OnHorrorEnded;
            phase.Initialize(Clock.Hours);
            Lamps = new StreetLampSystem(profile);
            if (skyboxMaterial != null)
            {
                skyCopy = new Material(skyboxMaterial) { name = skyboxMaterial.name + " (Runtime)", hideFlags = HideFlags.DontSave };
                RenderSettings.skybox = skyCopy;
            }
            if (ambience != null) ambience.Initialize(profile);
            moonPose = moon != null ? moon.transform.rotation : Quaternion.identity;
            elapsed = 0f;
            IsInitialized = true;
            Apply(0f);
        }

        public void Tick(float dt)
        {
            if (!IsInitialized) Initialize();
            if (dt > 0f) elapsed += dt;
            Clock.Tick(dt);
            PollLamps();
            Apply(dt);
            phase.Update(Clock.Hours);
        }

        public void ShowMessage(string text)
        {
            var hud = Hud;
            if (hud != null) hud.ShowMessage(text);
        }

        /// <summary>Restores lamp materials, the skybox asset and the moon's scene pose and frees runtime copies; safe to call twice.</summary>
        public void Shutdown()
        {
            if (IsInitialized && moon != null) moon.transform.rotation = moonPose;
            if (Lamps != null) Lamps.Dispose();
            if (skyCopy != null)
            {
                if (RenderSettings.skybox == skyCopy) RenderSettings.skybox = skyboxMaterial;
                DestroyOwned(skyCopy);
                skyCopy = null;
            }
            if (ownsProfile && profile != null)
            {
                DestroyOwned(profile);
                profile = null;
                ownsProfile = false;
            }
            IsInitialized = false;
        }

        void OnHorrorStarted()
        {
            ShowMessage(HorrorStartMessage);
            HorrorStarted?.Invoke();
        }

        void OnHorrorEnded()
        {
            ShowMessage(HorrorEndMessage);
            HorrorEnded?.Invoke();
        }

        /// <summary>Every frame until the generated world exists, so its lamps follow the cycle from its first frame.</summary>
        void PollLamps()
        {
            if (Lamps.Collected) return;
            var root = FindWorldRoot != null ? FindWorldRoot() : null;
            if (root == null) return;
            var found = root.GetComponentsInChildren<StreetLamp>(true);
            if (found.Length == 0) Debug.LogWarning(NoLampsWarning);
            Lamps.Collect(found);
        }

        void Apply(float dt)
        {
            var s = profile.Evaluate(Clock.Hours);
            State = s;
            var sunRotation = Quaternion.Euler(s.SunElevation, s.SunYaw, 0f);
            if (sun != null)
            {
                sun.enabled = s.SunEnabled;
                sun.intensity = s.SunIntensity;
                sun.color = s.SunColor;
                sun.transform.rotation = sunRotation;
            }
            if (moon != null)
            {
                moon.enabled = s.MoonEnabled;
                moon.intensity = s.MoonIntensity;
                moon.color = profile.moonColor;
                // The moon takes over RenderSettings.sun (the procedural sky's light) where the sun sets and glides to its
                // scene pose through twilight, so the sky never jumps at sunset or sunrise.
                moon.transform.rotation = Quaternion.Slerp(sunRotation, moonPose, s.MoonPoseWeight);
            }
            RenderSettings.sun = s.SunEnabled && sun != null ? sun : moon;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.AmbientSky;
            RenderSettings.ambientEquatorColor = s.AmbientEquator;
            RenderSettings.ambientGroundColor = s.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = Mathf.Max(s.FogDensity, MinFogDensity);
            RenderSettings.fogColor = s.FogColor;
            if (skyCopy != null)
            {
                skyCopy.SetFloat(ExposureId, s.SkyExposure);
                skyCopy.SetColor(SkyTintId, s.SkyTint);
            }
            if (nightVolume != null) nightVolume.weight = 1f;
            if (dayVolume != null) dayVolume.weight = s.DayVolumeWeight;
            if (horrorVolume != null) horrorVolume.weight = s.HorrorWeight;
            Lamps.Viewer = FindViewer != null ? FindViewer() : null;
            Lamps.Apply(s.LampIntensity01, s.HorrorWeight, elapsed, dt);
            if (ambience != null) ambience.Apply(s.DayAmbience, s.NightAmbience, s.HorrorDrone);
            var hud = Hud;
            if (hud != null) hud.SetClock(Clock.Display, ShowClock);
        }

        static Vector3? MainCameraPosition()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : (Vector3?)null;
        }

        static void DestroyOwned(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
