using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.WorldGen.Tests
{
    public class DistanceCullerTests
    {
        CullingFixture fx;
        DistanceCuller culler;
        Vector3 viewer;
        GeneratedRoot worldForCuller;

        [SetUp]
        public void SetUp()
        {
            fx = new CullingFixture();
            culler = new GameObject("Culler").AddComponent<DistanceCuller>();
            viewer = CullingFixture.Origin;
            worldForCuller = fx.World;
            culler.FindWorldRoot = () => worldForCuller;
            culler.FindViewer = () => viewer;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(culler.gameObject);
            fx.Dispose();
        }

        static bool Shown(GameObject go) => go.GetComponent<Renderer>().enabled;

        // Cubes are 1 m wide: a cube centred at x + 0.5 has its near face exactly x metres from the origin.

        [Test]
        public void InfiniteDistances_HideNothing()
        {
            var far = fx.Cube("Buildings/Chunk_9_0", new Vector3(900.5f, 0f, 0f));
            culler.Tick(0.25f);
            Assert.IsTrue(culler.HasWorld);
            Assert.IsTrue(Shown(far));
            Assert.AreEqual(0, culler.HiddenCount);
        }

        [Test]
        public void Structures_BeyondViewDistance_AreHidden_CollidersStay()
        {
            var near = fx.Cube("Buildings/Chunk_0_0", new Vector3(50.5f, 0f, 0f));
            var far = fx.Cube("Buildings/Chunk_1_0", new Vector3(150.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            Assert.IsTrue(Shown(near));
            Assert.IsFalse(Shown(far));
            Assert.IsTrue(far.GetComponent<Collider>().enabled);
            Assert.AreEqual(1, culler.HiddenCount);
        }

        [Test]
        public void Props_UseThePropDistance()
        {
            var tree = fx.Cube("Props/Trees/Chunk_0_0", new Vector3(75.5f, 0f, 0f));
            var building = fx.Cube("Buildings/Chunk_0_0", new Vector3(75.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.PropDistance = 60f;
            culler.Tick(0.25f);
            Assert.IsFalse(Shown(tree));
            Assert.IsTrue(Shown(building));
        }

        [Test]
        public void Hysteresis_KeepsStateWithin10Metres()
        {
            var b = fx.Cube("Buildings/Chunk_1_0", new Vector3(105.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            Assert.IsTrue(Shown(b), "105 m: masih di dalam batas + 10 m");
            culler.Evaluate(CullingFixture.Origin + new Vector3(-10f, 0f, 0f));
            Assert.IsFalse(Shown(b), "115 m: lewat batas + 10 m");
            culler.Evaluate(CullingFixture.Origin + new Vector3(12f, 0f, 0f));
            Assert.IsFalse(Shown(b), "93 m: belum masuk batas - 10 m");
            culler.Evaluate(CullingFixture.Origin + new Vector3(16f, 0f, 0f));
            Assert.IsTrue(Shown(b), "89 m: masuk batas - 10 m");
        }

        [Test]
        public void SmallMoves_WaitForTheInterval()
        {
            var b = fx.Cube("Buildings/Chunk_1_0", new Vector3(105.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            viewer = CullingFixture.Origin + new Vector3(-10f, 0f, 0f);
            culler.Tick(0.05f);
            Assert.IsTrue(Shown(b), "belum dievaluasi ulang");
            culler.Tick(0.25f);
            Assert.IsFalse(Shown(b));
        }

        [Test]
        public void ViewerJump_EvaluatesImmediately()
        {
            var b = fx.Cube("Buildings/Chunk_1_0", new Vector3(105.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            viewer = CullingFixture.Origin + new Vector3(-60f, 0f, 0f);
            culler.Tick(0.01f);
            Assert.IsFalse(Shown(b), "teleport > 50 m langsung dievaluasi");
        }

        [Test]
        public void ChangingViewDistance_EvaluatesOnTheNextTick()
        {
            var b = fx.Cube("Buildings/Chunk_1_0", new Vector3(200.5f, 0f, 0f));
            culler.ViewDistance = 300f;
            culler.Tick(0.25f);
            Assert.IsTrue(Shown(b));
            culler.ViewDistance = 150f;
            culler.Tick(0.01f);
            Assert.IsFalse(Shown(b));
        }

        [Test]
        public void WorldAppearsLater_IndexBuiltThen()
        {
            fx.Cube("Buildings/Chunk_0_0", new Vector3(10.5f, 0f, 0f));
            worldForCuller = null;
            culler.Tick(0.3f);
            Assert.IsFalse(culler.HasWorld);
            worldForCuller = fx.World;
            culler.Tick(0.3f);
            Assert.IsTrue(culler.HasWorld);
            Assert.AreEqual(1, culler.Groups.Count);
        }

        [Test]
        public void WorldMissing_NoErrors()
        {
            worldForCuller = null;
            for (int i = 0; i < 10; i++) culler.Tick(0.3f);
            Assert.IsFalse(culler.HasWorld);
            Assert.AreEqual(0, culler.Groups.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WorldDestroyed_ResetsWithoutErrors()
        {
            fx.Cube("Buildings/Chunk_0_0", new Vector3(10.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            Assert.IsTrue(culler.HasWorld);
            Object.DestroyImmediate(fx.Root);
            culler.Tick(0.25f);
            Assert.IsFalse(culler.HasWorld);
            Assert.AreEqual(0, culler.HiddenCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NoViewer_LeavesEverythingAsIs()
        {
            var far = fx.Cube("Buildings/Chunk_9_0", new Vector3(900.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.FindViewer = () => null;
            culler.Tick(0.25f);
            Assert.IsTrue(Shown(far));
        }

        [Test]
        public void ShowAll_RestoresHiddenRenderers()
        {
            var far = fx.Cube("Buildings/Chunk_1_0", new Vector3(150.5f, 0f, 0f));
            culler.ViewDistance = 100f;
            culler.Tick(0.25f);
            Assert.IsFalse(Shown(far));
            culler.ShowAll();
            Assert.IsTrue(Shown(far));
            Assert.AreEqual(0, culler.HiddenCount);
        }
    }
}
