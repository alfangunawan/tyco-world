using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>The barrier geometry and props for each kind of dead end (spec road-clearance §3–§5).</summary>
    public class BarrierBuilderTests
    {
        WorldGenSettings settings;

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        BarrierBuildResult One(DeadEndKind kind, float width = 5f) =>
            BarrierBuilder.Build(new[] { new DeadEnd(new Vector2(10f, 20f), Vector2.up, width, kind, 7) }, settings.deadEnds, settings.frontage, 100f);

        static Bounds BoundsOf(MeshData m)
        {
            var b = new Bounds(m.Vertices[0], Vector3.zero);
            foreach (var v in m.Vertices) b.Encapsulate(v);
            return b;
        }

        [Test]
        public void Wall_SpansTheRoad_AtWallHeight_WithACollider_AndTwoPots()
        {
            var r = One(DeadEndKind.Wall);
            var render = BoundsOf(r.Render.Values.Single());
            Assert.AreEqual(5f, render.size.x, 0.3f, "across the road");
            Assert.AreEqual(settings.deadEnds.wallHeight, render.max.y, 0.1f);
            var col = BoundsOf(r.Collider.Values.Single());
            Assert.GreaterOrEqual(col.size.x, 5f - 0.01f);
            Assert.GreaterOrEqual(col.max.y, settings.deadEnds.wallHeight - 0.01f);
            Assert.AreEqual(2, r.Props.Count(p => p.Variant == BarrierBuilder.PotVariant));
            Assert.IsTrue(r.Props.All(p => p.Position.y < 20f), "pots stand on the road side of the wall");
            Assert.AreEqual(1, r.Blockers.Count);
        }

        [Test]
        public void Portal_IsClosed_ToItsBarHeight()
        {
            var r = One(DeadEndKind.Portal);
            var col = BoundsOf(r.Collider.Values.Single());
            Assert.GreaterOrEqual(col.max.y, settings.deadEnds.portalHeight - 0.01f);
            Assert.GreaterOrEqual(col.size.x, 5f - 0.01f);
            Assert.IsEmpty(r.Props, "the portal is all code-built");
            Assert.AreEqual(3, r.Render.Values.Single().SubmeshCount);
        }

        [Test]
        public void Boundary_LinesConcreteAcross_PlusTheRoadClosedSign()
        {
            var r = One(DeadEndKind.Boundary, 6f);
            int blocks = r.Props.Count(p => p.Variant == BarrierBuilder.ConcreteVariant);
            Assert.GreaterOrEqual(blocks, Mathf.FloorToInt(6f / settings.deadEnds.blockSpacing));
            Assert.GreaterOrEqual(settings.deadEnds.blockSpacing, 1.85f, "a block is 1.9 m long: no overlapping blocks");
            Assert.AreEqual(1, r.Props.Count(p => p.Variant == BarrierBuilder.RoadClosedVariant));
            Assert.IsNotEmpty(r.Collider, "solid even before the prefabs are assigned");
        }

        [Test]
        public void Boundary_BlocksCoverTheWholeRoad()
        {
            Assert.Less(BarrierBuilder.MaxBlockGap, 0.7f, "narrower than a person");
            foreach (float width in new[] { 2.5f, 3.7f, 5f, 6f, 7.7f })
            {
                int blocks = One(DeadEndKind.Boundary, width).Props.Count(p => p.Variant == BarrierBuilder.ConcreteVariant);
                Assert.GreaterOrEqual(blocks * settings.deadEnds.blockSpacing, width - BarrierBuilder.MaxBlockGap - 1e-3f,
                    "no gap a person fits through beside the blocks at width " + width);
            }
        }

        [Test]
        public void Props_FaceTheRoad_PerVariant()
        {
            var wall = One(DeadEndKind.Wall);
            foreach (var p in wall.Props) Assert.AreEqual(0f, Mathf.DeltaAngle(p.YawDegrees, 0f), 0.5f, "pot: outward +Z is yaw 0");
            var edge = One(DeadEndKind.Boundary);
            foreach (var p in edge.Props.Where(p => p.Variant == BarrierBuilder.ConcreteVariant))
                Assert.AreEqual(0f, Mathf.DeltaAngle(p.YawDegrees, 180f), 0.5f, "blocks lie across the road (long along local X), striped face (+Z) toward whoever comes up it");
            foreach (var p in edge.Props.Where(p => p.Variant == BarrierBuilder.RoadClosedVariant))
                Assert.AreEqual(0f, Mathf.DeltaAngle(p.YawDegrees, 180f), 0.5f, "the sign faces back down the road");
        }

        [Test]
        public void EachBarrier_LandsInItsOwnChunk()
        {
            var r = BarrierBuilder.Build(new[]
            {
                new DeadEnd(new Vector2(10f, 20f), Vector2.up, 4f, DeadEndKind.Wall, 1),
                new DeadEnd(new Vector2(250f, 20f), Vector2.up, 4f, DeadEndKind.Portal, 2)
            }, settings.deadEnds, settings.frontage, 100f);
            Assert.AreEqual(2, r.Render.Count);
            Assert.AreEqual(2, r.Collider.Count);
        }
    }
}
