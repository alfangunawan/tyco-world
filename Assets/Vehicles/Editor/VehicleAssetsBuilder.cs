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
        public VehicleCatalog Catalog;
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
            a.Catalog = VehicleModelAssets.BuildAll(a.Materials, a.Clips);
            CreditsBuilder.Write();
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
            var vario = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path("vario"));
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var contents = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(MotorbikeBuilder.RootName);
            try
            {
                if (vario != null)
                {
                    // The kos bike is the Vario model (spec vehicles-3a §6); its id, tuning asset and parking pose stay.
                    var bounds = VehicleModelBuilder.Build(contents, VehicleModelCatalog.Get("vario"), vario, a.Tuning, a.Materials, a.Clips);
                    VehicleClassPresets.FitBody(a.Tuning, bounds, VehicleClass.MotorMatic);
                    Motorbike.ConfigureBody(contents.GetComponent<CharacterController>(), a.Tuning);
                    var interactable = contents.GetComponent<MotorbikeInteractable>();
                    if (interactable == null) interactable = contents.AddComponent<MotorbikeInteractable>();
                    interactable.SetInteractionId(MotorbikeBuilder.InteractionId);
                    EditorUtility.SetDirty(a.Tuning);
                }
                else
                {
                    MotorbikeBuilder.Build(contents, a.Tuning, a.Materials, a.Clips);
                }
                PlaceholderPropFactory.SaveMeshes(contents, VehiclePaths.MeshesFolder);
                return PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(contents);
                else Object.DestroyImmediate(contents);
            }
        }
    }
}
