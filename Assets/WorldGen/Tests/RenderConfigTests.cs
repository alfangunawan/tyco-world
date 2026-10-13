using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.WorldGen.Tests
{
    public class RenderConfigTests
    {
        [Test]
        public void PcRenderer_UsesForwardPlus()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            Assert.IsNotNull(data);
            Assert.AreEqual(UnityEngine.Rendering.Universal.RenderingMode.ForwardPlus, data.renderingMode);
        }

        [Test]
        public void PcPipeline_UsesGpuResidentDrawer()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            Assert.IsNotNull(asset);
            Assert.AreEqual(UnityEngine.Rendering.GPUResidentDrawerMode.InstancedDrawing, asset.gpuResidentDrawerMode);
            Assert.IsTrue(asset.useSRPBatcher);
        }

        [Test]
        public void BrgVariants_KeepAll()
        {
            Assert.AreEqual(UnityEditor.Rendering.BatchRendererGroupStrippingMode.KeepAll,
                UnityEditor.Rendering.EditorGraphicsSettings.batchRendererGroupShaderStrippingMode);
        }
    }
}
