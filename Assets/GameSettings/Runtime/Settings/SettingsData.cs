using System;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Everything the settings page stores (spec #4 §4; the spec calls this type "GameSettings"), saved to PlayerPrefs
    /// as JSON. Field defaults equal the Medium preset.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        // Grafis
        public QualityPreset preset = QualityPreset.Medium;
        public int viewDistance = 300;
        /// <summary>Percent; below 100 the STP upscaler is used.</summary>
        public int renderScale = 85;
        public ShadowQuality shadows = ShadowQuality.Medium;
        public bool ssao;
        public bool gpuOcclusion = true;
        public int lampLights = 16;
        /// <summary>GPU Resident Drawer small-mesh culling in % of the screen; set by presets only.</summary>
        public float smallMeshCulling = 1f;
        /// <summary>1 or 2; set by presets only and ignored while the render scale is below 100 %.</summary>
        public int msaa = 1;
        /// <summary>0 = no limit.</summary>
        public int fpsLimit = 60;
        public bool vSync;
        public bool fullscreen = true;
        /// <summary>0 × 0 keeps the current resolution.</summary>
        public int resolutionWidth;
        public int resolutionHeight;

        // Kontrol
        public float mouseSensitivity = 1f;
        public bool invertY;
        public int fieldOfView = 70;

        // Audio, in percent
        public int masterVolume = 100;
        public int effectsVolume = 100;
        public int ambienceVolume = 100;

        // Tampilan
        public bool showFps;
        public bool showClock;

        public SettingsData Clone() => (SettingsData)MemberwiseClone();
    }
}
