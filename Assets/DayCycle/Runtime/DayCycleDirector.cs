using System;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.DayCycle
{
    /// <summary>
    /// The single per-frame driver of the day cycle (spec #3 §3): clock → phase (events) → profile → sun/moon,
    /// RenderSettings, skybox copy, three volumes, street lamps, ambience and the player HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayCycleDirector : MonoBehaviour, ISceneLightingOwner
    {
        public const string HorrorStartMessage = "Malam semakin larut\u2026";
        public const string HorrorEndMessage = "Subuh. Malam sudah lewat.";
        public const string MissingProfileWarning = "[DayCycle] DayCycleProfile kosong; memakai nilai default.";
        public const string NoLampsWarning = "[DayCycle] Dunia belum punya penanda StreetLamp. Generate ulang untuk lampu dinamis.";
        const float LampPollSeconds = 0.5f;

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
        float nextLampPoll;

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
            elapsed = 0f;
            nextLampPoll = 0f;
            IsInitialized = true;
            Apply();
        }

        public void Tick(float dt)
        {
            if (!IsInitialized) Initialize();
            if (dt > 0f) elapsed += dt;
            Clock.Tick(dt);
            phase.Update(Clock.Hours);
            PollLamps();
            Apply();
        }

        public void ShowMessage(string text)
        {
            var hud = Hud;
            if (hud != null) hud.ShowMessage(text);
        }

        /// <summary>Restores lamp materials and the skybox asset and frees runtime copies; safe to call twice.</summary>
        public void Shutdown()
        {
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

        void PollLamps()
        {
            if (Lamps.Collected || elapsed < nextLampPoll) return;
            nextLampPoll = elapsed + LampPollSeconds;
            var root = FindWorldRoot != null ? FindWorldRoot() : null;
            if (root == null) return;
            var found = root.GetComponentsInChildren<StreetLamp>(true);
            if (found.Length == 0) Debug.LogWarning(NoLampsWarning);
            Lamps.Collect(found);
        }

        void Apply()
        {
            var s = profile.Evaluate(Clock.Hours);
            State = s;
            if (sun != null)
            {
                sun.enabled = s.SunEnabled;
                sun.intensity = s.SunIntensity;
                sun.color = s.SunColor;
                sun.transform.rotation = Quaternion.Euler(s.SunElevation, s.SunYaw, 0f);
            }
            if (moon != null)
            {
                moon.enabled = s.MoonEnabled;
                moon.intensity = s.MoonIntensity;
                moon.color = profile.moonColor;
            }
            RenderSettings.sun = s.SunEnabled && sun != null ? sun : moon;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.AmbientSky;
            RenderSettings.ambientEquatorColor = s.AmbientEquator;
            RenderSettings.ambientGroundColor = s.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = s.FogDensity;
            RenderSettings.fogColor = s.FogColor;
            if (skyCopy != null)
            {
                skyCopy.SetFloat(ExposureId, s.SkyExposure);
                skyCopy.SetColor(SkyTintId, s.SkyTint);
            }
            if (nightVolume != null) nightVolume.weight = 1f;
            if (dayVolume != null) dayVolume.weight = s.DayVolumeWeight;
            if (horrorVolume != null) horrorVolume.weight = s.HorrorWeight;
            Lamps.Apply(s.LampIntensity01, s.HorrorWeight, elapsed);
            if (ambience != null) ambience.Apply(s.DayAmbience, s.NightAmbience, s.HorrorDrone);
            var hud = Hud;
            if (hud != null) hud.SetClock(Clock.Display, ShowClock);
        }

        static void DestroyOwned(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
