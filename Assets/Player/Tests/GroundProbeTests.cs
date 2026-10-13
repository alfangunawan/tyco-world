using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    public class GroundProbeTests
    {
        PhysicsFixture fx;
        static Vector3 From => PhysicsFixture.Origin + Vector3.up * 0.5f;

        [SetUp]
        public void SetUp()
        {
            fx = new PhysicsFixture();
            GroundProbe.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            GroundProbe.ClearCache();
        }

        [Test]
        public void Probe_ReturnsSurfaceAndFeature()
        {
            var box = fx.Floor();
            box.AddComponent<GroundSurface>().Initialize(SurfaceType.Grass);
            box.AddComponent<OsmFeature>().Initialize(77, OsmElementType.Way, FeatureKind.Area, "lapangan", null);
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.IsTrue(info.Hit);
            Assert.AreEqual(SurfaceType.Grass, info.Surface);
            Assert.AreEqual(77, info.Feature.OsmId);
            Assert.AreSame(box.GetComponent<BoxCollider>(), info.Collider);
            Assert.AreEqual(PhysicsFixture.Origin.y, info.Point.y, 1e-3f);
        }

        [Test]
        public void Probe_PlainCollider_IsDefault()
        {
            fx.Floor();
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Default, info.Surface);
            Assert.IsNull(info.Feature);
        }

        [Test]
        public void Probe_IgnoresTriggersAndPlayerLayer()
        {
            fx.Floor().AddComponent<GroundSurface>().Initialize(SurfaceType.Dirt);
            fx.Box("Trigger", new Vector3(0f, 0.2f, 0f), new Vector3(2f, 0.1f, 2f)).GetComponent<BoxCollider>().isTrigger = true;
            fx.Box("PlayerPart", new Vector3(0f, 0.3f, 0f), new Vector3(2f, 0.1f, 2f)).layer = PlayerLayers.Player;
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Dirt, info.Surface);
        }

        [Test]
        public void Probe_TopMostSurfaceWins()
        {
            fx.Floor().AddComponent<GroundSurface>().Initialize(SurfaceType.Dirt);
            fx.Box("Road", new Vector3(0f, 0.04f, 0f), new Vector3(2f, 0.08f, 2f)).AddComponent<GroundSurface>().Initialize(SurfaceType.Asphalt);
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Asphalt, info.Surface);
        }

        [Test]
        public void Probe_ChildCollider_UsesParentSurface()
        {
            var parent = fx.Track(new GameObject("Area"));
            parent.AddComponent<GroundSurface>().Initialize(SurfaceType.Paving);
            var child = fx.Box("Collider", new Vector3(0f, -0.5f, 0f), new Vector3(4f, 1f, 4f));
            child.transform.SetParent(parent.transform, true);
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Paving, info.Surface);
        }

        [Test]
        public void Probe_AfterClearCache_DoesNotReturnDestroyedCollider()
        {
            var grass = fx.Floor();
            grass.AddComponent<GroundSurface>().Initialize(SurfaceType.Grass);
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out _));
            Object.DestroyImmediate(grass);
            GroundProbe.ClearCache();
            var plain = fx.Floor();
            fx.Sync();
            Assert.IsTrue(GroundProbe.Probe(From, 2f, out var info));
            Assert.AreEqual(SurfaceType.Default, info.Surface);
            Assert.AreSame(plain.GetComponent<BoxCollider>(), info.Collider);
        }

        [Test]
        public void Probe_Miss_ReturnsFalse()
        {
            Assert.IsFalse(GroundProbe.Probe(From, 2f, out var info));
            Assert.IsFalse(info.Hit);
        }
    }
}
