using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TycoWorld.GameSettings.Authoring
{
    /// <summary>Creates PC_Renderer_NoSSAO (PC_Renderer without renderer features) and registers it on PC_RPAsset once (spec #4 §5.4).</summary>
    public static class NoSsaoRendererBuilder
    {
        public const string MissingAssetsError = "[Settings] PC_RPAsset atau PC_Renderer tidak ditemukan di Assets/Settings.";

        /// <summary>The SSAO-free renderer's index in PC_RPAsset's renderer list; creates and registers it when missing.</summary>
        public static int Ensure()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(GameSettingsPaths.PipelineAsset);
            var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameSettingsPaths.RendererAsset);
            if (pipeline == null || source == null)
            {
                Debug.LogError(MissingAssetsError);
                return -1;
            }
            var copy = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameSettingsPaths.NoSsaoRendererAsset);
            if (copy == null)
            {
                copy = ScriptableObject.CreateInstance<UniversalRendererData>();
                EditorUtility.CopySerialized(source, copy);
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("m_RendererFeatures").ClearArray();
                serialized.FindProperty("m_RendererFeatureMap").ClearArray();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                copy.name = QualityApplier.NoSsaoRendererName;
                AssetDatabase.CreateAsset(copy, GameSettingsPaths.NoSsaoRendererAsset);
            }
            var pipelineObject = new SerializedObject(pipeline);
            var list = pipelineObject.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == copy) return i;
            int index = list.arraySize;
            list.arraySize = index + 1;
            list.GetArrayElementAtIndex(index).objectReferenceValue = copy;
            pipelineObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            return index;
        }
    }
}
