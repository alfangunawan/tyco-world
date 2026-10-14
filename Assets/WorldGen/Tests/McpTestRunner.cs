using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>Dev helper: runs the project's EditMode test assemblies synchronously (Procedure T, invoked through Unity MCP).</summary>
    public static class McpTestRunner
    {
        sealed class Collector : ICallbacks
        {
            public int Passed, Failed, Skipped;
            public readonly List<string> Failures = new List<string>();
            public void RunStarted(ITestAdaptor test) { }
            public void RunFinished(ITestResultAdaptor result) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                if (result.TestStatus == TestStatus.Passed) Passed++;
                else if (result.TestStatus == TestStatus.Failed)
                {
                    Failed++;
                    Failures.Add(result.FullName + " :: " + result.Message);
                }
                else Skipped++;
            }
        }

        public static string Run(string group, string resultPath = null)
        {
            string text;
            if (EditorApplication.isCompiling) text = "STILL COMPILING";
            else if (EditorUtility.scriptCompilationFailed) text = "COMPILE FAILED";
            else if (TryFindDirtyScene(out string dirty)) text = "DIRTY SCENE: " + dirty;
            else
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                var collector = new Collector();
                api.RegisterCallbacks(collector);
                var filter = new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = new[] { "TycoWorld.WorldGen.Tests", "TycoWorld.Player.Tests", "TycoWorld.DayCycle.Tests" },
                    groupNames = new[] { group }
                };
                api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
                api.UnregisterCallbacks(collector);
                UnityEngine.Object.DestroyImmediate(api);
                text = $"group={group} passed={collector.Passed} failed={collector.Failed} skipped={collector.Skipped}";
                if (collector.Failures.Count > 0) text += "\n" + string.Join("\n", collector.Failures);
            }
            if (!string.IsNullOrEmpty(resultPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
                File.WriteAllText(resultPath, text);
            }
            return text;
        }

        static bool TryFindDirtyScene(out string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty) continue;
                path = scene.path;
                return true;
            }
            path = null;
            return false;
        }
    }
}
