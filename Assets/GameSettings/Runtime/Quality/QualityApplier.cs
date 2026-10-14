using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Applies graphics settings to a runtime copy of the active URP asset (the asset file is never modified),
    /// switches the camera to the SSAO-free renderer, sets the sun/moon shadow type, the FPS limit and VSync, and puts
    /// every global it touched back on Dispose (spec #4 §5.4).
    /// </summary>
    public sealed class QualityApplier : IDisposable
    {
        public const string NoSsaoRendererName = "PC_Renderer_NoSSAO";
        public const string MissingPipelineWarning = "[Settings] URP asset aktif tidak ditemukan; pengaturan grafis dilewati.";
        public const string MissingNoSsaoRendererWarning =
            "[Settings] PC_Renderer_NoSSAO tidak terdaftar di URP asset; SSAO tidak bisa dimatikan. Jalankan \"Tyco World/Game Settings Setup\".";

        readonly RenderPipelineAsset originalPipeline;
        readonly int originalVSync;
        readonly int originalFrameRate;
        UniversalRenderPipelineAsset copy;
        bool disposed;
        bool warnedMissingRenderer;

        public QualityApplier()
        {
            originalPipeline = QualitySettings.renderPipeline;
            originalVSync = QualitySettings.vSyncCount;
            originalFrameRate = Application.targetFrameRate;
            var source = originalPipeline as UniversalRenderPipelineAsset;
            if (source == null) source = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (source == null)
            {
                Debug.LogWarning(MissingPipelineWarning);
                return;
            }
            copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name + " (Runtime)";
            copy.hideFlags = HideFlags.DontSave;
            QualitySettings.renderPipeline = copy;
        }

        public UniversalRenderPipelineAsset RuntimeAsset => copy;
        public RenderPipelineAsset OriginalPipeline => originalPipeline;
        /// <summary>The renderer index last given to the camera (-1 = the pipeline's default renderer).</summary>
        public int LastRendererIndex { get; private set; } = -1;

        public void Apply(SettingsData s, Camera camera, IEnumerable<Light> directionalLights)
        {
            if (disposed) return;
            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Application.targetFrameRate = s.fpsLimit > 0 ? s.fpsLimit : -1;
            var tier = ShadowTiers.For(s.shadows);
            if (directionalLights != null)
            {
                var type = !tier.Directional ? LightShadows.None : tier.Soft ? LightShadows.Soft : LightShadows.Hard;
                foreach (var light in directionalLights)
                    if (light != null) light.shadows = type;
            }
            if (copy == null) return;
            copy.shadowDistance = tier.Distance;
            copy.shadowCascadeCount = tier.Cascades;
            copy.mainLightShadowmapResolution = tier.Resolution;
            bool upscaled = s.renderScale < 100;
            copy.renderScale = s.renderScale / 100f;
#pragma warning disable CS0618 // obsolete only when the upscaler framework is enabled, which this project does not use
            copy.upscalingFilter = upscaled ? UpscalingFilterSelection.STP : UpscalingFilterSelection.Auto;
#pragma warning restore CS0618
            copy.msaaSampleCount = upscaled ? 1 : s.msaa;
            copy.gpuResidentDrawerEnableOcclusionCullingInCameras = s.gpuOcclusion;
            copy.smallMeshScreenPercentage = s.smallMeshCulling;
            if (camera != null)
            {
                int index = s.ssao ? -1 : FindRenderer(NoSsaoRendererName);
                if (!s.ssao && index < 0 && !warnedMissingRenderer)
                {
                    warnedMissingRenderer = true;
                    Debug.LogWarning(MissingNoSsaoRendererWarning);
                }
                camera.GetUniversalAdditionalCameraData().SetRenderer(index);
                LastRendererIndex = index;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (copy != null)
            {
                if (QualitySettings.renderPipeline == copy) QualitySettings.renderPipeline = originalPipeline;
                if (Application.isPlaying) UnityEngine.Object.Destroy(copy);
                else UnityEngine.Object.DestroyImmediate(copy);
                copy = null;
            }
            QualitySettings.vSyncCount = originalVSync;
            Application.targetFrameRate = originalFrameRate;
        }

        int FindRenderer(string rendererName)
        {
            var list = copy.rendererDataList;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null && list[i].name == rendererName) return i;
            return -1;
        }
    }
}
