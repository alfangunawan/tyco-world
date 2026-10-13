using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>All look values of the day cycle (spec #3 §4); Evaluate turns a game hour into a LightingState.</summary>
    [CreateAssetMenu(menuName = "Tyco World/Day Cycle Profile", fileName = "DayCycleProfile")]
    public sealed class DayCycleProfile : ScriptableObject
    {
        [Header("Sun & moon")]
        public float sunrise = 5.75f;
        public float sunset = 17.75f;
        public float sunMaxElevation = 75f;
        public float sunIntensity = 1.3f;
        public Color sunHorizonColor = new Color(1f, 0.69f, 0.44f);
        public Color sunNoonColor = new Color(1f, 0.957f, 0.878f);
        public float moonIntensity = 0.15f;
        public Color moonColor = new Color(0.616f, 0.706f, 1f);

        [Header("Sky")]
        public float skyExposureDay = 1.3f;
        public float skyExposureDusk = 0.8f;
        public float skyExposureNight = 0.08f;
        public Color skyTintDay = new Color(0.5f, 0.5f, 0.5f);
        public Color skyTintNight = new Color(0.2f, 0.25f, 0.4f);

        [Header("Ambient (Trilight)")]
        public Color ambientSkyDay = new Color(0.624f, 0.722f, 0.847f);
        public Color ambientEquatorDay = new Color(0.541f, 0.604f, 0.659f);
        public Color ambientGroundDay = new Color(0.353f, 0.314f, 0.251f);
        public Color ambientSkyDusk = new Color(0.69f, 0.478f, 0.549f);
        public Color ambientEquatorDusk = new Color(0.753f, 0.541f, 0.416f);
        public Color ambientGroundDusk = new Color(0.227f, 0.188f, 0.188f);
        public Color ambientSkyNight = new Color(0.071f, 0.094f, 0.149f);
        public Color ambientEquatorNight = new Color(0.055f, 0.071f, 0.094f);
        public Color ambientGroundNight = new Color(0.02f, 0.024f, 0.031f);

        [Header("Fog")]
        public float fogDensityDay = 0.004f;
        public float fogDensityDusk = 0.008f;
        public float fogDensityNight = 0.02f;
        public Color fogColorDay = new Color(0.722f, 0.769f, 0.8f);
        public Color fogColorDusk = new Color(0.753f, 0.541f, 0.416f);
        public Color fogColorNight = new Color(0.055f, 0.071f, 0.094f);

        [Header("Horror")]
        public float horrorFadeHours = 0.5f;
        public float horrorFogMultiplier = 1.6f;
        public Color horrorFogColor = new Color(0.027f, 0.035f, 0.047f);
        public float flickerFraction = 0.08f;
        public float deadFraction = 0.03f;

        [Header("Street lamps")]
        public float lampOnBelowElevation = 5f;
        public float lampFadeDegrees = 6f;
        public float lampIntensity = 3f;

        [Header("Ambience")]
        public AudioClip dayClip;
        public AudioClip nightClip;
        public AudioClip horrorDroneClip;
        public float dayVolume = 0.35f;
        public float nightVolume = 0.4f;
        public float droneVolume = 0.3f;

        const float DaylightFullElevation = 15f;
        const float TwilightBand = 12f;
        const float TwilightBlend = 0.7f;

        public static DayCycleProfile CreateDefault() => CreateInstance<DayCycleProfile>();

        /// <summary>Degrees above (positive) or below (negative) the horizon; a sine arc between sunrise and sunset.</summary>
        public float SunElevation(float hours)
        {
            float h = Mathf.Repeat(hours, 24f);
            if (h >= sunrise && h <= sunset) return Mathf.Sin(Mathf.PI * (h - sunrise) / (sunset - sunrise)) * sunMaxElevation;
            float sinceSunset = Mathf.Repeat(h - sunset, 24f), untilSunrise = Mathf.Repeat(sunrise - h, 24f);
            return -Mathf.Min(Mathf.Min(sinceSunset, untilSunrise) * 15f, 60f);
        }

        /// <summary>Light yaw: 270 at sunrise (light travels west from an eastern sun), 0 at noon, 90 at sunset.</summary>
        public float SunYaw(float hours)
        {
            float t = Mathf.Clamp01((Mathf.Repeat(hours, 24f) - sunrise) / (sunset - sunrise));
            return Mathf.Repeat(270f + 180f * t, 360f);
        }

        public float HorrorWeight(float hours)
        {
            float h = Mathf.Repeat(hours, 24f);
            if (h >= DayTimes.HorrorStart) return Mathf.Clamp01((h - DayTimes.HorrorStart) / horrorFadeHours);
            if (h < DayTimes.HorrorEnd) return Mathf.Clamp01((DayTimes.HorrorEnd - h) / horrorFadeHours);
            return 0f;
        }

        /// <summary>Day ambience: full 06:00–17:30, crossfades 05:00–06:00 and 17:30–18:30.</summary>
        public static float DayAmbienceWeight(float hours)
        {
            float h = Mathf.Repeat(hours, 24f);
            if (h >= 6f && h <= 17.5f) return 1f;
            if (h > 5f && h < 6f) return h - 5f;
            if (h > 17.5f && h < 18.5f) return 1f - (h - 17.5f);
            return 0f;
        }

        public LightingState Evaluate(float hours)
        {
            float h = Mathf.Repeat(hours, 24f);
            float elevation = SunElevation(h);
            float daylight = Mathf.Clamp01(elevation / DaylightFullElevation);
            float twilight = (1f - Mathf.Clamp01(Mathf.Abs(elevation) / TwilightBand)) * TwilightBlend;
            float horror = HorrorWeight(h);
            var s = new LightingState
            {
                SunElevation = elevation,
                SunYaw = SunYaw(h),
                SunEnabled = elevation > 0f,
                SunColor = Color.Lerp(sunHorizonColor, sunNoonColor, Mathf.Clamp01(elevation / 30f)),
                AmbientSky = Mix(ambientSkyNight, ambientSkyDay, ambientSkyDusk, daylight, twilight),
                AmbientEquator = Mix(ambientEquatorNight, ambientEquatorDay, ambientEquatorDusk, daylight, twilight),
                AmbientGround = Mix(ambientGroundNight, ambientGroundDay, ambientGroundDusk, daylight, twilight),
                FogDensity = Mix(fogDensityNight, fogDensityDay, fogDensityDusk, daylight, twilight) * Mathf.Lerp(1f, horrorFogMultiplier, horror),
                FogColor = Color.Lerp(Mix(fogColorNight, fogColorDay, fogColorDusk, daylight, twilight), horrorFogColor, horror * 0.6f),
                SkyExposure = Mix(skyExposureNight, skyExposureDay, skyExposureDusk, daylight, twilight),
                SkyTint = Color.Lerp(skyTintNight, skyTintDay, daylight),
                DayVolumeWeight = daylight,
                HorrorWeight = horror,
                LampIntensity01 = Mathf.Clamp01((lampOnBelowElevation - elevation) / lampFadeDegrees),
                DayAmbience = DayAmbienceWeight(h),
                HorrorDrone = horror
            };
            s.SunIntensity = s.SunEnabled ? sunIntensity * Mathf.Clamp01(elevation / 10f) : 0f;
            s.MoonEnabled = !s.SunEnabled;
            s.MoonIntensity = s.MoonEnabled ? moonIntensity * Mathf.Clamp01(-elevation / 6f) : 0f;
            s.NightAmbience = 1f - s.DayAmbience;
            return s;
        }

        static float Mix(float night, float day, float dusk, float daylight, float twilight) =>
            Mathf.Lerp(Mathf.Lerp(night, day, daylight), dusk, twilight);

        static Color Mix(Color night, Color day, Color dusk, float daylight, float twilight) =>
            Color.Lerp(Color.Lerp(night, day, daylight), dusk, twilight);
    }
}
