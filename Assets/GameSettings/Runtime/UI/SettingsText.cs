using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Every player-facing string of the pause and settings menus (spec #4 §4/§6) and how values are written.</summary>
    public static class SettingsText
    {
        public const string PauseTitle = "Jeda";
        public const string Resume = "Lanjutkan";
        public const string OpenSettings = "Pengaturan";
        public const string Quit = "Keluar";
        public const string QuitQuestion = "Keluar dari game?";
        public const string QuitYes = "Ya, keluar";
        public const string QuitNo = "Batal";
        public const string SettingsTitle = "Pengaturan";
        public const string Back = "Kembali";
        public const string ResetDefaults = "Kembalikan default";

        public const string TabGraphics = "Grafis";
        public const string TabControls = "Kontrol";
        public const string TabAudio = "Audio";
        public const string TabDisplay = "Tampilan";

        public const string PresetLabel = "Preset";
        public const string ViewDistanceLabel = "View distance";
        public const string RenderScaleLabel = "Render scale";
        public const string ShadowsLabel = "Bayangan";
        public const string SsaoLabel = "SSAO";
        public const string GpuOcclusionLabel = "GPU occlusion culling";
        public const string LampLightsLabel = "Lampu jalan menyala";
        public const string FpsLimitLabel = "Batas FPS";
        public const string VSyncLabel = "VSync";
        public const string ScreenModeLabel = "Mode layar";
        public const string ResolutionLabel = "Resolusi";
        public const string SensitivityLabel = "Sensitivitas mouse";
        public const string InvertYLabel = "Invert Y";
        public const string FovLabel = "FOV";
        public const string MasterLabel = "Master";
        public const string EffectsLabel = "Efek";
        public const string AmbienceLabel = "Ambience";
        public const string ShowFpsLabel = "Penghitung FPS";
        public const string ShowClockLabel = "Jam";
        public const string CurrentResolution = "Saat ini";

        public static string PresetName(QualityPreset preset)
        {
            switch (preset)
            {
                case QualityPreset.Low: return "Rendah";
                case QualityPreset.Medium: return "Sedang";
                case QualityPreset.High: return "Tinggi";
                default: return "Kustom";
            }
        }

        public static string ShadowName(ShadowQuality quality)
        {
            switch (quality)
            {
                case ShadowQuality.Off: return "Mati";
                case ShadowQuality.Low: return "Rendah";
                case ShadowQuality.High: return "Tinggi";
                default: return "Sedang";
            }
        }

        public static string Meters(int meters) => meters.ToString(CultureInfo.InvariantCulture) + " m";

        public static string Percent(int percent) => percent.ToString(CultureInfo.InvariantCulture) + " %";

        /// <summary>Indonesian decimal comma: 1.5 → "1,5×".</summary>
        public static string Multiplier(float value) => value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + "×";

        public static string Degrees(int degrees) => degrees.ToString(CultureInfo.InvariantCulture) + "°";

        public static string LampLights(int lamps) => lamps.ToString(CultureInfo.InvariantCulture);

        public static string FpsLimit(int fps) => fps <= 0 ? "Tanpa batas" : fps.ToString(CultureInfo.InvariantCulture) + " FPS";

        public static string ScreenMode(bool fullscreen) => fullscreen ? "Layar penuh" : "Jendela";

        public static string Resolution(Vector2Int size) =>
            size.x <= 0 || size.y <= 0
                ? CurrentResolution
                : size.x.ToString(CultureInfo.InvariantCulture) + " × " + size.y.ToString(CultureInfo.InvariantCulture);

        public static string Fps(float fps) => Mathf.RoundToInt(fps).ToString(CultureInfo.InvariantCulture) + " FPS";

        public static List<string> PresetChoices() => new List<string>
        {
            PresetName(QualityPreset.Low), PresetName(QualityPreset.Medium), PresetName(QualityPreset.High), PresetName(QualityPreset.Custom)
        };

        public static List<string> ShadowChoices() => new List<string>
        {
            ShadowName(ShadowQuality.Off), ShadowName(ShadowQuality.Low), ShadowName(ShadowQuality.Medium), ShadowName(ShadowQuality.High)
        };

        public static List<string> LampLightChoices()
        {
            var list = new List<string>();
            foreach (int lamps in SettingsValidator.LampLightChoices()) list.Add(LampLights(lamps));
            return list;
        }

        public static List<string> FpsLimitChoices()
        {
            var list = new List<string>();
            foreach (int fps in SettingsValidator.FpsLimitChoices()) list.Add(FpsLimit(fps));
            return list;
        }

        public static List<string> ScreenModeChoices() => new List<string> { ScreenMode(true), ScreenMode(false) };
    }
}
