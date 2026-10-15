using System.IO;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TycoWorld.Gameplay.Authoring
{
    /// <summary>
    /// "Tyco World/Gameplay Setup": bakes KampungData, builds the item catalog and base material, and adds the "Gameplay"
    /// root (KampungDirector + its UIDocument) to World.unity; safe to re-run (spec #6 §4).
    /// </summary>
    public static class GameplaySceneSetup
    {
        public const string RootName = "Gameplay";
        public const int HudSortingOrder = 5;
        public const string BaseMaterialAsset = GameplayPaths.MaterialsFolder + "/Gameplay_Base.mat";
        public const string MissingPanelError = "PlayerHudPanel belum ada. Jalankan \"Tyco World/Player Setup\" dulu.";

        [MenuItem("Tyco World/Gameplay Setup")]
        static void SetupMenu()
        {
            if (!Run(true, out string error)) EditorUtility.DisplayDialog("Gameplay Setup gagal", error, "OK");
        }

        public static bool Run(bool interactive, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PlayerPaths.PanelSettingsAsset);
            if (panel == null)
            {
                error = MissingPanelError;
                return false;
            }
            if (!KampungDataBakeMenu.Bake(out _, out error)) return false;
            var data = AssetDatabase.LoadAssetAtPath<KampungData>(GameplayPaths.KampungDataAsset);
            var catalog = EnsureCatalog();
            var material = EnsureBaseMaterial();
            var world = WorldScenes.EnsureWorldSceneOpen();
            SceneManager.SetActiveScene(world);
            Wire(world, panel, data, catalog, material);
            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            return true;
        }

        public static GameObject Wire(Scene world, PanelSettings panel, KampungData data, ItemCatalog catalog, Material material)
        {
            GameObject root = null;
            foreach (var go in world.GetRootGameObjects())
                if (go.name == RootName) root = go;
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, world);
            }
            var document = root.GetComponent<UIDocument>();
            bool newDocument = document == null;
            if (newDocument) document = root.AddComponent<UIDocument>();
            if (document.panelSettings == null) document.panelSettings = panel;
            if (newDocument) document.sortingOrder = HudSortingOrder;
            var director = root.GetComponent<KampungDirector>();
            if (director == null) director = root.AddComponent<KampungDirector>();
            var bike = AssetDatabase.LoadAssetAtPath<GameObject>(TycoWorld.Vehicles.Authoring.VehiclePaths.MotorbikePrefab);
            var people = CharacterArtSetup.Run(out string artError);
            if (people == null) Debug.LogWarning("[Gameplay] " + artError + " Orang memakai figur primitif.");
            director.Configure(data, catalog, material, bike, people);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(root);
            return root;
        }

        public static ItemCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(GameplayPaths.ItemCatalogAsset);
            if (catalog != null) return catalog;
            Directory.CreateDirectory(GameplayPaths.DataFolder);
            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            AssetDatabase.CreateAsset(catalog, GameplayPaths.ItemCatalogAsset);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        public static Material EnsureBaseMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialAsset);
            if (material != null) return material;
            Directory.CreateDirectory(GameplayPaths.MaterialsFolder);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "Gameplay_Base" };
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, BaseMaterialAsset);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
