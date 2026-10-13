using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class OsmParserTests
    {
        static OsmData Synthetic() => OsmParser.Parse(FixtureFiles.Read("synthetic_world.json"));
        static OsmData Sukabirus() => OsmParser.Parse(FixtureFiles.Read("sukabirus_small.json"));

        [Test]
        public void Synthetic_CountsElements()
        {
            var d = Synthetic();
            Assert.AreEqual(23, d.Nodes.Count);
            Assert.AreEqual(8, d.Ways.Count);
            Assert.AreEqual(1, d.Relations.Count);
        }

        [Test]
        public void Synthetic_ReadsWayNodesAndTags()
        {
            var w = Synthetic().Ways[401];
            CollectionAssert.AreEqual(new long[] { 11, 12, 13, 14, 11 }, w.NodeIds);
            Assert.IsTrue(w.IsClosed);
            Assert.AreEqual("house", w.Tags["building"]);
            Assert.AreEqual("Rumah Sintetis", w.Tags.Tag("name"));
            Assert.IsNull(w.Tags.Tag("missing"));
        }

        [Test]
        public void Synthetic_DateLikeTagStaysVerbatim()
        {
            Assert.AreEqual("2023-05-01", Synthetic().Ways[401].Tags["check_date"]);
        }

        [Test]
        public void Synthetic_ReadsRelationMembers()
        {
            var r = Synthetic().Relations[201];
            Assert.AreEqual(3, r.Members.Count);
            Assert.AreEqual(OsmElementType.Way, r.Members[0].Type);
            Assert.AreEqual(101, r.Members[0].Ref);
            Assert.AreEqual("inner", r.Members[2].Role);
            Assert.AreEqual("multipolygon", r.Tags["type"]);
        }

        [Test]
        public void Synthetic_NodeTags()
        {
            var d = Synthetic();
            Assert.AreEqual(0, d.Nodes[1].Tags.Count);
            Assert.AreEqual("tree", d.Nodes[301].Tags["natural"]);
        }

        [Test]
        public void OpenWay_IsNotClosed()
        {
            Assert.IsFalse(Synthetic().Ways[101].IsClosed);
        }

        [Test]
        public void MissingElements_Throws()
        {
            Assert.Throws<FormatException>(() => OsmParser.Parse("{\"version\":0.6}"));
        }

        [Test]
        public void Parse_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                Assert.AreEqual(107.634108, Synthetic().Nodes[12].Lon, 1e-9);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void Sukabirus_ContainsJalanSukabirus()
        {
            var d = Sukabirus();
            Assert.IsTrue(d.Ways.TryGetValue(95529338, out var w));
            Assert.AreEqual("Jalan Sukabirus", w.Tags["name"]);
            Assert.AreEqual("tertiary", w.Tags["highway"]);
        }

        [Test]
        public void Sukabirus_AllWayNodesResolvable()
        {
            var d = Sukabirus();
            foreach (var w in d.Ways.Values)
                foreach (long id in w.NodeIds)
                    Assert.IsTrue(d.Nodes.ContainsKey(id), $"way {w.Id} node {id}");
        }

        [Test]
        public void Sukabirus_HasBuildings()
        {
            Assert.GreaterOrEqual(Sukabirus().Ways.Values.Count(w => w.Tags.ContainsKey("building")), 5);
        }
    }
}
