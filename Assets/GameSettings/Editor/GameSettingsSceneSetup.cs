using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Authoring
{
    /// <summary>"Tyco World/Game Settings Setup": adds GameSystems (settings, culling, pause menu) to World.unity; safe to re-run (spec #4 §6).</summary>
    public static class GameSettingsSceneSetup
    {
        public const string RootName = "GameSystems";
        public const int MenuSortingOrder = 10;
        public const string MissingPanelError = "PlayerHudPanel belum ada. Jalankan \"Tyco World/Player Setup\" dulu.";

        [MenuItem("Tyco World/Game Settings Setup")]
        static void SetupMenu()
        {
            if (!Run(true, out string error)) EditorUtility.DisplayDialog("Game Settings Setup gagal", error, "OK");
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
            if (NoSsaoRendererBuilder.Ensure() < 0)
            {
                error = NoSsaoRendererBuilder.MissingAssetsError;
                return false;
            }
            var world = WorldScenes.EnsureWorldSceneOpen();
            SceneManager.SetActiveScene(world);
            Wire(world, panel);
            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            return true;
        }

        /// <summary>
        /// Creates or completes GameSystems in <paramref name="world"/>. A re-run keeps the panel and sort order already
        /// there and only adds missing components.
        /// </summary>
        public static GameObject Wire(Scene world, PanelSettings panel)
        {
            var root = FindRoot(world, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, world);
            }
            var document = GetOrAdd<UIDocument>(root, out bool newDocument);
            if (document.panelSettings == null) document.panelSettings = panel;
            if (newDocument) document.sortingOrder = MenuSortingOrder;
            GetOrAdd<SettingsApplier>(root, out _);
            GetOrAdd<DistanceCuller>(root, out _);
            GetOrAdd<GameMenu>(root, out _);
            EditorUtility.SetDirty(root);
            return root;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        static T GetOrAdd<T>(GameObject go, out bool added) where T : Component
        {
            var component = go.GetComponent<T>();
            added = component == null;
            if (added) component = go.AddComponent<T>();
            return component;
        }
    }
}
