using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class OsmFeatureRegistryTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [SetUp]
        public void SetUp() => OsmFeatureRegistry.Clear();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            OsmFeatureRegistry.Clear();
        }

        OsmFeature Make(long id, OsmElementType type, string name, params (string, string)[] tags)
        {
            var go = new GameObject(name);
            created.Add(go);
            var feature = go.AddComponent<OsmFeature>();
            var list = new List<KeyValuePair<string, string>>();
            foreach (var (k, v) in tags) list.Add(new KeyValuePair<string, string>(k, v));
            feature.Initialize(id, type, FeatureKind.Building, name, list);
            OsmFeatureRegistry.Register(feature);
            return feature;
        }

        [Test]
        public void TryGet_ReturnsRegisteredFeature()
        {
            var f = Make(10, OsmElementType.Way, "A");
            Assert.IsTrue(OsmFeatureRegistry.TryGet(OsmElementType.Way, 10, out var found));
            Assert.AreSame(f, found);
        }

        [Test]
        public void SameIdDifferentTypes_DoNotCollide()
        {
            var way = Make(5, OsmElementType.Way, "W");
            var node = Make(5, OsmElementType.Node, "N");
            Assert.IsTrue(OsmFeatureRegistry.TryGet(OsmElementType.Way, 5, out var w));
            Assert.IsTrue(OsmFeatureRegistry.TryGet(OsmElementType.Node, 5, out var n));
            Assert.AreSame(way, w);
            Assert.AreSame(node, n);
            Assert.AreEqual(2, OsmFeatureRegistry.Count);
        }

        [Test]
        public void TryGet_ByIdOnly_PrefersWay()
        {
            Make(7, OsmElementType.Node, "N");
            var way = Make(7, OsmElementType.Way, "W");
            Assert.IsTrue(OsmFeatureRegistry.TryGet(7, out var found));
            Assert.AreSame(way, found);
        }

        [Test]
        public void Unregister_StaleDuplicate_KeepsNewer()
        {
            var older = Make(1, OsmElementType.Way, "old");
            var newer = Make(1, OsmElementType.Way, "new");
            OsmFeatureRegistry.Unregister(older);
            Assert.IsTrue(OsmFeatureRegistry.TryGet(OsmElementType.Way, 1, out var found));
            Assert.AreSame(newer, found);
        }

        [Test]
        public void FindByName_IsCaseInsensitiveAndSortedById()
        {
            Make(30, OsmElementType.Way, "Jalan Sukabirus");
            Make(20, OsmElementType.Way, "jalan sukabirus");
            Make(25, OsmElementType.Way, "Jalan Telekomunikasi");
            var found = OsmFeatureRegistry.FindByName("JALAN SUKABIRUS");
            Assert.AreEqual(2, found.Count);
            Assert.AreEqual(20, found[0].OsmId);
            Assert.AreEqual(30, found[1].OsmId);
        }

        [Test]
        public void DestroyedFeature_IsNotReturned()
        {
            var f = Make(9, OsmElementType.Way, "gone");
            Object.DestroyImmediate(f.gameObject);
            Assert.IsFalse(OsmFeatureRegistry.TryGet(OsmElementType.Way, 9, out _));
        }

        [Test]
        public void TryGetTag_ReadsInitializedTags()
        {
            var f = Make(3, OsmElementType.Way, "T", ("highway", "tertiary"), ("width", "6"));
            Assert.IsTrue(f.TryGetTag("width", out var width));
            Assert.AreEqual("6", width);
            Assert.IsFalse(f.TryGetTag("name", out _));
            Assert.AreEqual(2, f.TagCount);
        }
    }
}
