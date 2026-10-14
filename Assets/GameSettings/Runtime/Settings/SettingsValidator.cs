using System;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Clamps loaded or edited settings into the ranges of spec #4 §4 and keeps <c>preset</c> consistent.</summary>
    public static class SettingsValidator
    {
        public const int RenderScaleMin = 50;
        public const int RenderScaleMax = 100;
        public const int RenderScaleStep = 5;
        public const float SensitivityMin = 0.2f;
        public const float SensitivityMax = 3f;
        public const int FovMin = 60;
        public const int FovMax = 100;

        public static int[] LampLightChoices() => new[] { 8, 16, 32 };

        /// <summary>0 = no limit.</summary>
        public static int[] FpsLimitChoices() => new[] { 30, 60, 120, 0 };

        /// <summary>True when an out-of-range value had to change. A preset that does not match the values is fixed silently.</summary>
        public static bool Sanitize(SettingsData s)
        {
            bool changed = false;
            changed |= Fix(ref s.viewDistance, Snap(s.viewDistance, ViewDistanceRules.Min, ViewDistanceRules.Max, ViewDistanceRules.Step));
            changed |= Fix(ref s.renderScale, Snap(s.renderScale, RenderScaleMin, RenderScaleMax, RenderScaleStep));
            changed |= Fix(ref s.lampLights, Nearest(s.lampLights, LampLightChoices()));
            changed |= Fix(ref s.fpsLimit, Array.IndexOf(FpsLimitChoices(), s.fpsLimit) >= 0 ? s.fpsLimit : 60);
            changed |= Fix(ref s.msaa, s.msaa == 2 ? 2 : 1);
            changed |= Fix(ref s.smallMeshCulling, Clamp(s.smallMeshCulling, 0f, 20f, 1f));
            changed |= Fix(ref s.mouseSensitivity, Clamp(s.mouseSensitivity, SensitivityMin, SensitivityMax, 1f));
            changed |= Fix(ref s.fieldOfView, Mathf.Clamp(s.fieldOfView, FovMin, FovMax));
            changed |= Fix(ref s.masterVolume, Mathf.Clamp(s.masterVolume, 0, 100));
            changed |= Fix(ref s.effectsVolume, Mathf.Clamp(s.effectsVolume, 0, 100));
            changed |= Fix(ref s.ambienceVolume, Mathf.Clamp(s.ambienceVolume, 0, 100));
            bool keepCurrent = s.resolutionWidth == 0 && s.resolutionHeight == 0;
            if (!keepCurrent && (s.resolutionWidth <= 0 || s.resolutionHeight <= 0))
            {
                s.resolutionWidth = 0;
                s.resolutionHeight = 0;
                changed = true;
            }
            if (!Enum.IsDefined(typeof(ShadowQuality), s.shadows))
            {
                s.shadows = ShadowQuality.Medium;
                changed = true;
            }
            s.preset = QualityPresets.Detect(s);
            return changed;
        }

        static int Snap(int value, int min, int max, int step)
        {
            int clamped = Mathf.Clamp(value, min, max);
            return min + Mathf.RoundToInt((clamped - min) / (float)step) * step;
        }

        static int Nearest(int value, int[] choices)
        {
            int best = choices[0];
            foreach (int choice in choices)
                if (Math.Abs(choice - value) < Math.Abs(best - value)) best = choice;
            return best;
        }

        static float Clamp(float value, float min, float max, float fallback) => float.IsNaN(value) ? fallback : Mathf.Clamp(value, min, max);

        static bool Fix(ref int field, int value)
        {
            if (field == value) return false;
            field = value;
            return true;
        }

        static bool Fix(ref float field, float value)
        {
            if (field == value) return false;
            field = value;
            return true;
        }
    }
}
