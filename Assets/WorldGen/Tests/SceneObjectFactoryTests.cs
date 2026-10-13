using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class SceneObjectFactoryTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        GameObject Make(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [Test]
        public void AddEdgeWalls_CreatesFourStaticInvisibleBoxes()
        {
            var parent = Make("WorldEdge").transform;
            SceneObjectFactory.AddEdgeWalls(parent, new Rect(0f, 0f, 100f, 50f));
            Assert.AreEqual(4, parent.childCount);
            CollectionAssert.AreEquivalent(new[] { "Edge_West", "Edge_East", "Edge_South", "Edge_North" },
                new[] { parent.GetChild(0).name, parent.GetChild(1).name, parent.GetChild(2).name, parent.GetChild(3).name });
            foreach (Transform child in parent)
            {
                Assert.IsNotNull(child.GetComponent<BoxCollider>());
                Assert.IsNull(child.GetComponent<MeshRenderer>());
                Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(child.gameObject, StaticEditorFlags.OccludeeStatic));
            }
            var west = parent.Find("Edge_West").GetComponent<BoxCollider>();
            Assert.AreEqual(0f, west.center.x + west.size.x * 0.5f, 1e-4f);
        }

        [Test]
        public void ApplySurface_DefaultAddsNothing()
        {
            var go = Make("building");
            SceneObjectFactory.ApplySurface(go, SurfaceType.Default);
            Assert.IsNull(go.GetComponent<GroundSurface>());
        }

        [Test]
        public void ApplySurface_AddsOrUpdatesComponent()
        {
            var go = Make("road");
            SceneObjectFactory.ApplySurface(go, SurfaceType.Asphalt);
            Assert.AreEqual(SurfaceType.Asphalt, go.GetComponent<GroundSurface>().Type);
            SceneObjectFactory.ApplySurface(go, SurfaceType.Paving);
            Assert.AreEqual(1, go.GetComponents<GroundSurface>().Length);
            Assert.AreEqual(SurfaceType.Paving, go.GetComponent<GroundSurface>().Type);
        }
    }
}
