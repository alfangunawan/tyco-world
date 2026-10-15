using System.Collections.Generic;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Builds every model prefab, its tuning asset and the catalog (spec vehicles-3a §4–§5). Safe to re-run.</summary>
    public static class VehicleModelAssets
    {
        public const string MissingWarning = "[Vehicle] Model kendaraan tidak ada, dilewati: ";

        public static int Missing { get; private set; }

        [MenuItem("Tyco World/Vehicle/Build Vehicle Models")]
        static void Menu() => BuildAll(VehicleMaterials.LoadOrCreate(), new VehicleClipSet
        {
            Engine = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.EngineName),
            Starter = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.StarterName),
            Horn = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.HornName),
            Bump = VehicleAudioBuilder.Resolve(VehicleAudioBuilder.BumpName)
        });

        public static VehicleCatalog BuildAll(VehicleMaterialSet materials, VehicleClipSet clips)
        {
            AssetFolders.Ensure(VehiclePaths.ModelPrefabsFolder);
            AssetFolders.Ensure(VehiclePaths.ModelTuningFolder);
            Missing = 0;
            var entries = new List<VehicleCatalog.Entry>();
            foreach (var spec in VehicleModelCatalog.All)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(spec.Id));
                var prefab = BuildOne(spec, source, materials, clips, out bool missing);
                if (missing)
                {
                    Missing++;
                    Debug.LogWarning(MissingWarning + spec.Id);
                    continue;
                }
                entries.Add(new VehicleCatalog.Entry
                {
                    id = spec.Id, vehicleClass = spec.Class, prefab = prefab,
                    paintMaterials = (string[])spec.PaintMaterials.Clone(), paintStrength = spec.PaintStrength
                });
            }
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehiclePaths.CatalogAsset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VehicleCatalog>();
                AssetDatabase.CreateAsset(catalog, VehiclePaths.CatalogAsset);
            }
            catalog.entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <returns>The saved prefab, or null with <paramref name="missing"/> set when the model file is absent.</returns>
        public static GameObject BuildOne(VehicleModelSpec spec, GameObject source, VehicleMaterialSet materials, VehicleClipSet clips, out bool missing)
        {
            missing = source == null;
            if (missing) return null;
            string tuningPath = VehiclePaths.ModelTuningFolder + "/" + spec.Id + ".asset";
            var tuning = AssetDatabase.LoadAssetAtPath<BikeTuning>(tuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<BikeTuning>();
                AssetDatabase.CreateAsset(tuning, tuningPath);
            }
            VehicleClassPresets.Apply(tuning, spec.Class);
            string path = VehiclePaths.ModelPrefabsFolder + "/" + spec.Id + ".prefab";
            // Built in a preview scene so the open scene is not marked dirty.
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(spec.Id);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var bounds = VehicleModelBuilder.Build(root, spec, source, tuning, materials, clips);
                VehicleClassPresets.FitBody(tuning, bounds, spec.Class);
                Motorbike.ConfigureBody(root.GetComponent<CharacterController>(), tuning);
                EditorUtility.SetDirty(tuning);
                PlaceholderPropFactory.SaveMeshes(root, VehiclePaths.MeshesFolder);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
