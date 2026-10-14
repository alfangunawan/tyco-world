using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.GameSettings.Tests
{
    public class QualityApplierTests
    {
        readonly List<Object> created = new List<Object>();
        RenderPipelineAsset pipelineBefore;
        int vSyncBefore, frameRateBefore;
        QualityApplier applier;

        [SetUp]
        public void SetUp()
        {
            pipelineBefore = QualitySettings.renderPipeline;
            vSyncBefore = QualitySettings.vSyncCount;
            frameRateBefore = Application.targetFrameRate;
            applier = new QualityApplier();
        }

        [TearDown]
        public void TearDown()
        {
            applier.Dispose();
            if (QualitySettings.renderPipeline != pipelineBefore) QualitySettings.renderPipeline = pipelineBefore;
            if (QualitySettings.vSyncCount != vSyncBefore) QualitySettings.vSyncCount = vSyncBefore;
            Application.targetFrameRate = frameRateBefore;
            foreach (var obj in created) if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        Light DirectionalLight(string name)
        {
            var light = Track(new GameObject(name)).AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            return light;
        }

        static SettingsData Preset(QualityPreset preset)
        {
            var s = new SettingsData();
            QualityPresets.Apply(preset, s);
            return s;
        }

        [Test]
        public void Constructor_SwapsInARuntimeCopy()
        {
            Assert.IsNotNull(applier.RuntimeAsset);
            Assert.AreSame(applier.RuntimeAsset, QualitySettings.renderPipeline);
            Assert.AreSame(pipelineBefore, applier.OriginalPipeline);
            Assert.AreNotSame(pipelineBefore, applier.RuntimeAsset);
            Assert.AreNotEqual(0, (int)(applier.RuntimeAsset.hideFlags & HideFlags.DontSave));
        }

        [Test]
        public void Apply_WritesTheCopyOnly()
        {
            var original = (UniversalRenderPipelineAsset)pipelineBefore;
            float shadowBefore = original.shadowDistance;
            float scaleBefore = original.renderScale;
            int msaaBefore = original.msaaSampleCount;
            bool occlusionBefore = original.gpuResidentDrawerEnableOcclusionCullingInCameras;

            applier.Apply(Preset(QualityPreset.High), null, null);

            var copy = applier.RuntimeAsset;
            Assert.AreEqual(80f, copy.shadowDistance);
            Assert.AreEqual(4, copy.shadowCascadeCount);
            Assert.AreEqual(2048, copy.mainLightShadowmapResolution);
            Assert.AreEqual(1f, copy.renderScale);
            Assert.AreEqual(2, copy.msaaSampleCount);
            Assert.IsFalse(copy.gpuResidentDrawerEnableOcclusionCullingInCameras);
            Assert.AreEqual(0f, copy.smallMeshScreenPercentage);
            Assert.AreEqual(shadowBefore, original.shadowDistance);
            Assert.AreEqual(scaleBefore, original.renderScale);
            Assert.AreEqual(msaaBefore, original.msaaSampleCount);
            Assert.AreEqual(occlusionBefore, original.gpuResidentDrawerEnableOcclusionCullingInCameras);
            Assert.IsFalse(EditorUtility.IsDirty(original), "PC_RPAsset tidak boleh berubah");
        }

        [Test]
        public void Apply_GpuOcclusionToggle_ReachesTheCopy()
        {
            var s = Preset(QualityPreset.Medium);
            s.gpuOcclusion = true;
            applier.Apply(s, null, null);
            Assert.IsTrue(applier.RuntimeAsset.gpuResidentDrawerEnableOcclusionCullingInCameras);
            s.gpuOcclusion = false;
            applier.Apply(s, null, null);
            Assert.IsFalse(applier.RuntimeAsset.gpuResidentDrawerEnableOcclusionCullingInCameras);
        }

        [Test]
        public void Apply_RenderScaleBelow100_UsesStpWithoutMsaa()
        {
            var s = Preset(QualityPreset.High);
            s.renderScale = 70;
            applier.Apply(s, null, null);
            Assert.AreEqual(0.7f, applier.RuntimeAsset.renderScale, 1e-4f);
#pragma warning disable CS0618
            Assert.AreEqual(UpscalingFilterSelection.STP, applier.RuntimeAsset.upscalingFilter);
#pragma warning restore CS0618
            Assert.AreEqual(1, applier.RuntimeAsset.msaaSampleCount);
        }

        [TestCase(ShadowQuality.Off, LightShadows.None)]
        [TestCase(ShadowQuality.Low, LightShadows.Hard)]
        [TestCase(ShadowQuality.Medium, LightShadows.Soft)]
        [TestCase(ShadowQuality.High, LightShadows.Soft)]
        public void Apply_SetsSunAndMoonShadowType(ShadowQuality quality, LightShadows expected)
        {
            var sun = DirectionalLight("Sun");
            var moon = DirectionalLight("Moon");
            applier.Apply(new SettingsData { shadows = quality }, null, new[] { sun, moon, null });
            Assert.AreEqual(expected, sun.shadows);
            Assert.AreEqual(expected, moon.shadows);
        }

        [Test]
        public void Apply_ShadowsOff_KeepsAShadowDistanceForTheFlashlight()
        {
            applier.Apply(new SettingsData { shadows = ShadowQuality.Off }, null, null);
            Assert.Greater(applier.RuntimeAsset.shadowDistance, 0f);
        }

        [Test]
        public void Apply_Ssao_SwitchesTheCameraRenderer()
        {
            var cam = Track(new GameObject("Cam")).AddComponent<Camera>();
            var s = new SettingsData { ssao = false };
            applier.Apply(s, cam, null);
            Assert.Greater(applier.LastRendererIndex, 0, "PC_Renderer_NoSSAO harus terdaftar di PC_RPAsset");
            s.ssao = true;
            applier.Apply(s, cam, null);
            Assert.AreEqual(-1, applier.LastRendererIndex);
        }

        [Test]
        public void Apply_FpsLimitAndVSync()
        {
            applier.Apply(new SettingsData { fpsLimit = 30, vSync = true }, null, null);
            Assert.AreEqual(30, Application.targetFrameRate);
            Assert.AreEqual(1, QualitySettings.vSyncCount);
            applier.Apply(new SettingsData { fpsLimit = 0, vSync = false }, null, null);
            Assert.AreEqual(-1, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
        }

        [Test]
        public void Dispose_RestoresPipelineVSyncAndFrameRate()
        {
            applier.Apply(new SettingsData { fpsLimit = 30, vSync = true }, null, null);
            var copy = applier.RuntimeAsset;
            applier.Dispose();
            Assert.AreSame(pipelineBefore, QualitySettings.renderPipeline);
            Assert.AreEqual(vSyncBefore, QualitySettings.vSyncCount);
            Assert.AreEqual(frameRateBefore, Application.targetFrameRate);
            Assert.IsTrue(copy == null, "salinan runtime dihancurkan");
            Assert.DoesNotThrow(() => applier.Dispose());
        }

        [Test]
        public void Apply_AfterDispose_DoesNothing()
        {
            applier.Dispose();
            applier.Apply(new SettingsData { fpsLimit = 30 }, null, null);
            Assert.AreEqual(frameRateBefore, Application.targetFrameRate);
        }
    }
}
