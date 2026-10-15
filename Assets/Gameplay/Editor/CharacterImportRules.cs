using UnityEditor;

namespace TycoWorld.Gameplay.Authoring
{
    /// <summary>
    /// Import settings for the downloaded character art (spec #7): every FBX under the Quaternius folder is a Humanoid
    /// (one avatar per model, so animations retarget between them); only the animation library keeps its clips, named
    /// without the "Rig|" prefix, looping when the name ends in "_Loop", and baked in place (code moves the people).
    /// </summary>
    public sealed class CharacterImportRules : AssetPostprocessor
    {
        public const string QuaterniusFolder = "Assets/Gameplay/Art/ThirdParty/Quaternius/";
        public const string AnimationFolder = QuaterniusFolder + "UAL/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(QuaterniusFolder, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = assetPath.StartsWith(AnimationFolder, System.StringComparison.Ordinal);
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationFolder, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                string name = clip.name;
                int bar = name.LastIndexOf('|');
                if (bar >= 0) name = name.Substring(bar + 1);
                clip.name = name;
                clip.loopTime = name.EndsWith("_Loop", System.StringComparison.Ordinal);
                // In place: the game moves people itself.
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clips[i] = clip;
            }
            importer.clipAnimations = clips;
        }
    }
}
