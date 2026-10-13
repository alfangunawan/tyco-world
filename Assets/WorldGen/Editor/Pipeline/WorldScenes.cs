using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Scene bookkeeping for World.unity (manual, tracked) and World_Generated.unity (owned by the generator).</summary>
    public static class WorldScenes
    {
        public static bool CheckSaved(bool interactive, out string error)
        {
            error = null;
            if (interactive)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;
                error = "Dibatalkan: ada scene yang belum disimpan.";
                return false;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty || scene.path == WorldGenPaths.GeneratedScenePath) continue;
                error = "Scene belum disimpan: '" + (scene.path.Length > 0 ? scene.path : scene.name) + "'. Simpan dulu, lalu ulangi.";
                return false;
            }
            return true;
        }

        public static Scene EnsureWorldSceneOpen()
        {
            var world = SceneManager.GetSceneByPath(WorldGenPaths.WorldScenePath);
            if (!world.IsValid() || !world.isLoaded)
            {
                if (File.Exists(WorldGenPaths.ToAbsolute(WorldGenPaths.WorldScenePath)))
                {
                    world = EditorSceneManager.OpenScene(WorldGenPaths.WorldScenePath, OpenSceneMode.Single);
                }
                else
                {
                    AssetFolders.Ensure("Assets/Scenes");
                    world = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    new GameObject("WorldSceneLoader").AddComponent<WorldSceneLoader>();
                    EditorSceneManager.SaveScene(world, WorldGenPaths.WorldScenePath);
                }
            }
            SceneManager.SetActiveScene(world);
            return world;
        }

        public static void CloseGeneratedScene()
        {
            var scene = SceneManager.GetSceneByPath(WorldGenPaths.GeneratedScenePath);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        public static void OpenGeneratedSceneAdditive()
        {
            if (!File.Exists(WorldGenPaths.ToAbsolute(WorldGenPaths.GeneratedScenePath))) return;
            var scene = SceneManager.GetSceneByPath(WorldGenPaths.GeneratedScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                EditorSceneManager.OpenScene(WorldGenPaths.GeneratedScenePath, OpenSceneMode.Additive);
        }

        /// <summary>Deletes Assets/WorldGen/Generated via System.IO (AssetDatabase.DeleteAsset is blocked under Unity MCP).</summary>
        public static void DeleteGeneratedFolder()
        {
            string abs = WorldGenPaths.ToAbsolute(WorldGenPaths.GeneratedFolder);
            if (!abs.Replace('\\', '/').EndsWith("/Assets/WorldGen/Generated", StringComparison.Ordinal))
                throw new InvalidOperationException("Menolak menghapus path tak terduga: " + abs);
            if (Directory.Exists(abs)) Directory.Delete(abs, true);
            if (File.Exists(abs + ".meta")) File.Delete(abs + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == WorldGenPaths.WorldScenePath))
                scenes.Insert(0, new EditorBuildSettingsScene(WorldGenPaths.WorldScenePath, true));
            if (!scenes.Any(s => s.path == WorldGenPaths.GeneratedScenePath))
            {
                int worldIndex = scenes.FindIndex(s => s.path == WorldGenPaths.WorldScenePath);
                scenes.Insert(worldIndex + 1, new EditorBuildSettingsScene(WorldGenPaths.GeneratedScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void RemoveGeneratedFromBuildSettings()
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != WorldGenPaths.GeneratedScenePath).ToArray();
        }
    }
}
