using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.GameSettings.Authoring;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class GameSettingsSetupTests
    {
        readonly List<Object> created = new List<Object>();
        readonly HashSet<GameObject> rootsBefore = new HashSet<GameObject>();
        Scene scratch;

        /// <summary>The untitled scene the test runner opens stands in for World.unity; every root a test adds is destroyed in TearDown.</summary>
        Scene Scratch()
        {
            scratch = SceneManager.GetActiveScene();
            Assert.IsTrue(string.IsNullOrEmpty(scratch.path), "Tes setup hanya boleh memakai scene untitled, bukan " + scratch.path);
            rootsBefore.Clear();
            rootsBefore.UnionWith(scratch.GetRootGameObjects());
            return scratch;
        }

        PanelSettings NewPanel()
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            created.Add(panel);
            return panel;
        }

        [TearDown]
        public void TearDown()
        {
            if (scratch.IsValid() && scratch.isLoaded)
                foreach (var go in scratch.GetRootGameObjects())
                    if (!rootsBefore.Contains(go)) Object.DestroyImmediate(go);
            scratch = default;
            rootsBefore.Clear();
            foreach (var obj in created) if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }

        [Test]
        public void Wire_FirstRun_CreatesTheSystemsObject()
        {
            var scene = Scratch();
            var panel = NewPanel();
            var root = GameSettingsSceneSetup.Wire(scene, panel);
            Assert.AreEqual(GameSettingsSceneSetup.RootName, root.name);
            Assert.AreEqual(scene, root.scene);
            Assert.IsNotNull(root.GetComponent<SettingsApplier>());
            Assert.IsNotNull(root.GetComponent<DistanceCuller>());
            Assert.IsNotNull(root.GetComponent<GameMenu>());
            var document = root.GetComponent<UIDocument>();
            Assert.AreSame(panel, document.panelSettings);
            Assert.AreEqual(GameSettingsSceneSetup.MenuSortingOrder, (int)document.sortingOrder);
        }

        [Test]
        public void Wire_Rerun_KeepsUserChoices_NoDuplicates()
        {
            var scene = Scratch();
            var panel = NewPanel();
            var other = NewPanel();
            var root = GameSettingsSceneSetup.Wire(scene, panel);
            var document = root.GetComponent<UIDocument>();
            document.panelSettings = other;
            document.sortingOrder = 3;

            var again = GameSettingsSceneSetup.Wire(scene, panel);

            Assert.AreSame(root, again);
            Assert.AreSame(other, document.panelSettings);
            Assert.AreEqual(3, (int)document.sortingOrder);
            int systems = 0;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == GameSettingsSceneSetup.RootName) systems++;
            Assert.AreEqual(1, systems);
            Assert.AreEqual(1, root.GetComponents<UIDocument>().Length);
            Assert.AreEqual(1, root.GetComponents<SettingsApplier>().Length);
            Assert.AreEqual(1, root.GetComponents<DistanceCuller>().Length);
            Assert.AreEqual(1, root.GetComponents<GameMenu>().Length);
        }

        [Test]
        public void Wire_Rerun_RefillsAMissingPanel()
        {
            var scene = Scratch();
            var panel = NewPanel();
            var root = GameSettingsSceneSetup.Wire(scene, panel);
            root.GetComponent<UIDocument>().panelSettings = null;
            GameSettingsSceneSetup.Wire(scene, panel);
            Assert.AreSame(panel, root.GetComponent<UIDocument>().panelSettings);
        }
    }
}
