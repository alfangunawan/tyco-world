using System.Collections.Generic;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Vehicles.Authoring
{
    public sealed class VehicleAssets
    {
        public BikeTuning Tuning;
        public VehicleMaterialSet Materials;
        public VehicleClipSet Clips;
        public GameObject MotorbikePrefab;
    }

    /// <summary>Creates or updates every vehicle asset in place (re-running is safe; tuning and materials keep user edits).</summary>
    public static class VehicleAssetsBuilder
    {
        [MenuItem("Tyco World/Vehicle/Build Vehicle Assets")]
        static void Menu() => BuildAll();

        public static VehicleAssets BuildAll()
        {
            // The first build creates a temporary object in the active scene; re-save scenes that were clean so Procedure R+T is not blocked.
            var cleanScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isDirty && s.path.Length > 0) cleanScenes.Add(s);
            }
            foreach (var folder in new[] { VehiclePaths.ConfigFolder, VehiclePaths.MaterialsFolder, VehiclePaths.PrefabsFolder, VehiclePaths.MeshesFolder })
                AssetFolders.Ensure(folder);
            VehicleAudioBuilder.WriteMissingClips();
            var a = new VehicleAssets
            {
                Tuning = LoadOrCreateTuning(),
                Materials = VehicleMaterials.LoadOrCreate(),
                Clips = new VehicleClipSet
                {
                    Engine = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.EngineName),
                    Starter = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.StarterName),
                    Horn = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.HornName),
                    Bump = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.BumpName)
                }
            };
            a.MotorbikePrefab = SavePrefab(a);
            AssetDatabase.SaveAssets();
            foreach (var s in cleanScenes)
                if (s.IsValid() && s.isDirty) EditorSceneManager.SaveScene(s);
            return a;
        }

        static BikeTuning LoadOrCreateTuning()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BikeTuning>(VehiclePaths.TuningAsset);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<BikeTuning>();
            AssetDatabase.CreateAsset(created, VehiclePaths.TuningAsset);
            return created;
        }

        /// <summary>
        /// An existing prefab is rebuilt inside its own contents, so its root keeps its file id and the pose override of
        /// the instance in World.unity survives a re-run.
        /// </summary>
        static GameObject SavePrefab(VehicleAssets a)
        {
            string path = VehiclePaths.MotorbikePrefab;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var root = new GameObject(MotorbikeBuilder.RootName);
                try
                {
                    MotorbikeBuilder.Build(root, a.Tuning, a.Materials, a.Clips);
                    PlaceholderPropFactory.SaveMeshes(root, VehiclePaths.MeshesFolder);
                    return PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MotorbikeBuilder.Build(contents, a.Tuning, a.Materials, a.Clips);
                PlaceholderPropFactory.SaveMeshes(contents, VehiclePaths.MeshesFolder);
                return PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
