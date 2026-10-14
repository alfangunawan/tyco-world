using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>The Low/Medium/High graphics presets of spec #4 §4; FPS limit, VSync, screen and non-graphics settings are not part of them.</summary>
    public static class QualityPresets
    {
        public static void Apply(QualityPreset preset, SettingsData s)
        {
            switch (preset)
            {
                case QualityPreset.Low:
                    Set(s, 200, 70, ShadowQuality.Low, false, true, 8, 2f, 1);
                    break;
                case QualityPreset.Medium:
                    Set(s, 300, 85, ShadowQuality.Medium, false, true, 16, 1f, 1);
                    break;
                case QualityPreset.High:
                    Set(s, 500, 100, ShadowQuality.High, true, true, 32, 0f, 2);
                    break;
            }
            s.preset = preset;
        }

        /// <summary>The named preset whose graphics values equal <paramref name="s"/>'s, otherwise Custom.</summary>
        public static QualityPreset Detect(SettingsData s)
        {
            var probe = new SettingsData();
            foreach (var preset in new[] { QualityPreset.Low, QualityPreset.Medium, QualityPreset.High })
            {
                Apply(preset, probe);
                if (GraphicsEqual(probe, s)) return preset;
            }
            return QualityPreset.Custom;
        }

        public static bool GraphicsEqual(SettingsData a, SettingsData b) =>
            a.viewDistance == b.viewDistance && a.renderScale == b.renderScale && a.shadows == b.shadows && a.ssao == b.ssao &&
            a.gpuOcclusion == b.gpuOcclusion && a.lampLights == b.lampLights &&
            Mathf.Abs(a.smallMeshCulling - b.smallMeshCulling) < 0.001f && a.msaa == b.msaa;

        static void Set(SettingsData s, int viewDistance, int renderScale, ShadowQuality shadows, bool ssao, bool gpuOcclusion,
            int lampLights, float smallMeshCulling, int msaa)
        {
            s.viewDistance = viewDistance;
            s.renderScale = renderScale;
            s.shadows = shadows;
            s.ssao = ssao;
            s.gpuOcclusion = gpuOcclusion;
            s.lampLights = lampLights;
            s.smallMeshCulling = smallMeshCulling;
            s.msaa = msaa;
        }
    }
}
