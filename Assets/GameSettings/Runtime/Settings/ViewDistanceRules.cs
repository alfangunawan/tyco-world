using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>How the view distance setting drives the camera, fog and culling (spec #4 §5.1–5.2).</summary>
    public static class ViewDistanceRules
    {
        public const int Min = 100;
        public const int Max = 800;
        public const int Step = 50;
        public const float FarClipFactor = 1.05f;
        /// <summary>Exponential fog reaches about 90 % (e^-2.3) at the view distance.</summary>
        public const float FogFactor = 2.3f;
        public const float PropRatio = 0.6f;
        public const float PropMinimum = 80f;
        public const float DetailRatio = 0.4f;
        public const float DetailMinimum = 80f;

        public static float FarClip(float viewDistance) => viewDistance * FarClipFactor;

        public static float MinFogDensity(float viewDistance) => FogFactor / Mathf.Max(1f, viewDistance);

        /// <summary>Trees and lamp posts disappear (and lamp lights are budgeted) within this distance.</summary>
        public static float PropDistance(float viewDistance) => Mathf.Max(PropMinimum, viewDistance * PropRatio);

        /// <summary>Property-line walls and road edges disappear within this distance (spec frontage-polish §2.5).</summary>
        public static float DetailDistance(float viewDistance) => Mathf.Max(DetailMinimum, viewDistance * DetailRatio);
    }
}
