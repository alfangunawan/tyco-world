using UnityEditor;

namespace TycoWorld.WorldGen.Authoring
{
    public static class AssetFolders
    {
        /// <summary>Creates a project folder (and missing parents), e.g. "Assets/WorldGen/Config".</summary>
        public static void Ensure(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
