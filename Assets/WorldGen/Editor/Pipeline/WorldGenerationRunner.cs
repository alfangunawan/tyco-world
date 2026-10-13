using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Generate / Clear entry points used by the window and by Unity MCP (interactive = false).</summary>
    public static class WorldGenerationRunner
    {
        public static GenerationReport LastReport { get; private set; }

        public static bool Generate(WorldGenSettings settings, bool interactive, out string error)
        {
            error = null;
            if (settings == null) { error = "Settings kosong."; return false; }
            string raw = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            if (!File.Exists(raw)) { error = "Download data dulu (tidak ada " + settings.rawDataPath + ")."; return false; }
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;

            var report = new GenerationReport();
            var total = System.Diagnostics.Stopwatch.StartNew();
            var stage = System.Diagnostics.Stopwatch.StartNew();
            Scene generated = default;

            void Progress(string label, float t)
            {
                if (interactive && EditorUtility.DisplayCancelableProgressBar("World Generator", label, t))
                    throw new OperationCanceledException();
            }

            void EndStage(string name)
            {
                report.Stage(name, stage.Elapsed.TotalMilliseconds);
                stage.Restart();
            }

            try
            {
                var world = WorldScenes.EnsureWorldSceneOpen();
                WorldScenes.CloseGeneratedScene();
                WorldScenes.DeleteGeneratedFolder();
                EndStage("prepare");

                Progress("Parsing & klasifikasi OSM", 0.05f);
                var classified = WorldMeshPipeline.Classify(File.ReadAllText(raw), settings, report);
                EndStage("classify");

                Progress("Membuat mesh", 0.25f);
                var built = WorldMeshPipeline.Build(classified, settings, report);
                EndStage("meshes");

                Progress("Menulis asset mesh", 0.45f);
                var meshes = ChunkAssetWriter.Write(built, WorldGenPaths.GeneratedMeshesFolder, out var groundMesh);
                EndStage("assets");

                generated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(generated);
                var root = SceneObjectFactory.Populate(generated, built, meshes, groundMesh, settings, report,
                    (label, t) => Progress(label, 0.6f + 0.35f * t));
                EndStage("scene");

                total.Stop();
                report.TotalMillis = total.Elapsed.TotalMilliseconds;
                root.AddComponent<GeneratedRoot>().Initialize(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), report.Summary());
                EditorSceneManager.SaveScene(generated, WorldGenPaths.GeneratedScenePath);
                SceneManager.SetActiveScene(world);
                WorldScenes.EnsureBuildSettings();
                AssetDatabase.SaveAssets();
                LastReport = report;
                Debug.Log("[WorldGen] Generate selesai.\n" + report.Summary());
                foreach (var warning in report.Warnings) Debug.LogWarning("[WorldGen] " + warning);
                return true;
            }
            catch (Exception ex)
            {
                if (generated.IsValid()) EditorSceneManager.CloseScene(generated, true);
                WorldScenes.DeleteGeneratedFolder();
                bool cancelled = ex is OperationCanceledException;
                error = cancelled ? "Generate dibatalkan." : "Generate gagal: " + ex.Message;
                if (!cancelled) Debug.LogException(ex);
                return false;
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
            }
        }

        public static bool Clear(bool interactive, out string error)
        {
            error = null;
            if (interactive && !EditorUtility.DisplayDialog("Clear World",
                    "Hapus semua hasil generate di Assets/WorldGen/Generated?", "Hapus", "Batal"))
            {
                error = "Dibatalkan.";
                return false;
            }
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            WorldScenes.EnsureWorldSceneOpen();
            WorldScenes.CloseGeneratedScene();
            WorldScenes.DeleteGeneratedFolder();
            WorldScenes.RemoveGeneratedFromBuildSettings();
            AssetDatabase.SaveAssets();
            LastReport = null;
            return true;
        }
    }
}
