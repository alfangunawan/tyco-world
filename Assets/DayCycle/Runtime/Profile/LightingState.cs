using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>Everything the director applies for one moment of the day (pure data, testable without a scene).</summary>
    public struct LightingState
    {
        public float SunElevation, SunYaw, SunIntensity;
        public Color SunColor;
        public bool SunEnabled;
        public float MoonIntensity;
        public bool MoonEnabled;
        public Color AmbientSky, AmbientEquator, AmbientGround;
        public float FogDensity;
        public Color FogColor;
        public float SkyExposure;
        public Color SkyTint;
        public float DayVolumeWeight, HorrorWeight;
        public float LampIntensity01;
        public float DayAmbience, NightAmbience, HorrorDrone;
    }
}
