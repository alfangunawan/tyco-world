using NUnit.Framework;
using TycoWorld.GameSettings.Authoring;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.GameSettings.Tests
{
    public class NoSsaoRendererBuilderTests
    {
        [Test]
        public void Ensure_RegistersOneFeaturelessCopyOfThePcRenderer()
        {
            int first = NoSsaoRendererBuilder.Ensure();
            int second = NoSsaoRendererBuilder.Ensure();
            Assert.AreEqual(first, second);
            Assert.Greater(first, 0);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(GameSettingsPaths.PipelineAsset);
            var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameSettingsPaths.RendererAsset);
            var copy = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameSettingsPaths.NoSsaoRendererAsset);
            Assert.IsNotNull(copy);
            Assert.AreEqual(QualityApplier.NoSsaoRendererName, copy.name);
            Assert.AreEqual(0, copy.rendererFeatures.Count);
            Assert.AreEqual(source.renderingMode, copy.renderingMode);
            int matches = 0;
            foreach (var data in pipeline.rendererDataList)
                if (data == copy) matches++;
            Assert.AreEqual(1, matches);
            Assert.AreSame(source, pipeline.rendererDataList[0], "renderer default tetap PC_Renderer");
        }
    }
}
