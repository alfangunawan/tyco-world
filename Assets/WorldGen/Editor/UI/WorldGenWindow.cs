using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class WorldGenWindow : EditorWindow
    {
        WorldGenSettings settings;
        Vector2 scroll;

        [MenuItem("Tyco World/World Generator")]
        public static void Open() => GetWindow<WorldGenWindow>("World Generator");

        void OnEnable()
        {
            if (settings == null) settings = DefaultSettingsFactory.LoadOrCreateAsset();
        }

        void OnInspectorUpdate()
        {
            if (OsmFetcher.Active != null && !OsmFetcher.Active.IsDone) Repaint();
        }

        void OnGUI()
        {
            settings = (WorldGenSettings)EditorGUILayout.ObjectField("Settings", settings, typeof(WorldGenSettings), false);
            if (settings == null)
            {
                EditorGUILayout.HelpBox("Belum ada WorldGenSettings.", MessageType.Warning);
                if (GUILayout.Button("Buat Settings")) settings = DefaultSettingsFactory.LoadOrCreateAsset();
                return;
            }

            EditorGUILayout.LabelField("Data OSM", CacheWriter.ReadInfoSummary(WorldGenPaths.ToAbsolute(settings.rawDataPath)), EditorStyles.wordWrappedLabel);

            var fetcher = OsmFetcher.Active;
            bool downloading = fetcher != null && !fetcher.IsDone;
            // Each action changes what the next layout pass draws, so it ends the current GUI pass with ExitGUI.
            using (new EditorGUI.DisabledScope(downloading))
            {
                if (GUILayout.Button("Download Data"))
                {
                    OsmFetcher.Start(settings, done =>
                    {
                        if (!done.Succeeded) EditorUtility.DisplayDialog("Download gagal", done.Error, "OK");
                        Repaint();
                    });
                    GUIUtility.ExitGUI();
                }
            }
            if (downloading)
            {
                EditorGUILayout.HelpBox(fetcher.Status, MessageType.Info);
                if (GUILayout.Button("Batalkan Download"))
                {
                    fetcher.Cancel();
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(downloading))
            {
                if (GUILayout.Button("Generate"))
                {
                    if (!WorldGenerationRunner.Generate(settings, true, out string error))
                        EditorUtility.DisplayDialog("Generate gagal", error, "OK");
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Clear"))
                {
                    WorldGenerationRunner.Clear(true, out _);
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Setup Preview Scene"))
                {
                    if (!PreviewSceneSetup.Run(settings, true, out string previewError))
                        EditorUtility.DisplayDialog("Setup Preview gagal", previewError, "OK");
                    GUIUtility.ExitGUI();
                }
            }

            var report = WorldGenerationRunner.LastReport;
            if (report != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Laporan terakhir", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.LabelField(report.Summary(), EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
